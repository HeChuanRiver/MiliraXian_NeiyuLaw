using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.QingHe.Abilities.SakuraDance;
using MiliraXian.Characters.QingHe.Vfx;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things
{
    /// <summary>
    /// Draws the Lotus Rain wall: continuous fields stay in per-pixel shaders, discrete elements
    /// are CPU-simulated and batched into meshes — the same split as the Flower Bell (bells and
    /// petals are geometry, the shock wave is an analytic shader).
    ///
    /// <list type="number">
    /// <item>the ground — the water stain, its cold tint, the landing ripples, the field's edge
    /// and the flowers' halo. A continuous field, solved per pixel (premultiplied blend, because
    /// the preview <em>darkens</em> the ground and no additive shader can do that).</item>
    /// <item>the mist — the standing curtain over the band. A continuous field, solved per pixel
    /// (additive).</item>
    /// <item>the strands — discrete falling drops, simulated on the CPU exactly the way the
    /// preview's <c>_strands()</c> does and drawn as batched streaks (additive). This is what the
    /// old per-pixel chord-solving approximated; doing it directly needs no column quantisation
    /// and no fitted gain.</item>
    /// <item>the lotus — four pre-cut flower sprites (plus reflections), opening in sequence.</item>
    /// </list>
    ///
    /// Cost per wall per frame: the ground/mist quads, 4 flower quads (2 draw calls) and one
    /// batched streak mesh shared by every wall on the map.
    /// </summary>
    [StaticConstructorOnStartup]
    internal sealed class RainWallVfx : IQingheVfxRenderer
    {
        /// <summary>How far above the band the rain wall reaches, in cells (the preview's CURTAIN_H).</summary>
        private const float SheetHeight = 2.20f;

        /// <summary>The longest strand; the quad has to reach this far above the sheet too.</summary>
        private const float RainLenMax = 1.45f;

        // --- the strands' fall speeds: shared by the emitter's strands and their landings ----------
        private const float FallSpeedLo = 1.70f;                 // RAIN_SPEED
        private const float FallSpeedHi = 2.60f;
        /// <summary>Strands per cell² of band: the preview's RAIN_COUNT = 300 over 9x3 = 27 cells.</summary>
        private const float StrandDensity = 300f / 27f;

        /// <summary>
        /// Padding around the footprint so nothing is clipped at the quad's edge. Has to cover the
        /// shaders' soft-edge falloff (0.30 cells beyond the band) or the fade-out is cut off
        /// mid-slope and the water's rim shows as a hard line.
        /// </summary>
        private const float Pad = 0.4f;

        /// <summary>
        /// How many flowers stand in the band: the preview's own count.
        /// </summary>
        private const int LotusCount = 4;

        private const float LotusOpenSeconds = 1.1f;

        /// <summary>The wall's colour constants.</summary>
        private static readonly Color Season = new(0.60f, 0.95f, 0.50f);
        private static readonly Color LotusTint = new(0.94f, 1f, 1.02f, 1f);

        private static readonly ScreenBloomProperties Bloom = new()
        {
            group = DefDatabase<ScreenBloomGroupDef>.GetNamedSilentFail("MX_ScreenBloom_Glow"),
            tint = Color.white,
            intensity = 1.1f,
        };

        /// <summary>
        /// The strands' own bloom submission: same group as the mist, but dim — without it the
        /// rain reads flat, at full strength it blows out into bright threads.
        /// </summary>
        private static readonly ScreenBloomProperties StrandBloom = new()
        {
            group = DefDatabase<ScreenBloomGroupDef>.GetNamedSilentFail("MX_ScreenBloom_Glow"),
            tint = Color.white,
            intensity = 0.45f,
        };

        /// <summary>
        /// The ground's bloom submission. Only the pass's rgb contributes: the edge line and the
        /// flowers' halo are additive there, while the darkening lives in alpha and is not read by
        /// the bloom layer.
        /// </summary>
        private static readonly ScreenBloomProperties GroundBloom = new()
        {
            group = DefDatabase<ScreenBloomGroupDef>.GetNamedSilentFail("MX_ScreenBloom_Glow"),
            tint = Color.white,
            intensity = 0.35f,
        };

        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int AxisId = Shader.PropertyToID("_Axis");
        private static readonly int BandId = Shader.PropertyToID("_Band");
        private static readonly int WindAId = Shader.PropertyToID("_WindA");
        private static readonly int WindBId = Shader.PropertyToID("_WindB");
        private static readonly int WindCId = Shader.PropertyToID("_WindC");
        private static readonly int WindDId = Shader.PropertyToID("_WindD");
        private static readonly int WindEId = Shader.PropertyToID("_WindE");
        private static readonly int DistortScaleId = Shader.PropertyToID("_DistortScale");
        private static readonly int LotusAId = Shader.PropertyToID("_LotusA");
        private static readonly int LotusBId = Shader.PropertyToID("_LotusB");
        private static readonly int LotusCId = Shader.PropertyToID("_LotusC");
        private static readonly int LotusDId = Shader.PropertyToID("_LotusD");
        private static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int AlphaScaleId = Shader.PropertyToID("_AlphaScale");
        private static readonly int AdditiveId = Shader.PropertyToID("_Additive");

        /// <summary>Pre-cut lotus textures, loaded directly without UV slicing.</summary>
        private const string LotusBloomTexPath = "MiliraXianQinghe/Effect/lotus_bloom";
        private const string LotusPadTexPath = "MiliraXianQinghe/Effect/lotus_pad";

        /// <summary>Aspect ratio (height/width) of each pre-cut texture.</summary>
        private const float BloomAspect = 333f / 544f;
        private const float PadAspect = 231f / 373f;

        /// <summary>Opacity of the reflection against the flower's own.</summary>
        private const float ReflectionAlpha = 0.20f;

        private readonly HashSet<CompLotusRainWall> walls = new();
        private static readonly Dictionary<Map, RainWallVfx> Instances = new();

        private Map map;
        private Material rainMat;
        private Material groundMat;
        private Material waterMat;
        private Material lotusBloomMat;
        private Material lotusPadMat;
        private Texture2D lotusBloomTex;
        private Texture2D lotusPadTex;

        private readonly List<Mesh> groundMeshes = new();
        private readonly List<Mesh> rainMeshes = new();
        private readonly List<Mesh> waterMeshes = new();
        private readonly List<Mesh> lotusBloomMeshes = new();
        private readonly List<Mesh> lotusPadMeshes = new();

        /// <summary>Reused between frames: <see cref="Draw"/> runs every frame and must not allocate.</summary>
        private readonly List<CompLotusRainWall> liveBuffer = new();

        /// <summary>The four flowers' (along, across, radius, start) — refilled by <see cref="LotusColumns"/>.</summary>
        private static readonly Vector4[] LotusBuffer = new Vector4[LotusCount];

        /// <summary>Up to 8 wind wave packets driving the water's background refraction.</summary>
        private static readonly Vector4[] WindBuffer = new Vector4[5];

        /// <summary>Screen UV per cell, cached per frame for the refraction pass.</summary>
        private Vector4 screenPerCell;

        public bool Active
        {
            get
            {
                if (map == null)
                {
                    return false;
                }
                walls.RemoveWhere(w => w == null || w.parent == null || w.parent.Destroyed);
                return walls.Count > 0;
            }
        }

        /// <summary>The walls still standing, into <see cref="liveBuffer"/>.</summary>
        private List<CompLotusRainWall> LiveWalls()
        {
            walls.RemoveWhere(w => w == null || w.parent == null || w.parent.Destroyed);
            liveBuffer.Clear();
            liveBuffer.AddRange(walls);
            return liveBuffer;
        }

        internal static void Add(Map map, CompLotusRainWall wall)
        {
            if (map == null || wall == null)
            {
                return;
            }
            Ensure(map)?.walls.Add(wall);
        }

        internal static void Remove(Map map, CompLotusRainWall wall)
        {
            if (map != null && Instances.TryGetValue(map, out RainWallVfx renderer))
            {
                renderer.walls.Remove(wall);
            }
        }

        internal static RainWallVfx Ensure(Map map)
        {
            if (map == null || map.GetComponent<MapComponent_QingheVfx>() == null)
            {
                return null;
            }
            if (Instances.TryGetValue(map, out RainWallVfx existing) && existing.map == map)
            {
                return existing;
            }

            RainWallVfx renderer = new() { map = map };
            Instances[map] = renderer;
            MapComponent_QingheVfx.Register(map, renderer);
            return renderer;
        }

        public void Update(Map map)
        {
        }

        public void Draw(Map map)
        {
            if (Find.CurrentMap != map)
            {
                return;
            }

            List<CompLotusRainWall> live = LiveWalls();
            if (live.Count == 0)
            {
                return;
            }

            groundMat ??= MatFor("MX_QH_RainWallGround");
            rainMat ??= MatFor("MX_QH_RainWall");
            waterMat ??= MatFor("MX_QH_RainWallWater");
            LotusMaterials();
            if (rainMat == null || groundMat == null || waterMat == null)
            {
                return;
            }

            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;

            // The refraction's screen scale, the same way FlowerBellWaveDrawer computes it.
            Camera cam = Find.Camera;
            screenPerCell = Vector4.zero;
            if (cam != null && cam.orthographicSize > 0f)
            {
                float h = 2f * cam.orthographicSize;
                screenPerCell = new Vector4(1f / (h * Mathf.Max(0.1f, cam.aspect)), -1f / h, 0.25f, 0.05f);
            }

            for (int i = 0; i < live.Count; i++)
            {
                CompLotusRainWall wall = live[i];
                float s = wall.Strength;
                if (s <= 0.01f)
                {
                    continue;
                }

                float seconds = wall.SecondsSinceStart;
                Vector2 al = wall.AxisLength;
                Vector2 ac = wall.AxisThickness;
                Vector3 centre = OriginOf(wall);

                // The band's own extent in map axes; the quad only has to cover that plus padding.
                float ex = wall.HalfLength * Mathf.Abs(al.x) + wall.HalfThickness * Mathf.Abs(ac.x);
                float ez = wall.HalfLength * Mathf.Abs(al.y) + wall.HalfThickness * Mathf.Abs(ac.y);

                // --- 1. the ground: stain, ripples, edge, the flowers' halo ------------------
                // MoteLow, not Floor: Floor is altitude layer 4 and grass is layer 11, so at ground
                // level every tuft would be drawn over the wet patch and cut the edge line into
                // pieces — which is exactly how it shipped (a barely visible, "too thin" border).
                // Water wets the grass standing in it too, so nothing is lost by drawing above it,
                // and MoteLow is still below the pawns, so anyone walking through covers it.
                Mesh gm = MeshAt(groundMeshes, i);
                BuildQuad(gm, centre.x, centre.z, ex + Pad, ez + Pad, 0f,
                    AltitudeLayer.MoteLow.AltitudeFor());
                LotusColumns(wall);
                block.Clear();
                block.SetColor(GlowId, Season);
                block.SetVector(AxisId, new Vector4(al.x, al.y, ac.x, ac.y));
                block.SetVector(BandId, new Vector4(wall.HalfLength, wall.HalfThickness, seconds, s));
                block.SetVector(LotusAId, LotusBuffer[0]);
                block.SetVector(LotusBId, LotusBuffer[1]);
                block.SetVector(LotusCId, LotusBuffer[2]);
                block.SetVector(LotusDId, LotusBuffer[3]);
                // 泛光只取 rgb：那里是亮线与荷花光晕，压暗在 alpha 里，不会被当成加光。
                ScreenBloom.Submit(GroundBloom, gm, Matrix4x4.identity, groundMat, block);
                Graphics.DrawMesh(gm, Matrix4x4.identity, groundMat, 0, null, 0, block);

                // --- 1b. the water: refraction --------------------------------------------------
                // The water is a lens: it bends the ground beneath it with slow wind packets.
                // The splashes are NOT here: they are discrete (one per strand landing) and no
                // uniform block can hold 300 of them — they are batched into a mesh below.
                Mesh wm = MeshAt(waterMeshes, i);
                BuildQuad(wm, centre.x, centre.z, ex + Pad, ez + Pad, 0f,
                    AltitudeLayer.MoteLow.AltitudeFor() + 0.01f);
                WindColumns(wall);
                block.Clear();
                block.SetVector(AxisId, new Vector4(al.x, al.y, ac.x, ac.y));
                block.SetVector(BandId, new Vector4(wall.HalfLength, wall.HalfThickness, seconds, s));
                block.SetVector(WindAId, WindBuffer[0]);
                block.SetVector(WindBId, WindBuffer[1]);
                block.SetVector(WindCId, WindBuffer[2]);
                block.SetVector(WindDId, WindBuffer[3]);
                block.SetVector(WindEId, WindBuffer[4]);
                block.SetVector(DistortScaleId, screenPerCell);
                Graphics.DrawMesh(wm, Matrix4x4.identity, waterMat, 0, null, 0, block);

                // --- 1c. the splashes now belong to the emitter -------------------------------
                // They used to be built here from a second walk of the same random stream, which is
                // exactly the coupling that had to stay in lockstep by hand. The emitter produces
                // them in the same loop as the strands, so the landing point and the birth time are
                // the same fact, not a convention.

                // --- 2. the mist: the standing curtain ----------------------------------------
                Mesh rm = MeshAt(rainMeshes, i);
                BuildQuad(rm, centre.x, centre.z, ex + Pad, ez + Pad, SheetHeight + RainLenMax + Pad,
                    AltitudeLayer.MoteOverhead.AltitudeFor());
                block.Clear();
                block.SetVector(AxisId, new Vector4(al.x, al.y, ac.x, ac.y));
                block.SetVector(BandId, new Vector4(wall.HalfLength, wall.HalfThickness, seconds, s));
                ScreenBloom.Submit(Bloom, rm, Matrix4x4.identity, rainMat, block);
                Graphics.DrawMesh(rm, Matrix4x4.identity, rainMat, 0, null, 0, block);

                // --- 3. the flowers ----------------------------------------------------------
                // One mesh per texture used, so each draw call binds the right lotus sprite.
                if (lotusBloomMat != null)
                {
                    Mesh bm = MeshAt(lotusBloomMeshes, i);
                    BuildLotus(bm, centre, wall, seconds, s, wantBloom: true);
                    if (!MapComponent_QingheVfx.BuffersEmpty)
                    {
                        block.Clear();
                        block.SetColor(ShaderPropertyIDs.Color, LotusTint);
                        block.SetFloat(BrightnessId, 0.80f);
                        block.SetFloat(AlphaScaleId, 1f);
                        block.SetFloat(AdditiveId, 0f);
                        Graphics.DrawMesh(bm, Matrix4x4.identity, lotusBloomMat, 0, null, 0, block);
                    }
                }
                if (lotusPadMat != null)
                {
                    Mesh pm = MeshAt(lotusPadMeshes, i);
                    BuildLotus(pm, centre, wall, seconds, s, wantBloom: false);
                    if (!MapComponent_QingheVfx.BuffersEmpty)
                    {
                        block.Clear();
                        block.SetColor(ShaderPropertyIDs.Color, LotusTint);
                        block.SetFloat(BrightnessId, 0.80f);
                        block.SetFloat(AlphaScaleId, 1f);
                        block.SetFloat(AdditiveId, 0f);
                        Graphics.DrawMesh(pm, Matrix4x4.identity, lotusPadMat, 0, null, 0, block);
                    }
                }

                block.Clear();
            }
        }

        /// <summary>
        /// The single "where am I" of the whole effect: the band's centre in world space. Every
        /// pass — the shader quads, the lotus, the CPU strands — derives its placement from this
        /// one value, so they cannot drift apart. IntVec3 is the cell's south-west corner; the
        /// band's centre is the cell's centre, half a cell east and north of it.
        /// </summary>
        private static Vector3 OriginOf(CompLotusRainWall wall)
        {
            return new Vector3(wall.parent.Position.x + 0.5f, 0f, wall.parent.Position.z + 0.5f);
        }

        /// <summary>
        /// Where the flowers stand, refilled into <see cref="LotusBuffer"/>: one
        /// (along, across, radius, open-start-second) vector each, shared by the halo in the rain
        /// shader and the quads drawn below.
        ///
        /// The preview puts them evenly along the band with a jitter, at a random across position
        /// inside 70 % of the thickness, and opens them one after another. It is refilled in place
        /// because <see cref="Draw"/> runs every frame.
        /// </summary>
        private static void LotusColumns(CompLotusRainWall wall)
        {
            float hl = wall.HalfLength;
            float ht = wall.HalfThickness;

            // Deterministic per wall: a save reloaded shows the same flowers.
            uint state = (uint)(wall.parent.thingIDNumber * 64013 + 41);
            for (int i = 0; i < LotusCount; i++)
            {
                float baseAlong = Mathf.Lerp(-hl + 0.7f, hl - 0.7f, i / (float)(LotusCount - 1));
                state = state * 1664525u + 1013904223u;
                float jitter = (state >> 8) / 16777216f * 2f - 1f;
                state = state * 1664525u + 1013904223u;
                float across = (state >> 8) / 16777216f * 2f - 1f;
                state = state * 1664525u + 1013904223u;
                float radius = Mathf.Lerp(0.34f, 0.52f, (state >> 8) / 16777216f);
                state = state * 1664525u + 1013904223u;
                float start = 0.05f + 0.28f * i + (state >> 8) / 16777216f * 0.12f;

                LotusBuffer[i] = new Vector4(baseAlong + jitter * 0.25f, across * 0.7f * ht, radius, start);
            }
        }

        /// <summary>
        /// Wind wave packets driving the water's background refraction: each is (along, across,
        /// wavelength, amplitude). Deterministic per wall.
        /// </summary>
        private static void WindColumns(CompLotusRainWall wall)
        {
            float hl = wall.HalfLength;
            float ht = wall.HalfThickness;

            uint state = (uint)(wall.parent.thingIDNumber * 777 + 123);
            for (int i = 0; i < 5; i++)
            {
                state = state * 1664525u + 1013904223u;
                float along = ((state >> 8) / 16777216f * 2f - 1f) * hl;
                state = state * 1664525u + 1013904223u;
                float across = ((state >> 8) / 16777216f * 2f - 1f) * ht;
                state = state * 1664525u + 1013904223u;
                float lam = 1.0f + (state >> 8) / 16777216f * 0.8f;
                state = state * 1664525u + 1013904223u;
                float amp = 0.003f + (state >> 8) / 16777216f * 0.002f;

                WindBuffer[i] = new Vector4(along, across, lam, amp);
            }
        }

        /// <summary>
        /// Four flowers, each with its reflection in the standing water.
        ///
        /// The preview never turns a flower: the sheet is drawn facing north and a flower keeps that
        /// facing wherever the wall points, so only its *place* follows the axis. Variety comes from
        /// the two different flowers and their sizes.
        ///
        /// The reflection is the same sprite flipped, dropped below the flower and drawn first. It
        /// used to be offset 3/44 of a cell — 0.068 cells — which is less than a tenth of the
        /// sprite's own height, so the copy lay almost entirely on top of the flower and its
        /// rectangular box edge showed straight through the petals. That is the "square corner"
        /// artefact: not clipping, not altitude, just a ghost sitting in the wrong place. It now
        /// sits clear of the flower at <see cref="ReflectionDrop"/> sprite heights below.
        /// </summary>
        private static void BuildLotus(Mesh mesh, Vector3 centre, CompLotusRainWall wall, float seconds,
            float strength, bool wantBloom)
        {
            Vector2 al = wall.AxisLength;
            Vector2 ac = wall.AxisThickness;
            // Above the grass (LowPlant is layer 11) and below the pawns (Pawn is layer 23), so the
            // tufts no longer eat the petals while anyone walking through still covers the flower.
            // Altitude only orders depth here; the camera looks straight down, so it does not move
            // the flower on screen. One step above the wet ground so the flower is never z-fought.
            float y = AltitudeLayer.MoteLow.AltitudeFor() + 0.02f;

            MapComponent_QingheVfx.ClearBuffers();
            for (int i = 0; i < LotusBuffer.Length; i++)
            {
                // Alternate between bloom and pad; skip the ones that don't match this pass.
                bool isBloom = i % 2 == 0;
                if (isBloom != wantBloom)
                {
                    continue;
                }

                Vector4 f = LotusBuffer[i];
                float open = Mathf.Clamp01((seconds - f.w) / LotusOpenSeconds);
                if (open <= 0f)
                {
                    continue;
                }

                float aspect = isBloom ? BloomAspect : PadAspect;
                float halfW = f.z;
                float halfH = f.z * aspect;

                Vector2 at = new(centre.x + al.x * f.x + ac.x * f.y, centre.z + al.y * f.x + ac.y * f.y);

                // Fade in as it opens, and go with the ground at the end.
                float alpha = Mathf.Min(1f, 0.35f + 0.65f * open) * strength;

                // Reflection first, so it never paints over the flower it belongs to.
                // The reflection sits *in* the water, just below the flower's base, not floating
                // half a sprite-height above it.
                AddLotusQuad(new Vector2(at.x, at.y - halfH * 0.05f), halfW, halfH,
                    true, alpha * ReflectionAlpha, y - 0.01f);
                AddLotusQuad(at, halfW, halfH, false, alpha, y);
            }

            if (!MapComponent_QingheVfx.BuffersEmpty)
            {
                MapComponent_QingheVfx.Upload(mesh);
            }
            else
            {
                mesh.Clear();
            }
        }

        /// <summary>One flower quad. <paramref name="flipV"/> turns it upside down for the reflection.</summary>
        private static void AddLotusQuad(Vector2 at, float halfW, float halfH, bool flipV,
            float alpha, float y)
        {
            // RimWorld loads textures flipped: v = 0 is the texture's top, v = 1 its bottom.
            // So the quad's bottom vertex gets v = 1, its top vertex v = 0.
            float vMin = 1f;
            float vMax = 0f;
            if (flipV)
            {
                (vMin, vMax) = (vMax, vMin);
            }

            int n = MapComponent_QingheVfx.VertexCount;
            // The texture has straight alpha (not premultiplied), so the shader's SrcAlpha /
            // OneMinusSrcAlpha blend expects the vertex colour to be non-premultiplied too.
            Color c = new(1f, 1f, 1f, alpha);
            MapComponent_QingheVfx.AddVertex(new Vector3(at.x - halfW, y, at.y - halfH),
                new Vector2(0f, vMin), c);
            MapComponent_QingheVfx.AddVertex(new Vector3(at.x - halfW, y, at.y + halfH),
                new Vector2(0f, vMax), c);
            MapComponent_QingheVfx.AddVertex(new Vector3(at.x + halfW, y, at.y + halfH),
                new Vector2(1f, vMax), c);
            MapComponent_QingheVfx.AddVertex(new Vector3(at.x + halfW, y, at.y - halfH),
                new Vector2(1f, vMin), c);
            MapComponent_QingheVfx.AddTriangle(n, n + 1, n + 2);
            MapComponent_QingheVfx.AddTriangle(n, n + 2, n + 3);
        }

        /// <summary>
        /// One quad over the band's footprint plus padding, laid flat at <paramref name="y"/>.
        ///
        /// The uv is, for every vertex, simply the map offset from the wall's centre in cells (x
        /// east, y north) — including <paramref name="rise"/>, the extra reach up the screen the rain
        /// needs to stand above the band. That is what lets one quad serve any aim angle: the shader
        /// projects that offset onto the wall's axes, and for the rain it reads
        /// <c>height = uv.y - groundZ</c> straight off it.
        ///
        /// The uv includes the padding: it IS the world offset, so the shaders' soft-edge falloff
        /// beyond the band (0.30 cells) lives inside the quad and can fade out. (An earlier
        /// "unpadded uv" variant pinned the quad's rim to exactly the band edge, which cut the
        /// falloff off entirely — the water's hard rim.)
        /// </summary>
        private static void BuildQuad(Mesh mesh, float cx, float cz, float ex, float ez, float rise, float y)
        {
            // The quad is axis-aligned in *world* space, but the band is rotated. The uv is the
            // map offset from the wall's centre in cells (x east, y north), so the shader
            // projects it onto the wall's axes and gets (along, across) right.
            float north = ez + rise;
            MapComponent_QingheVfx.ClearBuffers();
            MapComponent_QingheVfx.AddVertex(new Vector3(cx - ex, y, cz - ez), new Vector2(-ex, -ez), Color.white);
            MapComponent_QingheVfx.AddVertex(new Vector3(cx - ex, y, cz + north), new Vector2(-ex, north), Color.white);
            MapComponent_QingheVfx.AddVertex(new Vector3(cx + ex, y, cz + north), new Vector2(ex, north), Color.white);
            MapComponent_QingheVfx.AddVertex(new Vector3(cx + ex, y, cz - ez), new Vector2(ex, -ez), Color.white);
            MapComponent_QingheVfx.AddTriangle(0, 1, 2);
            MapComponent_QingheVfx.AddTriangle(0, 2, 3);
            MapComponent_QingheVfx.Upload(mesh);
        }

        private Mesh MeshAt(List<Mesh> pool, int i)
        {
            while (pool.Count <= i)
            {
                pool.Add(MapComponent_QingheVfx.NewMesh("MX_QH_RainWall"));
            }
            return pool[i];
        }

        private static Material MatFor(string shaderDef)
        {
            Shader shader = DefDatabase<ShaderDef>.GetNamedSilentFail(shaderDef)?.Shader;
            if (shader == null)
            {
                Log.ErrorOnce("[MiliraXian] Lotus Rain: missing shader '" + shaderDef + "'.",
                    GenText.StableStringHash(shaderDef));
                return null;
            }
            return MaterialPool.MatFrom(BaseContent.WhiteTex, shader, Color.white);
        }

        private void LotusMaterials()
        {
            if (lotusBloomMat != null && lotusPadMat != null)
            {
                return;
            }
            Shader shader = DefDatabase<ShaderDef>.GetNamedSilentFail("MX_QH_RainWallLotus")?.Shader;
            if (shader == null)
            {
                Log.ErrorOnce("[MiliraXian] Lotus Rain: missing shader 'MX_QH_RainWallLotus'.",
                    GenText.StableStringHash("MX_QH_RainWallLotus"));
                return;
            }
            lotusBloomTex = ContentFinder<Texture2D>.Get(LotusBloomTexPath, false);
            lotusPadTex = ContentFinder<Texture2D>.Get(LotusPadTexPath, false);
            if (lotusBloomTex != null)
            {
                lotusBloomMat = MaterialPool.MatFrom(lotusBloomTex, shader, Color.white);
            }
            if (lotusPadTex != null)
            {
                lotusPadMat = MaterialPool.MatFrom(lotusPadTex, shader, Color.white);
            }
        }

        public void Dispose()
        {
            DestroyAll(groundMeshes);
            DestroyAll(rainMeshes);
            DestroyAll(lotusBloomMeshes);
            DestroyAll(lotusPadMeshes);
            walls.Clear();
            if (map != null && Instances.TryGetValue(map, out RainWallVfx self) && ReferenceEquals(self, this))
            {
                Instances.Remove(map);
            }
            map = null;
        }

        private static void DestroyAll(List<Mesh> list)
        {
            foreach (Mesh m in list)
            {
                Object.Destroy(m);
            }
            list.Clear();
        }
    }
}

using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities.SakuraDance
{
    /// <summary>
    /// Draws the Sakura Dance rings, petals, the whirlwind of a jump and the electricity of a
    /// caught thunder. Purely visual and not saved: everything lives at most a few seconds. Time is
    /// the interpolated game clock, so it freezes when paused and follows the game speed. Every
    /// trail layer is a strip mesh rebuilt each frame from its source curve (a ring or a helix).
    /// </summary>
    [StaticConstructorOnStartup]
    public sealed class MapComponent_SakuraDanceVisuals : MapComponent
    {
        private const int MaxStrips = 16;
        private const float StepSeconds = 1f / 240f;
        private const int MaxSteps = 160;
        // Altitude layers are about 0.038 apart; stay inside half a layer around the pawn.
        private const float AltitudePerMetre = 0.004f;
        private const float AltitudeSpread = 0.018f;
        /// <summary>Thinner strips shimmer between pixels; they are drawn this wide with lower opacity instead.</summary>
        private const float MinWidthPixels = 1.6f;
        /// <summary>Share of a segment's life over which it fades out at the old end of the trail.</summary>
        private const float AgeFadeShare = 0.3f;

        internal static readonly int BrightnessId = Shader.PropertyToID("_Brightness");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int ShadeId = Shader.PropertyToID("_Shade");
        private static readonly int DistortScaleId = Shader.PropertyToID("_DistortScale");
        private static readonly int NormalTexId = Shader.PropertyToID("_NormalTex");
        internal static readonly int AlphaScaleId = Shader.PropertyToID("_AlphaScale");
        private static readonly int AlphaMaxId = Shader.PropertyToID("_AlphaMax");
        internal static readonly int AdditiveId = Shader.PropertyToID("_Additive");
        internal static readonly int ColorId = Shader.PropertyToID("_Color");
        /// <summary>
        /// Vertex alpha is written divided by this so opacities above 1 (up to 3 here) are
        /// kept; with the 8-bit vertex colour that still leaves 64 levels per unit.
        /// </summary>
        private const float AlphaScale = 4f;

        private static readonly Dictionary<string, Material> Materials = new();
        private static readonly List<Vector3> Verts = new();
        private static readonly List<Vector2> Uvs = new();
        private static readonly List<Color> Colors = new();
        private static readonly List<int> Tris = new();
        private static bool warned;

        private readonly List<StripSource> strips = new();
        private readonly List<SakuraThunderVisuals.ChargedBlade> charged = new();
        private float lastTicks = -1f;

        public MapComponent_SakuraDanceVisuals(Map map) : base(map)
        {
        }

        /// <param name="clock">Seconds since the cut started at a visual tick count; null = real time from <paramref name="startTick"/>.</param>
        public void AddCut(CompProperties_AbilitySakuraDance props, SakuraCutProps cut, SakuraRingFrame frame, int startTick, bool thunder, float radiusScale,
            System.Func<float, float> clock = null)
        {
            CutVisual v = new()
            {
                clock = clock,
                props = props,
                cut = cut,
                frame = frame,
                startTick = startTick,
                radiusScale = radiusScale,
                pawnAltitude = AltitudeLayer.Pawn.AltitudeFor(),
                groundZ = frame.center.z,
                noisePhase = (startTick * 0.6180339f) % 1f * 100f,
                emitSeconds = cut.spinSeconds,
            };
            v.layers.AddRange(cut.layers);
            v.baseLayerCount = cut.layers.Count;
            if (thunder)
            {
                v.layers.AddRange(cut.thunderLayers);
            }
            float life = v.StripLife();
            foreach (SakuraPetalEmitter e in cut.petals)
            {
                life = Mathf.Max(life, cut.spinSeconds + e.lifetime);
            }
            v.lifeSeconds = life;
            SakuraPetals.Emit(v.petals, props, cut, frame, startTick);
            Add(v);
        }

        /// <param name="bodyCenter">World position (x east, y height, z north) of the body centre at the spawn.</param>
        /// <param name="clock">Seconds since the spawn at a visual tick count (the dance's motion clock); null = real time.</param>
        public void AddUpdraft(CompProperties_AbilitySakuraDance props, SakuraUpdraftProps up, Vector3 bodyCenter, Vector3 aim, int startTick,
            System.Func<float, float> clock = null)
        {
            UpdraftVisual v = new()
            {
                clock = clock,
                props = props,
                up = up,
                bodyCenter = bodyCenter,
                startTick = startTick,
                pawnAltitude = AltitudeLayer.Pawn.AltitudeFor(),
                groundZ = bodyCenter.z,
                noisePhase = (startTick * 0.7548777f) % 1f * 100f,
                emitSeconds = up.emitSeconds,
            };
            SakuraRingFrame.BodyBasis(aim, out v.right, out v.upAxis, out v.forward);
            int strands = Mathf.Max(1, up.strands);
            for (int s = 0; s < strands; s++)
            {
                v.layers.AddRange(up.layers);
            }
            v.baseLayerCount = v.layers.Count;
            float life = v.StripLife();
            if (!up.dustTexPath.NullOrEmpty() && up.dustCount > 0)
            {
                life = Mathf.Max(life, up.dustInterval * up.dustCount + up.dustLife);
            }
            v.lifeSeconds = life;
            Add(v);
        }

        /// <summary>Starts (or keeps) the electricity on a pawn's blade; called every tick while charged.</summary>
        public void EnsureCharged(Pawn pawn)
        {
            foreach (SakuraThunderVisuals.ChargedBlade c in charged)
            {
                if (c.pawn == pawn)
                {
                    return;
                }
            }
            charged.Add(new SakuraThunderVisuals.ChargedBlade
            {
                pawn = pawn,
                seed = Gen.HashCombineInt(pawn.thingIDNumber, Find.TickManager.TicksGame),
            });
        }

        /// <summary>Adds a visual of another kind (for example a blade trail); it is drawn and dropped like the others.</summary>
        internal void AddSource(StripSource v)
        {
            v.lifeSeconds = Mathf.Max(v.lifeSeconds, v.StripLife());
            Add(v);
        }

        private void Add(StripSource v)
        {
            while (strips.Count >= MaxStrips)
            {
                strips[0].Dispose();
                strips.RemoveAt(0);
            }
            strips.Add(v);
        }

        public override void MapComponentDraw()
        {
            if ((strips.Count == 0 && charged.Count == 0) || Find.CurrentMap != map)
            {
                return;
            }
            float ticks = VisualClock.Ticks;
            float dt = lastTicks < 0f ? 0f : Mathf.Clamp((ticks - lastTicks) / 60f, 0f, 0.1f);
            lastTicks = ticks;
            Camera cam = Find.Camera;
            Vector4 distortScale = Vector4.zero;
            float minWidth = 0.02f;
            if (cam != null && cam.orthographicSize > 0f)
            {
                float h = 2f * cam.orthographicSize;
                distortScale = new Vector4(1f / (h * Mathf.Max(0.1f, cam.aspect)), 1f / h, 0f, 0f);
                float pixelsPerMetre = Screen.height / h;
                minWidth = MinWidthPixels / Mathf.Max(1f, pixelsPerMetre);
            }

            for (int i = strips.Count - 1; i >= 0; i--)
            {
                StripSource v = strips[i];
                float t = v.Seconds(ticks);
                if (t > v.lifeSeconds || !v.Alive(ticks))
                {
                    v.Dispose();
                    strips.RemoveAt(i);
                    continue;
                }
                if (t < 0f)
                {
                    continue;
                }
                try
                {
                    v.BeforeDraw();
                    DrawStrips(v, t, distortScale, minWidth);
                    v.DrawExtra(t, dt);
                }
                catch (System.Exception ex)
                {
                    Warn(ex);
                    v.Dispose();
                    strips.RemoveAt(i);
                }
            }

            for (int i = charged.Count - 1; i >= 0; i--)
            {
                try
                {
                    if (!SakuraThunderVisuals.Draw(charged[i], ticks))
                    {
                        charged[i].Dispose();
                        charged.RemoveAt(i);
                    }
                }
                catch (System.Exception ex)
                {
                    Warn(ex);
                    charged[i].Dispose();
                    charged.RemoveAt(i);
                }
            }
        }

        private static void Warn(System.Exception ex)
        {
            if (!warned)
            {
                warned = true;
                Log.Error("[MiliraXian] Sakura Dance visuals failed: " + ex);
            }
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            foreach (StripSource v in strips)
            {
                v.Dispose();
            }
            strips.Clear();
            foreach (SakuraThunderVisuals.ChargedBlade c in charged)
            {
                c.Dispose();
            }
            charged.Clear();
        }

        private static void DrawStrips(StripSource v, float t, Vector4 distortScale, float minWidth)
        {
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            for (int li = 0; li < v.layers.Count; li++)
            {
                SakuraTrailLayer layer = v.layers[li];
                float head = Mathf.Min(t, v.emitSeconds);
                float tail = Mathf.Max(0f, t - layer.segmentDuration);
                if (head - tail <= 1e-4f)
                {
                    continue;
                }
                string shaderName = layer.distortion ? "MX_QH_SakuraDistort" : layer.additive ? "MX_QH_SakuraTrailAdd" : "MX_QH_SakuraTrailAlpha";
                Material mat = Material(layer.texPath, shaderName);
                if (mat == null)
                {
                    continue;
                }
                v.meshes ??= new Mesh[v.layers.Count];
                Mesh mesh = v.meshes[li] ??= NewMesh();
                if (!BuildStrip(v, li, layer, t, head, tail, minWidth, mesh))
                {
                    continue;
                }

                block.Clear();
                if (layer.distortion)
                {
                    Texture2D normal = ContentFinder<Texture2D>.Get(layer.normalTexPath ?? v.DistortionNormalTexPath, reportFailure: false);
                    if (normal != null)
                    {
                        block.SetTexture(NormalTexId, normal);
                    }
                    float intensity = SakuraTrailLayer.Eval(layer.distortionIntensityCurve, layer.distortionIntensity, t);
                    block.SetFloat(IntensityId, intensity * v.DistortionMetresPerUnit);
                    block.SetFloat(ShadeId, v.ShadeOpacity);
                    block.SetVector(DistortScaleId, distortScale);
                }
                else
                {
                    block.SetFloat(BrightnessId, SakuraTrailLayer.Eval(layer.brightnessCurve, layer.brightness, t));
                    block.SetFloat(AlphaScaleId, AlphaScale);
                    block.SetFloat(AdditiveId, layer.additive ? 1f : 0f);
                    block.SetFloat(AlphaMaxId, layer.clampAlpha ? 1f : 1e6f);
                    ScreenBloomProperties bloom = v.BloomFor(li);
                    if (layer.bloom && bloom != null)
                    {
                        ScreenBloom.Submit(bloom, mesh, Matrix4x4.identity, mat, block);
                    }
                }
                Graphics.DrawMesh(mesh, Matrix4x4.identity, mat, 0, null, 0, block);
                block.Clear();
            }
        }

        /// <summary>
        /// One strip along the source curve between the trail's tail and head. U is
        /// per segment <c>textureFraction</c> of the texture plus the integrated U speed, or the whole
        /// trail mapped to <c>textureFraction</c> when the UV is attached to the source.
        /// The end fades are measured against the whole emitted trail, not the part still alive, and
        /// every sample also fades with its own age, so a shrinking trail fades out instead of
        /// compressing its fade ramps into a short bright remnant.
        /// </summary>
        private static bool BuildStrip(StripSource v, int layerIndex, SakuraTrailLayer layer, float t, float head, float tail, float minWidth, Mesh mesh)
        {
            float baseWidth = SakuraTrailLayer.Eval(layer.widthCurve, layer.width, t);
            float alpha = SakuraTrailLayer.Eval(layer.alphaCurve, layer.alpha, t);
            if (alpha <= 0.002f || baseWidth <= 0f)
            {
                return false;
            }
            float emitted = Mathf.Max(head, 1e-4f);
            float fadeBase = layer.liveFade ? Mathf.Max(head - tail, 1e-4f) : emitted;
            float startLen = Mathf.Max(SakuraTrailLayer.Eval(layer.startFadeCurve, layer.startFade, t), 0.1f) / 100f * fadeBase;
            float endLen = Mathf.Max(SakuraTrailLayer.Eval(layer.endFadeCurve, layer.endFade, t), 0.1f) / 100f * fadeBase;
            float ageFadeLen = layer.liveFade ? 0f : Mathf.Max(layer.segmentDuration * AgeFadeShare, 1e-3f);
            float scroll = layer.ScrollU(t);
            float si = layer.segmentInterval > 0f ? layer.segmentInterval : 1f / 60f;
            float shadeRgb = layer.distortion ? 1f - Mathf.Clamp01(layer.shadeDarkness) : 1f;
            Color tint = layer.distortion ? Color.white : v.TintFor(layerIndex, layer);
            float heightToScreen = v.HeightToScreen;
            // Animated layers show one frame (a horizontal band of the texture) at a time.
            int frames = Mathf.Max(1, layer.frames);
            float vLo = 0f;
            float vHi = 1f;
            if (frames > 1)
            {
                int f = Mathf.FloorToInt(t * layer.frameRate + v.noisePhase + layerIndex * 5f) % frames;
                vLo = 1f - (f + 1f) / frames;
                vHi = 1f - (float)f / frames;
            }

            // Samples sit on a fixed time grid (plus the two ends), so a point of the trail keeps
            // its position and UV from frame to frame; resampling a moving range made it shimmer.
            float step = Mathf.Max(StepSeconds, (head - tail) / MaxSteps);
            int first = Mathf.CeilToInt(tail / step);
            int last = Mathf.FloorToInt(head / step);
            int steps = Mathf.Max(0, last - first + 1) + 1;
            Verts.Clear();
            Uvs.Clear();
            Colors.Clear();
            Tris.Clear();
            for (int s = 0; s <= steps; s++)
            {
                float te = s == 0 ? tail : s == steps ? head : (first + s - 1) * step;
                te = Mathf.Clamp(te, tail, head);
                // 0 at the oldest emitted point, 1 at the head.
                float along01 = te / emitted;
                float along = Mathf.Clamp01((head - te) / startLen) * Mathf.Clamp01((te - tail) / endLen);
                if (ageFadeLen > 0f)
                {
                    along *= Mathf.Clamp01((te - tail) / ageFadeLen);
                }
                float noise = Noise(te, v.noisePhase + layer.radialOffset * 13f + layerIndex * 3.1f);
                float width = baseWidth * Mathf.Lerp(layer.tailWidth, 1f, along01) * (1f + layer.widthNoise * noise);
                float a = alpha * along * Mathf.Lerp(layer.tailAlpha, 1f, along01) * (1f + layer.alphaNoise * noise)
                    * v.SampleAlpha(layerIndex, layer, te, t);
                if (layer.headBoost > 0f)
                {
                    float h = Mathf.Clamp01(1f - (head - te) / Mathf.Max(layer.headLength * emitted, 1e-4f));
                    a *= 1f + layer.headBoost * h * h;
                }
                if (width < minWidth)
                {
                    a *= width / minWidth;
                    width = minWidth;
                }
                // Narrowing toward the tail keeps the anchored edge in place (1 = outer edge, next to
                // the bright strands), so a band does not pull away from the strands.
                float shift = (baseWidth - width) * 0.5f * layer.taperAnchor;
                v.Sample(layerIndex, te, layer.radialOffset + shift, out Vector3 centre, out Vector3 side);
                Vector3 inner = centre - side * (width * 0.5f);
                Vector3 outer = centre + side * (width * 0.5f);
                float u = layer.attachedUV
                    ? along01 * layer.textureFraction + scroll
                    : te / si * layer.textureFraction + scroll;
                Verts.Add(ToMap(v, inner, heightToScreen));
                Verts.Add(ToMap(v, outer, heightToScreen));
                Uvs.Add(new Vector2(u, layer.flipV ? vHi : vLo));
                Uvs.Add(new Vector2(u, layer.flipV ? vLo : vHi));
                // The distortion shader reads vertex alpha directly; the trail shader rescales it.
                float stored = layer.distortion ? Mathf.Clamp01(a) : Mathf.Clamp01(a / AlphaScale);
                Color c = new(tint.r * shadeRgb, tint.g * shadeRgb, tint.b * shadeRgb, stored);
                Colors.Add(c);
                Colors.Add(c);
                if (s > 0)
                {
                    int b = (s - 1) * 2;
                    Tris.Add(b);
                    Tris.Add(b + 2);
                    Tris.Add(b + 1);
                    Tris.Add(b + 1);
                    Tris.Add(b + 2);
                    Tris.Add(b + 3);
                }
            }
            Upload(mesh);
            return true;
        }

        internal static void Upload(Mesh mesh)
        {
            mesh.Clear();
            mesh.SetVertices(Verts);
            mesh.SetUVs(0, Uvs);
            mesh.SetColors(Colors);
            mesh.SetTriangles(Tris, 0);
            mesh.RecalculateBounds();
        }

        /// <summary>Appends one camera-facing (flat, map-space) quad to the shared buffers.</summary>
        /// <param name="squashZ">Screen-vertical scale after the rotation (below 1 = a sprite lying on the ground, seen at a slant).</param>
        internal static void AddQuad(Vector3 c, float size, float angleDeg, Vector2 uv0, Vector2 uv1, Color col, float squashZ = 1f)
        {
            float h = size * 0.5f;
            float cs = Mathf.Cos(angleDeg * Mathf.Deg2Rad) * h;
            float sn = Mathf.Sin(angleDeg * Mathf.Deg2Rad) * h;
            int b = Verts.Count;
            Verts.Add(new Vector3(c.x - cs + sn, c.y, c.z + (-sn - cs) * squashZ));
            Verts.Add(new Vector3(c.x - cs - sn, c.y, c.z + (-sn + cs) * squashZ));
            Verts.Add(new Vector3(c.x + cs - sn, c.y, c.z + (sn + cs) * squashZ));
            Verts.Add(new Vector3(c.x + cs + sn, c.y, c.z + (sn - cs) * squashZ));
            Uvs.Add(new Vector2(uv0.x, uv0.y));
            Uvs.Add(new Vector2(uv0.x, uv1.y));
            Uvs.Add(new Vector2(uv1.x, uv1.y));
            Uvs.Add(new Vector2(uv1.x, uv0.y));
            Colors.Add(col);
            Colors.Add(col);
            Colors.Add(col);
            Colors.Add(col);
            Tris.Add(b);
            Tris.Add(b + 1);
            Tris.Add(b + 2);
            Tris.Add(b);
            Tris.Add(b + 2);
            Tris.Add(b + 3);
        }

        /// <summary>Appends a map-space quad strip edge pair (two vertices) and links it to the previous pair.</summary>
        internal static void AddStripPair(Vector3 a, Vector3 b, float u, float v0, float v1, Color col, bool link)
        {
            int n = Verts.Count;
            Verts.Add(a);
            Verts.Add(b);
            Uvs.Add(new Vector2(u, v0));
            Uvs.Add(new Vector2(u, v1));
            Colors.Add(col);
            Colors.Add(col);
            if (link)
            {
                Tris.Add(n - 2);
                Tris.Add(n);
                Tris.Add(n - 1);
                Tris.Add(n - 1);
                Tris.Add(n);
                Tris.Add(n + 1);
            }
        }

        internal static void ClearBuffers()
        {
            Verts.Clear();
            Uvs.Clear();
            Colors.Clear();
            Tris.Clear();
        }

        internal static bool BuffersEmpty => Verts.Count == 0;

        /// <summary>Smooth value in about [-1, 1] along the trail; fixed per source, so it does not crawl.</summary>
        private static float Noise(float te, float phase)
        {
            return 0.6f * Mathf.Sin(te * 41f + phase) + 0.4f * Mathf.Sin(te * 97f + phase * 1.7f);
        }

        internal static Vector3 ToMap(StripSource v, Vector3 world, float heightToScreen)
        {
            float alt = v.pawnAltitude + Mathf.Clamp((v.groundZ - world.z) * AltitudePerMetre, -AltitudeSpread, AltitudeSpread);
            // North-south offsets from the body are scaled about the body (groundZ), as the blade is.
            float d = v.DepthToScreen;
            Vector3 w = d == 1f ? world : new Vector3(world.x, world.y, v.groundZ + (world.z - v.groundZ) * d);
            w.y += v.HeightOrigin;
            return SakuraDanceGeometry.ToMap(w, heightToScreen, alt);
        }

        internal static Mesh NewMesh()
        {
            Mesh mesh = new() { name = "MX_QH_SakuraDance" };
            mesh.MarkDynamic();
            return mesh;
        }

        internal static Material Material(string texPath, string shaderDef)
        {
            string key = shaderDef + "|" + texPath;
            if (Materials.TryGetValue(key, out Material cached))
            {
                return cached;
            }
            Shader shader = DefDatabase<ShaderDef>.GetNamedSilentFail(shaderDef)?.Shader;
            Texture2D tex = texPath.NullOrEmpty() ? null : ContentFinder<Texture2D>.Get(texPath, reportFailure: false);
            Material mat = null;
            if (shader != null && tex != null)
            {
                mat = MaterialPool.MatFrom(tex, shader, Color.white);
            }
            else
            {
                Log.ErrorOnce($"[MiliraXian] Sakura Dance: missing shader '{shaderDef}' or texture '{texPath}'.", GenText.StableStringHash(key));
            }
            Materials[key] = mat;
            return mat;
        }

        /// <summary>A visual made of trail strips that follow one emitter path each.</summary>
        internal abstract class StripSource
        {
            public CompProperties_AbilitySakuraDance props;
            public int startTick;
            /// <summary>Seconds since the start at a visual tick count (the dance's slowed motion clock); null = real time.</summary>
            public System.Func<float, float> clock;
            public float lifeSeconds;

            public float Seconds(float ticks) => clock?.Invoke(ticks) ?? (ticks - startTick) / 60f;
            /// <summary>Seconds the emitter runs; trail heads stop here.</summary>
            public float emitSeconds;
            public float pawnAltitude;
            public float groundZ;
            public float noisePhase;
            public readonly List<SakuraTrailLayer> layers = new();
            /// <summary>Layers at or past this index are the thunder layers (own bloom).</summary>
            public int baseLayerCount;
            public Mesh[] meshes;

            // Rendering settings; Sakura Dance sources read them from the ability props.
            public virtual float HeightToScreen => props.heightToScreen;
            // The rings keep their true north-south size: the damage reach is that circle on the ground.
            public virtual float DepthToScreen => 1f;
            public virtual float HeightOrigin => props.heightOrigin;
            public virtual string DistortionNormalTexPath => props.distortionNormalTexPath;
            public virtual float DistortionMetresPerUnit => props.distortionMetresPerUnit;
            public virtual float ShadeOpacity => props.shadeOpacity;
            public virtual ScreenBloomProperties BloomFor(int layerIndex) =>
                layerIndex >= baseLayerCount ? props.thunderBloom ?? props.bloom : props.bloom;
            public virtual Color TintFor(int layerIndex, SakuraTrailLayer layer) => layer.color;

            /// <summary>Checked every frame before drawing; false drops the source.</summary>
            public virtual bool Alive(float ticks) => true;

            /// <summary>Extra opacity of the trail point emitted at <paramref name="te"/>, at source time <paramref name="t"/> (1 = none).</summary>
            public virtual float SampleAlpha(int layerIndex, SakuraTrailLayer layer, float te, float t) => 1f;

            /// <summary>World point of the trail emitted at <paramref name="te"/> and the unit width direction there.</summary>
            public abstract void Sample(int layerIndex, float te, float radialOffset, out Vector3 centre, out Vector3 side);

            /// <summary>Called every frame before the strips are drawn (for sources that follow a moving pawn).</summary>
            public virtual void BeforeDraw()
            {
            }

            public virtual void DrawExtra(float t, float dt)
            {
            }

            public float StripLife()
            {
                float life = emitSeconds;
                foreach (SakuraTrailLayer layer in layers)
                {
                    life = Mathf.Max(life, emitSeconds + layer.segmentDuration);
                }
                return life;
            }

            public virtual void Dispose()
            {
                if (meshes != null)
                {
                    foreach (Mesh m in meshes)
                    {
                        if (m != null)
                        {
                            Object.Destroy(m);
                        }
                    }
                }
            }
        }

        internal sealed class CutVisual : StripSource
        {
            public SakuraCutProps cut;
            public SakuraRingFrame frame;
            public float radiusScale = 1f;
            public Mesh petalMesh;
            public readonly List<SakuraPetals.Petal> petals = new();

            public override void Sample(int layerIndex, float te, float radialOffset, out Vector3 centre, out Vector3 side)
            {
                float angle = SakuraDanceGeometry.RingAngle(cut, te);
                float r = cut.radius * radiusScale + radialOffset;
                centre = frame.Point(angle, r);
                side = frame.Point(angle, r + 1f) - centre;
            }

            public override void DrawExtra(float t, float dt)
            {
                SakuraPetals.StepAndDraw(this, t, dt, Material(props.petalTexPath, "MX_QH_SakuraPetal"));
            }

            public override void Dispose()
            {
                base.Dispose();
                if (petalMesh != null)
                {
                    Object.Destroy(petalMesh);
                }
            }
        }

        /// <summary>
        /// Rising whirlwind: emitters spin about the axis from the start to the end offset while the
        /// spin centre travels along it, so every strand trails a helix. Plus a dust ring at the feet.
        /// </summary>
        internal sealed class UpdraftVisual : StripSource
        {
            private const int DustColumns = 8;
            private const int DustFrames = 64;

            public SakuraUpdraftProps up;
            public Vector3 bodyCenter;
            public Vector3 right;
            public Vector3 upAxis;
            public Vector3 forward;
            public Mesh dustMesh;

            public override void Sample(int layerIndex, float te, float radialOffset, out Vector3 centre, out Vector3 side)
            {
                int perStrand = Mathf.Max(1, up.layers.Count);
                int strand = layerIndex / perStrand;
                Vector3 a = SakuraRingFrame.BodyToWorld(up.startOffset, right, upAxis, forward);
                Vector3 b = SakuraRingFrame.BodyToWorld(up.endOffset, right, upAxis, forward);
                Vector3 axis = b - a;
                if (axis.sqrMagnitude < 1e-6f)
                {
                    axis = Vector3.up;
                }
                axis.Normalize();
                Vector3 e1 = (right - Vector3.Dot(right, axis) * axis).normalized;
                Vector3 e2 = Vector3.Cross(axis, e1);
                float k = up.riseSeconds > 0f ? te / up.riseSeconds : 1f;
                Vector3 c = bodyCenter + Vector3.LerpUnclamped(a, b, k);
                float strandCount = Mathf.Max(1, up.strands);
                float ang = (up.angularSpeed * te + 360f * strand / strandCount) * Mathf.Deg2Rad;
                float r = up.spiralRadius + radialOffset;
                centre = c + (e1 * Mathf.Cos(ang) + e2 * Mathf.Sin(ang)) * r;
                // The ribbon stands along the rise axis, so the helix reads as a climbing band.
                side = axis;
            }

            public override void DrawExtra(float t, float dt)
            {
                if (up.dustTexPath.NullOrEmpty() || up.dustCount <= 0 || t > up.dustInterval * up.dustCount + up.dustLife)
                {
                    return;
                }
                Material mat = Material(up.dustTexPath, "MX_QH_SakuraPetal");
                if (mat == null)
                {
                    return;
                }
                ClearBuffers();
                Vector3 feet = bodyCenter + SakuraRingFrame.BodyToWorld(up.dustOffset, right, upAxis, forward);
                Vector3 c = ToMap(this, feet, props.heightToScreen);
                c.y += 0.005f;
                float cell = 1f / DustColumns;
                for (int i = 0; i < up.dustCount; i++)
                {
                    float age = t - i * up.dustInterval;
                    if (age < 0f || age > up.dustLife)
                    {
                        continue;
                    }
                    float x = age / up.dustLife;
                    uint h = (uint)(startTick * 7919 + i * 104729);
                    h ^= h >> 13;
                    h *= 0x5bd1e995;
                    h ^= h >> 15;
                    int frame = (int)(h % DustFrames);
                    float rot = (h >> 8) % 360u;
                    float size = Mathf.Lerp(up.dustStartSize, up.dustEndSize, x);
                    // Alpha: 0.5 -> 1 over the first 10 %, then down to 0.
                    float a = up.dustAlpha * (x < 0.1f ? Mathf.Lerp(0.5f, 1f, x / 0.1f) : Mathf.Lerp(1f, 0f, (x - 0.1f) / 0.9f));
                    float u0 = frame % DustColumns * cell;
                    float v1 = 1f - frame / DustColumns * cell;
                    AddQuad(c, size, rot, new Vector2(u0, v1 - cell), new Vector2(u0 + cell, v1),
                        new Color(up.dustColor.r, up.dustColor.g, up.dustColor.b, Mathf.Clamp01(a)), up.dustSquash);
                }
                if (BuffersEmpty)
                {
                    return;
                }
                dustMesh ??= NewMesh();
                Upload(dustMesh);
                MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
                block.Clear();
                Graphics.DrawMesh(dustMesh, Matrix4x4.identity, mat, 0, null, 0, block);
            }

            public override void Dispose()
            {
                base.Dispose();
                if (dustMesh != null)
                {
                    Object.Destroy(dustMesh);
                }
            }
        }
    }
}

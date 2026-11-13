using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.QingHe.Abilities.SakuraDance;
using MiliraXian.Characters.QingHe.Vfx;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Projectiles
{
    /// <summary>Season colour of a note, on each Flower Bell projectile def.</summary>
    public class FlowerBellNoteExtension : DefModExtension
    {
        public Color color = new(0.95f, 0.92f, 0.95f, 1f);
    }

    /// <summary>
    /// Every Flower Bell note on a map, drawn together: the
    /// bell sprite, the two sound-print strands, the petals shed along the way, the release and
    /// impact flashes, the shock wave and its refraction. The projectiles only report where they
    /// are each tick (the homing curve stays theirs); the picture is interpolated between those
    /// reports with the visual clock. Purely visual and not saved.
    /// </summary>
    [StaticConstructorOnStartup]
    internal sealed class FlowerBellVfx : IQingheVfxRenderer
    {
        // --- shape and timing (cells, seconds)
        /// <summary>Quad side of the bell: 0.55 / 4 cells of flower (the texture is padded: the flower fills 70.3 % of it).</summary>
        private const float BellQuad = 0.1375f * 0.8926f / 0.7031f;
        private const float FilLife = 0.5f;
        private const float FilAmp = 0.13f;
        private const float FilWavelength = 2f;
        private const float FilOpen = 0.06f;
        private const float FilWidth = 0.06f;
        private const float FilGain = 0.5f;
        private const int FilSubdivisions = 4;
        private const float PetalEvery = 0.06f;
        private const float PetalChance = 0.3f;
        /// <summary>Season colour pushed away from grey, for the bell's halo (the wave's is in the drawer).</summary>
        private const float WaveSaturation = 1.8f;
        private const float FlashRise = 0.03f;
        private const float FlashHold = 0.06f;
        private const float FlashFall = 0.33f;
        private const float AlphaScale = 4f;
        private const float PreviewPixelsPerCell = 60f;

        private const string BellTexPath = "MiliraXianQinghe/Effect/FlowerBell/BellNote";
        private const string GlowTexPath = "MiliraXianQinghe/Effect/SakuraDance/Glow";
        private const string PetalTexPath = "MiliraXianQinghe/Effect/SakuraDance/SakuraPetals";
        private const int PetalAtlasColumns = 8;
        private const int PetalAtlasFrames = 64;

        private static readonly Color White = new(1f, 0.95f, 0.98f, 1f);
        private static readonly ScreenBloomProperties Bloom = FlowerBellWaveDrawer.Bloom;

        internal sealed class Note
        {
            public Projectile proj;
            public Color color;
            public int seed;
            /// <summary>Full radius of this note's wave (later notes of a volley reach further).</summary>
            public float waveRadius = FlowerBellWave.Radius;
            public float phase;
            public int startTick;
            public Vector3 launchDir;
            public readonly List<Vector3> points = new();
            public readonly List<int> ticks = new();
            public readonly List<float> dist = new();
            public readonly List<Petal> petals = new();
            public int petalSlot;
            public int impactTick = -1;
            public Vector3 impactAt;
            public Vector3 waveCentre;
            public bool ended;
            public int endTick;

            public void AddPoint(Vector3 p, int tick)
            {
                p.y = 0f;
                if (ticks.Count > 0 && tick <= ticks[ticks.Count - 1])
                {
                    points[points.Count - 1] = p;
                    return;
                }
                dist.Add(points.Count == 0 ? 0f : dist[dist.Count - 1] + (p - points[points.Count - 1]).magnitude);
                points.Add(p);
                ticks.Add(tick);
                EmitPetals(p, tick);
            }

            private Vector3 HeadingAt(int i)
            {
                if (points.Count >= 2)
                {
                    int a = Mathf.Clamp(i - 1, 0, points.Count - 2);
                    Vector3 d = points[a + 1] - points[a];
                    if (d.sqrMagnitude > 1e-8f)
                    {
                        return d.normalized;
                    }
                }
                return launchDir;
            }

            /// <summary>Every <see cref="PetalEvery"/> s a 30 % chance of a petal shed from the flanks, decided by the note's seed.</summary>
            private void EmitPetals(Vector3 p, int tick)
            {
                float s = (tick - startTick) / 60f;
                Vector3 h = HeadingAt(points.Count - 1);
                while (petalSlot * PetalEvery <= s)
                {
                    Rand.PushState(Gen.HashCombineInt(seed, petalSlot));
                    if (Rand.Value < PetalChance)
                    {
                        float side = Rand.Range(-1f, 1f);
                        petals.Add(new Petal
                        {
                            t0 = petalSlot * PetalEvery,
                            pos = p,
                            vel = new Vector3(-h.x * 0.4f - h.z * side * 0.9f, 0f, -h.z * 0.4f + h.x * side * 0.9f),
                            life = Rand.Range(0.9f, 1.4f),
                            size = Rand.Range(0.20f, 0.26f),
                            angle = Rand.Range(0f, 360f),
                            spin = Rand.Range(-200f, 200f),
                            frame0 = Rand.Range(0f, PetalAtlasFrames),
                            sway = Rand.Range(0f, Mathf.PI * 2f),
                            drag = 3f,
                            fall = 0.35f,
                        });
                    }
                    Rand.PopState();
                    petalSlot++;
                }
            }

            public void Impact(Vector3 at, Vector3 waveAt, int tick)
            {
                AddPoint(at, tick);
                impactTick = tick;
                impactAt = at;
                waveCentre = waveAt;
                const int n = 6;
                float s = (tick - startTick) / 60f;
                for (int k = 0; k < n; k++)
                {
                    Rand.PushState(Gen.HashCombineInt(seed, 0x6b1 + k));
                    float a = 2f * Mathf.PI * (k + Rand.Range(-0.3f, 0.3f)) / n;
                    float v = Rand.Range(1.8f, 3f);
                    petals.Add(new Petal
                    {
                        t0 = s,
                        pos = at,
                        // The ground is not squashed, but the burst is flattened north-south.
                        vel = new Vector3(Mathf.Cos(a) * v, 0f, Mathf.Sin(a) * v * 0.55f),
                        life = Rand.Range(0.6f, 0.9f),
                        size = Rand.Range(0.20f, 0.26f),
                        angle = Rand.Range(0f, 360f),
                        spin = Rand.Range(-400f, 400f),
                        frame0 = Rand.Range(0f, PetalAtlasFrames),
                        sway = -1f,
                        drag = 4f,
                        fall = 0.4f,
                    });
                    Rand.PopState();
                }
            }

            /// <summary>Seconds after the start when nothing of the note is left.</summary>
            public float EndSeconds()
            {
                float end = ticks.Count > 0 ? (ticks[ticks.Count - 1] - startTick) / 60f + FilLife : 0f;
                foreach (Petal p in petals)
                {
                    end = Mathf.Max(end, p.t0 + p.life);
                }
                if (impactTick >= 0)
                {
                    end = Mathf.Max(end, (impactTick - startTick) / 60f + Mathf.Max(FlowerBellWave.LifeSeconds, FlashRise + FlashHold + FlashFall));
                }
                return end;
            }
        }

        internal sealed class Petal
        {
            public float t0;
            public Vector3 pos;
            public Vector3 vel;
            public float life;
            public float size;
            public float angle;
            public float spin;
            public float frame0;
            /// <summary>Phase of the sideways sway; negative for the impact burst (no sway).</summary>
            public float sway;
            public float drag;
            public float fall;
        }

        private readonly Map map;
        private readonly List<Note> notes = new();
        private readonly FlowerBellWaveDrawer waveDrawer = new();
        private Mesh trailMesh;
        private Mesh bellMesh;
        private Mesh bellBloomMesh;
        private Mesh petalMesh;
        private Mesh flashMesh;

        private FlowerBellVfx(Map map)
        {
            this.map = map;
        }

        public bool Active => notes.Count > 0;

        /// <summary>Starts drawing a projectile that has just been launched (or loaded) on its map.</summary>
        internal static Note Track(Projectile_FlowerBell proj)
        {
            Map map = proj.Map;
            if (map?.GetComponent<MapComponent_QingheVfx>() == null)
            {
                return null;
            }
            FlowerBellVfx vfx = MapComponent_QingheVfx.Find<FlowerBellVfx>(map, v => v.map == map);
            if (vfx == null)
            {
                vfx = new FlowerBellVfx(map);
                MapComponent_QingheVfx.Register(map, vfx);
            }
            int now = Find.TickManager.TicksGame;
            Note note = new()
            {
                proj = proj,
                color = proj.def.GetModExtension<FlowerBellNoteExtension>()?.color ?? new Color(0.95f, 0.92f, 0.95f),
                seed = Gen.HashCombineInt(proj.thingIDNumber, now),
                startTick = now,
                launchDir = proj.LaunchDirection,
                waveRadius = proj.WaveRadius,
            };
            Rand.PushState(note.seed);
            note.phase = Rand.Range(0f, Mathf.PI * 2f);
            Rand.PopState();
            note.AddPoint(proj.VisualPosition, now);
            vfx.notes.Add(note);
            return note;
        }

        public void Update(Map map)
        {
            float vt = VisualClock.Ticks;
            for (int i = notes.Count - 1; i >= 0; i--)
            {
                Note n = notes[i];
                if (n.impactTick < 0 && !n.ended && (n.proj == null || n.proj.Destroyed))
                {
                    // Gone without an impact (out of the map, vanished): the trail fades where it is.
                    n.ended = true;
                    n.endTick = n.ticks.Count > 0 ? n.ticks[n.ticks.Count - 1] : n.startTick;
                }
                if ((n.impactTick >= 0 || n.ended) && (vt - n.startTick) / 60f > n.EndSeconds())
                {
                    notes.RemoveAt(i);
                }
            }
        }

        public void Dispose()
        {
            Object.Destroy(trailMesh);
            Object.Destroy(bellMesh);
            Object.Destroy(bellBloomMesh);
            Object.Destroy(petalMesh);
            Object.Destroy(flashMesh);
            waveDrawer.Dispose();
        }

        public void Draw(Map map)
        {
            if (notes.Count == 0)
            {
                return;
            }
            float vt = VisualClock.Ticks;
            DrawTrails(vt);
            DrawBells(vt);
            DrawPetals(vt);
            DrawFlashes(vt);
            DrawWaves(vt);
        }

        private static bool Flying(Note n) => n.impactTick < 0 && !n.ended;

        /// <summary>The bell's place at the visual tick, between the two latest reports.</summary>
        private static Vector3 HeadAt(Note n, float vt, out Vector3 heading)
        {
            int last = n.points.Count - 1;
            heading = n.launchDir;
            if (last >= 1)
            {
                Vector3 d = n.points[last] - n.points[last - 1];
                if (d.sqrMagnitude > 1e-8f)
                {
                    heading = d.normalized;
                }
                int t1 = n.ticks[last];
                int t0 = n.ticks[last - 1];
                if (vt < t1)
                {
                    float f = Mathf.Clamp01((vt - t0) / Mathf.Max(1, t1 - t0));
                    return Vector3.Lerp(n.points[last - 1], n.points[last], f);
                }
            }
            return n.points[last];
        }

        private static Color Saturate(Color c, float k)
        {
            float g = (c.r + c.g + c.b) / 3f;
            return new Color(Mathf.Clamp(g + (c.r - g) * k, 0f, 1.5f), Mathf.Clamp(g + (c.g - g) * k, 0f, 1.5f), Mathf.Clamp(g + (c.b - g) * k, 0f, 1.5f), 1f);
        }

        private static Color Mix(Color a, Color b, float t) => new(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f);

        private static Color WithIntensity(Color c, float intensity) => new(c.r, c.g, c.b, Mathf.Clamp01(intensity / AlphaScale));

        // --- the sound-print: two mirrored sines fixed in space, opening behind the bell and fading
        private void DrawTrails(float vt)
        {
            Material mat = MapComponent_SakuraDanceVisuals.Material(GlowTexPath, "MX_QH_SakuraTrailAdd");
            if (mat == null)
            {
                return;
            }
            float y = AltitudeLayer.MoteOverhead.AltitudeFor();
            MapComponent_SakuraDanceVisuals.ClearBuffers();
            foreach (Note n in notes)
            {
                float t = (vt - n.startTick) / 60f;
                BuildStrands(n, vt, t, y);
            }
            if (MapComponent_SakuraDanceVisuals.BuffersEmpty)
            {
                return;
            }
            trailMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
            MapComponent_SakuraDanceVisuals.Upload(trailMesh);
            DrawAdditive(trailMesh, mat);
        }

        private static readonly List<Vector3> SamplePos = new();
        private static readonly List<Vector3> SampleDir = new();
        private static readonly List<float> SampleDist = new();
        private static readonly List<float> SampleTime = new();

        private static void BuildStrands(Note n, float vt, float t, float y)
        {
            SamplePos.Clear();
            SampleDir.Clear();
            SampleDist.Clear();
            SampleTime.Clear();
            // the reported points up to the visual tick, then the bell's interpolated place
            int count = 0;
            for (int i = 0; i < n.points.Count && n.ticks[i] <= vt; i++)
            {
                count = i + 1;
            }
            Vector3 head = Vector3.zero;
            float headDist = 0f;
            float headTime = 0f;
            bool hasHead = false;
            if (Flying(n) && count >= 1 && count < n.points.Count)
            {
                head = HeadAt(n, vt, out _);
                headDist = n.dist[count - 1] + (head - n.points[count - 1]).magnitude;
                headTime = t;
                hasHead = true;
            }
            int total = count + (hasHead ? 1 : 0);
            for (int i = 0; i + 1 < total; i++)
            {
                Vector3 a = n.points[i];
                Vector3 b = i + 1 < count ? n.points[i + 1] : head;
                float da = n.dist[i];
                float db = i + 1 < count ? n.dist[i + 1] : headDist;
                float ta = (n.ticks[i] - n.startTick) / 60f;
                float tb = i + 1 < count ? (n.ticks[i + 1] - n.startTick) / 60f : headTime;
                Vector3 dir = b - a;
                dir = dir.sqrMagnitude > 1e-8f ? dir.normalized : n.launchDir;
                int sub = i + 1 == total - 1 ? FilSubdivisions + 1 : FilSubdivisions;
                for (int s = 0; s < sub; s++)
                {
                    float f = s / (float)FilSubdivisions;
                    SamplePos.Add(Vector3.Lerp(a, b, f));
                    SampleDir.Add(dir);
                    SampleDist.Add(Mathf.Lerp(da, db, f));
                    SampleTime.Add(Mathf.Lerp(ta, tb, f));
                }
            }
            if (SamplePos.Count < 2)
            {
                return;
            }
            const float k = 1f;
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                bool link = false;
                for (int i = 0; i < SamplePos.Count; i++)
                {
                    float age = t - SampleTime[i];
                    if (age < 0f || age >= FilLife)
                    {
                        link = false;
                        continue;
                    }
                    float amp = FilAmp * k * Mathf.Clamp01(age / FilOpen) * Mathf.Clamp01(1f - age / FilLife);
                    float af = Mathf.Pow(Mathf.Clamp01(1f - age / FilLife), 1.2f);
                    Vector3 d = SampleDir[i];
                    Vector3 normal = new(-d.z, 0f, d.x);
                    float off = sgn * amp * Mathf.Sin(2f * Mathf.PI * SampleDist[i] / FilWavelength + n.phase);
                    Vector3 p = SamplePos[i] + normal * off;
                    p.y = y;
                    Vector3 side = normal * (FilWidth * 0.5f);
                    Color c = WithIntensity(n.color, af * FilGain);
                    MapComponent_SakuraDanceVisuals.AddStripPair(p - side, p + side, 0.5f, 0f, 1f, c, link);
                    link = true;
                }
            }
        }

        // --- the bell: a flat lily-of-the-valley, the flower base leading
        private void DrawBells(float vt)
        {
            Material mat = MapComponent_SakuraDanceVisuals.Material(BellTexPath, "MX_QH_SakuraPetal");
            if (mat == null)
            {
                return;
            }
            float y = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.02f;
            // Pass 1: the sprite (near white, lightly tinted); pass 2: the same quads in the bloom colour.
            for (int pass = 0; pass < 2; pass++)
            {
                MapComponent_SakuraDanceVisuals.ClearBuffers();
                foreach (Note n in notes)
                {
                    if (!Flying(n) || n.points.Count == 0)
                    {
                        continue;
                    }
                    Vector3 p = HeadAt(n, vt, out Vector3 h);
                    p.y = y;
                    // The texture's top (the dome) is +z at angle 0; turned counter-clockwise onto the heading.
                    float angle = Mathf.Atan2(-h.x, h.z) * Mathf.Rad2Deg;
                    Color c = pass == 0 ? Mix(White, n.color, 0.18f) : Mix(White, Saturate(n.color, WaveSaturation), 0.7f);
                    MapComponent_SakuraDanceVisuals.AddQuad(p, BellQuad, angle, Vector2.zero, Vector2.one, c);
                }
                if (MapComponent_SakuraDanceVisuals.BuffersEmpty)
                {
                    return;
                }
                MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
                block.Clear();
                block.SetFloat(MapComponent_SakuraDanceVisuals.BrightnessId, 1f);
                if (pass == 0)
                {
                    bellMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
                    MapComponent_SakuraDanceVisuals.Upload(bellMesh);
                    Graphics.DrawMesh(bellMesh, Matrix4x4.identity, mat, 0, null, 0, block);
                }
                else
                {
                    bellBloomMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
                    MapComponent_SakuraDanceVisuals.Upload(bellBloomMesh);
                    // Only the halo: the bloom colour carries the season (the sprite itself is near white).
                    ScreenBloom.Submit(Bloom, bellBloomMesh, Matrix4x4.identity, mat, block);
                }
                block.Clear();
            }
        }

        // --- petals: pushed out, then sinking slowly down the screen while they turn
        private void DrawPetals(float vt)
        {
            Material mat = MapComponent_SakuraDanceVisuals.Material(PetalTexPath, "MX_QH_SakuraPetal");
            if (mat == null)
            {
                return;
            }
            float y = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.01f;
            float cell = 1f / PetalAtlasColumns;
            MapComponent_SakuraDanceVisuals.ClearBuffers();
            foreach (Note n in notes)
            {
                float t = (vt - n.startTick) / 60f;
                Color tint = Mix(Color.white, n.color, 0.3f);
                foreach (Petal p in n.petals)
                {
                    float a = t - p.t0;
                    if (a < 0f || a > p.life)
                    {
                        continue;
                    }
                    float drag = (1f - Mathf.Exp(-p.drag * a)) / p.drag;
                    Vector3 pos = p.pos + p.vel * drag;
                    pos.z -= p.fall * a;
                    if (p.sway >= 0f)
                    {
                        pos.x += 0.12f * Mathf.Sin(p.sway + 2f * a);
                    }
                    pos.y = y;
                    float x = a / p.life;
                    float alpha = p.sway >= 0f ? Mathf.Min(1f, a / 0.06f) * Mathf.Pow(1f - x, 1.2f) : Mathf.Pow(1f - x, 1.2f);
                    int frame = Mathf.FloorToInt(p.frame0 + (PetalAtlasFrames - 1) * x);
                    frame = ((frame % PetalAtlasFrames) + PetalAtlasFrames) % PetalAtlasFrames;
                    float u0 = frame % PetalAtlasColumns * cell;
                    // Row 0 of the atlas is the top of the image; UV v grows upward.
                    float v1 = 1f - frame / PetalAtlasColumns * cell;
                    MapComponent_SakuraDanceVisuals.AddQuad(pos, p.size, p.angle + p.spin * a, new Vector2(u0, v1 - cell), new Vector2(u0 + cell, v1),
                        new Color(tint.r, tint.g, tint.b, alpha));
                }
            }
            if (MapComponent_SakuraDanceVisuals.BuffersEmpty)
            {
                return;
            }
            petalMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
            MapComponent_SakuraDanceVisuals.Upload(petalMesh);
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetFloat(MapComponent_SakuraDanceVisuals.BrightnessId, 1f);
            Graphics.DrawMesh(petalMesh, Matrix4x4.identity, mat, 0, null, 0, block);
            block.Clear();
        }

        // --- flashes: a short one at the bell in her hand, a longer one at the impact
        private void DrawFlashes(float vt)
        {
            Material mat = MapComponent_SakuraDanceVisuals.Material(GlowTexPath, "MX_QH_SakuraTrailAdd");
            if (mat == null)
            {
                return;
            }
            float y = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.03f;
            MapComponent_SakuraDanceVisuals.ClearBuffers();
            foreach (Note n in notes)
            {
                float t = (vt - n.startTick) / 60f;
                if (t >= 0f && t < 0.08f && n.points.Count > 0)
                {
                    float a = Mathf.Exp(-t / 0.03f) * 0.8f;
                    AddFlash(n.points[0], y, 4f / PreviewPixelsPerCell, 40f * a, n.color);
                }
                if (n.impactTick < 0)
                {
                    continue;
                }
                float age = (vt - n.impactTick) / 60f;
                if (age < 0f || age >= FlashRise + FlashHold + FlashFall)
                {
                    continue;
                }
                float fl = age < FlashRise ? age / FlashRise
                    : age < FlashRise + FlashHold ? 1f
                    : Mathf.Pow(1f - (age - FlashRise - FlashHold) / FlashFall, 2f);
                float grow = 1f + 0.6f * Mathf.Min(1f, age / (FlashRise + FlashHold + FlashFall));
                const float k = 1f;
                AddFlash(n.impactAt, y, 0.20f * k * grow, fl * 110f * k, White);
                AddFlash(n.impactAt, y, 0.45f * k * grow, fl * 140f * k, n.color);
            }
            if (MapComponent_SakuraDanceVisuals.BuffersEmpty)
            {
                return;
            }
            flashMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
            MapComponent_SakuraDanceVisuals.Upload(flashMesh);
            DrawAdditive(flashMesh, mat);
        }

        /// <summary>A gaussian spot (sigma in cells, amp in px units at PreviewPixelsPerCell), as a soft quad.</summary>
        private static void AddFlash(Vector3 at, float y, float sigma, float amp, Color color)
        {
            float sigmaPx = sigma * PreviewPixelsPerCell;
            float peak = amp / (2f * Mathf.PI * sigmaPx * sigmaPx);
            if (peak < 0.002f)
            {
                return;
            }
            MapComponent_SakuraDanceVisuals.AddQuad(new Vector3(at.x, y, at.z), sigma * 4f, 0f, Vector2.zero, Vector2.one, WithIntensity(color, peak));
        }

        private static void DrawAdditive(Mesh mesh, Material mat)
        {
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetFloat(MapComponent_SakuraDanceVisuals.BrightnessId, 1f);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AlphaScaleId, AlphaScale);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AdditiveId, 1f);
            ScreenBloom.Submit(Bloom, mesh, Matrix4x4.identity, mat, block);
            Graphics.DrawMesh(mesh, Matrix4x4.identity, mat, 0, null, 0, block);
            block.Clear();
        }

        // --- the shock wave: drawn by the shared drawer, one entry per note that has landed
        private void DrawWaves(float vt)
        {
            waveDrawer.Begin();
            foreach (Note n in notes)
            {
                if (n.impactTick < 0)
                {
                    continue;
                }
                waveDrawer.Draw(n.waveCentre, n.waveRadius, n.color, (vt - n.impactTick) / 60f, FlowerBellWave.RingSeconds);
            }
        }
    }
}

using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.QingHe.Abilities.SakuraDance;
using MiliraXian.Characters.QingHe.Things.Projectiles;
using MiliraXian.Characters.QingHe.Vfx;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    /// <summary>
    /// The Flower Bell's charge on the map (RangedRework.md 2.9, the preview charge_preview.py):
    /// a static five-petal peach blossom of light on the ground under Qinghe (one petal a tier,
    /// fixed to the map, the first petal south, then clockwise), a hovering note over the tip of
    /// every lit petal (the notes leave from there), a small ring effect at the bell on each tier up,
    /// and petals falling off a petal that goes out. Purely visual and not saved; the state is read
    /// from <see cref="CompFlowerBellCharge"/> each frame, the moments are pushed by the stance and
    /// the verb.
    /// </summary>
    [StaticConstructorOnStartup]
    internal sealed class FlowerBellChargeVfx : IQingheVfxRenderer
    {
        // --- the preview's constants (cells, seconds)
        private const int Tiers = 5;
        /// <summary>Petal length, centre to tip (owner 11-11: 0.8 of the preview was too small in game).</summary>
        private const float FlowerR = 1.2f;
        /// <summary>The hovering notes keep the preview's place (owner 11-11: fine as it is).</summary>
        private const float HoverRadius = 0.8f;
        /// <summary>Drawn centre of a pawn to the ground under it (as the notes' wave).</summary>
        private const float FootDrop = 0.3f;
        private const float Depth = 0.55f;
        private const float FirstPetal = 180f;
        private const float PetalDim = 0.14f;
        private const float PetalCharging = 0.55f;
        private const float LitFlash = 0.8f;
        private const float LitFlashDecay = 0.25f;
        private const float BloomFlash = 0.9f;
        private const float BloomFlashDecay = 0.4f;
        private const float FlowerIn = 0.2f;
        private const float FlowerOut = 0.35f;
        private const float DecayDim = 0.6f;
        private const float DecayGrey = 0.6f;
        private const float GroundGain = 0.75f;
        private const float HoverAt = 1f;
        private const float HoverLift = 0.45f;
        private const float HoverBobAmp = 0.04f;
        private const float HoverBobHz = 0.8f;
        private const float HoverIn = 0.2f;
        private const float HoverOut = 0.25f;
        private const float HoverScale = 1.6f;
        private const float HoverBloom = 0.45f;
        private const float HoverBloomBehind = 0.15f;
        private const float RingLife = 0.3f;
        private const float RingSpan = 60f;
        private const float RingR0 = 0.08f;
        private const float RingR1 = 0.38f;
        private const float RingWidth = 0.05f;
        private const float AlphaScale = 4f;
        private const float PreviewPixelsPerCell = 60f;
        /// <summary>Bell quad side: as the flying note (FlowerBellVfx.BellQuad).</summary>
        private const float BellQuad = 0.1375f * 0.8926f / 0.7031f;

        private const string PetalTexPath = "MiliraXianQinghe/Effect/FlowerBell/PeachPetal";
        private const string CenterTexPath = "MiliraXianQinghe/Effect/FlowerBell/PeachCenter";
        private const string BellTexPath = "MiliraXianQinghe/Effect/FlowerBell/BellNote";
        private const string GlowTexPath = "MiliraXianQinghe/Effect/SakuraDance/Glow";
        private const string FallTexPath = "MiliraXianQinghe/Effect/SakuraDance/SakuraPetals";
        private const int AtlasColumns = 8;
        private const int AtlasFrames = 64;

        // Owner 11-11: petals and hovering notes were upside down with the plain uv (the image's top
        // came out at the quad's south end: these textures load with v flipped); sampled with v flipped.
        private static readonly Vector2 FlipV0 = new(0f, 1f);
        private static readonly Vector2 FlipV1 = new(1f, 0f);
        private static readonly Color White = new(1f, 0.95f, 0.98f, 1f);
        /// <summary>The stamens: 55 % white, 45 % warm gold (the preview's mix).</summary>
        private static readonly Color Warm = new(1f, 0.905f, 0.741f, 1f);
        private static readonly ScreenBloomProperties GroundBloom;
        private static readonly ScreenBloomProperties NoteBloom;

        static FlowerBellChargeVfx()
        {
            ScreenBloomGroupDef group = DefDatabase<ScreenBloomGroupDef>.GetNamedSilentFail("MX_ScreenBloom_Glow");
            GroundBloom = new ScreenBloomProperties { group = group, tint = Color.white, intensity = 0.6f };
            NoteBloom = new ScreenBloomProperties { group = group, tint = Color.white, intensity = 1.35f };
        }

        private sealed class State
        {
            public CompFlowerBellCharge comp;
            public Pawn pawn;
            public Color color;
            public int startTick;
            public readonly int[] onTick = { int.MinValue, int.MinValue, int.MinValue, int.MinValue, int.MinValue };
            public readonly int[] offTick = { -1, -1, -1, -1, -1 };
            public int releaseTick = -1;
            public int released;
            public int emptyTick = -1;
            public readonly List<(int tick, int seed)> rings = new();
            public readonly List<Petal> petals = new();
        }

        private sealed class Petal
        {
            public float t0;
            public Vector3 pos;
            public Vector3 vel;
            public float life;
            public float size;
            public float angle;
            public float spin;
            public float frame0;
            public float drag;
            public float fall;
        }

        private readonly Map map;
        private readonly Dictionary<CompFlowerBellCharge, State> states = new();
        private readonly List<CompFlowerBellCharge> removeBuffer = new();
        private Mesh groundMesh;
        private Mesh centerMesh;
        private Mesh bellMesh;
        private Mesh bellBloomMesh;
        private Mesh glowMesh;
        private Mesh fallMesh;

        private FlowerBellChargeVfx(Map map)
        {
            this.map = map;
        }

        public bool Active => states.Count > 0;

        // --- events ---------------------------------------------------------------------------
        private static State Ensure(CompFlowerBellCharge comp, Pawn pawn)
        {
            Map map = pawn?.MapHeld;
            if (map == null || !pawn.Spawned || map.GetComponent<MapComponent_QingheVfx>() == null)
            {
                return null;
            }
            FlowerBellChargeVfx vfx = MapComponent_QingheVfx.Find<FlowerBellChargeVfx>(map, v => v.map == map);
            if (vfx == null)
            {
                vfx = new FlowerBellChargeVfx(map);
                MapComponent_QingheVfx.Register(map, vfx);
            }
            int now = Find.TickManager.TicksGame;
            if (vfx.states.TryGetValue(comp, out State s) && s.pawn == pawn)
            {
                if (s.releaseTick >= 0 && now > s.releaseTick + Verbs.Verb_ShootFlowerBell.ThrowTicks && comp.Charging)
                {
                    // A new run while the last one fades: start afresh.
                    vfx.states.Remove(comp);
                }
                else
                {
                    if (s.emptyTick >= 0 && comp.Charge > 0f)
                    {
                        s.emptyTick = -1;
                    }
                    return s;
                }
            }
            s = new State { comp = comp, pawn = pawn, startTick = now, color = SeasonColor(comp, pawn) };
            // Petals lit before we saw them keep onTick = MinValue: no flash.
            if (comp.Charge > 0f && !comp.Charging)
            {
                s.startTick = now - 60;       // decaying when first seen (a loaded game): no fade-in
            }
            vfx.states[comp] = s;
            return s;
        }

        private static Color SeasonColor(CompFlowerBellCharge comp, Pawn pawn)
        {
            ThingDef proj = comp.parent.TryGetComp<CompFlowerBellResonance>()?.CurrentProjectileFor(pawn);
            Color c = proj?.GetModExtension<FlowerBellNoteExtension>()?.color ?? new Color(0.95f, 0.92f, 0.95f, 1f);
            c.a = 1f;
            return c;
        }

        /// <summary>Every charge tick: keeps the picture alive (also before the first tier).</summary>
        internal static void Notify(CompFlowerBellCharge comp, Pawn pawn)
        {
            State s = Ensure(comp, pawn);
            if (s != null)
            {
                s.color = SeasonColor(comp, pawn);
            }
        }

        /// <summary>Petal <paramref name="index"/> (0-based) has just lit: its note appears, the bell rings.</summary>
        internal static void TierUp(CompFlowerBellCharge comp, Pawn pawn, int index)
        {
            State s = Ensure(comp, pawn);
            if (s == null || index < 0 || index >= Tiers)
            {
                return;
            }
            int now = Find.TickManager.TicksGame;
            s.onTick[index] = now;
            s.offTick[index] = -1;
            s.rings.Add((now, index + 1));
            AddRingPetals(s, now, index + 1);
        }

        /// <summary>Petal <paramref name="index"/> went out in the decay: its note dissolves, petals fall off.</summary>
        internal static void TierDown(CompFlowerBellCharge comp, Pawn pawn, int index)
        {
            if (pawn == null || !pawn.Spawned)
            {
                return;
            }
            State s = Ensure(comp, pawn);
            if (s == null || index < 0 || index >= Tiers)
            {
                return;
            }
            int now = Find.TickManager.TicksGame;
            s.offTick[index] = now;
            Vector3 c = Centre(pawn);
            Vector2 tip = PetalTip(index, FlowerR);
            Rand.PushState(Gen.HashCombineInt(pawn.thingIDNumber, now + index * 31));
            for (int k = 0; k < 3; k++)
            {
                s.petals.Add(new Petal
                {
                    t0 = now / 60f,
                    pos = new Vector3(c.x + tip.x + Rand.Range(-0.15f, 0.15f), 0f, c.z + tip.y * Depth + Rand.Range(-0.15f, 0.15f)),
                    vel = new Vector3(Rand.Range(-0.3f, 0.3f), 0f, 0f),
                    life = Rand.Range(0.8f, 1.1f),
                    size = Rand.Range(0.16f, 0.22f),
                    angle = Rand.Range(0f, 360f),
                    spin = 150f,
                    frame0 = Rand.Range(0f, AtlasFrames),
                    drag = 0f,
                    fall = 0.45f,
                });
            }
            Rand.PopState();
        }

        /// <summary>Released with <paramref name="count"/> notes: they hover through the throw, then leave.</summary>
        internal static void Released(CompFlowerBellCharge comp, Pawn pawn, int count)
        {
            State s = Ensure(comp, pawn);
            if (s == null)
            {
                return;
            }
            s.releaseTick = Find.TickManager.TicksGame;
            s.released = Mathf.Clamp(count, 0, Tiers);
        }

        /// <summary>Where note <paramref name="index"/> hovers now (the visual place, height shown up the screen).</summary>
        internal static Vector3 HoverPoint(Pawn pawn, int index, float seconds)
        {
            Vector3 g = HoverGround(pawn, index, out float lift, seconds);
            return new Vector3(g.x, 0f, g.z + lift);
        }

        /// <summary>The ground under note <paramref name="index"/>, and its height above it (shown up the screen).</summary>
        internal static Vector3 HoverGround(Pawn pawn, int index, out float lift, float seconds)
        {
            Vector3 c = Centre(pawn);
            Vector2 tip = PetalTip(index, HoverRadius * HoverAt);
            lift = HoverLift + HoverBobAmp * Mathf.Sin(2f * Mathf.PI * HoverBobHz * seconds + 1.7f * index);
            return new Vector3(c.x + tip.x, 0f, c.z + tip.y * Depth);
        }

        private static Vector3 Centre(Pawn pawn)
        {
            Vector3 p = pawn.DrawPos;
            return new Vector3(p.x, 0f, p.z - FootDrop);
        }

        /// <summary>Clockwise from north by FirstPetal + 72 i; the tip at 0.8 r (as the preview).</summary>
        private static Vector2 PetalTip(int i, float r)
        {
            float a = (FirstPetal + 72f * i) * Mathf.Deg2Rad;
            return new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * (r * 0.8f);
        }

        private static bool Behind(int i) => PetalTip(i, 1f).y > 0.3f;

        private static Vector3 Muzzle(Pawn pawn)
        {
            Vector3 p = pawn.DrawPos;
            Rot4 r = pawn.Rotation;
            float x = r == Rot4.East ? 0.35f : r == Rot4.West ? -0.35f : r == Rot4.South ? 0.2f : -0.2f;
            float z = r == Rot4.North ? 0.05f : r == Rot4.South ? -0.15f : -0.05f;
            return new Vector3(p.x + x, 0f, p.z + z);
        }

        private static void AddRingPetals(State s, int tick, int seed)
        {
            Vector3 m = Muzzle(s.pawn);
            Rand.PushState(Gen.HashCombineInt(s.pawn.thingIDNumber, tick * 7 + seed));
            int n = 2 + seed % 2;
            for (int k = 0; k < n; k++)
            {
                float a = Rand.Range(0f, Mathf.PI * 2f);
                float v = Rand.Range(0.9f, 1.4f);
                s.petals.Add(new Petal
                {
                    t0 = tick / 60f,
                    pos = m,
                    vel = new Vector3(Mathf.Cos(a) * v, 0f, Mathf.Sin(a) * v),
                    life = Rand.Range(0.45f, 0.6f),
                    size = Rand.Range(0.12f, 0.16f),
                    angle = Rand.Range(0f, 360f),
                    spin = 300f,
                    frame0 = Rand.Range(0f, AtlasFrames),
                    drag = 5f,
                    fall = 0.25f,
                });
            }
            Rand.PopState();
        }

        // --- update -----------------------------------------------------------------------------
        public void Update(Map map)
        {
            float vt = VisualClock.Ticks;
            int now = Find.TickManager.TicksGame;
            removeBuffer.Clear();
            foreach (KeyValuePair<CompFlowerBellCharge, State> kv in states)
            {
                State s = kv.Value;
                if (s.pawn == null || !s.pawn.Spawned || s.pawn.Map != map || s.comp.Wielder != s.pawn)
                {
                    removeBuffer.Add(kv.Key);
                    continue;
                }
                s.petals.RemoveAll(p => vt / 60f - p.t0 > p.life);
                s.rings.RemoveAll(r => (vt - r.tick) / 60f > RingLife);
                if (s.releaseTick < 0 && s.emptyTick < 0 && s.comp.Charge <= 0f && !s.comp.Charging)
                {
                    s.emptyTick = now;
                }
                int end = s.releaseTick >= 0 ? s.releaseTick : s.emptyTick;
                if (end >= 0 && (vt - end) / 60f > FlowerOut + 0.1f && s.petals.Count == 0 && s.rings.Count == 0
                    && (s.releaseTick < 0 || vt > s.releaseTick + Verbs.Verb_ShootFlowerBell.ThrowTicks + 1))
                {
                    removeBuffer.Add(kv.Key);
                }
            }
            foreach (CompFlowerBellCharge c in removeBuffer)
            {
                states.Remove(c);
            }
        }

        public void Dispose()
        {
            Object.Destroy(groundMesh);
            Object.Destroy(centerMesh);
            Object.Destroy(bellMesh);
            Object.Destroy(bellBloomMesh);
            Object.Destroy(glowMesh);
            Object.Destroy(fallMesh);
        }

        // --- draw -------------------------------------------------------------------------------
        public void Draw(Map map)
        {
            if (states.Count == 0)
            {
                return;
            }
            float vt = VisualClock.Ticks;
            DrawGround(vt);
            DrawNotes(vt);
            DrawGlow(vt);
            DrawFalling(vt);
        }

        private static Color Grey(Color c, float k)
        {
            float g = (c.r + c.g + c.b) / 3f;
            return new Color(Mathf.Lerp(c.r, g, k), Mathf.Lerp(c.g, g, k), Mathf.Lerp(c.b, g, k), 1f);
        }

        private static Color Mix(Color a, Color b, float t) => new(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f);

        private static Color Saturate(Color c, float k)
        {
            float g = (c.r + c.g + c.b) / 3f;
            return new Color(Mathf.Clamp(g + (c.r - g) * k, 0f, 1.5f), Mathf.Clamp(g + (c.g - g) * k, 0f, 1.5f), Mathf.Clamp(g + (c.b - g) * k, 0f, 1.5f), 1f);
        }

        private static Color Light(Color c, float intensity) => new(c.r, c.g, c.b, Mathf.Clamp01(intensity / AlphaScale));

        /// <summary>Lit petals now: the live tier, or the released count while the released flower fades.</summary>
        private static int LitCount(State s) => s.releaseTick >= 0 ? s.released : s.comp.Tier;

        private static float FlowerFade(State s, float vt)
        {
            float fade = Mathf.Clamp01((vt - s.startTick) / 60f / FlowerIn);
            int end = s.releaseTick >= 0 ? s.releaseTick : s.emptyTick;
            if (end >= 0)
            {
                fade *= Mathf.Max(0f, 1f - (vt - end) / 60f / FlowerOut);
            }
            return fade;
        }

        private void DrawGround(float vt)
        {
            Material petalMat = MapComponent_SakuraDanceVisuals.Material(PetalTexPath, "MX_QH_SakuraTrailAdd");
            Material centerMat = MapComponent_SakuraDanceVisuals.Material(CenterTexPath, "MX_QH_SakuraTrailAdd");
            if (petalMat == null || centerMat == null)
            {
                return;
            }
            float y = AltitudeLayer.FloorEmplacement.AltitudeFor() + 0.02f;
            // Pass 0: the five petals; pass 1: the stamens (another texture).
            for (int pass = 0; pass < 2; pass++)
            {
                MapComponent_SakuraDanceVisuals.ClearBuffers();
                foreach (State s in states.Values)
                {
                    float fade = FlowerFade(s, vt);
                    if (fade <= 0f)
                    {
                        continue;
                    }
                    bool decaying = s.releaseTick < 0 && s.comp.Decaying;
                    int lit = LitCount(s);
                    float progress = s.releaseTick >= 0 ? 0f : s.comp.TierProgress;
                    float bloom = 0f;
                    if (lit >= Tiers && s.onTick[Tiers - 1] > int.MinValue)
                    {
                        bloom = BloomFlash * Mathf.Exp(-Mathf.Max(0f, vt - s.onTick[Tiers - 1]) / 60f / BloomFlashDecay);
                    }
                    Color col = decaying ? Grey(s.color, DecayGrey) : s.color;
                    Vector3 c = Centre(s.pawn);
                    c.y = y;
                    if (pass == 0)
                    {
                        for (int i = 0; i < Tiers; i++)
                        {
                            float g;
                            if (i < lit)
                            {
                                g = 1f;
                                if (s.onTick[i] > int.MinValue && vt >= s.onTick[i])
                                {
                                    g += LitFlash * Mathf.Exp(-(vt - s.onTick[i]) / 60f / LitFlashDecay);
                                }
                            }
                            else if (i == lit && !decaying && s.releaseTick < 0)
                            {
                                g = PetalDim + (PetalCharging - PetalDim) * progress;
                            }
                            else
                            {
                                g = PetalDim;
                                float off = s.offTick[i] >= 0 ? (vt - s.offTick[i]) / 60f : -1f;
                                if (off >= 0f && off < 0.15f)
                                {
                                    g = 1f + 0.6f * (1f - off / 0.15f);
                                }
                            }
                            if (decaying)
                            {
                                g *= DecayDim;
                            }
                            g = (g + bloom) * fade * GroundGain;
                            float angle = FirstPetal + 72f * i;
                            float a = angle * Mathf.Deg2Rad;
                            Vector3 pc = c + new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a) * Depth) * (FlowerR * 0.5f);
                            // AddQuad turns counter-clockwise; the petal texture points north at 0.
                            MapComponent_SakuraDanceVisuals.AddQuad(pc, FlowerR, -angle, FlipV0, FlipV1, Light(col, g), Depth);
                        }
                    }
                    else
                    {
                        float cg = (0.35f + 0.65f * lit / Tiers + bloom) * fade * (decaying ? DecayDim : 1f) * GroundGain;
                        MapComponent_SakuraDanceVisuals.AddQuad(c, FlowerR * 0.42f, 0f, Vector2.zero, Vector2.one, Light(Warm, cg), Depth);
                    }
                }
                if (MapComponent_SakuraDanceVisuals.BuffersEmpty)
                {
                    continue;
                }
                Mesh mesh = pass == 0 ? (groundMesh ??= MapComponent_SakuraDanceVisuals.NewMesh()) : (centerMesh ??= MapComponent_SakuraDanceVisuals.NewMesh());
                MapComponent_SakuraDanceVisuals.Upload(mesh);
                DrawAdditive(mesh, pass == 0 ? petalMat : centerMat, GroundBloom);
            }
        }

        private void DrawNotes(float vt)
        {
            Material mat = MapComponent_SakuraDanceVisuals.Material(BellTexPath, "MX_QH_SakuraPetal");
            if (mat == null)
            {
                return;
            }
            float front = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.02f;
            float seconds = vt / 60f;
            for (int pass = 0; pass < 2; pass++)
            {
                MapComponent_SakuraDanceVisuals.ClearBuffers();
                foreach (State s in states.Values)
                {
                    // Behind her the notes stand under her body (depth tested against it).
                    float behind = s.pawn.DrawPos.y - 0.05f;
                    int lit = LitCount(s);
                    for (int i = 0; i < Tiers; i++)
                    {
                        if (!HoverState(s, i, lit, vt, out float alpha, out float scale))
                        {
                            continue;
                        }
                        Vector3 p = HoverPoint(s.pawn, i, seconds);
                        p.y = Behind(i) ? behind : front;
                        Color c;
                        if (pass == 0)
                        {
                            c = Mix(White, s.color, 0.18f);
                            c.a = alpha;
                        }
                        else
                        {
                            float gain = (Behind(i) ? HoverBloomBehind : HoverBloom) * alpha;
                            c = Mix(White, Saturate(s.color, 1.8f), 0.7f);
                            c = new Color(c.r * gain, c.g * gain, c.b * gain, alpha);
                        }
                        // Mouth down (the dome up), as a hanging bell.
                        MapComponent_SakuraDanceVisuals.AddQuad(p, BellQuad * HoverScale * scale, 0f, FlipV0, FlipV1, c);
                    }
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
                    ScreenBloom.Submit(NoteBloom, bellBloomMesh, Matrix4x4.identity, mat, block);
                }
                block.Clear();
            }
        }

        /// <summary>Whether note <paramref name="i"/> hovers now, and how: fading in at its tier, out when its petal goes out or it leaves.</summary>
        private static bool HoverState(State s, int i, int lit, float vt, out float alpha, out float scale)
        {
            alpha = 0f;
            scale = 1f;
            bool on = i < lit;
            float off = s.offTick[i] >= 0 && !on ? (vt - s.offTick[i]) / 60f : -1f;
            if (!on && (off < 0f || off >= HoverOut))
            {
                return false;
            }
            if (s.releaseTick >= 0 && vt >= s.releaseTick + Verbs.Verb_ShootFlowerBell.ThrowTicks)
            {
                return false;   // it has left (the projectile draws it from here)
            }
            float age = s.onTick[i] > int.MinValue ? (vt - s.onTick[i]) / 60f : HoverIn;
            float a = Mathf.Clamp01(age / HoverIn);
            alpha = a;
            float e = 1f - (1f - a) * (1f - a);
            scale = 0.5f + 0.5f * e;
            if (off >= 0f)
            {
                float u = off / HoverOut;
                alpha *= 1f - u;
                scale *= 1f + 0.2f * u;
            }
            return alpha > 0.001f;
        }

        // --- the ring effect at the bell: a small flash and three short sound-print arcs
        private void DrawGlow(float vt)
        {
            Material mat = MapComponent_SakuraDanceVisuals.Material(GlowTexPath, "MX_QH_SakuraTrailAdd");
            if (mat == null)
            {
                return;
            }
            float y = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.03f;
            MapComponent_SakuraDanceVisuals.ClearBuffers();
            foreach (State s in states.Values)
            {
                Vector3 m = Muzzle(s.pawn);
                m.y = y;
                foreach ((int tick, int seed) in s.rings)
                {
                    float a = (vt - tick) / 60f;
                    if (a < 0f || a >= RingLife)
                    {
                        continue;
                    }
                    float u = a / RingLife;
                    float fl = Mathf.Exp(-a / 0.05f);
                    AddFlash(m, 0.07f, 30f * fl, White);
                    AddFlash(m, 0.16f, 45f * fl, s.color);
                    float e = 1f - (1f - u) * (1f - u);
                    float rad = RingR0 + (RingR1 - RingR0) * e;
                    float amp = Mathf.Pow(1f - u, 1.5f);
                    Color arcCol = Mix(White, s.color, 0.6f);
                    float baseDeg = 90f + 37f * seed;
                    for (int j = 0; j < 3; j++)
                    {
                        float centre = (baseDeg + 120f * j) * Mathf.Deg2Rad;
                        const int n = 10;
                        bool link = false;
                        for (int k = 0; k <= n; k++)
                        {
                            float f = k / (float)n - 0.5f;
                            float th = centre + f * RingSpan * Mathf.Deg2Rad;
                            float w = Mathf.Pow(Mathf.Cos(f * Mathf.PI), 2f);
                            Vector3 dir = new(Mathf.Cos(th), 0f, Mathf.Sin(th));
                            Vector3 p = m + dir * rad;
                            Vector3 side = dir * (RingWidth * 0.5f);
                            MapComponent_SakuraDanceVisuals.AddStripPair(p - side, p + side, 0.5f, 0f, 1f, Light(arcCol, 1.6f * w * amp), link);
                            link = true;
                        }
                    }
                }
            }
            if (MapComponent_SakuraDanceVisuals.BuffersEmpty)
            {
                return;
            }
            glowMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
            MapComponent_SakuraDanceVisuals.Upload(glowMesh);
            DrawAdditive(glowMesh, mat, NoteBloom);
        }

        /// <summary>A gaussian spot of the preview (sigma in cells, amp in its px units) as a soft quad.</summary>
        private static void AddFlash(Vector3 at, float sigma, float amp, Color color)
        {
            float sigmaPx = sigma * PreviewPixelsPerCell;
            float peak = amp / (2f * Mathf.PI * sigmaPx * sigmaPx);
            if (peak < 0.002f)
            {
                return;
            }
            MapComponent_SakuraDanceVisuals.AddQuad(at, sigma * 4f, 0f, Vector2.zero, Vector2.one, Light(color, peak));
        }

        // --- petals: off a petal going out (sinking), and from the ring effect (pushed out)
        private void DrawFalling(float vt)
        {
            Material mat = MapComponent_SakuraDanceVisuals.Material(FallTexPath, "MX_QH_SakuraPetal");
            if (mat == null)
            {
                return;
            }
            float y = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.01f;
            float cell = 1f / AtlasColumns;
            float t = vt / 60f;
            MapComponent_SakuraDanceVisuals.ClearBuffers();
            foreach (State s in states.Values)
            {
                Color tint = Mix(Color.white, s.color, 0.3f);
                foreach (Petal p in s.petals)
                {
                    float a = t - p.t0;
                    if (a < 0f || a > p.life)
                    {
                        continue;
                    }
                    float d = p.drag > 0f ? (1f - Mathf.Exp(-p.drag * a)) / p.drag : a;
                    Vector3 pos = p.pos + p.vel * d;
                    pos.z -= p.fall * a;
                    pos.y = y;
                    float x = a / p.life;
                    float alpha = Mathf.Min(1f, a / 0.05f) * Mathf.Pow(1f - x, 1.2f);
                    int frame = Mathf.FloorToInt(p.frame0 + (AtlasFrames - 1) * x);
                    frame = ((frame % AtlasFrames) + AtlasFrames) % AtlasFrames;
                    float u0 = frame % AtlasColumns * cell;
                    float v1 = 1f - frame / AtlasColumns * cell;
                    MapComponent_SakuraDanceVisuals.AddQuad(pos, p.size, p.angle + p.spin * a, new Vector2(u0, v1 - cell), new Vector2(u0 + cell, v1),
                        new Color(tint.r, tint.g, tint.b, alpha));
                }
            }
            if (MapComponent_SakuraDanceVisuals.BuffersEmpty)
            {
                return;
            }
            fallMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
            MapComponent_SakuraDanceVisuals.Upload(fallMesh);
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetFloat(MapComponent_SakuraDanceVisuals.BrightnessId, 1f);
            Graphics.DrawMesh(fallMesh, Matrix4x4.identity, mat, 0, null, 0, block);
            block.Clear();
        }

        private static void DrawAdditive(Mesh mesh, Material mat, ScreenBloomProperties bloom)
        {
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetFloat(MapComponent_SakuraDanceVisuals.BrightnessId, 1f);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AlphaScaleId, AlphaScale);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AdditiveId, 1f);
            ScreenBloom.Submit(bloom, mesh, Matrix4x4.identity, mat, block);
            Graphics.DrawMesh(mesh, Matrix4x4.identity, mat, 0, null, 0, block);
            block.Clear();
        }
    }
}

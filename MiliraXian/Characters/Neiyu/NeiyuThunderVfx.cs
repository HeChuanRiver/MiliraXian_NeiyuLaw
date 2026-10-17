using System;
using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using ShaderRegistry = Waylith.Rendering.ShaderRegistry;

namespace MiliraXian.Characters.Neiyu;

public sealed class NeiyuThunderVfxDef : Def
{
    public int maxStrikeVisuals = 48;
    public int particlesPerStrike = 22;
}

internal struct ThunderMarkFrame(int applied, int next, int last, int seed, bool final)
{
    internal int appliedTick = applied, nextStrikeTick = next, lastStrikeTick = last, seed = seed;
    internal bool final = final;
}

// Samples the existing warmup stance; it owns no cast clock or gameplay state.
public sealed class Mote_NeiyuThunderWarmup : Mote
{
    private Ability owner;
    private bool Active => owner?.pawn is { Spawned: true, Dead: false, Downed: false }
        && owner.pawn.Map == Map && owner.pawn.CurJob?.ability == owner
        && owner.verb.WarmingUp && !NeiyuPowerBalance.AbilitiesDisabled;
    protected override bool EndOfLife => !Active;
    internal void Bind(Ability ability) => owner = ability;

    protected override void TimeInterval(float deltaTime)
    {
        if (Active)
        {
            exactPosition = owner.pawn.DrawPos;
            if (exactPosition.ToIntVec3() != Position) Position = exactPosition.ToIntVec3();
        }
        base.TimeInterval(deltaTime);
    }

    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        if (Active && Find.CurrentMap == Map && !Find.UIRoot.HideMotes && !Find.ScreenshotModeHandler.Active)
            NeiyuThunderVfx.DrawWarmup(owner.pawn.DrawPos, owner.verb.WarmupProgress,
                owner.pawn.thingIDNumber);
    }
}

// Shared Def materials + two bounded native mesh batches per map. Transients are
// reconstructed from saved task deadlines, and never serialize damage or cast state.
internal sealed class NeiyuThunderVfx : IDisposable
{
    private readonly struct Flash(Vector3 position, int tick, int seed, int kind, bool final)
    {
        internal readonly Vector3 position = position;
        internal readonly int tick = tick, seed = seed, kind = kind;
        internal readonly bool final = final;
    }

    private static readonly Color Ice = new(0.45f, 0.875f, 1f);
    private static readonly Color Violet = new(0.735f, 0.635f, 1f);
    private static readonly Color Pink = new(0.95f, 0.72f, 0.96f);
    private static readonly Color Hot = new(1f, 0.94f, 0.80f);
    private static readonly MaterialPropertyBlock Properties = new();
    private static Material sigil, ribbon, particle;
    private static NeiyuThunderVfxDef tuning;
    private static bool attempted;
    private readonly Map map;
    private readonly List<Flash> flashes = new(64);
    private ArcaneParticleBatch ribbons, particles;
    private int lastSoundTick = -100, geometryTick = -1;
    private Vector3 geometryRight, geometryUp;
    private bool disabled;

    internal NeiyuThunderVfx(Map map) => this.map = map;
    internal bool Idle => flashes.Count == 0;
    private static int Quality => (int)ArcaneVfxStyle.Quality;

    private static void Ready()
    {
        if (attempted) return;
        attempted = true;
        try
        {
            tuning = DefDatabase<NeiyuThunderVfxDef>.GetNamed("MX_NeiyuThunderVfx");
            if (!ShaderRegistry.TryResolve("MX_NeiyuThunderSigil", out _)
                || !ShaderRegistry.TryResolve("MX_NeiyuThunderEnergy", out _))
                throw new InvalidOperationException("雷锁印域 GLSL 未注册，请检查 ShaderDef 与 Content/ShaderSources/NeiyuThunder。请同时检查引擎的着色器编译日志。");
            sigil = DefDatabase<MaterialDef>.GetNamed("MX_NeiyuThunderSigil").Material;
            ribbon = DefDatabase<MaterialDef>.GetNamed("MX_NeiyuThunderRibbon").Material;
            particle = DefDatabase<MaterialDef>.GetNamed("MX_NeiyuThunderParticle").Material;
        }
        catch (Exception ex)
        {
            sigil = null;
            ribbon = particle = MaterialPool.MatFrom(
                "Common/WorldArcaneStrike/neiyu_halo_vfx/SoftGlow", ShaderDatabase.MoteGlow, Color.white);
            Log.ErrorOnce($"[雷锁印域] 自定义特效不可用，使用基础光效；雷击结算保持运行。{ex}", 197631242);
        }
    }

    // Shader geometry is procedural and uses no sampled textures, so no UV flip
    // or per-effect render target is needed. The shared vertex stage supplies ViewProj.
    private static void Plate(Vector3 position, Vector2 size, bool billboard, float seconds,
        int mode, float seed, float charge, float reveal, float intensity, bool final = false)
    {
        Ready();
        if (sigil == null)
        {
            if (mode is 0 or 3) GenDraw.DrawRadiusRing(position.ToIntVec3(), size.x * 0.44f, Ice);
            return;
        }
        Camera camera = Find.Camera;
        if (camera == null) return;
        Quaternion rotation = billboard
            ? Quaternion.LookRotation(camera.transform.up, -camera.transform.forward) : Quaternion.identity;
        Properties.Clear();
        Properties.SetColor("_Color", Ice);
        Properties.SetColor("_SecondaryColor", Violet);
        Properties.SetVector("_Clock", new Vector4(seconds, mode, seed % 4096, Quality));
        Properties.SetVector("_State", new Vector4(charge, reveal, intensity, final ? 1f : 0f));
        Graphics.DrawMesh(MeshPool.plane10, Matrix4x4.TRS(position, rotation, new Vector3(size.x, 1f, size.y)),
            sigil, 0, camera, 0, Properties);
    }

    internal static void DrawPreview(IntVec3 center, float radius, List<Pawn> targets, int now)
    {
        Vector3 at = center.ToVector3ShiftedWithAltitude(AltitudeLayer.MetaOverlays);
        float size = radius * 2f / 0.88f;
        Plate(at, new Vector2(size, size), false, now / 60f, 0, 0, 0, 1, 0.36f);
        foreach (Pawn pawn in targets)
        {
            if (pawn is not { Spawned: true, Dead: false } || pawn.Map != Find.CurrentMap) continue;
            at = pawn.DrawPos; at.y = AltitudeLayer.MetaOverlays.AltitudeFor();
            Plate(at, new Vector2(0.9f, 0.9f), false, now / 60f, 4, pawn.thingIDNumber, 0, 1, 0.16f);
        }
    }

    internal static void DrawWarmup(Vector3 position, float progress, int seed)
    {
        position.y = AltitudeLayer.MoteOverheadLow.AltitudeFor();
        progress = Mathf.Clamp01(progress);
        Plate(position, new Vector2(3.2f, 3.2f), false, progress * 1.2f,
            1, seed, progress * progress, progress, 0.55f);
    }

    internal void CastPulse(Vector3 position, int now) => Add(new Flash(position, now, now, 2, false));
    internal void Release(Vector3 position, int now, int seed) => Add(new Flash(position, now, seed, 1, false));

    internal void Strike(IntVec3 cell, int now, int seed, bool final)
    {
        Add(new Flash(cell.ToVector3Shifted(), now, seed, 0, final));
        // The native bolt is visual only. WeatherEvent_LightningStrike.DoStrike
        // also explodes and ignites the area, so it must not settle this skill's hit.
        try { map.weatherManager?.eventHandler?.AddEvent(new WeatherEvent_NeiyuLightningVisual(map, cell)); }
        catch (Exception ex) { Log.ErrorOnce($"[雷锁印域] 原版落雷特效播放失败：{ex}", 197631245); }
        if (Find.CurrentMap != map || now - lastSoundTick < 8) return;
        lastSoundTick = now;
        try { SoundDefOf.Thunder_OnMap?.PlayOneShot(new TargetInfo(cell, map)); }
        catch (Exception ex) { Log.ErrorOnce($"[雷锁印域] 雷声播放失败：{ex}", 197631243); }
    }

    private void Add(Flash flash)
    {
        // Bound secondary flashes only; every hit still creates its native lightning bolt.
        int limit = Mathf.Clamp(tuning?.maxStrikeVisuals ?? 48, 8, 64);
        if (flashes.Count >= limit)
        {
            int secondary = flashes.FindIndex(f => f.kind != 0);
            if (secondary >= 0) flashes.RemoveAt(secondary);
            else if (flash.kind == 0) flashes.RemoveAt(0);
            else return;
        }
        flashes.Add(flash);
        geometryTick = -1;
    }

    internal void Tick(int now)
    {
        for (int i = flashes.Count - 1; i >= 0; i--)
            if (now - flashes[i].tick >= (flashes[i].kind == 0 ? 60 : 30)) flashes.RemoveAt(i);
    }

    internal void Draw(Dictionary<Pawn, ThunderMarkFrame> marks, int now)
    {
        if (disabled || Find.Camera == null) return;
        try { DrawCore(marks, now); }
        catch (Exception ex)
        {
            disabled = true;
            Dispose();
            Log.ErrorOnce($"[雷锁印域] 已停用本地图异常视觉，雷击结算保持运行。{ex}", 197631244);
        }
    }

    private void DrawCore(Dictionary<Pawn, ThunderMarkFrame> marks, int now)
    {
        Ready();
        if (flashes.Count == 0 && marks.Count == 0) return;
        ribbons ??= new ArcaneParticleBatch(ribbon);
        particles ??= new ArcaneParticleBatch(particle);
        Camera camera = Find.Camera;
        Vector3 right = camera.transform.right, up = camera.transform.up;
        // Pause renders the same geometry; zoom submits it through the current camera.
        bool rebuild = geometryTick != now || right != geometryRight || up != geometryUp;
        if (rebuild)
        {
            ribbons.Begin(); particles.Begin();
            foreach (var pair in marks) BuildMarkEnergy(pair.Key, pair.Value, now, right, up, marks.Count);
            int density = Mathf.Min(tuning?.particlesPerStrike ?? 22,
                ArcaneVfxStyle.ParticleBudget / Mathf.Max(1, flashes.Count));
            density = Mathf.Min(density, Quality == 0 ? 4 : Quality == 1 ? 12 : 32);
            foreach (Flash flash in flashes) BuildParticles(flash, now, right, up, density);
            ribbons.Upload(); particles.Upload();
            geometryTick = now; geometryRight = right; geometryUp = up;
        }
        foreach (var pair in marks) DrawMark(pair.Key, pair.Value, now, up, marks.Count);
        foreach (Flash flash in flashes)
        {
            float age = (now - flash.tick) / 60f;
            Vector3 at = flash.position; at.y = AltitudeLayer.MoteOverheadLow.AltitudeFor();
            if (flash.kind == 0)
            {
                float size = flash.final ? 3.5f : 2.8f;
                Plate(at, new Vector2(size, size), false, age, 5, flash.seed, 0,
                    Mathf.Clamp01(age), 0.85f, flash.final);
            }
            else
            {
                float size = flash.kind == 1 ? 1.5f : 2.5f;
                if (flash.kind == 1) at += up * 0.35f;
                Plate(at, new Vector2(size, size), flash.kind == 1, age, 6, flash.seed,
                    0, Mathf.Clamp01(age * 2f), flash.kind == 1 ? 0.6f : 0.28f);
            }
        }
        ribbons.Draw(now / 60f, 0, camera);
        particles.Draw(now / 60f, 0, camera);
    }

    private static float Charge(ThunderMarkFrame frame, int now) =>
        1f - Mathf.Clamp01((frame.nextStrikeTick - now) / 45f);

    private static void DrawMark(Pawn pawn, ThunderMarkFrame frame, int now, Vector3 up, int count)
    {
        float age = Mathf.Max(0, now - frame.appliedTick) / 60f;
        float charge = Charge(frame, now);
        float recovery = frame.lastStrikeTick < 0 ? 1f : Mathf.Lerp(0.35f, 1f,
            Mathf.Clamp01((now - frame.lastStrikeTick) / 22f));
        Vector3 ground = pawn.DrawPos; ground.y = AltitudeLayer.MoteOverheadLow.AltitudeFor();
        Vector3 body = ground; body.y = AltitudeLayer.MoteOverhead.AltitudeFor();
        Vector3 seal = body + up * 0.42f;
        float size = 0.86f * (1f - Mathf.InverseLerp(0.76f, 1f, charge) * 0.14f);
        float appear = Mathf.SmoothStep(0, 1, Mathf.Clamp01((age - 0.25f) / 0.55f));
        Plate(seal, new Vector2(size, size), true, age, 3, frame.seed,
            charge, 1, (0.44f + charge * 0.38f) * appear * recovery, frame.final);
        if (age < 0.8f)
        {
            Vector3 ripple = body + up * Mathf.Lerp(0.95f, 0.43f, age / 0.8f);
            Plate(ripple, new Vector2(1.35f, 0.60f), true, age, 2, frame.seed,
                charge, age / 0.8f, Quality == 0 ? 0.30f : 0.65f);
        }
        if (Quality > 0 && count <= 48)
            Plate(ground, new Vector2(1.65f, 1.65f), false, age, 4, frame.seed,
                charge, 1, 0.18f + charge * 0.18f, frame.final);
    }

    private void BuildMarkEnergy(Pawn pawn, ThunderMarkFrame frame, int now, Vector3 right, Vector3 up, int count)
    {
        if (Quality == 0 || count > 48) return;
        float age = (now - frame.appliedTick) / 60f, charge = Charge(frame, now);
        Vector3 body = pawn.DrawPos; body.y = AltitudeLayer.MoteOverhead.AltitudeFor();
        Vector3 seal = body + up * 0.42f;
        if (age is > 0.13f and < 0.8f)
        {
            float p = Mathf.Clamp01((age - 0.13f) / 0.67f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 a = body + up * (0.95f - p * 0.35f) + right * ((i - 1) * 0.24f * (1f - p));
                Line(a, Vector3.Lerp(a, seal - up * 0.24f, Mathf.SmoothStep(0, 1, p)),
                    0.06f, WithAlpha(Color.Lerp(Ice, Pink, p), Mathf.Sin(p * Mathf.PI) * 0.5f), up);
            }
        }
        if (charge < 0.15f)
        {
            int phase = (now + (frame.seed & 4095)) % 70;
            if (phase < 9)
            {
                float pulse = Mathf.Sin(phase / 9f * Mathf.PI) * 0.35f;
                Vector3 air = seal + right * 0.46f + up * 0.16f;
                Line(air, air - right * 0.21f - up * 0.13f, 0.045f, WithAlpha(Ice, pulse), up);
                Line(air - right * 0.21f - up * 0.13f, seal, 0.035f, WithAlpha(Violet, pulse), up);
            }
            return;
        }
        int chains = Quality == 2 && count < 16 ? 4 : 2;
        for (int c = 0; c < chains; c++)
        {
            float angle = c * Mathf.PI * 2f / chains + (frame.seed & 4095) * 0.03f;
            Vector3 radial = right * Mathf.Cos(angle) + up * Mathf.Sin(angle) * 0.65f;
            Vector3 start = seal + radial * 0.25f;
            Vector3 end = body + radial * (0.85f - charge * 0.28f) - up * 0.18f;
            Vector3 previous = start;
            for (int j = 1; j <= 5; j++)
            {
                float p = j / 5f;
                Vector3 point = Vector3.Lerp(start, end, p) + right *
                    (Mathf.Sin(p * 14f + age * 5f + angle) * 0.055f * Mathf.Sin(p * Mathf.PI));
                Line(previous, Vector3.Lerp(previous, point, 0.72f), 0.075f,
                    WithAlpha(Color.Lerp(Violet, Ice, charge), charge * 0.55f), up);
                previous = point;
            }
            particles.Add(end, right, up, 0.10f, 0.10f, WithAlpha(Ice, charge * 0.7f));
        }
        if (frame.nextStrikeTick - now <= 12)
            particles.Add(seal + up * 0.48f, right, up, 0.20f + charge * 0.12f,
                0.20f + charge * 0.12f, WithAlpha(Hot, charge));
    }

    private void BuildParticles(Flash flash, int now, Vector3 right, Vector3 up, int count)
    {
        float age = (now - flash.tick) / 60f;
        float duration = flash.kind == 0 ? 1f : 0.5f;
        float p = Mathf.Clamp01(age / duration);
        Vector3 at = flash.position; at.y = AltitudeLayer.MoteOverhead.AltitudeFor();
        if (flash.kind == 1) at += up * 0.4f;
        for (int i = 0; i < count; i++)
        {
            float h = Hash(flash.seed + i * 131), angle = h * Mathf.PI * 2f;
            float distance = (0.25f + Hash(flash.seed + i * 53) * 1.3f) * (1f - Mathf.Exp(-p * 3.5f));
            Vector3 direction = right * Mathf.Cos(angle) + up * Mathf.Sin(angle) * 0.65f;
            Vector3 position = at + direction * distance + up * (p * 0.25f);
            float size = (0.045f + h * 0.09f) * (1f - p * 0.7f);
            Color color = i % 7 == 0 ? Hot : i % 4 == 0 ? Pink : Ice;
            particles.Add(position, right, up, size, size * (i % 3 == 0 ? 2f : 1f),
                WithAlpha(color, (1f - p) * (1f - p) * 0.9f));
            if (Quality > 0 && flash.kind == 0 && p < 0.3f && i < 6)
                Line(at, at + direction * (0.4f + distance), 0.09f,
                    WithAlpha(Ice, (1f - p / 0.3f) * 0.5f), up);
        }
    }

    private void Line(Vector3 a, Vector3 b, float width, Color color, Vector3 up)
    {
        Vector3 direction = b - a;
        float length = direction.magnitude;
        if (length <= 0.001f) return;
        direction /= length;
        Vector3 perpendicular = Vector3.Cross(-Find.Camera.transform.forward, direction).normalized;
        if (perpendicular.sqrMagnitude < 0.1f) perpendicular = up;
        // Slight overlap hides joins without an opaque tube or cracks at bends.
        ribbons.Add((a + b) * 0.5f, direction, perpendicular, length * 1.18f, width, color);
    }

    private static Color WithAlpha(Color color, float alpha) { color.a = alpha; return color; }
    private static float Hash(int seed)
    {
        uint x = unchecked((uint)seed);
        x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16;
        return (x & 0x00ffffff) / 16777215f;
    }

    public void Dispose()
    {
        ribbons?.Dispose(); particles?.Dispose();
        ribbons = particles = null;
        flashes.Clear();
        geometryTick = -1;
    }
}

// Reuse the game's pooled bolt mesh, weather material and tick-driven flash.
// Gameplay damage, EMP and attached fire remain owned by TickThunder.
[StaticConstructorOnStartup]
public sealed class WeatherEvent_NeiyuLightningVisual : WeatherEvent_LightningFlash
{
    private static readonly Material LightningMat = MatLoader.LoadMat("Weather/LightningBolt");
    private readonly IntVec3 strikeLoc;
    private Mesh boltMesh;

    public WeatherEvent_NeiyuLightningVisual(Map map, IntVec3 strikeLoc) : base(map)
    {
        this.strikeLoc = strikeLoc;
    }

    public override void FireEvent() => boltMesh = LightningBoltMeshPool.RandomBoltMesh;

    public override void WeatherEventDraw()
    {
        if (!strikeLoc.IsValid || boltMesh == null) return;
        Graphics.DrawMesh(boltMesh, strikeLoc.ToVector3ShiftedWithAltitude(AltitudeLayer.Weather),
            Quaternion.identity, FadedMaterialPool.FadedVersionOf(LightningMat, LightningBrightness), 0);
    }
}

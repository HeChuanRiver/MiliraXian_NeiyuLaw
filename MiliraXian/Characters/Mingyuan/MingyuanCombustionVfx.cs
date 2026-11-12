using System;
using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan;

public sealed class MingyuanCombustionVfxDef : Def
{
    public float smallSize = 1.25f;
    public float coreSize = 3.2f;
    public float height = 4.5f;
    public float trailWidth = 0.85f;
    public float trailDistortion = 0.10f;
    public float trailFlowSpeed = 1.3f;
    public int smallRemnantTicks = 52;
    public int coreRemnantTicks = 110;
    public float bloomIntensity = 0.16f;
}

// One cache per saved volley. Everything is derived from its authoritative schedule and impact cursor.
internal sealed class MingyuanCombustionVfx
{
    private readonly struct Fireball
    {
        internal readonly Vector3 Landing;
        internal readonly int Launch, Impact;
        internal readonly float Seed, Size;

        internal Fireball(IntVec3 cell, int launch, int impact, float seed, float size)
        {
            Landing = cell.ToVector3Shifted();
            Launch = launch;
            Impact = impact;
            Seed = seed;
            Size = size;
        }
    }

    private readonly Fireball[] balls;
    private readonly Vector3 center;
    private readonly float seed;
    private readonly CompProperties_AbilityMingyuanInstantCombustion settings;

    internal MingyuanCombustionVfx(IntVec3 cell, List<IntVec3> landings,
        CompProperties_AbilityMingyuanInstantCombustion props, int casterId)
    {
        center = cell.ToVector3Shifted();
        settings = props;
        seed = Seed(casterId, cell);
        balls = new Fireball[landings.Count + 1];
        for (int i = 0; i < landings.Count; i++)
            balls[i] = new Fireball(landings[i], props.SmallLaunchAt(i), props.SmallImpactAt(i),
                seed + i * 13.71f, Tuning.smallSize * (0.86f + 0.28f * Hash(seed + i)));
        balls[^1] = new Fireball(cell, props.CoreLaunchAt(landings.Count), props.CoreImpactAt(landings.Count),
            seed + 91.3f, Tuning.coreSize);
    }

    internal static MingyuanCombustionVfxDef Tuning =>
        DefDatabase<MingyuanCombustionVfxDef>.GetNamed("MX_Mingyuan_CombustionVfx");

    internal void Draw(float elapsed, int settled)
    {
        if (!Visible(center, settings.radius + Tuning.height + 5f)) return;
        try
        {
            EnsureResources();
            float time = elapsed / 60f;
            Fireball core = balls[^1];
            float fade = 1f - Mathf.InverseLerp(core.Impact, core.Impact + 22f, elapsed);
            Sigil(center, settings.radius, time, 1f, fade * 0.64f, seed);
            Vector3 origin = CorePosition(center);
            for (int i = 0; i < balls.Length; i++)
            {
                Fireball ball = balls[i];
                bool finisher = i == balls.Length - 1;
                if (i < settled)
                {
                    Impact(ball.Landing, elapsed - ball.Impact, finisher, ball.Seed,
                        finisher ? settings.coreFireball.radius : settings.smallFireball.radius);
                    continue;
                }
                if (elapsed < ball.Launch)
                {
                    float charge = finisher ? Mathf.InverseLerp(core.Launch - 24, core.Launch, elapsed) : 0f;
                    Vector3 at = finisher ? origin : Vector3.Lerp(
                        Orbit(origin, i, balls.Length - 1, time, seed), origin,
                        Mathf.InverseLerp(ball.Launch - 6, ball.Launch, elapsed));
                    Body(at, ball.Size * (finisher ? 0.8f + charge * 0.3f : 1f), time, ball.Seed, 1f, finisher);
                    if (finisher) Particles(at, ball.Size, time, charge, ball.Seed, 0.65f, 0);
                    continue;
                }
                float fall = Mathf.Clamp01((elapsed - ball.Launch) / Math.Max(1f, ball.Impact - ball.Launch));
                Vector3 head = Flight(origin, ball.Landing, fall, ball.Seed);
                Vector3 tail = Flight(origin, ball.Landing, Mathf.Max(0f, fall - 0.36f), ball.Seed);
                Vector3 direction = (head - tail).Yto0();
                float angle = direction.AngleFlat();
                float speed = Mathf.Lerp(0.25f, 1f, fall);
                float size = ball.Size * Mathf.Lerp(0.88f, 1.12f, fall * fall);
                DrawLayer(flame, Strip, (head + tail) * 0.5f, angle,
                    size * Tuning.trailWidth, direction.magnitude + size * 0.65f, time, fall,
                    ball.Seed, speed, 1, finisher ? 1f : 0f, 0.09f);
                Body(head, size, time, ball.Seed, 1f, finisher, angle, speed);
                DrawLayer(impact, MeshPool.plane10, ball.Landing, 0, size * (1.2f - 0.6f * fall),
                    size * (1.2f - 0.6f * fall), time, 0.5f, ball.Seed, fall * 0.24f, 3, 0, 0, ground: true);
                Particles(tail, size, time, fall, ball.Seed, 0.7f, 2);
                // A short midair rupture travels with the head and never drives an impact.
                float burst = 1f - Mathf.Clamp01(Mathf.Abs(fall - 0.68f) / 0.12f);
                if (!finisher && burst > 0f)
                    DrawLayer(impact, MeshPool.plane10, head, 0, size * 1.7f, size * 1.7f,
                        time, (fall - 0.56f) / 0.24f, ball.Seed, burst * 0.55f, 0, 0, 0.07f);
            }
        }
        catch (Exception ex)
        {
            failed = true;
            Log.ErrorOnce("[明渊瞬时燃烧] 视觉绘制失败，切换简化显示。" + ex, 197633801);
        }
    }

    internal static void DrawWarmup(Vector3 center, CompProperties_AbilityMingyuanInstantCombustion props,
        float progress, float alpha, int casterId)
    {
        if (!Visible(center, props.radius + Tuning.height + 5f)) return;
        try
        {
            EnsureResources();
            float seed = Seed(casterId, center.ToIntVec3());
            float time = (progress - 1f) * 3f;
            Sigil(center, props.radius, time, Mathf.Clamp01(progress * 2.4f), alpha * 0.65f, seed);
            Vector3 core = CorePosition(center);
            float growth = Mathf.InverseLerp(0.24f, 0.7f, progress);
            Body(core, Tuning.coreSize * Mathf.Lerp(0.16f, 0.8f, growth), time, seed + 91.3f,
                alpha * growth, true);
            Particles(core, Tuning.coreSize, time, growth, seed, alpha * growth, 0);
            for (int i = 0; i < props.smallFireballCount; i++)
            {
                float reveal = 0.34f + i * 0.40f / props.smallFireballCount;
                float formation = Mathf.InverseLerp(reveal, reveal + 0.16f, progress);
                Vector3 at = Orbit(core, i, props.smallFireballCount, time, seed);
                if (i == 0) at = Vector3.Lerp(at, core, Mathf.InverseLerp(0.965f, 1f, progress));
                float size = Tuning.smallSize * (0.86f + 0.28f * Hash(seed + i));
                Body(at, size * Mathf.Lerp(0.12f, 1f, Mathf.SmoothStep(0f, 1f, formation)),
                    time, seed + i * 13.71f, alpha * formation, false);
            }
        }
        catch (Exception ex)
        {
            failed = true;
            Log.ErrorOnce("[明渊瞬时燃烧] 蓄力视觉失败，切换简化显示。" + ex, 197633802);
        }
    }

    private static void Impact(Vector3 at, float age, bool core, float seed, float radius)
    {
        if (failed) return;
        int duration = core ? Tuning.coreRemnantTicks : Tuning.smallRemnantTicks;
        if (age < 0 || age >= duration) return;
        float time = age / 60f;
        float life = age / duration;
        float flash = 1f - Mathf.Clamp01(age / (core ? 7f : 4f));
        if (flash > 0)
            DrawLayer(impact, MeshPool.plane10, at, 0, radius * 1.2f, radius * 1.2f,
                time, life, seed, flash * 0.94f, 2, core ? 1 : 0, 0.19f);
        int waves = core ? 3 : 1;
        for (int i = 0; i < waves; i++)
        {
            float wave = (age - i * 3f) / (core ? 23f + i * 9f : 18f);
            if (wave < 0f || wave >= 1f) continue;
            float size = radius * 2.5f * (1f + i * 0.12f);
            DrawLayer(impact, MeshPool.plane10, at, i * 67f, size, size, time, wave,
                seed + i * 9.1f, (1f - wave) * (i == 2 ? 0.24f : 0.72f),
                0, i, i == 0 ? 0.12f : 0.035f, ground: true);
        }
        float plume = Mathf.Clamp01(age / (core ? 44f : 24f));
        if (plume < 1f)
        {
            int lobes = core ? 3 : 1;
            for (int i = 0; i < lobes; i++)
            {
                float side = i == 0 ? 0f : i == 1 ? -0.62f : 0.62f;
                Vector3 position = at + new Vector3(side * radius * plume, 0f, radius * (0.35f + 0.8f * plume));
                float size = radius * (1.6f + 0.8f * Mathf.Sin(plume * Mathf.PI));
                DrawLayer(flame, Strip, position, -side * 26f, size, size * (core ? 1.6f : 1.1f),
                    time, plume, seed + i * 16f, Mathf.Pow(1f - plume, 1.3f) * 0.92f,
                    2, core ? 1 : 0, i == 0 ? 0.10f : 0.035f);
            }
        }
        DrawLayer(impact, MeshPool.plane10, at, seed * 17f, radius * 2.3f, radius * 2.3f,
            time, life, seed, (1f - life) * 0.65f, 1, core ? 1 : 0, 0, ground: true);
        if (Quality > 0)
            DrawLayer(impact, MeshPool.plane10, at + Vector3.forward * life * radius, 0,
                radius * (1f + life * 1.6f), radius * (1f + life * 1.6f),
                time, life, seed, Mathf.Sin(life * Mathf.PI) * 0.21f, 4, 0, 0);
        Particles(at, radius * (core ? 1.8f : 1.4f), time, life, seed, 1f, 1);
    }

    private static void Body(Vector3 at, float size, float time, float seed, float alpha,
        bool core, float angle = 0f, float stretch = 0f) =>
        DrawLayer(flame, MeshPool.plane10, at, angle, size, size * (1f + stretch * 0.18f),
            time, stretch, seed, alpha, 0, core ? 1 : 0, core ? 0.16f : 0.08f);

    private static void Sigil(Vector3 at, float radius, float time, float progress, float alpha, float seed)
    {
        if (alpha <= 0f) return;
        if (failed)
        {
            GenDraw.DrawRadiusRing(at.ToIntVec3(), radius, new Color(1f, 0.55f, 0.2f, alpha * progress));
            return;
        }
        DrawLayer(sigil, MeshPool.plane10, at, 0, radius * 2.12f, radius * 2.12f,
            time, progress, seed, alpha, 0, 0, 0.035f, ground: true);
    }

    private static void Particles(Vector3 at, float size, float time, float life, float seed, float alpha, int mode)
    {
        if (failed) return;
        DrawLayer(particle, particleMeshes[Quality], at, 0, size, size, time, life, seed,
            alpha, mode, 0, 0);
    }

    private static Vector3 CorePosition(Vector3 center) => center + Vector3.forward * Tuning.height;
    private static Vector3 Orbit(Vector3 center, int i, int count, float time, float seed)
    {
        float angle = i * Mathf.PI * 2f / count + time * 1.3f + seed;
        return center + new Vector3(2.25f * Mathf.Cos(angle), 0, 1.1f * Mathf.Sin(angle));
    }
    private static Vector3 Flight(Vector3 from, Vector3 to, float progress, float seed) =>
        Vector3.Lerp(from, to, Mathf.Pow(progress, 2.15f))
        + Vector3.right * ((Hash(seed) - 0.5f) * 0.9f * Mathf.Sin(progress * Mathf.PI));
    private static float Seed(int id, IntVec3 cell) => (uint)Gen.HashCombineInt(id, cell.GetHashCode()) % 8191 / 97f;
    private static float Hash(float seed) => Mathf.Repeat(Mathf.Sin(seed * 12.9898f) * 43758.545f, 1f);
    private static int Quality => (int)ArcaneVfxStyle.Quality;
    private static bool Visible(Vector3 center, float margin) => Find.CameraDriver != null
        && Find.CameraDriver.CurrentViewRect.ExpandedBy(Mathf.CeilToInt(margin)).Contains(center.ToIntVec3());

    private static Material flame, impact, sigil, particle, fallback, fallbackCore;
    private static Mesh[] strips, particleMeshes;
    private static bool initialized, failed;
    private static readonly MaterialPropertyBlock Block = new();
    private static readonly int FrameId = Shader.PropertyToID("_CombustionFrame");
    private static readonly int ShapeId = Shader.PropertyToID("_CombustionShape");
    private static readonly int CaptureId = Shader.PropertyToID("_CombustionCapture");
    private static ScreenBloomProperties bloom;
    private static Mesh Strip => failed ? MeshPool.plane10 : strips[Quality];

    private static void EnsureResources()
    {
        if (initialized) return;
        initialized = true;
        fallback = fallbackCore = FleckDefOf.FireGlow.graphicData.Graphic.MatSingle;
        try
        {
            Shader fireShader = ThingDefOf.Fire.graphicData.shaderType.Shader;
            const string path = "MiliraXianMingyuan/Effect/InstantCombustion/";
            fallback = MaterialPool.MatFrom(path + "SmallFireball", fireShader, Color.white);
            fallbackCore = MaterialPool.MatFrom(path + "CoreFireball", fireShader, Color.white);
            flame = Material("MX_Mingyuan_CombustionFlame");
            impact = Material("MX_Mingyuan_CombustionImpact");
            sigil = Material("MX_Mingyuan_CombustionSigil");
            particle = Material("MX_Mingyuan_CombustionParticle");
            strips = new Mesh[3];
            particleMeshes = new Mesh[3];
            for (int i = 0; i < 3; i++)
            {
                strips[i] = BuildMesh(i == 0 ? 6 : i == 1 ? 10 : 16, false);
                particleMeshes[i] = BuildMesh(i == 0 ? 12 : i == 1 ? 24 : 40, true);
            }
            bloom = new ScreenBloomProperties { group = DefDatabase<ScreenBloomGroupDef>.GetNamed("MX_ScreenBloom_Glow") };
        }
        catch (Exception ex)
        {
            failed = true;
            if (strips != null)
                foreach (Mesh mesh in strips) UnityEngine.Object.Destroy(mesh);
            if (particleMeshes != null)
                foreach (Mesh mesh in particleMeshes) UnityEngine.Object.Destroy(mesh);
            Log.ErrorOnce("[明渊瞬时燃烧] 专用 Shader 加载失败，使用原版火光与爆炸。" + ex, 197633803);
        }
    }

    private static Material Material(string name)
    {
        if (!Waylith.Rendering.ShaderRegistry.TryResolve(name, out _))
            throw new InvalidOperationException(name + " 未在原生渲染管线中注册");
        Shader shader = DefDatabase<ShaderDef>.GetNamed(name).Shader;
        if (shader == null) throw new InvalidOperationException(name + " 未能加载");
        return MaterialPool.MatFrom(new MaterialRequest(BaseContent.WhiteTex, shader, Color.white));
    }

    internal static bool NeedsFallback
    {
        get { EnsureResources(); return failed; }
    }

    private static void DrawLayer(Material material, Mesh mesh, Vector3 at, float angle, float width, float height,
        float time, float progress, float seed, float alpha, int mode, float detail, float glow, bool ground = false)
    {
        if (alpha <= 0.001f) return;
        at.y = (ground ? AltitudeLayer.MoteLow : AltitudeLayer.MoteOverheadLow).AltitudeFor();
        var matrix = Matrix4x4.TRS(at, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(width, 1, height));
        Block.Clear();
        if (failed)
        {
            if (ground || mode != 0) return;
            Block.SetColor(ShaderPropertyIDs.Color, Color.white.WithAlpha(alpha));
            Graphics.DrawMesh(MeshPool.plane10, matrix, detail > 0f ? fallbackCore : fallback, 0, null, 0, Block);
            return;
        }
        if (material == flame && mode == 1) time *= Tuning.trailFlowSpeed * (0.65f + progress);
        // Frame = seconds, lifecycle, stable seed, opacity.
        // Shape = profile, core/layer variant, ribbon displacement, quality.
        Block.SetVector(FrameId, new Vector4(time, progress, seed, alpha));
        Block.SetVector(ShapeId, new Vector4(mode, detail, Tuning.trailDistortion, Quality));
        Block.SetColor("_BloomTint", Color.white);
        Block.SetFloat("_BloomIntensity", 1f);
        Block.SetFloat(CaptureId, 0);
        Graphics.DrawMesh(mesh, matrix, material, 0, null, 0, Block);
        if (glow <= 0f) return;
        Block.SetFloat(CaptureId, 1);
        bloom.intensity = glow * Mathf.Clamp(Tuning.bloomIntensity, 0f, 0.35f) / 0.16f;
        ScreenBloom.Submit(bloom, mesh, matrix, material, Block);
    }

    private static Mesh BuildMesh(int count, bool particles)
    {
        var vertices = new List<Vector3>();
        var uv = new List<Vector2>();
        var data = new List<Vector4>();
        var indices = new List<int>();
        for (int i = 0; i < count; i++)
        {
            int start = vertices.Count;
            for (int corner = 0; corner < 4; corner++)
            {
                float x = corner >= 2 ? 1f : 0f;
                float y = corner == 1 || corner == 2 ? 1f : 0f;
                float along = particles ? y : (i + y) / count;
                vertices.Add(new Vector3(x - 0.5f, 0, along - 0.5f));
                uv.Add(new Vector2(x, along));
                data.Add(new Vector4(i, 0, 0, 0));
            }
            indices.Add(start); indices.Add(start + 1); indices.Add(start + 2);
            indices.Add(start); indices.Add(start + 2); indices.Add(start + 3);
        }
        var mesh = new Mesh { name = particles ? "Mingyuan combustion sparks" : "Mingyuan combustion ribbon" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetUVs(1, data);
        mesh.SetTriangles(indices, 0);
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(8, 2, 10));
        return mesh;
    }
}

using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;
using MiliraXian.Characters.Common.Vfx;

namespace MiliraXian.Characters.Mingyuan;

[StaticConstructorOnStartup]
internal static class MingyuanFireVfx
{
    private enum Shape { Scorch, Sigil, Phoenix, Vortex, Funnel, Crystal, Burst, Wake, Particles }
    private const int MaxParticles = 32;
    private static readonly Material FireMaterial;
    private static readonly Material PhoenixTakeoff;
    private static readonly Material PhoenixDive;
    private static readonly MaterialPropertyBlock Properties = new();
    private static readonly int ParamsId = Shader.PropertyToID("_FireParams");
    private static readonly int ShapeId = Shader.PropertyToID("_FireShape");
    private static readonly ScreenBloomProperties Bloom;
    private static readonly Mesh Funnel;
    private static readonly Mesh Particles;
    private static readonly List<Vector3> ScorchVertices = new(640);
    private static readonly List<Vector2> ScorchUv = new(640);
    private static readonly List<Vector4> ScorchCellData = new(640);
    private static readonly List<int> ScorchTriangles = new(960);
    private static readonly IntVec3[] Edges = { IntVec3.West, IntVec3.East, IntVec3.South, IntVec3.North };
    private static readonly Vector2[] QuadUv = { new(0, 0), new(0, 1), new(1, 1), new(1, 0) };

    static MingyuanFireVfx()
    {
        Shader shader = DefDatabase<ShaderDef>.GetNamed("MX_Mingyuan_FireStages").Shader;
        FireMaterial = MaterialPool.MatFrom(new MaterialRequest(BaseContent.WhiteTex, shader, Color.white));
        PhoenixTakeoff = LoadPhoenixMaterial(shader, "PhoenixTakeoff");
        PhoenixDive = LoadPhoenixMaterial(shader, "PhoenixDive");
        Bloom = new ScreenBloomProperties
        {
            group = DefDatabase<ScreenBloomGroupDef>.GetNamed("MX_ScreenBloom_Glow"),
            tint = new Color(1f, 0.78f, 0.55f),
            intensity = 0.34f
        };
        Funnel = BuildFunnel();
        Particles = BuildParticles();
    }

    private static float GroundAltitude => AltitudeLayer.MoteLow.AltitudeFor();
    private static float AirAltitude => AltitudeLayer.MoteOverheadLow.AltitudeFor();

    private static Material LoadPhoenixMaterial(Shader shader, string name)
    {
        Texture2D texture = ContentFinder<Texture2D>.Get("MiliraXianMingyuan/Effect/AscendantFlameDash/" + name);
        return texture == null ? null : MaterialPool.MatFrom(new MaterialRequest(texture, shader, Color.white));
    }

    private static void Draw(Shape shape, Mesh mesh, Matrix4x4 matrix, float time, float progress,
        float alpha, Vector4 options = default, bool bloom = false, Material material = null)
    {
        material ??= FireMaterial;
        if (alpha <= 0.001f || material == null) return;
        Properties.SetVector(ParamsId, new Vector4((int)shape, time, progress, alpha));
        Properties.SetVector(ShapeId, options);
        if (bloom) ScreenBloom.Submit(Bloom, mesh, matrix, material, Properties);
        Graphics.DrawMesh(mesh, matrix, material, 0, null, 0, Properties);
    }

    private static void Quad(Shape shape, Vector3 center, Quaternion rotation, float width, float height,
        float time, float progress, float alpha, Vector4 options = default, bool bloom = false, Material material = null) =>
        Draw(shape, MeshPool.plane10, Matrix4x4.TRS(center, rotation, new Vector3(width, 1f, height)),
            time, progress, alpha, options, bloom, material);

    private static bool Visible(Vector3 center, float margin) =>
        Find.CameraDriver.CurrentViewRect.ExpandedBy(Mathf.CeilToInt(margin)).Contains(center.ToIntVec3());

    private static void Sparks(Vector3 center, Quaternion rotation, float scale, float time, float style, float alpha, int count)
    {
        if (!Visible(center, scale * 2f)) return;
        float limit = Mathf.Clamp(count, 0, MaxParticles);
        if (Find.CameraDriver.CurrentViewRect.Width > 55) limit *= 0.5f;
        if (limit < 1f) return;
        Draw(Shape.Particles, Particles, Matrix4x4.TRS(center.WithY(AirAltitude), rotation, new Vector3(scale, 1f, scale)),
            time, style, alpha, new Vector4(0f, 0f, limit, 0f));
    }

    internal static void DrawDash(Vector3 ground, Vector3 pawn, Rot4 facing, Vector3 direction,
        int ticks, int diveStart, float progress, int feathers)
    {
        if (!Visible(pawn, 10f)) return;
        float time = ticks / 60f;
        Quaternion rotation = Quaternion.AngleAxis(direction.AngleFlat(), Vector3.up);
        Vector3 forward = direction.normalized;
        if (ticks < diveStart)
        {
            float lift = Mathf.Clamp01((pawn.z - ground.z) / 5f);
            Quad(Shape.Vortex, ground.WithY(GroundAltitude), Quaternion.identity, 3.6f, 3.6f, time, 0f, (1f - lift) * 0.8f);
            if (PhoenixTakeoff != null)
                Quad(Shape.Phoenix, pawn.WithY(AirAltitude), facing.AsQuat, 4.4f, 3.8f, time, 0f,
                    Mathf.Clamp01(time * 7f) * (1f - 0.65f * lift), bloom: true, material: PhoenixTakeoff);
            Sparks(ground + Vector3.forward * (pawn.z - ground.z) * 0.45f, rotation, 2.6f, time, 0f, 0.9f, feathers);
            return;
        }

        float fade = 1f - Mathf.SmoothStep(0f, 0.35f, Mathf.InverseLerp(0.85f, 1f, progress));
        Quad(Shape.Wake, (pawn - forward * 2.45f).WithY(AirAltitude - 0.01f), rotation,
            4.3f, 7.3f, time, 0f, fade * 0.88f, bloom: true);
        if (PhoenixDive != null)
            Quad(Shape.Phoenix, (pawn + forward * 0.23f).WithY(AirAltitude + 0.01f), rotation,
                6.8f, 7f, time, 0f, fade, bloom: true, material: PhoenixDive);
        Sparks(pawn, rotation, 3.6f, time, 1f, fade, feathers);
    }

    internal static void BuildScorch(Mesh mesh, Dictionary<IntVec3, CompMingyuanAscendantFlameScorch.ScorchedCell> cells)
    {
        ScorchVertices.Clear();
        ScorchUv.Clear();
        ScorchCellData.Clear();
        ScorchTriangles.Clear();
        foreach (var (cell, state) in cells)
        {
            int mask = 0;
            for (int i = 0; i < Edges.Length; i++)
                if (!cells.ContainsKey(cell + Edges[i])) mask |= 1 << i;
            Vector4 data = new(mask, state.Expires, 0f, 0f);
            AddQuad(ScorchVertices, ScorchUv, ScorchCellData, ScorchTriangles,
                cell.ToVector3ShiftedWithAltitude(GroundAltitude), data);
        }
        mesh.Clear();
        mesh.indexFormat = ScorchVertices.Count > 65535 ? UnityEngine.Rendering.IndexFormat.UInt32 : UnityEngine.Rendering.IndexFormat.UInt16;
        mesh.SetVertices(ScorchVertices);
        mesh.SetUVs(0, ScorchUv);
        mesh.SetUVs(1, ScorchCellData);
        mesh.SetTriangles(ScorchTriangles, 0);
    }

    internal static void DrawScorch(Mesh mesh, int duration)
    {
        int now = Find.TickManager.TicksGame;
        Draw(Shape.Scorch, mesh, Matrix4x4.identity, now / 60f, Mathf.Max(1, duration), 1f,
            new Vector4(now, 0f, 0f, 0f));
    }

    internal static void DrawGesture(Vector3 pawn, Rot4 facing, float progress)
    {
        if (!Visible(pawn, 2f)) return;
        float strength = Mathf.Sin(Mathf.Clamp01(progress) * Mathf.PI);
        Vector3 right = facing.AsQuat * Vector3.right;
        Vector3 forward = facing.AsQuat * Vector3.forward;
        Vector3 center = pawn + forward * 0.40f;
        float reach = Mathf.Lerp(0.48f, 0.12f, strength);
        Vector3 leftHand = center - right * reach;
        Vector3 rightHand = center + right * reach;
        MingyuanSkillVfx.Line(leftHand, leftHand + (right + forward) * 0.15f, 0.06f, strength);
        MingyuanSkillVfx.Line(rightHand, rightHand + (-right + forward) * 0.15f, 0.06f, strength);
        Quad(Shape.Crystal, (center + forward * 0.12f).WithY(AirAltitude + 0.02f), facing.AsQuat,
            0.34f, 0.45f, progress * 0.35f, progress, strength * 0.90f);
    }

    internal static void DrawWarning(Vector3 point, float alpha)
    {
        if (!Visible(point, 2f)) return;
        Quad(Shape.Sigil, point.WithY(GroundAltitude + 0.012f), Quaternion.identity, 3.0f, 3.0f,
            Find.TickManager.TicksGame / 60f, 1f, alpha * 0.76f, new Vector4(1f, 0f, 0f, 0f));
    }

    internal static void DrawWarmup(Verb_MingyuanFireCast verb, Vector3 origin, Vector3 target, float progress, float alpha)
    {
        float time = Find.TickManager.TicksGame / 60f;
        if (verb?.Combustion is { } combustion)
        {
            DrawSigil(origin, combustion.Props.radius, progress, alpha, time);
            Sparks(origin, Quaternion.identity, Mathf.Lerp(1f, combustion.Props.radius * 0.72f, progress),
                time, 2f, alpha, 12);
        }
        else
        {
            Vector3 direction = (target - origin).Yto0().normalized;
            Vector3 emitter = MingyuanBowVisualDrawer.EmitterPosition(origin, direction).WithY(AirAltitude);
            if (Visible(emitter, 2f))
            {
                MingyuanBowVisualDrawer.DrawFocusCharge(origin, direction, progress * alpha);
                Quad(Shape.Crystal, emitter, Quaternion.AngleAxis(direction.AngleFlat(), Vector3.up),
                    0.65f, 1.15f, time, progress, alpha, bloom: true);
                Sparks(emitter, Quaternion.identity, 1.25f, time, 0f, alpha, 10);
            }
            DrawWarning(target, alpha * Mathf.Lerp(0.55f, 1f, progress));
        }
    }

    internal static void DrawSigil(Vector3 center, float radius, float progress, float alpha, float time)
    {
        if (!Visible(center, radius + 1f)) return;
        if (progress < 0.01f) DrawWarning(center, alpha * 0.50f);
        Quad(Shape.Sigil, center.WithY(GroundAltitude + 0.02f), Quaternion.identity, radius * 2f, radius * 2f,
            time, progress, alpha);
    }

    internal static void DrawProjectile(Vector3 center, Quaternion rotation, float progress)
    {
        float time = Find.TickManager.TicksGame / 60f;
        Vector3 tail = center - rotation * Vector3.forward * 1.04f;
        Quad(Shape.Wake, tail, rotation, 0.95f, 2.7f, time, 0f, 0.72f);
        Quad(Shape.Crystal, center.WithY(AirAltitude), rotation, 0.86f, 1.56f, time, progress, 1f, bloom: true);
    }

    internal static void DrawTornado(Vector3 center, float radius, float growth, int age, float fade, int particleLimit)
    {
        if (!Visible(center, radius * 2f + 2f) || fade <= 0f) return;
        float time = age / 60f;
        float extent = radius * Mathf.Lerp(0.58f, 1f, fade);
        Quad(Shape.Vortex, center.WithY(GroundAltitude), Quaternion.identity, extent * 2f, extent * 2f,
            time, growth, fade * 0.82f);
        float height = Mathf.Lerp(1.25f, 7.1f, growth) * fade;
        var matrix = Matrix4x4.TRS(center.WithY(AirAltitude - 0.015f), Quaternion.identity, new Vector3(extent, 1f, extent));
        Draw(Shape.Funnel, Funnel, matrix, time, growth, fade * 0.80f,
            new Vector4(height / extent, 0f, 0f, 0f));
        Draw(Shape.Funnel, Funnel, matrix, time, growth, fade * 0.52f,
            new Vector4(height / extent, 1f, 0f, 0f));
        Vector3 core = center.WithY(AirAltitude + 0.018f) + Vector3.forward * (0.52f + 0.16f * Mathf.Sin(time * 3f));
        Quaternion tilt = Quaternion.AngleAxis(78f * (1f - Mathf.Clamp01(time * 3f)), Vector3.up);
        Quad(Shape.Crystal, core, tilt, 1.25f * fade, 1.95f * fade, time, growth, fade, bloom: true);
        if (age < 22)
            Quad(Shape.Burst, center.WithY(GroundAltitude + 0.02f), Quaternion.identity, 3.4f, 3.4f,
                time, age / 22f, fade, bloom: true);
        Sparks(center, Quaternion.identity, extent, time, 3f, fade, Mathf.RoundToInt(Mathf.Lerp(8, particleLimit, growth)));
    }

    internal static void PlayWarmup(Pawn caster, AbilityDef ability, Stance_Warmup stance, IntVec3 target)
    {
        var visual = (Thing_MingyuanFireCastVisual)ThingMaker.MakeThing(MX_MingyuanDefOf.MX_Mingyuan_FireCastVisual);
        visual.InitWarmup(caster, ability, stance, target);
        GenSpawn.Spawn(visual, caster.Position, caster.Map);
    }

    internal static void PlayBurst(Map map, Vector3 center, float radius, int duration, bool sigil = false)
    {
        if (map == null || !center.ToIntVec3().InBounds(map)) return;
        var visual = (Thing_MingyuanFireCastVisual)ThingMaker.MakeThing(MX_MingyuanDefOf.MX_Mingyuan_FireCastVisual);
        visual.InitBurst(center, radius, duration, sigil);
        GenSpawn.Spawn(visual, center.ToIntVec3(), map);
    }

    internal static void DrawBurst(Vector3 center, float radius, float progress, bool sigil)
    {
        if (!Visible(center, radius + 1f)) return;
        float time = Find.TickManager.TicksGame / 60f;
        float fade = 1f - progress;
        if (sigil) DrawSigil(center, radius, 1f, fade * fade, time);
        Quad(Shape.Burst, center.WithY(GroundAltitude + 0.025f), Quaternion.identity, radius * 2f, radius * 2f,
            time, Mathf.Clamp01(progress * 1.6f), fade, bloom: true);
        Sparks(center, Quaternion.identity, radius, time, 4f, fade, 16);
    }

    private static Mesh BuildFunnel()
    {
        const int around = 32;
        const int up = 12;
        var vertices = new List<Vector3>((around + 1) * (up + 1));
        var uv = new List<Vector2>(vertices.Capacity);
        var triangles = new List<int>(around * up * 6);
        for (int y = 0; y <= up; y++)
        for (int x = 0; x <= around; x++)
        {
            uv.Add(new Vector2(x / (float)around, y / (float)up));
            vertices.Add(new Vector3(x / (float)around - 0.5f, 0f, y / (float)up));
            if (x == around || y == up) continue;
            int i = y * (around + 1) + x;
            triangles.Add(i);
            triangles.Add(i + around + 1);
            triangles.Add(i + 1);
            triangles.Add(i + 1);
            triangles.Add(i + around + 1);
            triangles.Add(i + around + 2);
        }
        Mesh mesh = new() { name = "Mingyuan funnel" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetTriangles(triangles, 0);
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(5f, 2f, 8f));
        return mesh;
    }

    private static Mesh BuildParticles()
    {
        var vertices = new List<Vector3>(MaxParticles * 4);
        var uv = new List<Vector2>(MaxParticles * 4);
        var indices = new List<Vector4>(MaxParticles * 4);
        var triangles = new List<int>(MaxParticles * 6);
        for (int i = 0; i < MaxParticles; i++)
            AddQuad(vertices, uv, indices, triangles, Vector3.zero, new Vector4(i, 0, 0, 0));

        Mesh mesh = new() { name = "Mingyuan fire particles" };
        mesh.SetVertices(vertices);
        mesh.SetUVs(0, uv);
        mesh.SetUVs(1, indices);
        mesh.SetTriangles(triangles, 0);
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(5f, 2f, 8f));
        return mesh;
    }

    private static void AddQuad(List<Vector3> vertices, List<Vector2> uv, List<Vector4> data, List<int> triangles,
        Vector3 center, Vector4 extra)
    {
        int first = vertices.Count;
        foreach (Vector2 corner in QuadUv)
        {
            vertices.Add(center + new Vector3(corner.x - 0.5f, 0f, corner.y - 0.5f));
            uv.Add(corner);
            data.Add(extra);
        }
        triangles.Add(first);
        triangles.Add(first + 1);
        triangles.Add(first + 2);
        triangles.Add(first);
        triangles.Add(first + 2);
        triangles.Add(first + 3);
    }
}

public class Thing_MingyuanFireCastVisual : Thing
{
    private Pawn caster;
    private AbilityDef ability;
    private Vector3 origin;
    private IntVec3 aimCell;
    private int stanceStart;
    private int endTick;
    private int duration;
    private float progress;
    private float radius;
    private bool burst;
    private bool sigil;
    private bool etchPlayed;
    private Sustainer castLoop;

    internal void InitWarmup(Pawn pawn, AbilityDef def, Stance_Warmup stance, IntVec3 target)
    {
        caster = pawn;
        ability = def;
        stanceStart = stance.startedTick;
        origin = pawn.Position.ToVector3Shifted();
        aimCell = target;
        duration = 20;
        radius = (stance.verb as Verb_MingyuanFireCast)?.Combustion?.Props.radius ?? 0f;
        sigil = radius > 0f;
    }

    internal void InitBurst(Vector3 center, float effectRadius, int ticks, bool isSigil)
    {
        origin = center;
        radius = effectRadius;
        duration = Mathf.Max(1, ticks);
        endTick = Find.TickManager.TicksGame + duration;
        burst = true;
        sigil = isSigil;
    }

    private Verb_MingyuanFireCast Verb => caster?.abilities?.GetAbility(ability)?.verb as Verb_MingyuanFireCast;

    private bool IsWarming(Verb_MingyuanFireCast verb) => !MingyuanPowerBalance.Sealed
        && caster is { Spawned: true, Dead: false } && caster.Map == Map
        && verb?.WarmupStance is { } stance && stance.startedTick == stanceStart;

    protected override void Tick()
    {
        int now = Find.TickManager.TicksGame;
        if (endTick > 0)
        {
            if (now >= endTick) Destroy();
            return;
        }

        Verb_MingyuanFireCast verb = Verb;
        if (!IsWarming(verb))
        {
            // A completed cast has its own release visual. An interrupted circle fades at the last drawn angle.
            castLoop?.End();
            castLoop = null;
            if (progress >= 0.99f) Destroy();
            else endTick = now + duration;
            return;
        }

        origin = caster.Position.ToVector3Shifted();
        if (Position != caster.Position) Position = caster.Position;
        int elapsed = verb.CastTicks - verb.WarmupTicksLeft;
        progress = Mathf.Clamp01((elapsed - verb.JumpTicks) / (float)(verb.CastTicks - verb.JumpTicks));
        SoundDef hum = verb.Combustion?.Props.warmupLoop ?? verb.Pillar?.Props.warmupLoop;
        if (hum != null && Map == Find.CurrentMap)
        {
            if (castLoop == null || castLoop.Ended)
                castLoop = hum.TrySpawnSustainer(SoundInfo.InMap(this, MaintenanceType.PerTick));
            if (castLoop != null)
            {
                castLoop.externalParams["FireCharge"] = Mathf.Lerp(0.26f, 0.85f, progress);
                castLoop.Maintain();
            }
        }
        if (!etchPlayed && elapsed >= verb.JumpTicks && verb.Combustion is { } combustion)
        {
            etchPlayed = true;
            combustion.Props.etchSound?.PlayOneShot(new TargetInfo(Position, Map));
        }
    }

    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        float remaining = endTick > 0 ? Mathf.Clamp01((endTick - Find.TickManager.TicksGame) / (float)duration) : 1f;
        if (burst) MingyuanFireVfx.DrawBurst(origin, radius, 1f - remaining, sigil);
        else if (sigil && endTick > 0)
            MingyuanFireVfx.DrawSigil(origin, radius, progress, remaining, Find.TickManager.TicksGame / 60f);
        else MingyuanFireVfx.DrawWarmup(Verb, origin, aimCell.ToVector3Shifted(), progress, remaining);
    }

    public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
    {
        castLoop?.End();
        castLoop = null;
        base.DeSpawn(mode);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref caster, "caster");
        Scribe_Defs.Look(ref ability, "ability");
        Scribe_Values.Look(ref origin, "origin");
        Scribe_Values.Look(ref aimCell, "aimCell");
        Scribe_Values.Look(ref stanceStart, "stanceStart");
        Scribe_Values.Look(ref endTick, "endTick");
        Scribe_Values.Look(ref duration, "duration");
        Scribe_Values.Look(ref progress, "progress");
        Scribe_Values.Look(ref radius, "radius");
        Scribe_Values.Look(ref burst, "burst");
        Scribe_Values.Look(ref sigil, "sigil");
        Scribe_Values.Look(ref etchPlayed, "etchPlayed");
    }
}

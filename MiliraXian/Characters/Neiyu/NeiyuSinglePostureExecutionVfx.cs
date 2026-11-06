using System;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Common.Vfx.BladeBody;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu;

[StaticConstructorOnStartup]
internal static class NeiyuExecutionMaterials
{
    private const string Folder = "MiliraXianNeiyu/Effect/SwordFight/";
    internal static readonly Material Cross = Sprite("ExecutionCross");
    internal static readonly Material Sword = Sprite("ExecutionSpiritSword");
    internal static readonly Material Glyph = Sprite("ExecutionDuan");
    internal static readonly Material Cut = Sprite("ExecutionCut");
    internal static readonly Material Glow = MaterialPool.MatFrom(
        "Common/WorldArcaneStrike/neiyu_halo_vfx/SoftGlow", ShaderDatabase.MoteGlow, Color.white);
    internal static readonly Material Wave = MaterialPool.MatFrom(BaseContent.WhiteTex, ShaderDatabase.Transparent, Color.white);

    private static Material Sprite(string name)
    {
        Texture2D texture = ContentFinder<Texture2D>.Get(Folder + name);
        return texture == null ? null : MaterialPool.MatFrom(texture, ShaderDatabase.Transparent, Color.white);
    }
}

public sealed partial class JobDriver_NeiyuPostureExecution
{
    private static readonly Color ExecutionPink = new(1f, .64f, .84f);
    private static Color ExecutionIce => NeiyuSwordCombatVfx.Ice;
    private Mesh slashWave;

    private void FinishSingle()
    {
        executionMap?.GetComponent<MapComponent_PawnAfterimages>()?.RemoveOwnedBy(this);
        if (slashWave != null)
        {
            UnityEngine.Object.Destroy(slashWave);
            slashWave = null;
        }
    }

    internal void DrawSingleVfx(BladeGraphicExtension ext, in BladePose pose, Vector3 body)
    {
        if (!NeiyuSwordCombatVfx.Enabled || NeiyuSwordCombatVfx.Intensity <= 0f || pawn.IsHiddenFromPlayer()
            || Find.UIRoot.HideMotes || Find.ScreenshotModeHandler.Active) return;
        try
        {
            float tick = Mathf.Max(0f, SceneFrame);
            if (tick >= Single.readyTicks && tick < SwingAt)
            {
                BladeRenderer.GetGripAndTip(ext, pose, body, out Vector3 grip, out Vector3 tip);
                DrawEnchantment(grip, tip, tick, pose.reveal);
            }
            if (tick >= SwingAt && tick < ApproachAt)
                DrawBladeTrail(ext, tick, body);
            if (tick >= FirstCutAt && tick < FirstCutAt + Single.waveTicks)
                DrawSlashWave(ext, tick);
            if (tick >= ReleaseAt)
                DrawVolley(tick, body);
        }
        catch (Exception exception)
        {
            Log.ErrorOnce($"[霓羽弹刀] 单体处决绘制失败：{exception}", 197632105);
        }
    }

    private void DrawEnchantment(Vector3 grip, Vector3 tip, float tick, float lit)
    {
        Vector3 axis = tip - grip;
        Vector3 side = Vector3.Cross(Vector3.up, axis).normalized;
        int count = Mathf.Clamp(Mathf.RoundToInt(12f * NeiyuSwordCombatVfx.Intensity), 2, 18);
        for (int i = 0; i < count; i++)
        {
            float particle = Mathf.Repeat((tick - Single.readyTicks) / 12f + i * .618034f, 1f);
            float along = particle * lit;
            Vector3 at = grip + axis * along + side * (.12f * Mathf.Sin(i * 2.1f + particle * 6f));
            Sprite(NeiyuExecutionMaterials.Glow, at, 0f, .15f, .15f,
                Color.Lerp(ExecutionPink, ExecutionIce, particle), lit * Mathf.Sin(particle * Mathf.PI));
        }
        float front = Mathf.Clamp01(lit);
        Sprite(NeiyuExecutionMaterials.Glow, Vector3.Lerp(grip, tip, front), 0f, .23f, .23f, Color.white, front);
        if (tick >= FlashAt)
        {
            float fade = 1f - ProgressBetween(tick, FlashAt, SwingAt);
            Sprite(NeiyuExecutionMaterials.Cross, tip, -10f, 1.68f, 1.68f, Color.white, fade);
        }
    }

    private void DrawBladeTrail(BladeGraphicExtension ext, float tick, Vector3 body)
    {
        // Sweep the same calibrated grip and tip through the previous poses.
        for (int i = 1; i <= 6; i++)
        {
            float sample = tick - i * .72f;
            if (sample < SwingAt) break;
            BladePose past = BladeAt(sample, body, aimPoint);
            BladeRenderer.GetGripAndTip(ext, past, body, out Vector3 grip, out Vector3 tip);
            Vector3 line = (tip - grip).Yto0();
            float fade = (1f - i / 7f) * (1f - ProgressBetween(tick, FirstCutAt + Single.hitStopTicks, ApproachAt));
            Sprite(NeiyuExecutionMaterials.Glow, Vector3.Lerp(grip, tip, .63f), line.AngleFlat(),
                .11f + .038f * i, line.magnitude, Color.Lerp(ExecutionIce, ExecutionPink, i / 6f), .58f * fade);
        }
    }

    private void DrawSlashWave(BladeGraphicExtension ext, float tick)
    {
        slashWave ??= BuildSlashWave(ext);
        float p = ProgressBetween(tick, FirstCutAt, FirstCutAt + Single.waveTicks);
        Vector3 at = origin.ToVector3Shifted() + Forward(initialYaw) * Mathf.Lerp(.08f, 1.65f, p);
        at.y = AltitudeLayer.MoteOverhead.AltitudeFor();
        Matrix4x4 matrix = Matrix4x4.TRS(at, Quaternion.AngleAxis(initialYaw + 11f, Vector3.up),
            new Vector3(Mathf.Lerp(.92f, 1f, p), 1f, Mathf.Lerp(.7f, 1.2f, p)));
        DrawMesh(slashWave, matrix, NeiyuExecutionMaterials.Wave, Color.white, (1f - p) * (1f - p));
    }

    private Mesh BuildSlashWave(BladeGraphicExtension ext)
    {
        const int segments = 28, rows = 5;
        float[] depths = [.035f, 0f, -.10f, -.37f, -.67f];
        float[] opacity = [0f, 1f, .68f, .38f, 0f];
        Vector3[] edge = new Vector3[segments + 1];
        float min = float.PositiveInfinity, max = float.NegativeInfinity;
        for (int i = 0; i <= segments; i++)
        {
            Single.drawBlade.Evaluate(Mathf.Lerp(.20f, 1f, i / (float)segments),
                out Vector3 grip, out Vector3 direction, out Vector3 bladeEdge);
            BladePose sample = new() { grip = grip, dir = direction, edge = bladeEdge, heightToScreen = .68f };
            BladeRenderer.GetGripAndTip(ext, sample, Vector3.zero, out _, out Vector3 tip);
            edge[i] = tip;
            min = Mathf.Min(min, tip.x);
            max = Mathf.Max(max, tip.x);
        }
        Vector3[] vertices = new Vector3[(segments + 1) * rows];
        Vector2[] uv = new Vector2[vertices.Length];
        Color[] colors = new Color[vertices.Length];
        int[] triangles = new int[segments * (rows - 1) * 6];
        for (int i = 0; i <= segments; i++)
        {
            float u = i / (float)segments;
            float taper = Mathf.Sqrt(Mathf.Max(0f, Mathf.Sin(u * Mathf.PI)));
            float span = Mathf.Lerp(u, Mathf.InverseLerp(min, max, edge[i].x), .75f);
            for (int row = 0; row < rows; row++)
            {
                int v = i * rows + row;
                vertices[v] = new Vector3((span - .5f) * Single.slashWidth, 0f,
                    edge[i].z * .62f + depths[row] * taper);
                uv[v] = new Vector2(u, row / (float)(rows - 1));
                Color tint = row == 1 ? Color.white : Color.Lerp(ExecutionIce, ExecutionPink, .15f + .7f * u);
                colors[v] = tint.WithAlpha(opacity[row] * taper);
                if (i == segments || row == rows - 1) continue;
                int t = (i * (rows - 1) + row) * 6;
                triangles[t] = v; triangles[t + 1] = v + rows; triangles[t + 2] = v + 1;
                triangles[t + 3] = v + 1; triangles[t + 4] = v + rows; triangles[t + 5] = v + rows + 1;
            }
        }
        Mesh mesh = new() { name = "MX_NeiyuExecutionWave", vertices = vertices, uv = uv, colors = colors, triangles = triangles };
        mesh.RecalculateBounds();
        return mesh;
    }

    private void DrawVolley(float tick, Vector3 body)
    {
        Vector3 aim = Vector3.Lerp(previousTargetDraw, aimPoint, FrameLerp);
        Vector3 forward = Forward(throwYaw);
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        float glyphAge = tick - VolleyAt;
        if (glyphAge >= 0f && glyphAge < Single.glyphHoldTicks + Single.glyphFadeTicks)
        {
            Vector3 at = body + side * 1.16f + Vector3.forward * .57f;
            float fade = 1f - Mathf.Clamp01((glyphAge - Single.glyphHoldTicks) / Single.glyphFadeTicks);
            Sprite(NeiyuExecutionMaterials.Glyph, at, 0f, 1.28f, 1.28f, Color.white, fade);
        }
        for (int i = 0; i < Single.swordCount; i++)
        {
            int offset = i * Single.swordStaggerTicks;
            int launch = VolleyAt + offset, impact = FirstImpactAt + offset;
            float slot = i == 0 ? 0f : ((i + 1) / 2) * (i % 2 == 1 ? -1f : 1f);
            Vector3 seat = body.Yto0() + side * (.52f * slot) - forward * (.72f + .16f * Mathf.Abs(slot))
                + Vector3.forward * (.42f + .11f * Mathf.Abs(slot));
            if (tick < impact)
            {
                float p = ProgressBetween(tick, launch, impact);
                float appear = ProgressBetween(tick, ReleaseAt, VolleyAt);
                Vector3 direction = (aim - seat).Yto0().normalized;
                Vector3 tip = Vector3.Lerp(seat + direction * .70f, aim, p * p);
                Vector3 center = tip - direction * .70f;
                float yaw = direction.sqrMagnitude > .001f ? direction.AngleFlat() : throwYaw;
                Sprite(NeiyuExecutionMaterials.Sword, center, yaw, .85f, 1.5f, Color.white, appear);
                if (p > 0f)
                    Sprite(NeiyuExecutionMaterials.Glow, center - direction * .53f, yaw, .18f, 1.55f,
                        Color.Lerp(ExecutionIce, ExecutionPink, Mathf.Abs(slot) / 4f), .73f);
            }
            else if (impacts > i)
            {
                float age = tick - impact;
                DrawPierce(aimPoint, forward, side, i, age);
            }
        }
    }

    private void DrawPierce(Vector3 at, Vector3 forward, Vector3 side, int blade, float age)
    {
        if (age >= Single.pierceTicks) return;
        float p = age / Single.pierceTicks;
        float fade = (1f - p) * (1f - p);
        float size = blade == 0 ? 1.65f : .75f;
        Sprite(NeiyuExecutionMaterials.Cut, at, blade == 0 ? -10f : 16f * (blade - 2), size * 1.3f, size,
            Color.Lerp(Color.white, ExecutionIce, blade * .11f), fade);
        int count = Mathf.Clamp(Mathf.RoundToInt(7f * NeiyuSwordCombatVfx.Intensity), 1, 10);
        for (int i = 0; i < count; i++)
        {
            int seed = pawn.thingIDNumber + blade * 593 + i * 97;
            float spread = NeiyuSwordCombatVfx.Hash(seed);
            Vector3 drift = forward * (.22f + NeiyuSwordCombatVfx.Hash(seed + 37)) + side * (spread - .5f);
            Sprite(NeiyuExecutionMaterials.Glow, at + drift * p, drift.AngleFlat(), .11f, .21f,
                Color.Lerp(ExecutionIce, ExecutionPink, spread), fade);
        }
    }

    private static void Sprite(Material material, Vector3 at, float angle, float width, float length, Color tint, float alpha)
    {
        at.y = AltitudeLayer.MoteOverhead.AltitudeFor() + .015f;
        Matrix4x4 matrix = Matrix4x4.TRS(at, Quaternion.AngleAxis(angle, Vector3.up), new Vector3(width, 1f, length));
        DrawMesh(MeshPool.plane10, matrix, material, tint, alpha);
    }

    private static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, Color tint, float alpha)
    {
        if (material == null || alpha <= .001f) return;
        MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
        block.SetColor(ShaderPropertyIDs.Color, tint.WithAlpha(Mathf.Clamp01(alpha * NeiyuSwordCombatVfx.Intensity)));
        Graphics.DrawMesh(mesh, matrix, material, 0, null, 0, block);
        block.Clear();
    }
}

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
    internal static readonly Material Wave = MaterialPool.MatFrom(
        DefDatabase<FleckDef>.GetNamed("MXNL_Fleck_BladeLight_Neiyu_E").graphicData.texPath,
        ShaderDatabase.Transparent, Color.white);
    internal static readonly Material MultiWave = Wave?.mainTexture is Texture2D texture
        && DefDatabase<ShaderDef>.GetNamedSilentFail("MX_NeiyuExecutionWave")?.Shader is { } shader
        ? MaterialPool.MatFrom(texture, shader, Color.white) : Wave;

    private static Material Sprite(string name)
    {
        Texture2D texture = ContentFinder<Texture2D>.Get(Folder + name);
        return texture == null ? null : MaterialPool.MatFrom(texture, ShaderDatabase.Transparent, Color.white);
    }
}

public sealed partial class JobDriver_NeiyuPostureExecution
{
    private static readonly Color ExecutionPink = new(1f, .64f, .84f);
    // Turn the diagonal artwork to face forward and center its visible arc.
    private static readonly Matrix4x4 SlashArtwork = Matrix4x4.TRS(
        new Vector3(-.011f, 0f, -.07f), Quaternion.AngleAxis(35f, Vector3.up), Vector3.one);
    private static Color ExecutionIce => NeiyuSwordCombatVfx.Ice;

    private void FinishScene()
    {
        executionMap?.GetComponent<MapComponent_PawnAfterimages>()?.RemoveOwnedBy(this);
        if (IsSingle) executionMap?.GetComponent<MapComponent_NeiyuSwordVfx>()?.UntrackVolley(this);
        if (multiRing != null) UnityEngine.Object.Destroy(multiRing);
        multiRing = null;
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
            if (tick >= SwingAt && tick < KnockbackAt)
                DrawBladeTrail(ext, tick, body);
            if (tick >= FirstCutAt && tick < FirstCutAt + Single.waveTicks)
                DrawSlashWave(tick);
        }
        catch (Exception exception)
        {
            Log.ErrorOnce($"[霓羽弹刀] 单体处决绘制失败：{exception}", 197632105);
        }
    }

    internal void DrawSingleVolley()
    {
        if (pawn?.jobs?.curDriver != this || pawn.IsHiddenFromPlayer()
            || NeiyuSwordCombatVfx.Intensity <= 0f) return;
        float tick = SceneFrame;
        if (tick < ReleaseAt) return;
        try { DrawVolley(tick, pawn.DrawPos); }
        catch (Exception exception)
        {
            Log.ErrorOnce($"[霓羽弹刀] 飞剑绘制失败：{exception}", 197632106);
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
            float fade = (1f - i / 7f) * (1f - ProgressBetween(tick, FirstCutAt + hitStopTicks, KnockbackAt));
            Sprite(NeiyuExecutionMaterials.Glow, Vector3.Lerp(grip, tip, .63f), line.AngleFlat(),
                .11f + .038f * i, line.magnitude, Color.Lerp(ExecutionIce, ExecutionPink, i / 6f), .58f * fade);
        }
    }

    private void DrawSlashWave(float tick)
    {
        float p = ProgressBetween(tick, FirstCutAt, FirstCutAt + Single.waveTicks);
        Vector3 at = origin.ToVector3Shifted() + Forward(initialYaw) * Mathf.Lerp(.18f, 4f, p);
        at.y = AltitudeLayer.MoteOverhead.AltitudeFor() + .015f;
        float fade = 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(.28f, 1f, p));
        DrawSlashArtwork(at, initialYaw + 11f, Single.slashWidth * Mathf.Lerp(.97f, 1.07f, p),
            Single.slashWidth * .38f * Mathf.Lerp(.92f, 1.13f, p), fade);
    }

    private static void DrawSlashArtwork(Vector3 at, float yaw, float width, float length, float fade, Material material = null)
    {
        // The rotated arc covers 52% of the texture width and 81% of its length.
        Matrix4x4 matrix = Matrix4x4.TRS(at, Quaternion.AngleAxis(yaw, Vector3.up),
            new Vector3(width / .52f, 1f, length / .81f));
        DrawMesh(MeshPool.plane10, matrix * SlashArtwork, material ?? NeiyuExecutionMaterials.Wave, Color.white, fade);
    }

    private void DrawVolley(float tick, Vector3 body)
    {
        Vector3 aim = Vector3.Lerp(previousTargetDraw, aimPoint, FrameLerp);
        Vector3 forward = Forward(throwYaw);
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        float swordScale = Single.spiritSwordScale;
        float tipOffset = .70f * swordScale;
        float swordWidth = .85f * swordScale, swordLength = 1.5f * swordScale;
        float slotSpacing = Mathf.Lerp(.52f, .52f * swordScale, .65f);
        float glyphAge = tick - (singleAirVolley ? AimAt : VolleyAt);
        if (glyphAge >= 0f && glyphAge < Single.glyphHoldTicks + Single.glyphFadeTicks)
        {
            Vector3 at = body + side * 1.16f + Vector3.forward * .57f;
            float fade = 1f - Mathf.Clamp01((glyphAge - Single.glyphHoldTicks) / Single.glyphFadeTicks);
            Sprite(NeiyuExecutionMaterials.Glyph, at, 0f, 1.28f, 1.28f, Color.white, fade);
        }
        for (int i = 0; i < swordCount; i++)
        {
            int launch = SwordLaunchAt(i), impact = SwordImpactAt(i);
            bool terminal = i == swordCount - 1;
            float slot = i == 0 ? 0f : ((i + 1) / 2) * (i % 2 == 1 ? -1f : 1f);
            Vector3 seat = body.Yto0() + side * (slotSpacing * slot) - forward * (.72f + .16f * Mathf.Abs(slot))
                + Vector3.forward * (.42f + .11f * Mathf.Abs(slot));
            Vector3 contact = terminal ? aim : aim + side * (.13f * slot)
                + Vector3.forward * (.13f - .065f * Mathf.Abs(slot));
            Vector3 toContact = (contact - seat).Yto0();
            float distance = toContact.magnitude;
            Vector3 direction = distance > .001f ? toContact / distance : forward;
            float yaw = direction.AngleFlat();
            if (tick < impact)
            {
                float p = ProgressBetween(tick, launch, impact);
                float appear = singleAirVolley ? 1f : ProgressBetween(tick, ReleaseAt, AimAt);
                // Keep the enlarged tip on the same contact point, including short throws into walls.
                Vector3 startTip = seat + direction * Mathf.Min(tipOffset, distance * .8f);
                Vector3 tip = Vector3.Lerp(startTip, contact, p * (.72f + .28f * p));
                Vector3 center = tip - direction * tipOffset;
                Sprite(NeiyuExecutionMaterials.Sword, center, yaw, swordWidth, swordLength, Color.white, appear);
                if (p > 0f)
                    Sprite(NeiyuExecutionMaterials.Glow, center - direction * (.53f * swordScale),
                        yaw, .18f * swordScale, 1.55f * swordScale,
                        Color.Lerp(ExecutionIce, ExecutionPink, Mathf.Abs(slot) / 4f), .73f);
            }
            else if (impacts > i)
            {
                float age = tick - impact;
                if (!terminal)
                {
                    float settle = Mathf.Clamp01(age / 6f);
                    float pinYaw = yaw + slot * 8f * settle;
                    Vector3 center = contact - Forward(pinYaw) * (tipOffset * Mathf.Lerp(1f, .68f, settle));
                    float fade = 1f - ProgressBetween(tick, FinalImpactAt, FinalImpactAt + Single.pierceTicks);
                    Sprite(NeiyuExecutionMaterials.Sword, center, pinYaw, swordWidth, swordLength, Color.white, .92f * fade);
                }
                DrawPierce(contact, forward, side, i, terminal, age);
            }
        }
    }

    private void DrawPierce(Vector3 at, Vector3 forward, Vector3 side, int blade, bool terminal, float age)
    {
        if (age >= Single.pierceTicks) return;
        float p = age / Single.pierceTicks;
        float fade = (1f - p) * (1f - p);
        float size = terminal ? 1.65f : .75f;
        Sprite(NeiyuExecutionMaterials.Cut, at, terminal ? -10f : 16f * (blade - 2), size * 1.3f, size,
            terminal ? Color.white : Color.Lerp(Color.white, ExecutionIce, .44f), fade);
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

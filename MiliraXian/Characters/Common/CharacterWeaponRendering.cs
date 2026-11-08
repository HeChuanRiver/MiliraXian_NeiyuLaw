using HarmonyLib;
using MiliraXian.Characters.Common.Vfx.BladeBody;
using MiliraXian.Characters.Common.Vfx.BladeMotion;
using MiliraXian.Characters.Mingyuan;
using MiliraXian.Characters.Neiyu;
using MiliraXian.Characters.Zhaoli;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common;

// One interception point for held-weapon poses. Character renderers own their
// geometry; the shared entry does not own a second animation clock or cache.
[HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
internal static class Patch_CharacterWeaponRendering
{
    private static bool Prefix(Thing eq, ref Vector3 drawLoc, ref float aimAngle)
    {
        NeiyuSwordCombatVfx.AdjustExecutionWeapon(eq, ref drawLoc, ref aimAngle);
        return Patch_ZhaoliDuanzhan_DrawEquipmentAiming.DrawWeapon(eq, drawLoc, aimAngle);
    }
}

// Rigid 3D blade: while the job driver poses the blade (IBladePoseSource) or the pawn plays a
// motion with a blade track (BladeMotionPlayer) and the weapon is calibrated
// (BladeGraphicExtension), the blade is drawn here instead of the vanilla flat weapon. Worn
// apparel extras are still drawn.
[HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAndApparelExtras))]
internal static class Patch_CharacterBladeBodyRendering
{
    private static bool Prefix(Pawn pawn, PawnRenderFlags flags)
    {
        if (pawn == null
            || flags.FlagSet(PawnRenderFlags.Portrait)
            || flags.FlagSet(PawnRenderFlags.Cache))
        {
            return true;
        }
        // Mingyuan's time burn warmup hides the held weapon (its own effect is drawn instead).
        if (pawn.stances?.curStance is not Stance_Warmup { verb: Verb_MingyuanTimeBurn }
            || MingyuanPowerBalance.Sealed)
        {
            // The playing motion's own blade, else the held weapon's calibration (or its whole-texture default).
            BladeGraphicExtension ext = BladeMotionPlayer.DrawnBlade(pawn);
            if (ext == null || pawn.CurJob?.def?.neverShowWeapon != false || !TryGetBladePose(pawn, out BladePose pose))
            {
                return true;
            }
            Vector3 body = pawn.DrawPos;
            BladeRenderer.Draw(ext, pose, body, BladeOpacity.For(pawn));
            if (pawn.jobs.curDriver is JobDriver_NeiyuPostureExecution { IsSingle: true } execution)
                execution.DrawSingleVfx(ext, pose, body);
            else if (pawn.jobs.curDriver is JobDriver_NeiyuPostureExecution { IsMulti: true } multi)
                multi.DrawMultiVfx(body);
        }
        if (pawn.apparel != null)
        {
            foreach (Apparel apparel in pawn.apparel.WornApparel)
            {
                apparel.DrawWornExtras();
            }
        }
        return false;
    }

    // A job driver that poses the blade itself (a procedural path) comes before the motion player.
    private static bool TryGetBladePose(Pawn pawn, out BladePose pose)
    {
        if (pawn.jobs?.curDriver is IBladePoseSource source && source.TryGetBladePose(out pose))
        {
            return true;
        }
        return BladeMotionPlayer.TryGetBladePose(pawn, out pose);
    }
}

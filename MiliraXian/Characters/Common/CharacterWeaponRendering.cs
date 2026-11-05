using HarmonyLib;
using MiliraXian.Characters.Common.Vfx.BladeBody;
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

// Rigid 3D blade: while the current job driver (IBladePoseSource) or a registered provider (for
// example an ability warmup) poses the blade and the weapon is calibrated (BladeGraphicExtension),
// the blade is drawn here instead of the vanilla flat weapon. Worn apparel extras are still drawn.
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
        ThingWithComps weapon = pawn.equipment?.Primary;
        BladeGraphicExtension ext = weapon?.def.GetModExtension<BladeGraphicExtension>();
        if (ext == null || pawn.CurJob?.def?.neverShowWeapon != false || !BladePoseProviders.TryGet(pawn, out BladePose pose))
        {
            return true;
        }
        BladeRenderer.Draw(ext, pose, pawn.DrawPos);
        if (pawn.apparel != null)
        {
            foreach (Apparel apparel in pawn.apparel.WornApparel)
            {
                apparel.DrawWornExtras();
            }
        }
        return false;
    }
}

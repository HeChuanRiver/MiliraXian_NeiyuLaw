using HarmonyLib;
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

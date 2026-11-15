using HarmonyLib;
using MiliraXian.Characters.Neiyu;
using MiliraXian.Characters.Zhaoli;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Combat;

[HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedCooldown), new[] { typeof(Verb), typeof(Pawn) })]
internal static class Patch_VerbProperties_AdjustedCooldown
{
    private static void Postfix(Verb ownerVerb, Pawn attacker, ref float __result)
    {
        HediffComp_ZhaoliMinghuo minghuo = ZhaoliMinghuoUtility.GetActiveComp(ownerVerb);
        if (minghuo != null && minghuo.PropsMinghuo.attackSpeedMultiplier > 0f)
        {
            __result = Mathf.Max(0.1f, __result / minghuo.PropsMinghuo.attackSpeedMultiplier);
        }

        __result = NeiyuSwordCombat.AdjustSwingCooldown(ownerVerb, attacker, __result);
    }
}

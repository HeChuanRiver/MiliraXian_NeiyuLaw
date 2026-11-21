using HarmonyLib;
using MiliraXian.Characters.Common;
using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities.SakuraDance
{
    /// <summary>Pays Sakura Dance's cost when its aim (the ability warmup) starts.</summary>
    [HarmonyPatch(typeof(Verb_CastAbility), nameof(Verb_CastAbility.TryStartCastOn))]
    internal static class Patch_SakuraDanceCastCost
    {
        private static void Postfix(Verb_CastAbility __instance, bool __result)
        {
            if (!__result || __instance.CasterPawn is not { } pawn || __instance.Ability is not { } ability)
            {
                return;
            }

            CompAbilityEffect_SakuraDance dance = ability.CompOfType<CompAbilityEffect_SakuraDance>();
            if (dance?.Props is not { } props)
            {
                return;
            }

            // The amount lives once, in the ability's CastResource requirement; payment reads it from there.
            float cost = CompAbilityEffect_CastResource.RequiredAmountFor(ability, MX_QHDefOf.MX_QH_SwordPressure);
            SakuraDanceInvulnerability.PayCast(pawn, props, cost);
        }
    }
}

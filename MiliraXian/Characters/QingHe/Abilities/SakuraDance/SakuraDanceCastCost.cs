using HarmonyLib;
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
            if (!__result || __instance.CasterPawn is not { } pawn
                || __instance.Ability?.CompOfType<CompAbilityEffect_SakuraDance>()?.Props is not { } props)
            {
                return;
            }
            SakuraDanceInvulnerability.PayCast(pawn, props);
        }
    }
}

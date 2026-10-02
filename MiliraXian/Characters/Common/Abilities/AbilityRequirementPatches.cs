using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Common.Abilities
{
    /// <summary>
    /// Enforces <see cref="AbilityRequirement"/> for every ability regardless of its abilityClass,
    /// so a stance requirement and a perk requirement can apply to the same ability without either
    /// one knowing about the other.
    /// </summary>
    public static class AbilityRequirementUtility
    {
        public static AbilityRequirement FirstUnmet(Ability ability)
        {
            List<AbilityRequirement> requirements =
                ability?.def?.GetModExtension<AbilityRequirementExtension>()?.requirements;
            if (requirements == null || ability.pawn == null)
            {
                return null;
            }

            for (int i = 0; i < requirements.Count; i++)
            {
                AbilityRequirement requirement = requirements[i];
                if (requirement != null && !requirement.Met(ability.pawn))
                {
                    return requirement;
                }
            }

            return null;
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.CanCast), MethodType.Getter)]
    public static class Patch_Ability_CanCast_Requirements
    {
        public static void Postfix(Ability __instance, ref AcceptanceReport __result)
        {
            if (!__result.Accepted)
            {
                return;
            }

            // The only checkpoint the AI and script paths cross; the gizmo filter below covers
            // the player's path.
            AbilityRequirement unmet = AbilityRequirementUtility.FirstUnmet(__instance);
            if (unmet != null)
            {
                __result = unmet.DisabledReason(__instance.pawn);
            }
        }
    }

    [HarmonyPatch(typeof(Ability), nameof(Ability.GetGizmos))]
    public static class Patch_Ability_GetGizmos_Requirements
    {
        public static IEnumerable<Command> Postfix(IEnumerable<Command> values, Ability __instance)
        {
            if (AbilityRequirementUtility.FirstUnmet(__instance) != null)
            {
                yield break;
            }

            foreach (Command command in values)
            {
                yield return command;
            }
        }
    }
}

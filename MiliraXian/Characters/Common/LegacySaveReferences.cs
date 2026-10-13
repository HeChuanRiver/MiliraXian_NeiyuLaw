using System;
using System.Collections.Generic;
using HarmonyLib;
using MiliraXian.Characters.Common.Biography;
using MiliraXian.Characters.Common.Vfx;
using Verse;

namespace MiliraXian.Characters.Common
{
    // Exact references found in the 1.6 save, routed to the existing owners of their data.
    // This does not rewrite namespaces generally or add a second component/state implementation.
    internal static class LegacySaveReferences
    {
        internal static readonly Dictionary<string, Type> Types = new(StringComparer.Ordinal)
        {
            { "MiliraXian.Characters.GameComponent_CharacterPowerTransition", typeof(GameComponent_CharacterPowerTransition) },
            { "MiliraXian.Characters.GameComponent_CharacterUnityVfx", typeof(GameComponent_CharacterUnityVfx) },
            { "MiliraXian.Characters.GameComponent_SpecialPawnPrimaryCultureConversion", typeof(GameComponent_SpecialPawnPrimaryCultureConversion) },
            { "MiliraXian.Characters.Biography.GameComponent_BiographyFramework", typeof(GameComponent_BiographyFramework) },
            { "MiliraXian.Characters.Biography.Hediff_BiographyTracker", typeof(Hediff_BiographyTracker) },
            { "MiliraXian.Characters.Neiyu.GameComponent_NeiyuLawUpdateLog", typeof(GameComponent_NeiyuLawUpdateLog) },
            { "MiliraXian.Characters.Vfx.MapComponent_PawnAfterimages", typeof(MapComponent_PawnAfterimages) }
        };

        internal static readonly Dictionary<string, string> FlightAnimations = CreateFlightAliases();

        private static Dictionary<string, string> CreateFlightAliases()
        {
            var aliases = new Dictionary<string, string>(StringComparer.Ordinal);
            // These sixteen defs were replaced by the shared race animation and CL's frame binding.
            foreach (string character in new[] { "Neiyu", "Zhaoli", "Qinghe", "Mingyuan" })
                foreach (string direction in new[] { "North", "East", "South", "West" })
                    aliases.Add("Milira_Fly" + direction + "_" + character, "Milira_Fly" + direction);
            return aliases;
        }
    }

    [HarmonyPatch(typeof(BackCompatibility), nameof(BackCompatibility.GetBackCompatibleType))]
    internal static class Patch_BackCompatibility_LegacyCharacterTypes
    {
        [HarmonyPostfix]
        private static void Postfix(Type baseType, string providedClassName, ref Type __result)
        {
            if (__result != null || Scribe.mode != LoadSaveMode.LoadingVars || providedClassName == null) return;
            if (LegacySaveReferences.Types.TryGetValue(providedClassName, out Type current)
                && baseType != null && baseType.IsAssignableFrom(current))
                __result = current;
        }
    }

    [HarmonyPatch(typeof(BackCompatibility), nameof(BackCompatibility.BackCompatibleDefName))]
    internal static class Patch_BackCompatibility_LegacyCharacterFlight
    {
        [HarmonyPostfix]
        private static void Postfix(Type defType, string defName, bool forDefInjections, ref string __result)
        {
            if (defType != typeof(AnimationDef) || forDefInjections || Scribe.mode == LoadSaveMode.Inactive
                || defName == null || __result != defName) return;
            if (LegacySaveReferences.FlightAnimations.TryGetValue(defName, out string current)
                && DefDatabase<AnimationDef>.GetNamedSilentFail(defName) == null
                && DefDatabase<AnimationDef>.GetNamedSilentFail(current) != null)
                __result = current;
        }
    }
}

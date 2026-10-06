using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Common
{
    /// <summary>
    /// Ongoing effects a character trait confers on its bearer. CharacterLib decides which
    /// traits a pawn carries; what those traits do is gameplay and belongs here.
    /// </summary>
    public class CharacterTraitExtension : DefModExtension
    {
        public bool freezeBiologicalAge = true;
        public bool freezeChronologicalAge;
        public bool noSkillDecay = true;
        public bool preventAutonomousRomance;
        /// <summary>Skills whose negative gene aptitude the bearer ignores.</summary>
        public List<SkillDef> ignoreNegativeAptitudes = new();
    }

    [StaticConstructorOnStartup]
    public static class CharacterTraitEffects
    {
        private static readonly Dictionary<TraitDef, CharacterTraitExtension> traits = new();

        static CharacterTraitEffects()
        {
            foreach (TraitDef trait in DefDatabase<TraitDef>.AllDefsListForReading)
                if (trait.GetModExtension<CharacterTraitExtension>() is { } rule) traits.Add(trait, rule);
        }

        // The value is the actual Def extension, so power-profile changes take effect immediately.
        internal static CharacterTraitExtension For(TraitDef trait) =>
            trait != null && traits.TryGetValue(trait, out var rule) ? rule : null;

        internal static bool PreventsAutonomousRomance(Pawn pawn)
        {
            var carriedTraits = pawn?.story?.traits?.allTraits;
            if (carriedTraits == null) return false;
            foreach (Trait trait in carriedTraits)
                if (For(trait.def)?.preventAutonomousRomance == true) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(InteractionWorker_RomanceAttempt), nameof(InteractionWorker_RomanceAttempt.RandomSelectionWeight))]
    internal static class Patch_CharacterTraitAutonomousRomance
    {
        private static void Postfix(Pawn initiator, Pawn recipient, ref float __result)
        {
            if (__result > 0f && (CharacterTraitEffects.PreventsAutonomousRomance(initiator)
                || CharacterTraitEffects.PreventsAutonomousRomance(recipient)))
                __result = 0f;
        }
    }

    [HarmonyPatch(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.AgeTickInterval))]
    internal static class Patch_CharacterTraitAge
    {
        private static void Prefix(Pawn_AgeTracker __instance, Pawn ___pawn, ref int delta) => Freeze(__instance, ___pawn, ref delta);

        internal static void Freeze(Pawn_AgeTracker tracker, Pawn pawn, ref int ticks)
        {
            if (ticks <= 0) return;
            var traits = pawn?.story?.traits?.allTraits;
            if (traits == null || pawn.ParentHolder is Building_CryptosleepCasket || pawn.ParentHolder is Building_GrowthVat) return;
            bool biological = false;
            bool chronological = false;
            foreach (Trait trait in traits)
            {
                var rule = CharacterTraitEffects.For(trait.def);
                if (rule == null) continue;
                biological |= rule.freezeBiologicalAge;
                chronological |= rule.freezeChronologicalAge;
            }
            if (chronological) tracker.BirthAbsTicks += ticks;
            if (biological) ticks = 0;
        }
    }

    [HarmonyPatch(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.AgeTickMothballed))]
    internal static class Patch_CharacterTraitMothballedAge
    {
        private static void Prefix(Pawn_AgeTracker __instance, Pawn ___pawn, ref int interval) => Patch_CharacterTraitAge.Freeze(__instance, ___pawn, ref interval);
    }

    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Interval))]
    internal static class Patch_CharacterTraitSkillDecay
    {
        // In 1.6 Interval only applies natural decay; explicit Learn losses stay intact.
        private static bool Prefix(SkillRecord __instance)
        {
            var traits = __instance.Pawn?.story?.traits?.allTraits;
            if (traits == null) return true;
            foreach (Trait trait in traits)
                if (CharacterTraitEffects.For(trait.def)?.noSkillDecay == true) return false;
            return true;
        }
    }

    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Aptitude), MethodType.Getter)]
    internal static class Patch_CharacterTraitAptitude
    {
        private static void Prefix(int? ___aptitudeCached, out bool __state) => __state = !___aptitudeCached.HasValue;

        private static void Postfix(SkillRecord __instance, bool __state, ref int? ___aptitudeCached, ref int __result)
        {
            if (!__state || !ModsConfig.BiotechActive) return;
            Pawn pawn = __instance.Pawn;
            if (pawn?.genes == null || pawn.story?.traits == null) return;
            bool ignore = false;
            foreach (Trait trait in pawn.story.traits.allTraits)
                if (CharacterTraitEffects.For(trait.def)?.ignoreNegativeAptitudes?.Contains(__instance.def) == true) { ignore = true; break; }
            if (!ignore) return;
            foreach (Gene gene in pawn.genes.GenesListForReading)
            {
                if (!gene.Active) continue;
                int penalty = gene.def.AptitudeFor(__instance.def);
                if (penalty < 0) __result -= penalty;
            }
            // Use the vanilla cache and its trait/gene invalidation, not a second pawn cache.
            ___aptitudeCached = __result;
        }
    }
}

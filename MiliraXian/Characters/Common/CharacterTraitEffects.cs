using System.Collections.Generic;
using System.Text;
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
        public float caravanCapacityOffset;
        public float diseaseTendQuality = -1f;
        public float fixedTendQuality = -1f;
        public float entityDamageFactor = 1f;
        public HediffDef grantedHediff;
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

        internal static bool Grants(Pawn pawn, HediffDef hediff)
        {
            var carriedTraits = pawn?.story?.traits?.allTraits;
            if (carriedTraits == null || hediff == null) return false;
            foreach (Trait trait in carriedTraits)
                if (!trait.Suppressed && For(trait.def)?.grantedHediff == hediff) return true;
            return false;
        }

        // TraitSet calls RecacheTraits on gain, removal, suppression changes and post-load.
        // Keep a granted state container when its trait is suppressed/removed; the container
        // gates its stage and behavior without discarding saved progress.
        internal static void SyncGrantedHediffs(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || pawn.story?.traits == null) return;
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                HediffDef hediff = For(trait.def)?.grantedHediff;
                if (hediff != null && !trait.Suppressed && !pawn.health.hediffSet.HasHediff(hediff))
                    pawn.health.AddHediff(hediff);
            }
            pawn.health.hediffSet.DirtyCache();
        }

        internal static bool PreventsAutonomousRomance(Pawn pawn)
        {
            var carriedTraits = pawn?.story?.traits?.allTraits;
            if (carriedTraits == null) return false;
            foreach (Trait trait in carriedTraits)
                if (!trait.Suppressed && For(trait.def)?.preventAutonomousRomance == true) return true;
            return false;
        }
    }

    [HarmonyPatch(typeof(TraitSet), "RecacheTraits")]
    internal static class Patch_CharacterTraitState
    {
        private static void Postfix(Pawn ___pawn) => CharacterTraitEffects.SyncGrantedHediffs(___pawn);
    }

    [HarmonyPatch(typeof(MassUtility), nameof(MassUtility.Capacity))]
    internal static class Patch_CharacterTraitCaravanCapacity
    {
        private static void Postfix(Pawn p, StringBuilder explanation, ref float __result)
        {
            if (!MassUtility.CanEverCarryAnything(p) || p.story?.traits == null) return;
            foreach (Trait trait in p.story.traits.allTraits)
            {
                if (trait.Suppressed) continue;
                float offset = CharacterTraitEffects.For(trait.def)?.caravanCapacityOffset ?? 0f;
                if (offset == 0f) continue;
                __result += offset;
                explanation?.AppendLine().Append("  - ").Append(trait.LabelCap).Append(": ").Append(offset.ToStringMassOffset());
            }
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    internal static class Patch_CharacterTraitEntityDamage
    {
        // Before the target's shields/armor, once per damage packet, for every weapon/ability.
        private static void Prefix(Thing __instance, ref DamageInfo dinfo)
        {
            if (__instance is not Pawn target || !ModsConfig.AnomalyActive || dinfo.Amount <= 0f
                || !(target.RaceProps.IsAnomalyEntity || target.IsSubhuman || target.IsShambler)
                || dinfo.Instigator is not Pawn attacker || attacker.story?.traits == null) return;
            float factor = 1f;
            foreach (Trait trait in attacker.story.traits.allTraits)
                if (!trait.Suppressed) factor *= CharacterTraitEffects.For(trait.def)?.entityDamageFactor ?? 1f;
            if (factor != 1f) dinfo.SetAmount(dinfo.Amount * factor);
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
            if (traits == null || pawn.ParentHolder is Building_CryptosleepCasket) return;
            bool biological = false;
            bool chronological = false;
            foreach (Trait trait in traits)
            {
                if (trait.Suppressed) continue;
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
        // In the installed 1.7 Interval only applies natural decay; explicit Learn losses stay intact.
        private static bool Prefix(SkillRecord __instance)
        {
            var traits = __instance.Pawn?.story?.traits?.allTraits;
            if (traits == null) return true;
            foreach (Trait trait in traits)
                if (!trait.Suppressed && CharacterTraitEffects.For(trait.def)?.noSkillDecay == true) return false;
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

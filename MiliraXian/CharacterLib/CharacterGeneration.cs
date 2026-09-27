using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraXian.CharacterLib
{
    public class CharacterProtectionExtension : DefModExtension
    {
        /// <summary>
        /// Generated in this kind's place when the request asks for a newborn. A character must
        /// never be born as the offspring of another pawn.
        /// </summary>
        public PawnKindDef childKind;
    }

    /// <summary>A trait a character must carry, identified by def and degree together.</summary>
    public class TraitSpec
    {
        public TraitDef def;
        public int degree;
    }

    /// <summary>
    /// Everything that makes a character who they are. Vanilla generation randomises most of
    /// this even when a PawnKindDef narrows the pool, so each value declared here is asserted
    /// after generation rather than left to the pipeline.
    /// </summary>
    public class CharacterIdentityExtension : DefModExtension
    {
        public string firstNameKey;
        public string lastNameKey;
        public string nickNameKey;
        public HeadTypeDef headType;
        public BodyTypeDef bodyType;
        public BackstoryDef childhoodBackstory;
        public BackstoryDef adulthoodBackstory;
        public int? biologicalAgeYears;
        public int? chronologicalAgeYears;

        /// <summary>
        /// The complete trait list. Vanilla adds traits from seven places, two of which have no
        /// XML opt-out, so the list is rebuilt from this declaration instead of filtered.
        /// </summary>
        public List<TraitSpec> traits;

        /// <summary>
        /// Hediffs vanilla adds during generation that a fixed character must never carry.
        /// Pregnancy and sterilisation come from PawnKindDef field defaults that no XML on the
        /// Milira inheritance chain overrides; scarification and blindness come from the pawn's
        /// ideo. Declared even where XML already zeroes the chance, since this also catches
        /// hediffs a third-party Mod appends after generation.
        /// </summary>
        public List<HediffDef> forbiddenHediffs;
    }

    internal static class CharacterIdentity
    {
        private const long TicksPerYear = 3600000L;

        internal static void Apply(Pawn pawn, CharacterIdentityExtension identity)
        {
            // Backstory first: vanilla derives bodyType from it, and the traits rebuilt below
            // must not be read off a randomly picked backstory.
            if (identity.childhoodBackstory != null) pawn.story.Childhood = identity.childhoodBackstory;
            if (identity.adulthoodBackstory != null) pawn.story.Adulthood = identity.adulthoodBackstory;

            NameTriple old = pawn.Name as NameTriple;
            pawn.Name = new NameTriple(Resolve(identity.firstNameKey, old?.First ?? pawn.LabelShort),
                Resolve(identity.nickNameKey, old?.Nick ?? pawn.LabelShort), Resolve(identity.lastNameKey, old?.Last ?? ""));

            if (identity.headType != null) pawn.story.headType = identity.headType;
            if (identity.bodyType != null) pawn.story.bodyType = identity.bodyType;

            ApplyAge(pawn, identity);
            if (identity.traits != null) ApplyTraits(pawn, identity.traits);
            if (identity.forbiddenHediffs != null) RemoveForbiddenHediffs(pawn, identity.forbiddenHediffs);

            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        private static string Resolve(string key, string fallback) =>
            !string.IsNullOrEmpty(key) && key.CanTranslate() ? key.Translate().ToString() : fallback;

        private static void ApplyAge(Pawn pawn, CharacterIdentityExtension identity)
        {
            // Not TickManager.TicksAbs: starting pawns are generated before gameStartAbsTick is
            // set, where that property logs an error and returns a bogus value. GenTicks falls
            // back to the tick the game is configured to start at.
            long now = GenTicks.TicksAbs;
            if (identity.biologicalAgeYears.HasValue)
            {
                long bio = identity.biologicalAgeYears.Value * TicksPerYear;
                pawn.ageTracker.AgeBiologicalTicks = bio;
                // Without a declared chronological age, only push the birth date back far
                // enough to keep chronological from trailing biological.
                if (!identity.chronologicalAgeYears.HasValue && now - pawn.ageTracker.BirthAbsTicks < bio)
                    pawn.ageTracker.BirthAbsTicks = now - bio;
            }
            if (!identity.chronologicalAgeYears.HasValue) return;
            long chrono = identity.chronologicalAgeYears.Value * TicksPerYear;
            if (identity.biologicalAgeYears.HasValue)
                chrono = Math.Max(chrono, identity.biologicalAgeYears.Value * TicksPerYear);
            pawn.ageTracker.BirthAbsTicks = now - chrono;
        }

        private static void ApplyTraits(Pawn pawn, List<TraitSpec> declared)
        {
            List<Trait> current = pawn.story.traits.allTraits;
            for (int i = current.Count - 1; i >= 0; i--)
            {
                Trait trait = current[i];
                // A gene-sourced trait cannot be removed here: RemoveTrait would delete the
                // owning gene along with it, stripping the character's xenotype.
                if (trait.sourceGene != null) continue;
                if (!Declares(declared, trait.def, trait.Degree)) pawn.story.traits.RemoveTrait(trait);
            }
            foreach (TraitSpec spec in declared)
            {
                if (spec.def == null || pawn.story.traits.HasTrait(spec.def, spec.degree)) continue;
                pawn.story.traits.GainTrait(new Trait(spec.def, spec.degree), suppressConflicts: true);
            }
        }

        private static bool Declares(List<TraitSpec> declared, TraitDef def, int degree)
        {
            foreach (TraitSpec spec in declared)
                if (spec.def == def && spec.degree == degree) return true;
            return false;
        }

        private static void RemoveForbiddenHediffs(Pawn pawn, List<HediffDef> forbidden)
        {
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0; i--)
                if (forbidden.Contains(hediffs[i].def)) pawn.health.RemoveHediff(hediffs[i]);
        }
    }

    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), typeof(PawnGenerationRequest))]
    internal static class CharacterGeneration
    {
        private static void Prefix(ref PawnGenerationRequest request)
        {
            PawnKindDef kind = request.KindDef;
            if (kind == null) return;

            bool newborn = request.AllowedDevelopmentalStages.Newborn() || request.AllowedDevelopmentalStages.Baby()
                || request.FixedBiologicalAge == 0f;

            // A character's own kind must never produce offspring of that kind. Vanilla's
            // ApplyBirthOutcome takes the mother's kindDef verbatim, and growth vats reuse the
            // same newborn request, so the substitution belongs here rather than at either site.
            if (newborn)
            {
                PawnKindDef childKind = kind.GetModExtension<CharacterProtectionExtension>()?.childKind;
                if (childKind == null) return;
                // Clear the getter first: it would otherwise win over KindDef further down.
                request.PawnKindDefGetter = null;
                request.KindDef = childKind;
                return;
            }

            var identity = kind.GetModExtension<CharacterIdentityExtension>();
            if (identity == null) return;

            // A character's xenotype is part of who they are, and the starting-pawn page offers
            // no way to lock the xenotype button the way a scenario locks race and life stage.
            // These four win over the kind's own xenotypeSet in GetXenotypeForGeneratedPawn, so
            // clearing them is what hands the choice back to the PawnKindDef.
            //
            // Only worth doing when the kind actually names a xenotype: a character's entry may
            // be MayRequire'd on an optional gene mod, and without it the set is empty and hands
            // back nothing but a baseliner. Overriding the player's pick with that would be
            // worse than leaving the pick alone.
            if (kind.xenotypeSet?.Count > 0)
            {
                request.ForcedXenotype = null;
                request.ForcedCustomXenotype = null;
                request.AllowedXenotypes = null;
                request.ForceBaselinerChance = 0f;
            }

            // Without a fixed age, GenerateRandomAge draws a continuous age from the race curve
            // and retries until AgeAllowed accepts it. Pinning a character's age with
            // min/maxGenerationAge would almost never be hit by that draw: vanilla gives up
            // after 300 attempts, logs an error, and keeps the last age it happened to draw.
            if (identity.biologicalAgeYears == null || request.FixedBiologicalAge.HasValue) return;
            request.FixedBiologicalAge = identity.biologicalAgeYears.Value;
            // A fixed age is mutually exclusive with both ranges, and the starting-pawn request
            // always carries one to skip puberty. Clear them rather than give up on the age:
            // vanilla resolves the conflict the same way, only with an error logged first.
            request.BiologicalAgeRange = null;
            request.ExcludeBiologicalAgeRange = null;
        }

        private static void Postfix(Pawn __result)
        {
            Pawn pawn = __result;
            var identity = pawn?.kindDef?.GetModExtension<CharacterIdentityExtension>();
            if (identity == null || pawn.story == null) return;
            CharacterIdentity.Apply(pawn, identity);
        }
    }
}

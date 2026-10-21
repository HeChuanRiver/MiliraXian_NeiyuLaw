using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Common
{
    internal static class CharacterTraitMedicalEffects
    {
        // DoTend knows the doctor; Hediff/comp callbacks only know the patient. This scoped
        // context bridges that API gap, restores nested calls and is never pawn/save state.
        [ThreadStatic] internal static (Pawn patient, float quality) Context;

        internal static float Quality(Pawn pawn, bool disease)
        {
            float quality = -1f;
            if (pawn?.story?.traits == null) return quality;
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                if (trait.Suppressed || CharacterTraitEffects.For(trait.def) is not { } rule) continue;
                quality = Math.Max(quality, disease ? rule.diseaseTendQuality : rule.fixedTendQuality);
            }
            return quality;
        }

        internal static void AutoTend(HediffComp_TendDuration tend)
        {
            if (tend.tendTicksLeft > 1 || tend.Pawn.Dead || tend.parent is Hediff_Injury
                || !(tend.parent.def.makesSickThought || tend.parent.TryGetComp<HediffComp_Immunizable>() != null)
                || !tend.parent.TendableNow(ignoreTimer: true)) return;
            float quality = Quality(tend.Pawn, disease: true);
            if (quality < 0f) return;
            var previous = Context;
            Context = (tend.Pawn, quality);
            try { tend.parent.Tended(quality, quality); }
            finally { Context = previous; }
        }
    }

    [HarmonyPatch(typeof(TendUtility), nameof(TendUtility.DoTend))]
    internal static class Patch_CharacterTraitTendContext
    {
        private static void Prefix(Pawn doctor, Pawn patient, out (Pawn patient, float quality) __state)
        {
            __state = CharacterTraitMedicalEffects.Context;
            CharacterTraitMedicalEffects.Context = (patient, CharacterTraitMedicalEffects.Quality(doctor, disease: false));
        }

        private static void Finalizer((Pawn patient, float quality) __state) => CharacterTraitMedicalEffects.Context = __state;
    }

    [HarmonyPatch(typeof(HediffWithComps), nameof(HediffWithComps.Tended))]
    internal static class Patch_CharacterTraitTendQuality
    {
        private static void Prefix(HediffWithComps __instance, ref float quality, ref float maxQuality)
        {
            var context = CharacterTraitMedicalEffects.Context;
            if (context.patient != __instance.pawn || context.quality < 0f) return;
            quality = maxQuality = context.quality;
        }
    }

    [HarmonyPatch(typeof(HediffComp_TendDuration), nameof(HediffComp_TendDuration.CompTended))]
    internal static class Patch_CharacterTraitTendVariance
    {
        private static void Prefix(HediffComp_TendDuration __instance, ref float quality, ref float maxQuality)
        {
            var context = CharacterTraitMedicalEffects.Context;
            if (context.patient != __instance.Pawn || context.quality < 0f) return;
            // Vanilla adds [-0.25, 0.25] and clamps. Pin the outcome without replacing
            // its duration, total-quality bookkeeping, notifications or treatment effects.
            quality = context.quality + HediffComp_TendDuration.TendQualityRandomVariance;
            maxQuality = context.quality;
        }
    }

    [HarmonyPatch(typeof(HediffComp_TendDuration), nameof(HediffComp_TendDuration.CompPostTick))]
    internal static class Patch_CharacterTraitDiseaseTending
    {
        // Use the disease's existing timer for first treatment and renewal; no pawn scans.
        private static void Prefix(HediffComp_TendDuration __instance) => CharacterTraitMedicalEffects.AutoTend(__instance);
    }
}

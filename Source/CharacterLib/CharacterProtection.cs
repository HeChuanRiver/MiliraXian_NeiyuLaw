using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.CharacterLib
{
    public class CharacterProtectionExtension : DefModExtension
    {
        public bool blockMentalBreak = true;
        public bool blockRescue = true;
        public bool blockCapture = true;
        public bool blockArrest = true;
        public bool blockScanner = true;
        public bool blockLowTech = true;
        public bool blockHarvest = true;
        public bool freezeBiologicalAge = true;
        public bool freezeChronologicalAge;
        public bool protectAnomaly = true;
        public bool recoverOnDeath;
        public int recoveryTicks = 2700000;
        public PawnKindDef childKind;
        public List<TraitDef> requiredTraits = new();
    }

    public class CharacterTraitExtension : DefModExtension
    {
        public bool noSkillDecay = true;
        public bool reflectPsychicShock = true;
        public SkillDef ignoreNegativeAptitude;
        public SkillDef lockedSkill;
        public int lockedLevel = 20;
    }

    public static class CharacterProtection
    {
        public static CharacterProtectionExtension For(Pawn pawn) => pawn?.kindDef?.GetModExtension<CharacterProtectionExtension>();
        public static void RestoreRequiredTraits(Pawn pawn)
        {
            var rules = For(pawn);
            if (rules == null || pawn.story?.traits == null || pawn.Faction != Faction.OfPlayer) return;
            foreach (TraitDef trait in rules.requiredTraits)
                if (!pawn.story.traits.HasTrait(trait)) pawn.story.traits.GainTrait(new Trait(trait));
        }

        public static void InitializeSkills(Pawn pawn)
        {
            if (pawn?.story?.traits == null || pawn.skills == null) return;
            foreach (Trait trait in pawn.story.traits.allTraits)
            {
                var rule = trait.def.GetModExtension<CharacterTraitExtension>();
                if (rule?.lockedSkill == null) continue;
                SkillRecord skill = pawn.skills.GetSkill(rule.lockedSkill);
                if (skill.Level < rule.lockedLevel) skill.Level = rule.lockedLevel;
            }
        }
    }

    [HarmonyPatch(typeof(MentalStateHandler), nameof(MentalStateHandler.TryStartMentalState))]
    internal static class Patch_CharacterMentalState
    {
        private static bool Prefix(Pawn ___pawn, ref bool __result)
        {
            if (CharacterProtection.For(___pawn)?.blockMentalBreak != true) return true;
            ThoughtDef calm = DefDatabase<ThoughtDef>.GetNamedSilentFail("MX_CharacterCalm");
            var memories = ___pawn.needs?.mood?.thoughts?.memories;
            if (calm != null && memories != null)
            {
                memories.RemoveMemoriesOfDef(calm);
                memories.TryGainMemory(calm);
            }
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.AgeTickInterval))]
    internal static class Patch_CharacterAge
    {
        private static void Prefix(Pawn_AgeTracker __instance, Pawn ___pawn, ref int delta) => Freeze(__instance, ___pawn, ref delta);
        internal static void Freeze(Pawn_AgeTracker tracker, Pawn pawn, ref int ticks)
        {
            if (ticks <= 0) return;
            var rule = CharacterProtection.For(pawn);
            if (rule == null || pawn.ParentHolder is Building_CryptosleepCasket || pawn.ParentHolder is Building_GrowthVat) return;
            if (rule.freezeChronologicalAge) tracker.BirthAbsTicks += ticks;
            if (rule.freezeBiologicalAge) ticks = 0;
        }
    }
    [HarmonyPatch(typeof(Pawn_AgeTracker), nameof(Pawn_AgeTracker.AgeTickMothballed))]
    internal static class Patch_CharacterMothballedAge
    {
        private static void Prefix(Pawn_AgeTracker __instance, Pawn ___pawn, ref int interval) => Patch_CharacterAge.Freeze(__instance, ___pawn, ref interval);
    }

    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Interval))]
    internal static class Patch_CharacterSkillDecay
    {
        // In 1.6 Interval only applies natural decay; explicit Learn losses stay intact.
        private static bool Prefix(SkillRecord __instance)
        {
            var traits = __instance.Pawn?.story?.traits?.allTraits;
            if (traits == null) return true;
            bool prevent = false;
            foreach (Trait trait in traits)
            {
                var rule = trait.def.GetModExtension<CharacterTraitExtension>();
                if (rule == null) continue;
                prevent |= rule.noSkillDecay;
                if (rule.lockedSkill == __instance.def)
                {
                    prevent = true;
                    if (__instance.Level < rule.lockedLevel) __instance.Level = rule.lockedLevel;
                }
            }
            return !prevent;
        }
    }

    [HarmonyPatch(typeof(SkillRecord), nameof(SkillRecord.Aptitude), MethodType.Getter)]
    internal static class Patch_CharacterAptitude
    {
        private static void Prefix(int? ___aptitudeCached, out bool __state) => __state = !___aptitudeCached.HasValue;
        private static void Postfix(SkillRecord __instance, bool __state, ref int? ___aptitudeCached, ref int __result)
        {
            if (!__state || !ModsConfig.BiotechActive) return;
            Pawn pawn = __instance.Pawn;
            if (pawn?.genes == null || pawn.story?.traits == null) return;
            bool ignore = false;
            foreach (Trait trait in pawn.story.traits.allTraits)
                if (trait.def.GetModExtension<CharacterTraitExtension>()?.ignoreNegativeAptitude == __instance.def) { ignore = true; break; }
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

    [HarmonyPatch(typeof(TraitSet), nameof(TraitSet.RemoveTrait))]
    internal static class Patch_CharacterRequiredTrait
    {
        private static bool Prefix(Pawn ___pawn, Trait trait) => ___pawn?.Faction != Faction.OfPlayer
            || CharacterProtection.For(___pawn)?.requiredTraits.Contains(trait.def) != true;
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class Patch_CharacterProtectionSpawn
    {
        private static void Postfix(Pawn __instance)
        {
            CharacterServices.Storage?.NotifySpawned(__instance);
            CharacterProtection.RestoreRequiredTraits(__instance);
            CharacterProtection.InitializeSkills(__instance);
        }
    }

    [HarmonyPatch(typeof(CompTargetEffect_PsychicShock), nameof(CompTargetEffect_PsychicShock.DoEffectOn))]
    internal static class Patch_CharacterShockReflect
    {
        [ThreadStatic] private static bool reflecting;
        private static bool Prefix(CompTargetEffect_PsychicShock __instance, Pawn user, Thing target)
        {
            if (reflecting || !(target is Pawn pawn) || pawn.story?.traits == null) return true;
            bool reflect = false;
            foreach (Trait trait in pawn.story.traits.allTraits)
                if (trait.def.GetModExtension<CharacterTraitExtension>()?.reflectPsychicShock == true) { reflect = true; break; }
            if (!reflect) return true;
            if (user != null && !user.Dead)
            {
                try { reflecting = true; __instance.DoEffectOn(user, user); }
                finally { reflecting = false; }
            }
            return false;
        }
    }
}

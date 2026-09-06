using RimWorld;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Defs;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public static class MX_QH_HediffUtility
    {
        public static HediffComp_SkillTreeState EnsureFlowerResonance(Pawn pawn)
        {
            return EnsureHediffComp<HediffComp_SkillTreeState>(pawn, MX_QHDefOf.MX_QH_FlowerResonance);
        }

        public static HediffComp_SkillTreeState GetFlowerResonance(Pawn pawn)
        {
            return GetHediffComp<HediffComp_SkillTreeState>(pawn, MX_QHDefOf.MX_QH_FlowerResonance);
        }

        public static HediffComp_FlowerDecree EnsureFlowerDecree(Pawn pawn)
        {
            return PawnSpecialResourceUtility.EnsureSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_FlowerDecree) as HediffComp_FlowerDecree;
        }

        public static HediffComp_FlowerDecree GetFlowerDecree(Pawn pawn)
        {
            return PawnSpecialResourceUtility.GetSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_FlowerDecree) as HediffComp_FlowerDecree;
        }

        public static HediffComp_QingheCombatState EnsureCombatState(Pawn pawn)
        {
            return EnsureHediffComp<HediffComp_QingheCombatState>(pawn, MX_QHDefOf.MX_QH_CombatState);
        }

        public static HediffComp_QingheCombatState GetCombatState(Pawn pawn)
        {
            return HediffComp_QingheCombatState.GetFor(pawn);
        }

        public static Hediff_SeasonalResonance GetSeasonalResonance(Pawn pawn)
        {
            return GetCombatState(pawn)?.CurrentResonance;
        }

        public static HediffComp_SwordPressure EnsureSwordPressure(Pawn pawn)
        {
            return PawnSpecialResourceUtility.EnsureSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_SwordPressure) as HediffComp_SwordPressure;
        }

        public static HediffComp_SwordPressure GetSwordPressure(Pawn pawn)
        {
            return PawnSpecialResourceUtility.GetSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_SwordPressure) as HediffComp_SwordPressure;
        }

        public static HediffComp_MeditativeStillness EnsureMeditativeStillness(Pawn pawn)
        {
            if (!MX_QHCharacterUtility.IsQinghe(pawn))
            {
                return null;
            }

            return EnsureHediffComp<HediffComp_MeditativeStillness>(pawn, MX_QHDefOf.MX_QH_MeditativeStillness);
        }

        public static void SyncDivineProtectionForPowerLevel(Pawn pawn)
        {
            HediffComp_DivineProtection protection = GetHediffComp<HediffComp_DivineProtection>(
                pawn,
                MX_QHDefOf.MX_QH_DivineProtection);
            protection?.SyncForPowerLevel();
        }

        public static void AddMeditativeStillnessFromLotusPond(Pawn pawn, Building lotusPond)
        {
            HediffComp_MeditativeStillness stillness = EnsureMeditativeStillness(pawn);
            if (stillness == null || lotusPond == null)
            {
                return;
            }

            float gain = stillness.PropsStillness.meditationGainPerDay / 60000f * ResolveLotusPondRoomStillnessFactor(lotusPond);
            stillness.AddStillness(gain);
        }

        public static void AddMeditativeStillnessFromReading(Pawn pawn, int delta, float roomBonusFactor)
        {
            HediffComp_MeditativeStillness stillness = EnsureMeditativeStillness(pawn);
            if (stillness == null || delta <= 0)
            {
                return;
            }

            float gain = stillness.PropsStillness.readingGainPerDay / 60000f * delta * Mathf.Max(0.1f, roomBonusFactor);
            stillness.AddStillness(gain);
        }

        public static void AddMeditativeStillnessFromSleep(Pawn pawn, int delta)
        {
            HediffComp_MeditativeStillness stillness = EnsureMeditativeStillness(pawn);
            if (stillness == null || delta <= 0)
            {
                return;
            }

            float gain = stillness.PropsStillness.sleepGainPerDay / 60000f * delta;
            stillness.AddStillness(gain);
        }

        public static void ApplyMeditativeStillnessQualityBonus(Pawn pawn, ref QualityCategory quality)
        {
            HediffComp_MeditativeStillness stillness = GetHediffComp<HediffComp_MeditativeStillness>(pawn, MX_QHDefOf.MX_QH_MeditativeStillness);
            int bonusLevels = stillness?.ConsumeForQualityBonus() ?? 0;
            if (bonusLevels > 0)
            {
                quality = (QualityCategory)Mathf.Clamp((int)quality + bonusLevels, 0, 6);
            }
        }

        public static void EnsureCoreHediffs(Pawn pawn)
        {
            EnsureHediff(pawn, MX_QHDefOf.MX_QH_DivineBlessing);
            EnsureHediff(pawn, MX_QHDefOf.MX_QH_DivineProtection);
            EnsureHediff(pawn, MX_QHDefOf.MX_QH_Trickle);
            EnsureFlowerResonance(pawn);
            EnsureFlowerDecree(pawn);
            EnsureCombatState(pawn);
            EnsureSwordPressure(pawn);
            EnsureMeditativeStillness(pawn);
            EnsureDivineGraceComp(pawn);

            GetHediffComp<HediffComp_DivineProtection>(pawn, MX_QHDefOf.MX_QH_DivineProtection)?.EnsureShieldBound();
        }

        public static int GetDivineGraceLevel(Pawn pawn)
        {
            return GetDivineGraceComp(pawn)?.EffectiveLevel ?? 0;
        }

        public static void AddDivineGraceLevel(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }

            HediffComp_QingheGraceSync comp = EnsureDivineGraceComp(pawn);
            if (comp == null || comp.IsMaxLevel)
            {
                return;
            }

            comp.AddProgress(comp.RequiredProgressForCurrentLevel);
            Messages.Message(
                "MX_QH_DivineGraceGainedMessage".Translate(comp.CurrentLevel),
                pawn,
                MessageTypeDefOf.PositiveEvent,
                historical: false);
            MX_QHSkillUtility.SyncChoices(pawn);
        }

        public static HediffComp_QingheGraceSync GetDivineGraceComp(Pawn pawn)
        {
            return GetHediffComp<HediffComp_QingheGraceSync>(pawn, MX_QHDefOf.MX_QH_FlowerResonance);
        }

        public static HediffComp_QingheGraceSync EnsureDivineGraceComp(Pawn pawn)
        {
            return EnsureHediffComp<HediffComp_QingheGraceSync>(pawn, MX_QHDefOf.MX_QH_FlowerResonance);
        }

        public static float GetDivineGraceProgress(Pawn pawn)
        {
            return GetDivineGraceComp(pawn)?.Progress ?? 0f;
        }

        public static float GetDivineGraceProgressRequired(Pawn pawn)
        {
            return GetDivineGraceComp(pawn)?.RequiredProgressForCurrentLevel ?? 0f;
        }

        public static float GetDivineGraceProgressPercent(Pawn pawn)
        {
            return GetDivineGraceComp(pawn)?.ProgressPercent ?? 0f;
        }

        public static void AddDivineGraceProgress(Pawn pawn, float amount)
        {
            if (!MX_QHCharacterUtility.IsQinghe(pawn) || amount <= 0f)
            {
                return;
            }

            EnsureDivineGraceComp(pawn)?.AddProgress(amount);
        }

        public static void AddDivineGraceProgressFromCraft(Pawn pawn, RecipeDef recipe, Thing product)
        {
            if (!MX_QHCharacterUtility.IsQinghe(pawn)
                || !IsGraceCraftRecipe(recipe)
                || product == null
                || product.def?.category != ThingCategory.Item)
            {
                return;
            }

            float amount = CalculateDivineGraceProgressFromCraft(product);
            if (amount > 0f)
            {
                AddDivineGraceProgress(pawn, amount);
            }
        }

        private static bool IsGraceCraftRecipe(RecipeDef recipe)
        {
            return recipe != null
                && recipe.workSkillLearnFactor > 0f
                && (recipe.workSkill == SkillDefOf.Crafting || recipe.workSkill == SkillDefOf.Artistic);
        }

        private static float CalculateDivineGraceProgressFromCraft(Thing product)
        {
            CompQuality compQuality = product.TryGetComp<CompQuality>();
            float qualityFactor = compQuality == null ? 0.9f : 0.7f + 0.28f * (int)compQuality.Quality;
            float marketValue = Mathf.Max(0f, product.MarketValue * Mathf.Max(1, product.stackCount));
            float cappedValue = Mathf.Min(marketValue, 60000f);
            return (12f + cappedValue * 0.045f) * qualityFactor;
        }
        private static T EnsureHediffComp<T>(Pawn pawn, HediffDef hediffDef) where T : HediffComp
        {
            Hediff hediff = EnsureHediff(pawn, hediffDef);
            return (hediff as HediffWithComps)?.GetComp<T>();
        }

        private static T GetHediffComp<T>(Pawn pawn, HediffDef hediffDef) where T : HediffComp
        {
            if (pawn?.health?.hediffSet == null || hediffDef == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            return (hediff as HediffWithComps)?.GetComp<T>();
        }

        private static Hediff EnsureHediff(Pawn pawn, HediffDef hediffDef)
        {
            if (pawn?.health?.hediffSet == null || hediffDef == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(hediffDef, pawn);
                pawn.health.AddHediff(hediff);
            }

            return hediff;
        }

        private static float ResolveLotusPondRoomStillnessFactor(Building lotusPond)
        {
            Room room = lotusPond.GetRoom();
            if (room == null)
            {
                return 1f;
            }

            float beauty = room.GetStat(RoomStatDefOf.Beauty);
            float cleanliness = room.GetStat(RoomStatDefOf.Cleanliness);
            return Mathf.Lerp(0.75f, 1.5f, Mathf.InverseLerp(-5f, 20f, beauty))
                * Mathf.Lerp(0.75f, 1.25f, Mathf.InverseLerp(-2f, 1f, cleanliness));
        }
    }
}



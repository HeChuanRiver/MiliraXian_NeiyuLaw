using RimWorld;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.CombatStance;
using MiliraXian.Characters.QingHe.Defs;
using UnityEngine;
using Verse;
using MiliraXian.Characters.Common;
using MiliraXian.Characters.Common.PerkSystem;
using MiliraXian.Characters.QingHe;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public static class MX_QH_HediffUtility
    {
        public static HediffComp_SkillTreeState GetFlowerResonance(Pawn pawn)
        {
            return GetHediffComp<HediffComp_SkillTreeState>(pawn, MX_QHDefOf.MX_QH_FlowerResonance);
        }

        public static HediffComp_PawnSpecialResource GetFlowerDecree(Pawn pawn)
        {
            return PawnSpecialResourceUtility.GetSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_FlowerDecree) as HediffComp_PawnSpecialResource;
        }

        public static HediffComp_QingheCombatState GetCombatState(Pawn pawn)
        {
            return GetHediffComp<HediffComp_QingheCombatState>(pawn, MX_QHDefOf.MX_QH_CombatState);
        }

        public static Hediff_SeasonalResonance GetSeasonalResonance(Pawn pawn)
        {
            return GetCombatState(pawn)?.CurrentResonance;
        }

        public static HediffComp_SwordPressure GetSwordPressure(Pawn pawn)
        {
            return PawnSpecialResourceUtility.GetSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_SwordPressure) as HediffComp_SwordPressure;
        }

        public static HediffComp_MeditativeStillness GetMeditativeStillness(Pawn pawn)
        {
            return GetHediffComp<HediffComp_MeditativeStillness>(pawn, MX_QHDefOf.MX_QH_MeditativeStillness);
        }

        public static HediffComp_CombatStance GetCombatStance(Pawn pawn)
        {
            return GetHediffComp<HediffComp_CombatStance>(pawn, MX_QHDefOf.MX_QH_CombatStance);
        }

        public static void SyncAuraShieldForPowerLevel(Pawn pawn)
        {
            HediffComp_AuraShield protection = GetHediffComp<HediffComp_AuraShield>(
                pawn,
                MX_QHDefOf.MX_QH_AuraShield);
            protection?.SyncForPowerLevel();
        }

        private const int StillnessGatheringLapseTicks = 30;

        /// <summary>
        /// Stillness accrues continuously while an activity lasts, so the rate rides a short-lived
        /// hediff whose severity is the rate per second: the activity refreshes it each tick and it
        /// lapses on its own once the activity stops.
        /// </summary>
        public static void SetStillnessGathering(Pawn pawn, float ratePerSecond)
        {
            if (ratePerSecond <= 0f)
            {
                return;
            }

            Hediff gathering = pawn.health.GetOrAddHediff(MX_QHDefOf.MX_QH_StillnessGathering);
            gathering.Severity = ratePerSecond;
            (gathering as HediffWithComps)?.GetComp<HediffComp_Disappears>()?.SetDuration(StillnessGatheringLapseTicks);
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

        public static int GetAuraMasteryLevel(Pawn pawn)
        {
            return GetAuraMasteryComp(pawn)?.EffectiveLevel ?? 0;
        }

        public static void AddAuraMasteryLevel(Pawn pawn)
        {
            HediffComp_AuraMastery comp = GetAuraMasteryComp(pawn);
            if (comp == null || comp.IsMaxLevel)
            {
                return;
            }

            comp.AddProgress(comp.RequiredProgressForCurrentLevel);
        }

        public static HediffComp_AuraMastery GetAuraMasteryComp(Pawn pawn)
        {
            return GetHediffComp<HediffComp_AuraMastery>(pawn, MX_QHDefOf.MX_QH_AuraMastery);
        }

        public static void AddAuraMasteryProgress(Pawn pawn, float amount)
        {
            if (!MX_QHCharacterUtility.IsQinghe(pawn) || amount <= 0f)
            {
                return;
            }

            GetAuraMasteryComp(pawn)?.AddProgress(amount);
        }

        public static void AddAuraMasteryProgressFromCraft(Pawn pawn, RecipeDef recipe, Thing product)
        {
            if (!MX_QHCharacterUtility.IsQinghe(pawn)
                || !IsAuraMasteryCraftRecipe(recipe)
                || product == null
                || product.def?.category != ThingCategory.Item)
            {
                return;
            }

            float amount = CalculateAuraMasteryProgressFromCraft(recipe, product);
            if (amount > 0f)
            {
                AddAuraMasteryProgress(pawn, amount);
            }
        }

        private static bool IsAuraMasteryCraftRecipe(RecipeDef recipe)
        {
            return recipe != null
                && recipe.workSkillLearnFactor > 0f
                && (recipe.workSkill == SkillDefOf.Crafting || recipe.workSkill == SkillDefOf.Artistic);
        }

        private static float CalculateAuraMasteryProgressFromCraft(RecipeDef recipe, Thing product)
        {
            // Crafted sculptures and instruments arrive wrapped in a MinifiedThing.
            Thing innerProduct = product.GetInnerIfMinified();
            CompQuality compQuality = innerProduct?.TryGetComp<CompQuality>();
            float qualityFactor = compQuality == null ? 0.9f : 0.7f + 0.28f * (int)compQuality.Quality;
            float marketValue = Mathf.Max(0f, product.MarketValue * Mathf.Max(1, product.stackCount));
            float cappedValue = Mathf.Min(marketValue, 60000f);

            // GenRecipe returns one result per declared product entry, not per item in its stack.
            // Share recipe work across those results; dynamic byproducts get only value-based XP.
            int declaredResults = 0;
            bool isDeclaredProduct = false;
            if (recipe.products != null)
            {
                foreach (ThingDefCountClass entry in recipe.products)
                {
                    if (entry?.thingDef == null || entry.count <= 0)
                    {
                        continue;
                    }

                    declaredResults++;
                    isDeclaredProduct |= entry.thingDef == innerProduct?.def;
                }
            }

            float workReward = 0f;
            if (isDeclaredProduct)
            {
                float work = Mathf.Clamp(recipe.WorkAmountTotal(innerProduct), 0f, 250000f);
                workReward = (10f + work * 0.01f) / declaredResults;
            }

            return (workReward + cappedValue * 0.3f) * qualityFactor;
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
    }
}



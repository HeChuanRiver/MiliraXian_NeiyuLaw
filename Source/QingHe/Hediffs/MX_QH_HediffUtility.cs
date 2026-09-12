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

        public static void SyncAuraShieldForPowerLevel(Pawn pawn)
        {
            HediffComp_AuraShield protection = GetHediffComp<HediffComp_AuraShield>(
                pawn,
                MX_QHDefOf.MX_QH_AuraShield);
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
            EnsureHediff(pawn, MX_QHDefOf.MX_QH_FlowingForm);
            EnsureHediff(pawn, MX_QHDefOf.MX_QH_AuraShield);
            EnsureHediff(pawn, MX_QHDefOf.MX_QH_Trickle);
            EnsureFlowerResonance(pawn);
            EnsureFlowerDecree(pawn);
            EnsureCombatState(pawn);
            EnsureSwordPressure(pawn);
            EnsureMeditativeStillness(pawn);
            EnsureAuraMasteryComp(pawn);

            GetHediffComp<HediffComp_AuraShield>(pawn, MX_QHDefOf.MX_QH_AuraShield)?.EnsureShieldBound();
        }

        public static int GetAuraMasteryLevel(Pawn pawn)
        {
            return GetAuraMasteryComp(pawn)?.EffectiveLevel ?? 0;
        }

        public static void AddAuraMasteryLevel(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }

            HediffComp_QingheAuraMasterySync comp = EnsureAuraMasteryComp(pawn);
            if (comp == null || comp.IsMaxLevel)
            {
                return;
            }

            comp.AddProgress(comp.RequiredProgressForCurrentLevel);
            MX_QHSkillUtility.SyncChoices(pawn);
        }

        public static HediffComp_QingheAuraMasterySync GetAuraMasteryComp(Pawn pawn)
        {
            return GetHediffComp<HediffComp_QingheAuraMasterySync>(pawn, MX_QHDefOf.MX_QH_FlowerResonance);
        }

        public static HediffComp_QingheAuraMasterySync EnsureAuraMasteryComp(Pawn pawn)
        {
            return EnsureHediffComp<HediffComp_QingheAuraMasterySync>(pawn, MX_QHDefOf.MX_QH_FlowerResonance);
        }

        public static float GetAuraMasteryProgress(Pawn pawn)
        {
            return GetAuraMasteryComp(pawn)?.Progress ?? 0f;
        }

        public static float GetAuraMasteryProgressRequired(Pawn pawn)
        {
            return GetAuraMasteryComp(pawn)?.RequiredProgressForCurrentLevel ?? 0f;
        }

        public static float GetAuraMasteryProgressPercent(Pawn pawn)
        {
            return GetAuraMasteryComp(pawn)?.ProgressPercent ?? 0f;
        }

        public static void AddAuraMasteryProgress(Pawn pawn, float amount)
        {
            if (!MX_QHCharacterUtility.IsQinghe(pawn) || amount <= 0f)
            {
                return;
            }

            EnsureAuraMasteryComp(pawn)?.AddProgress(amount);
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



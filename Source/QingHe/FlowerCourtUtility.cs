using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Things;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class FlowerCourtUtility
    {
        public static HediffComp_FlowerResonance EnsureSkillTreeState(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || MX_QHDefOf.MX_QH_FlowerResonance == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_FlowerResonance);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(MX_QHDefOf.MX_QH_FlowerResonance, pawn);
                pawn.health.AddHediff(hediff);
            }

            return (hediff as HediffWithComps)?.GetComp<HediffComp_FlowerResonance>();
        }

        public static HediffComp_FlowerResonance GetSkillTreeState(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || MX_QHDefOf.MX_QH_FlowerResonance == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_FlowerResonance);
            return (hediff as HediffWithComps)?.GetComp<HediffComp_FlowerResonance>();
        }

        public static void EnsureFlowerResources(Pawn pawn)
        {
            PawnSpecialResourceUtility.EnsureSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_FlowerDecree);
        }

        public static HediffComp_FlowerDecree GetFlowerDecree(Pawn pawn)
        {
            return PawnSpecialResourceUtility.GetSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_FlowerDecree) as HediffComp_FlowerDecree;
        }

        public static HediffComp_MeditativeStillness EnsureMeditativeStillness(Pawn pawn)
        {
            if (!MX_QHUtility.IsQinghe(pawn))
            {
                return null;
            }

            return EnsureResourceHediff(pawn, MX_QHDefOf.MX_QH_MeditativeStillness) as HediffComp_MeditativeStillness;
        }

        public static HediffComp_MeditativeStillness GetMeditativeStillness(Pawn pawn)
        {
            return GetResourceHediff(pawn, MX_QHDefOf.MX_QH_MeditativeStillness) as HediffComp_MeditativeStillness;
        }

        public static void AddMeditativeStillnessFromLotusPond(Pawn pawn, Building_LotusPond lotusPond)
        {
            HediffComp_MeditativeStillness stillness = EnsureMeditativeStillness(pawn);
            if (stillness == null || lotusPond == null)
            {
                return;
            }

            stillness.AddStillness(stillness.PropsStillness.baseGainPerDay / 60000f * ResolveLotusPondRoomStillnessFactor(lotusPond));
        }

        public static void ApplyMeditativeStillnessQualityBonus(Pawn pawn, ref QualityCategory quality)
        {
            HediffComp_MeditativeStillness stillness = GetMeditativeStillness(pawn);
            if (stillness == null)
            {
                return;
            }

            int bonusLevels = stillness.ConsumeForQualityBonus();
            if (bonusLevels <= 0)
            {
                return;
            }

            quality = (QualityCategory)Mathf.Clamp((int)quality + bonusLevels, 0, 6);
        }

        public static HediffComp_DivineFortune GetDivineFortune(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || MX_QHDefOf.MX_QH_DivineFortune == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_DivineFortune);
            return (hediff as HediffWithComps)?.GetComp<HediffComp_DivineFortune>();
        }

        public static void EnsureFlowerCourtSystems(Pawn pawn)
        {
            if (!MX_QHUtility.IsQinghe(pawn))
            {
                return;
            }

            EnsureCoreHediffs(pawn);
            QingheSkillTreeSystem.SyncChoices(pawn);
            EnsureFlowerResources(pawn);
            EnsureMeditativeStillness(pawn);
            EnsureQixiRitualPrecept();
        }

        private static void EnsureQixiRitualPrecept()
        {
            if (!ModsConfig.IdeologyActive || Faction.OfPlayer?.ideos?.PrimaryIdeo == null)
            {
                return;
            }

            PreceptDef qixiRitualDef = DefDatabase<PreceptDef>.GetNamedSilentFail("MX_QH_QixiRitual");
            if (qixiRitualDef == null)
            {
                return;
            }

            Ideo playerIdeo = Faction.OfPlayer.ideos.PrimaryIdeo;
            if (playerIdeo.HasPrecept(qixiRitualDef))
            {
                return;
            }

            playerIdeo.AddPrecept(PreceptMaker.MakePrecept(qixiRitualDef), true, null, qixiRitualDef.ritualPatternBase);
        }

        private static void EnsureCoreHediffs(Pawn pawn)
        {
            EnsureHediff(pawn, MX_QHDefOf.MX_QH_DivineBlessing);
            EnsureHediff(pawn, MX_QHDefOf.MX_QH_DivineProtection);
            EnsureHediff(pawn, MX_QHDefOf.MX_QH_DivineFortune);

            // Re-bind CompDivineProtectionShield if the hediff already existed (e.g. after loading a save)
            // where CompPostPostAdd() won't fire. EnsureShieldBound() is idempotent.
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_DivineProtection);
            if (hediff is HediffWithComps hwc)
            {
                hwc.GetComp<HediffComp_DivineProtection>()?.EnsureShieldBound();
            }
        }

        private static void EnsureHediff(Pawn pawn, HediffDef hediffDef)
        {
            if (pawn?.health?.hediffSet == null || hediffDef == null)
            {
                return;
            }

            if (pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef) != null)
            {
                return;
            }

            Hediff hediff = HediffMaker.MakeHediff(hediffDef, pawn);
            pawn.health.AddHediff(hediff);
        }

        private static HediffComp_PawnSpecialResource EnsureResourceHediff(Pawn pawn, HediffDef hediffDef)
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

            return (hediff as HediffWithComps)?.GetComp<HediffComp_PawnSpecialResource>();
        }

        private static HediffComp_PawnSpecialResource GetResourceHediff(Pawn pawn, HediffDef hediffDef)
        {
            if (pawn?.health?.hediffSet == null || hediffDef == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            return (hediff as HediffWithComps)?.GetComp<HediffComp_PawnSpecialResource>();
        }

        private static float ResolveLotusPondRoomStillnessFactor(Building_LotusPond lotusPond)
        {
            Room room = lotusPond.GetRoom();
            if (room == null)
            {
                return 1f;
            }

            float beauty = room.GetStat(RoomStatDefOf.Beauty);
            float cleanliness = room.GetStat(RoomStatDefOf.Cleanliness);
            float beautyFactor = Mathf.Lerp(0.75f, 1.5f, Mathf.InverseLerp(-5f, 20f, beauty));
            float cleanlinessFactor = Mathf.Lerp(0.75f, 1.25f, Mathf.InverseLerp(-2f, 1f, cleanliness));
            return beautyFactor * cleanlinessFactor;
        }
    }
}

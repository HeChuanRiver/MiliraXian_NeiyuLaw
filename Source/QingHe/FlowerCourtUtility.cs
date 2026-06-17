using MiliraXian.Characters.QingHe.Hediffs;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class FlowerCourtUtility
    {
        public static HediffComp_QingheSkillTreeState EnsureSkillTreeState(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || MX_QHDefOf.MX_QH_SkillTreeState == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_SkillTreeState);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(MX_QHDefOf.MX_QH_SkillTreeState, pawn);
                pawn.health.AddHediff(hediff);
            }

            return (hediff as HediffWithComps)?.GetComp<HediffComp_QingheSkillTreeState>();
        }

        public static HediffComp_FlowerDivination GetFlowerDivination(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || MX_QHDefOf.MX_QH_SkillTreeState == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_SkillTreeState);
            return (hediff as HediffWithComps)?.GetComp<HediffComp_FlowerDivination>();
        }

        public static HediffComp_FlowerDivination EnsureFlowerDivination(Pawn pawn)
        {
            return EnsureSkillTreeState(pawn)?.parent?.GetComp<HediffComp_FlowerDivination>();
        }

        public static void EnsureFlowerResources(Pawn pawn)
        {
            PawnSpecialResourceUtility.EnsureSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_FlowerDecree);
        }

        public static HediffComp_FlowerDecree GetFlowerDecree(Pawn pawn)
        {
            return PawnSpecialResourceUtility.GetSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_FlowerDecree) as HediffComp_FlowerDecree;
        }

        public static void AddFlowerDecreeRecoveryProgress(Pawn pawn, float amount)
        {
            HediffComp_FlowerDecree decree = PawnSpecialResourceUtility.EnsureSpecialResourceComp(pawn, MX_QHDefOf.MX_QH_FlowerDecree) as HediffComp_FlowerDecree;
            decree?.AddRecoveryProgress(amount);
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
        }

        private static void EnsureCoreHediffs(Pawn pawn)
        {
            EnsureHediff(pawn, MX_QHDefOf.MX_QH_LongBreath);
            EnsureHediff(pawn, MX_QHDefOf.MX_QH_LotusShield);

            // Re-bind CompLotusShield if the hediff already existed (e.g. after loading a save)
            // where CompPostPostAdd() won't fire. EnsureShieldBound() is idempotent.
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_LotusShield);
            if (hediff is HediffWithComps hwc)
            {
                hwc.GetComp<HediffComp_LotusShield>()?.EnsureShieldBound();
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
    }
}

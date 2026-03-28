using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class TempestUtility
    {
        public static void AddTempest(Pawn pawn, float amount)
        {
            if (pawn == null || amount <= 0f)
            {
                return;
            }

            PawnSpecialResourceUtility.AddResource(pawn, MX_QHDefOf.MX_QH_Tempest, amount);
            NotifyRecoverEvent(pawn);
        }

        public static float GetCurrent(Pawn pawn)
        {
            return pawn == null ? 0f : PawnSpecialResourceUtility.GetCurrentResource(pawn, MX_QHDefOf.MX_QH_Tempest);
        }

        public static float GetMax(Pawn pawn)
        {
            return pawn == null ? 0f : PawnSpecialResourceUtility.GetMaxResource(pawn, MX_QHDefOf.MX_QH_Tempest);
        }

        public static float GetPercent(Pawn pawn)
        {
            var max = Mathf.Max(1f, GetMax(pawn));
            return Mathf.Clamp01(GetCurrent(pawn) / max);
        }

        public static HediffComp_PawnSpecialResource GetResourceComp(Pawn pawn)
        {
            return GetComp(pawn);
        }

        public static void TryConsume(Pawn pawn, float amount)
        {
            if (pawn == null || amount <= 0f)
            {
                return;
            }

            PawnSpecialResourceUtility.TryConsumeResource(pawn, MX_QHDefOf.MX_QH_Tempest, amount);
        }

        public static void NotifyRecoverEvent(Pawn pawn)
        {
            GetComp(pawn)?.NotifyRecoverEvent();
        }

        public static HediffComp_Tempest GetComp(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || MX_QHDefOf.MX_QH_Tempest == null)
            {
                return null;
            }

            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_Tempest);
            return (hediff as HediffWithComps)?.GetComp<HediffComp_Tempest>();
        }
    }
}
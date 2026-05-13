using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class EleganceUtility
    {
        public static void AddElegance(Pawn pawn, float amount)
        {
            if (pawn == null || amount <= 0f)
            {
                return;
            }

            PawnSpecialResourceUtility.AddResource(pawn, MX_QHDefOf.MX_QH_Elegance, amount);
        }

        public static float GetCurrent(Pawn pawn)
        {
            return pawn == null ? 0f : PawnSpecialResourceUtility.GetCurrentResource(pawn, MX_QHDefOf.MX_QH_Elegance);
        }

        public static float GetMax(Pawn pawn)
        {
            return pawn == null ? 0f : PawnSpecialResourceUtility.GetMaxResource(pawn, MX_QHDefOf.MX_QH_Elegance);
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

        public static float GetTempestRecoverThreshold(Pawn pawn)
        {
            return GetComp(pawn)?.TempestRecoverThreshold ?? 0.8f;
        }

        public static void NotifyCombatEvent(Pawn pawn)
        {
            GetComp(pawn)?.NotifyCombatEvent();
        }

        public static void NotifyDecayEvent(Pawn pawn)
        {
            GetComp(pawn)?.NotifyDecayEvent();
        }

        public static float FactorLinear(float max, Pawn pawn)
        {
            return pawn == null ? 1f : 1f + max * GetPercent(pawn);
        }

        public static HediffComp_Elegance GetComp(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || MX_QHDefOf.MX_QH_Elegance == null)
            {
                return null;
            }

            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_Elegance);
            return (hediff as HediffWithComps)?.GetComp<HediffComp_Elegance>();
        }
    }
}
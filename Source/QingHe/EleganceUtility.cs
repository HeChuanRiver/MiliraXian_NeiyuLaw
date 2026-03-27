using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class EleganceUtility
    {
        public static void AddElegance(Pawn caster, float amount)
        {
            if (caster == null || amount <= 0f)
            {
                return;
            }

            PawnSpecialResourceUtility.AddResource(caster, MX_QHDefOf.MX_QH_Elegance, amount);
        }

        public static float FactorLinear(float max, Pawn pawn)
        {
            if (pawn == null)
            {
                return 1.0f;
            }

            float current = PawnSpecialResourceUtility.GetCurrentResource(pawn, MX_QHDefOf.MX_QH_Elegance);
            float resourceMax = PawnSpecialResourceUtility.GetMaxResource(pawn, MX_QHDefOf.MX_QH_Elegance);
            if (resourceMax <= 0f)
            {
                return 1.0f;
            }

            float p = current / resourceMax;
            return 1f + max * p;
        }
    }
}
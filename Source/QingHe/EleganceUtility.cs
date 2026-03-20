using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class EleganceUtility
    {
        public static float FactorLinear(float max, Pawn pawn)
        {
            if (pawn == null)
            {
                return 1.0f;
            }

            var p = PawnSpecialResourceUtility.GetCurrentResource(pawn, MX_QHDefOf.MX_QH_Elegance) /
                    PawnSpecialResourceUtility.GetMaxResource(pawn, MX_QHDefOf.MX_QH_Elegance);
            return 1 + max * p;
        }
    }
}
using MiliraXian.Characters.QingHe.Hediffs;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class FlowerDivinationBuffUtility
    {
        public const float ShieldDamageFactor = 0.5f;

        public static bool Active(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || pawn.Dead || MX_QHDefOf.MX_QH_FlowerDivinationBuff == null)
            {
                return false;
            }

            Hediff buff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_FlowerDivinationBuff);
            if (buff == null)
            {
                return false;
            }

            HediffComp_FlowerDivination divination = FlowerCourtUtility.GetFlowerDivination(pawn);
            return divination == null || divination.Active;
        }
    }
}

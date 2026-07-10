using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Stats
{
    public class StatPart_FlowerBellCorrosionArmor : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            Pawn pawn = GetAffectedPawn(req);
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_AbnormalDefOf.MX_AbnormalCorroded);
            HediffComp_AbnormalCorroded comp = hediff?.TryGetComp<HediffComp_AbnormalCorroded>();
            if (comp == null)
            {
                return;
            }

            val *= comp.ArmorMultiplier;
        }

        public override string ExplanationPart(StatRequest req)
        {
            Pawn pawn = GetAffectedPawn(req);
            if (pawn?.health?.hediffSet == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_AbnormalDefOf.MX_AbnormalCorroded);
            HediffComp_AbnormalCorroded comp = hediff?.TryGetComp<HediffComp_AbnormalCorroded>();
            if (comp == null)
            {
                return null;
            }

            return $"{hediff.LabelCap}: x{comp.ArmorMultiplier.ToStringPercent()}";
        }

        private static Pawn GetAffectedPawn(StatRequest req)
        {
            Pawn pawn = req.Thing as Pawn;
            if (pawn != null)
            {
                return pawn;
            }

            Apparel apparel = req.Thing as Apparel;
            return apparel?.Wearer;
        }
    }
}

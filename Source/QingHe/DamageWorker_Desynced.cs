using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class DamageWorker_Desynced : DamageWorker_AddInjury
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            if (thing is Pawn pawn && MX_QHDefOf.MX_QH_Desynced != null)
            {
                Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_Desynced);
                if (hediff == null)
                {
                    hediff = HediffMaker.MakeHediff(MX_QHDefOf.MX_QH_Desynced, pawn);
                    hediff.Severity = 0f;
                    pawn.health.AddHediff(hediff, dinfo: dinfo);
                }

                hediff.Severity += dinfo.Amount * 0.02f;
            }

            return base.Apply(dinfo, thing);
        }
    }
}

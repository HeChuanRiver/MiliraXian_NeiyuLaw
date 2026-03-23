using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class DamageWorker_Dehydrate : DamageWorker_AddInjury
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing thing)
        {
            if (thing is Pawn pawn)
            {
                var hediff = HediffMaker.MakeHediff(MX_QHDefOf.MX_DehydrateDamage, pawn);
                hediff.Severity = dinfo.Amount * 0.01f;
                pawn.health.AddHediff(hediff, dinfo: dinfo);
            }
            return base.Apply(dinfo, thing);
        }
    }
}
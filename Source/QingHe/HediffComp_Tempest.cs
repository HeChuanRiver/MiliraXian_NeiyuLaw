using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class HediffCompProperties_Tempest : HediffCompProperties_PawnSpecialResource
    {
        public HediffCompProperties_Tempest()
        {
            compClass = typeof(HediffComp_Tempest);
        }
    }
    
    public class HediffComp_Tempest : HediffComp_PawnSpecialResource
    {
        public HediffCompProperties_Tempest Props => (HediffCompProperties_Tempest)props;
        
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            var pawn = parent.pawn;
            if (pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MXQH_Elegance)?.TryGetComp<HediffComp_Elegance>().ValuePercent > 0.8f)
            {
                AddValue(0.01f);
            }
            else
            {
                AddValue(-0.02f);
            }
        }
    }
}
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe
{
    public class HediffCompProperties_Elegance : HediffCompProperties_PawnSpecialResource
    {
        public HediffCompProperties_Elegance()
        {
            this.compClass = typeof(HediffComp_Elegance);
        }
    }
    
    public class HediffComp_Elegance : HediffComp_PawnSpecialResource
    {
        public HediffCompProperties_Elegance Props => (HediffCompProperties_Elegance)props;
        
        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            var pawn = parent.pawn;
            if (GenAI.InDangerousCombat(pawn))
            {
                AddValue(0.02f);
            }
            else
            {
                AddValue(-0.03f);
            }
        }
    }
}
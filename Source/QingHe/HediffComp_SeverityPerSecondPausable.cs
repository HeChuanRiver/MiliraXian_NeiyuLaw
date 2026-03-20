using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class HediffCompProperties_SeverityPerSecondPausable : HediffCompProperties
    {
        public float severityPerSecond;
        public int ticksBeforeDecrease = 120;
        
        public HediffCompProperties_SeverityPerSecondPausable()
        {
            compClass = typeof(HediffComp_SeverityPerSecondPausable);
        }
    }
    
    public class HediffComp_SeverityPerSecondPausable : HediffComp
    {
        private float currentTicksBeforeDecrease;

        public HediffCompProperties_SeverityPerSecondPausable Props =>
            (HediffCompProperties_SeverityPerSecondPausable)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            currentTicksBeforeDecrease = Props.ticksBeforeDecrease;
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (currentTicksBeforeDecrease > 0)
            {
                currentTicksBeforeDecrease--;
            }
            float stageFactor = parent.CurStage?.severityGainFactor ?? 1f;
            severityAdjustment = (currentTicksBeforeDecrease == 0 ? Props.severityPerSecond : 0) / 60.0f * stageFactor;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref currentTicksBeforeDecrease, "currentTicksBeforeDecrease");
        }

        public void ResetTimer()
        {
            currentTicksBeforeDecrease = Props.ticksBeforeDecrease;
        }
    }
}
using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_OnAccumulated : HediffCompProperties
    {
        public HediffDef addHediff;
        public float addHediffSeverityPerAccumulation;
        public bool addHediffFleshOnly;

        public HediffCompProperties_OnAccumulated()
        {
            compClass = typeof(HediffComp_OnAccumulated);
        }
    }

    public class HediffComp_OnAccumulated : HediffComp
    {
        private HediffCompProperties_OnAccumulated PropsOnAccumulated => (HediffCompProperties_OnAccumulated)props;

        public virtual void NotifyAccumulationApplied(Pawn caster, float finalSeverityOffset)
        {
            AddHediffByAccumulation(finalSeverityOffset);
        }

        protected void AddHediffByAccumulation(float finalSeverityOffset)
        {
            if (Pawn == null || Pawn.Dead || Pawn.health?.hediffSet == null || finalSeverityOffset <= 0f)
            {
                return;
            }

            if (PropsOnAccumulated.addHediff == null || PropsOnAccumulated.addHediffSeverityPerAccumulation <= 0f)
            {
                return;
            }

            if (PropsOnAccumulated.addHediffFleshOnly && Pawn.RaceProps?.IsFlesh != true)
            {
                return;
            }

            float multiplier = PropsOnAccumulated.addHediffSeverityPerAccumulation > 0f
                ? PropsOnAccumulated.addHediffSeverityPerAccumulation
                : 0f;
            HealthUtility.AdjustSeverity(Pawn, PropsOnAccumulated.addHediff, finalSeverityOffset * multiplier);
        }
    }
}

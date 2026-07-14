using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_OnAbnormalApplied : HediffCompProperties
    {
        public HediffDef addHediff;
        public float severityPerAccumulation;
        public bool fleshOnly;

        public HediffCompProperties_OnAbnormalApplied()
        {
            compClass = typeof(HediffComp_OnAbnormalApplied);
        }
    }

    public class HediffComp_OnAbnormalApplied : HediffComp
    {
        protected HediffCompProperties_OnAbnormalApplied PropsApplied => (HediffCompProperties_OnAbnormalApplied)props;

        public virtual void NotifyApplied(Pawn source, float amount)
        {
            if (Pawn == null || Pawn.Dead || Pawn.health?.hediffSet == null || amount <= 0f)
            {
                return;
            }

            if (PropsApplied.addHediff == null || PropsApplied.severityPerAccumulation <= 0f)
            {
                return;
            }

            if (PropsApplied.fleshOnly && Pawn.RaceProps?.IsFlesh != true)
            {
                return;
            }

            HealthUtility.AdjustSeverity(Pawn, PropsApplied.addHediff, amount * PropsApplied.severityPerAccumulation);
        }
    }
}

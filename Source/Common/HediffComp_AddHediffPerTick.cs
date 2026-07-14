using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_AddHediffPerTick : HediffCompProperties
    {
        public HediffDef addHediff;
        public float severityPerTick;
        public bool fleshOnly;

        public HediffCompProperties_AddHediffPerTick()
        {
            compClass = typeof(HediffComp_AddHediffPerTick);
        }
    }

    public class HediffComp_AddHediffPerTick : HediffComp
    {
        private HediffCompProperties_AddHediffPerTick PropsAdd => (HediffCompProperties_AddHediffPerTick)props;

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (Pawn == null || Pawn.Dead || Pawn.health?.hediffSet == null || PropsAdd.addHediff == null || PropsAdd.severityPerTick <= 0f)
            {
                return;
            }

            if (PropsAdd.fleshOnly && Pawn.RaceProps?.IsFlesh != true)
            {
                return;
            }

            HealthUtility.AdjustSeverity(Pawn, PropsAdd.addHediff, PropsAdd.severityPerTick);
        }
    }
}

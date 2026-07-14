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
        private Hediff cachedHediff;

        private HediffCompProperties_AddHediffPerTick PropsAdd => (HediffCompProperties_AddHediffPerTick)props;

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (delta <= 0
                || Pawn == null
                || Pawn.Dead
                || Pawn.health?.hediffSet == null
                || PropsAdd.addHediff == null
                || PropsAdd.severityPerTick <= 0f)
            {
                return;
            }

            if (PropsAdd.fleshOnly && Pawn.RaceProps?.IsFlesh != true)
            {
                return;
            }

            AddSeverity(PropsAdd.severityPerTick * delta);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                cachedHediff = null;
            }
        }

        public override void CompPostPostRemoved()
        {
            cachedHediff = null;
            base.CompPostPostRemoved();
        }

        private void AddSeverity(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            Pawn pawn = Pawn;
            HediffDef hediffDef = PropsAdd.addHediff;
            if (pawn?.health?.hediffSet == null || pawn.Dead || hediffDef == null)
            {
                return;
            }

            if (cachedHediff == null
                || cachedHediff.def != hediffDef
                || !pawn.health.hediffSet.hediffs.Contains(cachedHediff))
            {
                cachedHediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            }

            if (cachedHediff == null)
            {
                cachedHediff = HediffMaker.MakeHediff(hediffDef, pawn);
                cachedHediff.Severity = amount;
                pawn.health.AddHediff(cachedHediff);
            }
            else
            {
                cachedHediff.Severity += amount;
            }
        }
    }
}

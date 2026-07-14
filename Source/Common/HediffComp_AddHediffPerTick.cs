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
        private const int FlushIntervalTicks = 10;

        private Hediff cachedHediff;
        private float pendingSeverity;
        private int pendingTicks;

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

            pendingSeverity += PropsAdd.severityPerTick;
            pendingTicks++;
            if (pendingTicks >= FlushIntervalTicks)
            {
                FlushPendingSeverity();
            }
        }

        public override void CompExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                FlushPendingSeverity();
            }

            base.CompExposeData();
            Scribe_Values.Look(ref pendingSeverity, "mx_abnormal_pendingAddedHediffSeverity", 0f);
            Scribe_Values.Look(ref pendingTicks, "mx_abnormal_pendingAddedHediffTicks", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                cachedHediff = null;
            }
        }

        public override void CompPostPostRemoved()
        {
            FlushPendingSeverity();
            cachedHediff = null;
            base.CompPostPostRemoved();
        }

        private void FlushPendingSeverity()
        {
            if (pendingSeverity <= 0f)
            {
                pendingSeverity = 0f;
                pendingTicks = 0;
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
                cachedHediff.Severity = pendingSeverity;
                pawn.health.AddHediff(cachedHediff);
            }
            else
            {
                cachedHediff.Severity += pendingSeverity;
            }

            pendingSeverity = 0f;
            pendingTicks = 0;
        }
    }
}

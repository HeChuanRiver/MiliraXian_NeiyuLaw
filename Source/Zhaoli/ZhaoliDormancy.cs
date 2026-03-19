using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Zhaoli
{
    public class HediffCompProperties_ZhaoliDormancy : HediffCompProperties
    {
        public int refreshIntervalTicks = 60;
        public bool dropRestToZero = true;

        public HediffCompProperties_ZhaoliDormancy()
        {
            compClass = typeof(HediffComp_ZhaoliDormancy);
        }
    }

    public class HediffComp_ZhaoliDormancy : HediffComp
    {
        public HediffCompProperties_ZhaoliDormancy PropsDormancy => (HediffCompProperties_ZhaoliDormancy)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            ForceSleepNow();
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || pawn.Destroyed || !pawn.Spawned)
            {
                return;
            }

            int intervalTicks = PropsDormancy.refreshIntervalTicks;
            if (intervalTicks <= 0)
            {
                intervalTicks = 1;
            }

            if (Find.TickManager.TicksGame % intervalTicks != 0)
            {
                return;
            }

            ForceSleepNow();
        }

        public override void CompPostPostRemoved()
        {
            Pawn pawn = Pawn;
            Job currentJob = pawn?.jobs?.curJob;
            if (currentJob != null && currentJob.def == JobDefOf.LayDown && currentJob.forceSleep)
            {
                currentJob.forceSleep = false;
            }
        }

        public void ForceSleepNow()
        {
            Pawn pawn = Pawn;
            if (pawn == null || pawn.Dead || pawn.Destroyed || !pawn.Spawned || pawn.jobs == null)
            {
                return;
            }

            if (pawn.drafter != null && pawn.drafter.Drafted)
            {
                pawn.drafter.Drafted = false;
            }

            if (PropsDormancy.dropRestToZero && pawn.needs?.rest != null)
            {
                pawn.needs.rest.CurLevel = 0f;
            }

            if (pawn.needs?.food != null)
            {
                pawn.needs.food.CurLevelPercentage = 0.01f;
            }

            if (pawn.Downed)
            {
                return;
            }

            Job currentJob = pawn.jobs.curJob;
            if (currentJob != null && currentJob.def == JobDefOf.LayDown && currentJob.forceSleep)
            {
                return;
            }

            Job job = JobMaker.MakeJob(JobDefOf.LayDown, pawn.Position);
            job.forceSleep = true;
            pawn.jobs.StartJob(job, JobCondition.InterruptForced, continueSleeping: true);
        }
    }
}

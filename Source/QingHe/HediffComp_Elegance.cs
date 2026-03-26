using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class HediffCompProperties_Elegance : HediffCompProperties_PawnSpecialResource
    {
        public int inCombatTimerMaxTicks = 360;
        public float gainPerTickInCombat = 0.02f;
        public float gainPerTickOutOfCombat = -0.03f;
        public float gainPerTickMeditation = 0.04f;
        public float gainPerTickInstrumentJoy = 0.05f;

        public HediffCompProperties_Elegance()
        {
            compClass = typeof(HediffComp_Elegance);
        }
    }

    public class HediffComp_Elegance : HediffComp_PawnSpecialResource
    {
        private int inCombatTimer;

        public HediffCompProperties_Elegance Props => (HediffCompProperties_Elegance)props;

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref inCombatTimer, "inCombatTimer", 0);
        }

        public void NotifyCombatEvent()
        {
            inCombatTimer = Props?.inCombatTimerMaxTicks ?? 0;
            if (inCombatTimer < 0)
            {
                inCombatTimer = 0;
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            Pawn pawn = parent?.pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead)
            {
                return;
            }

            float delta = inCombatTimer > 0 ? Props.gainPerTickInCombat : Props.gainPerTickOutOfCombat;
            if (inCombatTimer > 0)
            {
                inCombatTimer--;
            }

            JobDef currentJobDef = pawn.CurJob?.def;
            if (IsMeditationJob(currentJobDef) && Props.gainPerTickMeditation > delta)
            {
                delta = Props.gainPerTickMeditation;
            }

            if (IsInstrumentJoyJob(currentJobDef) && Props.gainPerTickInstrumentJoy > delta)
            {
                delta = Props.gainPerTickInstrumentJoy;
            }

            AddValue(delta);
        }

        private static bool IsMeditationJob(JobDef jobDef)
        {
            string defName = jobDef?.defName;
            return defName == "Meditate" || defName == "MeditatePray";
        }

        private static bool IsInstrumentJoyJob(JobDef jobDef)
        {
            return jobDef?.defName == "Play_MusicalInstrument";
        }
    }
}

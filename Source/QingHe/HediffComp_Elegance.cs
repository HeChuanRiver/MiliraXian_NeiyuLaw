using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class HediffCompProperties_Elegance : HediffCompProperties_PawnSpecialResource
    {
        public int inCombatTimerMaxTicks = 360;
        public int decayTimerMaxTicks = 360;
        public float tempestRecoverThreshold = 0.8f;
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
        private int decayTimer;

        public HediffCompProperties_Elegance Props => (HediffCompProperties_Elegance)props;
        public float TempestRecoverThreshold => Mathf.Clamp01(Props?.tempestRecoverThreshold ?? 0.8f);

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref inCombatTimer, "inCombatTimer", 0);
            Scribe_Values.Look(ref decayTimer, "decayTimer", 0);
        }

        public void NotifyCombatEvent()
        {
            inCombatTimer = Mathf.Max(0, Props?.inCombatTimerMaxTicks ?? 0);
            decayTimer = Mathf.Max(0, Props?.decayTimerMaxTicks ?? 0);
        }

        public void NotifyDecayEvent()
        {
            decayTimer = Mathf.Max(0, Props?.decayTimerMaxTicks ?? 0);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            var pawn = parent?.pawn;
            if (pawn == null || !pawn.Spawned || pawn.Dead)
            {
                return;
            }

            var delta = 0f;
            if (inCombatTimer > 0)
            {
                inCombatTimer--;
                delta = Props.gainPerTickInCombat;
            }

            if (decayTimer > 0)
            {
                decayTimer--;
            }
            else if (Props.gainPerTickOutOfCombat < delta)
            {
                delta = Props.gainPerTickOutOfCombat;
            }

            var jobDef = pawn.CurJob?.def;
            if (IsMeditationJob(jobDef) && Props.gainPerTickMeditation > delta)
            {
                delta = Props.gainPerTickMeditation;
            }

            if (IsInstrumentJoyJob(jobDef) && Props.gainPerTickInstrumentJoy > delta)
            {
                delta = Props.gainPerTickInstrumentJoy;
            }

            AddValue(delta);
        }

        private static bool IsMeditationJob(JobDef jobDef)
        {
            var defName = jobDef?.defName;
            return defName == "Meditate" || defName == "MeditatePray";
        }

        private static bool IsInstrumentJoyJob(JobDef jobDef)
        {
            return jobDef?.defName == "Play_MusicalInstrument";
        }
    }
}

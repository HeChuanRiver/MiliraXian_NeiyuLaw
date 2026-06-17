using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_StatusEffectFeared : HediffCompProperties
    {
        public MentalStateDef mentalStateDef;
        public int mentalStateDurationTicks = 600;
        public bool forced = true;
        public bool forceWake = true;

        public HediffCompProperties_StatusEffectFeared()
        {
            compClass = typeof(HediffComp_StatusEffectFeared);
        }
    }

    public class HediffComp_StatusEffectFeared : HediffComp
    {
        private bool startedMentalState;

        private HediffCompProperties_StatusEffectFeared PropsFeared => (HediffCompProperties_StatusEffectFeared)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            TryStartFearMentalState(dinfo?.Instigator as Pawn);
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            RecoverStartedMentalState();
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref startedMentalState, "startedMentalState", false);
        }

        private void TryStartFearMentalState(Pawn causedBy)
        {
            if (Pawn == null || Pawn.Dead || Pawn.mindState?.mentalStateHandler == null)
            {
                return;
            }

            MentalStateDef mentalStateDef = PropsFeared.mentalStateDef;
            if (mentalStateDef == null)
            {
                return;
            }

            bool started = Pawn.mindState.mentalStateHandler.TryStartMentalState(
                mentalStateDef,
                reason: parent.LabelCap,
                forced: PropsFeared.forced,
                forceWake: PropsFeared.forceWake,
                causedByMood: false,
                otherPawn: causedBy,
                transitionSilently: true,
                causedByDamage: true,
                causedByPsycast: false);

            if (!started || Pawn.MentalState == null || Pawn.MentalStateDef != mentalStateDef)
            {
                return;
            }

            startedMentalState = true;
            if (PropsFeared.mentalStateDurationTicks > 0)
            {
                Pawn.MentalState.forceRecoverAfterTicks = PropsFeared.mentalStateDurationTicks;
            }
        }

        private void RecoverStartedMentalState()
        {
            MentalStateDef mentalStateDef = PropsFeared.mentalStateDef;
            if (!startedMentalState || Pawn == null || Pawn.Dead || Pawn.MentalStateDef != mentalStateDef)
            {
                return;
            }

            Pawn.MentalState?.RecoverFromState();
        }
    }
}

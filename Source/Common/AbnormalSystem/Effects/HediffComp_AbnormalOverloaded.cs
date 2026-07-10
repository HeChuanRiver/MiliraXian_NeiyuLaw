using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_AbnormalOverloaded : HediffCompProperties
    {
        public MentalStateDef mentalStateDef;
        public int mentalStateDurationTicks = 600;
        public bool forced = true;
        public bool forceWake = true;

        public HediffCompProperties_AbnormalOverloaded()
        {
            compClass = typeof(HediffComp_AbnormalOverloaded);
        }
    }

    public class HediffComp_AbnormalOverloaded : HediffComp
    {
        private bool startedMentalState;

        private HediffCompProperties_AbnormalOverloaded PropsOverloaded => (HediffCompProperties_AbnormalOverloaded)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            TryStartOverloadMentalState(dinfo?.Instigator as Pawn);
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

        public void NotifyPawnDowned()
        {
            RecoverStartedMentalState();
        }

        private void TryStartOverloadMentalState(Pawn causedBy)
        {
            if (Pawn == null || Pawn.Dead || Pawn.Destroyed || Pawn.Downed || Pawn.Deathresting || Pawn.mindState?.mentalStateHandler == null)
            {
                return;
            }

            MentalStateDef mentalStateDef = PropsOverloaded.mentalStateDef ?? MentalStateDefOf.BerserkMechanoid;
            if (mentalStateDef == null)
            {
                return;
            }

            bool started = Pawn.mindState.mentalStateHandler.TryStartMentalState(
                mentalStateDef,
                reason: parent.LabelCap,
                forced: PropsOverloaded.forced,
                forceWake: PropsOverloaded.forceWake,
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
            if (PropsOverloaded.mentalStateDurationTicks > 0)
            {
                Pawn.MentalState.forceRecoverAfterTicks = PropsOverloaded.mentalStateDurationTicks;
            }
        }

        private void RecoverStartedMentalState()
        {
            MentalStateDef mentalStateDef = PropsOverloaded.mentalStateDef ?? MentalStateDefOf.BerserkMechanoid;
            if (!startedMentalState || Pawn == null || Pawn.Dead || Pawn.MentalStateDef != mentalStateDef)
            {
                return;
            }

            Pawn.MentalState?.RecoverFromState();
        }
    }
}

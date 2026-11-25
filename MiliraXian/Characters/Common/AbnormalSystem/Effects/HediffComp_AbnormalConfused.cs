using RimWorld;
using Verse;
using MiliraXian.Characters.Common.AbnormalSystem;

namespace MiliraXian.Characters.Common.AbnormalSystem.Effects
{
    public class HediffCompProperties_AbnormalConfused : HediffCompProperties
    {
        public MentalStateDef mentalStateDef;
        public int mentalStateDurationTicks = 600;
        public bool forced = true;
        public bool forceWake = true;

        public HediffCompProperties_AbnormalConfused()
        {
            compClass = typeof(HediffComp_AbnormalConfused);
        }
    }

    public class HediffComp_AbnormalConfused : HediffComp
    {
        private bool startedMentalState;

        private HediffCompProperties_AbnormalConfused PropsConfused => (HediffCompProperties_AbnormalConfused)props;

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

        public void NotifyPawnDowned()
        {
            RecoverStartedMentalState();
        }

        private void TryStartFearMentalState(Pawn causedBy)
        {
            if (Pawn == null || Pawn.Dead || Pawn.Destroyed || Pawn.Downed || Pawn.Deathresting || Pawn.mindState?.mentalStateHandler == null)
            {
                return;
            }

            MentalStateDef mentalStateDef = PropsConfused.mentalStateDef;
            if (mentalStateDef == null)
            {
                return;
            }

            bool started = Pawn.mindState.mentalStateHandler.TryStartMentalState(
                mentalStateDef,
                reason: parent.LabelCap,
                forced: PropsConfused.forced,
                forceWake: PropsConfused.forceWake,
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
            if (PropsConfused.mentalStateDurationTicks > 0)
            {
                Pawn.MentalState.forceRecoverAfterTicks = PropsConfused.mentalStateDurationTicks;
            }
        }

        private void RecoverStartedMentalState()
        {
            MentalStateDef mentalStateDef = PropsConfused.mentalStateDef;
            if (!startedMentalState || Pawn == null || Pawn.Dead || Pawn.MentalStateDef != mentalStateDef)
            {
                return;
            }

            Pawn.MentalState?.RecoverFromState();
        }
    }
}

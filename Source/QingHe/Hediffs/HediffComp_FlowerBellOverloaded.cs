using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerBellOverloaded : HediffCompProperties
    {
        public MentalStateDef mentalStateDef;
        public int mentalStateDurationTicks = 600;
        public bool forced = true;
        public bool forceWake = true;

        public HediffCompProperties_FlowerBellOverloaded()
        {
            compClass = typeof(HediffComp_FlowerBellOverloaded);
        }
    }

    public class HediffComp_FlowerBellOverloaded : HediffComp
    {
        private bool startedMentalState;

        private HediffCompProperties_FlowerBellOverloaded PropsOverloaded => (HediffCompProperties_FlowerBellOverloaded)props;

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

        private void TryStartOverloadMentalState(Pawn causedBy)
        {
            if (Pawn == null || Pawn.Dead || Pawn.mindState?.mentalStateHandler == null)
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

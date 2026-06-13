using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Things;
using MiliraXian.Characters.QingHe.UI;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Jobs
{
    public class JobDriver_AttuneSeasonResonance : JobDriver
    {
        private const TargetIndex PondIndex = TargetIndex.A;

        private Building_LotusPond LotusPond => job.GetTarget(PondIndex).Thing as Building_LotusPond;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(PondIndex), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(PondIndex);
            this.FailOn(() => !MX_QHUtility.IsQinghe(pawn));
            yield return Toils_Goto.GotoThing(PondIndex, PathEndMode.InteractionCell);

            Toil openCourt = ToilMaker.MakeToil("OpenFlowerCourt");
            openCourt.initAction = delegate
            {
                HediffComp_SeasonResonance resonance = FlowerCourtUtility.EnsureSeasonResonance(pawn);
                FlowerCourtUtility.EnsureFlowerResources(pawn);
                if (resonance == null || LotusPond == null)
                {
                    Messages.Message("清荷尚未建立四时共鸣。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                Find.WindowStack.Add(new Dialog_SeasonResonance(pawn, resonance));
            };
            openCourt.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return openCourt;
        }
    }

    public class JobDriver_MeditateAtFlowerCourt : JobDriver
    {
        private const TargetIndex PondIndex = TargetIndex.A;
        private const int MeditationTicks = 2500;
        private const float AttunementGain = 8f;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(job.GetTarget(PondIndex), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(PondIndex);
            this.FailOn(() => !MX_QHUtility.IsQinghe(pawn));
            yield return Toils_Goto.GotoThing(PondIndex, PathEndMode.InteractionCell);

            Toil meditate = ToilMaker.MakeToil("MeditateAtFlowerCourt");
            meditate.defaultCompleteMode = ToilCompleteMode.Delay;
            meditate.defaultDuration = MeditationTicks;
            meditate.WithProgressBarToilDelay(PondIndex);
            meditate.tickAction = delegate
            {
                pawn.rotationTracker.FaceTarget(job.GetTarget(PondIndex));
                if (pawn.IsHashIntervalTick(100))
                {
                    FleckMaker.ThrowMetaIcon(pawn.Position, pawn.Map, FleckDefOf.Meditating, 0.42f);
                }
            };
            yield return meditate;

            Toil finish = ToilMaker.MakeToil("FinishFlowerCourtMeditation");
            finish.initAction = delegate
            {
                HediffComp_SeasonResonance resonance = FlowerCourtUtility.EnsureSeasonResonance(pawn);
                if (resonance == null || resonance.CurrentAttunedSeason == AttunedSeason.None)
                {
                    Messages.Message("清荷尚未选择四时共鸣。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                resonance.MeditateAtFlowerCourt(AttunementGain);
                Messages.Message("清荷在荷池旁完成冥想，四时共鸣更加清晰。", pawn, MessageTypeDefOf.PositiveEvent, historical: false);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}

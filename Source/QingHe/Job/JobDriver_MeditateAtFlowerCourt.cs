using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Jobs
{
    public class JobDriver_MeditateAtFlowerCourt : JobDriver
    {
        private const TargetIndex PondIndex = TargetIndex.A;
        private const int MeditationTicks = 2500;
        private const float ExperienceGain = 8f;

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
                HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(pawn);
                if (state == null)
                {
                    Messages.Message("清荷尚未建立花神庭。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                state.AddExperience(ExperienceGain);
                Messages.Message("清荷在荷池旁完成冥想，技能树经验有所增长。", pawn, MessageTypeDefOf.PositiveEvent, historical: false);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}

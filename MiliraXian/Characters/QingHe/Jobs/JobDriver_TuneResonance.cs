using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Hediffs;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Jobs
{
    public class JobDriver_TuneResonance : JobDriver
    {
        private const int TuneDurationTicks = 300;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            // Toil-level finish actions run on cleanup even when the job is interrupted;
            // only the driver-level one sees the end condition.
            AddFinishAction(delegate(JobCondition condition)
            {
                HediffComp_ResonanceTracker tracker = MX_QH_HediffUtility.GetResonanceTracker(pawn);
                if (condition == JobCondition.Succeeded)
                {
                    tracker?.CompleteTuning();
                }
                else
                {
                    tracker?.CancelTuning();
                }
            });
            Toil tune = ToilMaker.MakeToil("TuneResonance");
            tune.defaultDuration = TuneDurationTicks;
            tune.defaultCompleteMode = ToilCompleteMode.Delay;
            tune.WithProgressBarToilDelay(TargetIndex.None);
            yield return tune;
        }
    }
}

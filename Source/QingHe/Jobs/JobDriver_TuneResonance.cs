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
            Toil tune = ToilMaker.MakeToil("TuneResonance");
            tune.defaultDuration = TuneDurationTicks;
            tune.defaultCompleteMode = ToilCompleteMode.Delay;
            tune.AddFinishAction(delegate
            {
                HediffComp_QingheCombatState state = MX_QH_HediffUtility.GetCombatState(pawn);
                state?.CompleteTuning();
            });
            yield return tune;
        }
    }
}

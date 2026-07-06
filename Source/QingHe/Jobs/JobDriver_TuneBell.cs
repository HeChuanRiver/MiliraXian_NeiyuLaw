using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Things;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Jobs
{
    public class JobDriver_TuneBell : JobDriver
    {
        private const TargetIndex PondIndex = TargetIndex.A;
        private const TargetIndex WeaponIndex = TargetIndex.B;
        private const int TuneTicks = 360;

        private Building_LotusPond LotusPond => job.GetTarget(PondIndex).Thing as Building_LotusPond;

        private ThingWithComps FlowerBell => job.GetTarget(WeaponIndex).Thing as ThingWithComps;

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return pawn.Reserve(LotusPond, job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !MX_QHUtility.IsQinghe(pawn));
            this.FailOnIncapable(PawnCapacityDefOf.Manipulation);
            this.FailOnDespawnedNullOrForbidden(PondIndex);
            this.FailOnBurningImmobile(PondIndex);
            this.FailOn(() => FlowerBell?.TryGetComp<CompFlowerBellResonance>() == null);

            yield return Toils_Goto.GotoThing(PondIndex, PathEndMode.InteractionCell);
            yield return Toils_General.Wait(TuneTicks)
                .FailOnDestroyedNullOrForbidden(PondIndex)
                .FailOnCannotTouch(PondIndex, PathEndMode.InteractionCell)
                .WithProgressBarToilDelay(PondIndex);

            Toil finish = ToilMaker.MakeToil("FinishTuningFlowerBellAtLotusPond");
            finish.initAction = delegate
            {
                CompFlowerBellResonance comp = FlowerBell?.TryGetComp<CompFlowerBellResonance>();
                if (comp == null)
                {
                    Messages.Message("MX_QH_TuneFlowerBellNoWeapon".Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                comp.SetResonance((FlowerBellResonance)job.count);
            };
            finish.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return finish;
        }
    }
}

using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Job
{
    public class JobDriver_FlowerGodDescent : JobDriver
    {
        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !MX_QHUtility.IsQinghe(pawn));

            Toil activate = ToilMaker.MakeToil("ActivateFlowerGodDescent");
            activate.initAction = delegate
            {
                HediffComp_FlowerGodDescent descent = FlowerCourtUtility.EnsureFlowerGodDescent(pawn);
                if (descent == null)
                {
                    Messages.Message("清荷尚未建立四时共鸣。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                if (!descent.TryStartDescent())
                {
                    descent.CanStartDescent(out string reason);
                    if (!reason.NullOrEmpty())
                    {
                        Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                    }

                    return;
                }

                string message = descent.Props?.activatedMessage;
                if (!message.NullOrEmpty())
                {
                    Messages.Message(message, pawn, MessageTypeDefOf.PositiveEvent, historical: false);
                }
            };
            activate.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return activate;
        }
    }
}

using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Thoughts
{
    public class ThoughtWorker_QingheLuoshenContractMaintained : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            return Hediff_LuoshenContract.IsMaintainedFor(p)
                ? ThoughtState.ActiveAtStage(0)
                : ThoughtState.Inactive;
        }
    }
}

using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Thoughts
{
    public class ThoughtWorker_LuoshenContractMaintained : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            int stage = HediffComp_LuoshenContract.MaintainedThoughtStageFor(p);
            return stage >= 0 ? ThoughtState.ActiveAtStage(stage) : ThoughtState.Inactive;
        }
    }

    public class ThoughtWorker_QingheNoLotusPond : ThoughtWorker_QingheLotusPavilionBase
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!MX_QHCharacterUtility.IsQinghe(p) || p?.Map == null)
            {
                return ThoughtState.Inactive;
            }

            return FindLotusPond(p) == null ? ThoughtState.ActiveAtStage(0) : ThoughtState.Inactive;
        }
    }

    public class ThoughtWorker_QingheLotusPavilionBeautyLow : ThoughtWorker_QingheLotusPavilionBase
    {
        private const float RequiredImpressiveness = 40f;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!MX_QHCharacterUtility.IsQinghe(p) || p?.Map == null)
            {
                return ThoughtState.Inactive;
            }

            Building lotusPond = FindLotusPond(p);
            if (lotusPond == null)
            {
                return ThoughtState.Inactive;
            }

            Room room = lotusPond.GetRoom();
            return room == null || room.GetStat(RoomStatDefOf.Impressiveness) < RequiredImpressiveness
                ? ThoughtState.ActiveAtStage(0)
                : ThoughtState.Inactive;
        }
    }

    public abstract class ThoughtWorker_QingheLotusPavilionBase : ThoughtWorker
    {
        protected static Building FindLotusPond(Pawn p)
        {
            if (p?.Map == null || MX_QHDefOf.MX_QH_LotusPond == null)
            {
                return null;
            }

            Building assignedLotusPond = p.ownership?.AssignedMeditationSpot as Building;
            if (assignedLotusPond?.def == MX_QHDefOf.MX_QH_LotusPond && assignedLotusPond.Map == p.Map)
            {
                return assignedLotusPond;
            }

            foreach (Building building in p.Map.listerBuildings.AllBuildingsColonistOfDef(MX_QHDefOf.MX_QH_LotusPond))
            {
                if (building != null && !building.Destroyed)
                {
                    return building;
                }
            }

            return null;
        }
    }
}

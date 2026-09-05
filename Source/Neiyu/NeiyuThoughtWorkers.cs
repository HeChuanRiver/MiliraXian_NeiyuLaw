using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Neiyu
{
    internal static class NeiyuThoughtUtility
    {
        internal static bool AreNeedsNormal(Pawn pawn)
        {
            Need_Food food = pawn?.needs?.food;
            Need_Rest rest = pawn?.needs?.rest;
            Need_Joy joy = pawn?.needs?.joy;
            if (food == null || rest == null || joy == null)
            {
                return false;
            }

            JoyCategory joyCategory = joy.CurCategory;
            return food.CurCategory == HungerCategory.Fed
                   && rest.CurCategory == RestCategory.Rested
                   && (joyCategory == JoyCategory.Satisfied
                       || joyCategory == JoyCategory.High
                       || joyCategory == JoyCategory.Extreme);
        }

        internal static bool IsHungry(Pawn pawn)
        {
            Need_Food food = pawn?.needs?.food;
            return food != null && food.CurCategory != HungerCategory.Fed;
        }

        internal static bool IsTired(Pawn pawn)
        {
            Need_Rest rest = pawn?.needs?.rest;
            return rest != null && rest.CurCategory != RestCategory.Rested;
        }

        internal static bool IsRecreationLow(Pawn pawn)
        {
            Need_Joy joy = pawn?.needs?.joy;
            if (joy == null)
            {
                return false;
            }

            JoyCategory category = joy.CurCategory;
            return category == JoyCategory.Low
                   || category == JoyCategory.VeryLow
                   || category == JoyCategory.Empty;
        }
    }

    public sealed class ThoughtWorker_NeiyuJoyful : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (NeiyuPowerBalance.PassivesDisabled || !NeiyuEquipmentUtility.IsNeiyu(p))
            {
                return ThoughtState.Inactive;
            }

            return NeiyuThoughtUtility.AreNeedsNormal(p)
                ? ThoughtState.ActiveAtStage(0)
                : ThoughtState.Inactive;
        }
    }

    public sealed class ThoughtWorker_NeiyuRelaxedNearby : ThoughtWorker
    {
        private const float Radius = 10f;

        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (NeiyuPowerBalance.PassivesDisabled
                || p == null || !p.Spawned || !p.IsFreeNonSlaveColonist || p.IsQuestLodger()
                || NeiyuEquipmentUtility.IsNeiyu(p))
            {
                return ThoughtState.Inactive;
            }

            Map map = p.Map;
            if (map == null)
            {
                return ThoughtState.Inactive;
            }

            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int index = 0; index < pawns.Count; index++)
            {
                Pawn source = pawns[index];
                if (!NeiyuEquipmentUtility.IsNeiyu(source)
                    || !p.Position.InHorDistOf(source.Position, Radius))
                {
                    continue;
                }

                if (NeiyuThoughtUtility.AreNeedsNormal(source))
                {
                    return ThoughtState.ActiveAtStage(0);
                }
            }

            return ThoughtState.Inactive;
        }
    }

    public sealed class ThoughtWorker_NeiyuHungryLightheaded : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!NeiyuEquipmentUtility.IsNeiyu(p))
            {
                return ThoughtState.Inactive;
            }

            return NeiyuThoughtUtility.IsHungry(p)
                ? ThoughtState.ActiveAtStage(0)
                : ThoughtState.Inactive;
        }
    }

    public sealed class ThoughtWorker_NeiyuTiredCultivating : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!NeiyuEquipmentUtility.IsNeiyu(p))
            {
                return ThoughtState.Inactive;
            }

            return NeiyuThoughtUtility.IsTired(p)
                ? ThoughtState.ActiveAtStage(0)
                : ThoughtState.Inactive;
        }
    }

    public sealed class ThoughtWorker_NeiyuRecreationBored : ThoughtWorker
    {
        protected override ThoughtState CurrentStateInternal(Pawn p)
        {
            if (!NeiyuEquipmentUtility.IsNeiyu(p))
            {
                return ThoughtState.Inactive;
            }

            return NeiyuThoughtUtility.IsRecreationLow(p)
                ? ThoughtState.ActiveAtStage(0)
                : ThoughtState.Inactive;
        }
    }
}

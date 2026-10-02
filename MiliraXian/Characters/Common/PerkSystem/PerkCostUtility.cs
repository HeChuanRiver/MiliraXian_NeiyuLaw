using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Common.PerkSystem
{
    /// <summary>
    /// Material lookup and consumption for perk costs. Only lister buckets for the selected defs
    /// are visited, never all map things.
    /// </summary>
    public static class PerkCostUtility
    {
        public static bool UsableMaterial(Pawn pawn, Thing thing)
        {
            return thing.Spawned && !thing.Destroyed && thing.stackCount > 0 && thing.IsInAnyStorage()
                && !thing.IsForbidden(pawn) && pawn.CanReserveAndReach(thing, PathEndMode.Touch, Danger.None);
        }

        public static List<Thing> Materials(Pawn pawn, ThingDef def, out int count, int stopAt = int.MaxValue)
        {
            List<Thing> result = new List<Thing>();
            count = 0;
            if (pawn?.Map == null || def == null)
            {
                return result;
            }

            List<Thing> candidates = pawn.Map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < candidates.Count; i++)
            {
                Thing thing = candidates[i];
                if (!UsableMaterial(pawn, thing))
                {
                    continue;
                }

                result.Add(thing);
                count += thing.stackCount;
                if (count >= stopAt)
                {
                    break;
                }
            }

            return result;
        }

        public static bool HasCosts(Pawn pawn, List<ThingDefCountClass> costs)
        {
            if (costs.NullOrEmpty())
            {
                return true;
            }

            for (int i = 0; i < costs.Count; i++)
            {
                ThingDefCountClass cost = costs[i];
                Materials(pawn, cost.thingDef, out int available, cost.count);
                if (available < cost.count)
                {
                    return false;
                }
            }

            return true;
        }

        public static void ConsumeCosts(Pawn pawn, List<ThingDefCountClass> costs)
        {
            if (costs.NullOrEmpty())
            {
                return;
            }

            for (int i = 0; i < costs.Count; i++)
            {
                ThingDefCountClass cost = costs[i];
                int remaining = cost.count;
                List<Thing> stacks = Materials(pawn, cost.thingDef, out _, remaining);
                for (int j = 0; j < stacks.Count && remaining > 0; j++)
                {
                    int take = UnityEngine.Mathf.Min(stacks[j].stackCount, remaining);
                    stacks[j].SplitOff(take).Destroy();
                    remaining -= take;
                }
            }
        }
    }
}

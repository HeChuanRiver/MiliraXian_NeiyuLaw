using System;
using System.Collections.Generic;
using MiliraXian.Characters.Biography;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Neiyu.Cultivation
{
    internal static class CultivationService
    {
        private static readonly string[] RankKeys = { "cultivation_wing", "cultivation_arrow", "cultivation_halo", "cultivation_law" };

        // The biography tracker already persists stable string keys and custom progress.
        // Missing keys in old saves mean rank zero. There is no new global pawn scan.
        public static int Rank(Pawn pawn, CultivationBranch branch)
        {
            if (!NeiyuEquipmentUtility.IsNeiyu(pawn)) return 0;
            float value = BiographyFrameworkUtility.GetTracker(pawn)?.GetProgress(RankKeys[(int)branch]) ?? 0f;
            return float.IsNaN(value) || float.IsInfinity(value) ? 0 : Math.Max(0, Math.Min(3, (int)value));
        }

        public static float Fraction(int rank) => rank <= 0 ? 0f : rank == 1 ? .25f : rank == 2 ? .60f : 1f;
        public static float Fraction(Pawn pawn, CultivationBranch branch) =>
            NeiyuPowerBalance.PassivesDisabled ? 0f : Fraction(Rank(pawn, branch));

        public static string Eligibility(Pawn pawn)
        {
            if (!NeiyuEquipmentUtility.IsNeiyu(pawn) || pawn.Destroyed || pawn.Dead)
                return CultivationText.Get("Unavailable", "角色当前无法修行。");
            if (pawn.Faction != Faction.OfPlayer || !pawn.IsColonistPlayerControlled)
                return CultivationText.Get("PlayerOnly", "加入殖民地后才能修行。");
            if (!pawn.Spawned || pawn.Map == null)
                return CultivationText.Get("OnMap", "返回地图后才能修行。");
            if (pawn.Downed || pawn.InMentalState || pawn.Drafted)
                return CultivationText.Get("AtRest", "需要清醒、未征召，且不处于精神崩溃状态。");
            if (NeiyuPowerBalance.PassivesDisabled)
                return CultivationText.Get("Sealed", "当前强度设置封印了力量，修行进度仍会保留。");
            return null;
        }

        public static string Check(Pawn pawn, NeiyuCultivationNodeDef node)
        {
            string reason = Eligibility(pawn);
            if (reason != null) return reason;
            if (node == null) return CultivationText.Get("SelectNode", "选择一项修行。");
            int rank = Rank(pawn, node.branch);
            if (rank >= node.rank) return CultivationText.Get("Learned", "已领悟");
            if (node.rank != rank + 1)
                return CultivationText.Get("Prerequisite", "需要先领悟前一阶段。");
            if (node.condition != null && !node.condition.IsSatisfied(pawn, BiographyFrameworkUtility.GetTracker(pawn)))
                return CultivationText.Get("ConditionMissing", "尚未达成修行条件。");
            return null;
        }

        // Only lister buckets for the selected costs are visited, never all map things.
        // Revalidate on the click: cached UI totals are not authority to consume resources.
        public static bool UsableMaterial(Pawn pawn, Thing thing) =>
            thing.Spawned && !thing.Destroyed && thing.stackCount > 0 && thing.IsInAnyStorage()
            && !thing.IsForbidden(pawn) && pawn.CanReserveAndReach(thing, PathEndMode.Touch, Danger.None);

        public static List<Thing> Materials(Pawn pawn, ThingDef def, out int count, int stopAt = int.MaxValue)
        {
            var result = new List<Thing>();
            count = 0;
            if (pawn?.Map == null || def == null) return result;
            List<Thing> candidates = pawn.Map.listerThings.ThingsOfDef(def);
            for (int i = 0; i < candidates.Count; i++)
            {
                Thing thing = candidates[i];
                if (!UsableMaterial(pawn, thing)) continue;
                result.Add(thing);
                count += thing.stackCount;
                if (count >= stopAt) break;
            }
            return result;
        }

        public static bool TryLearn(Pawn pawn, NeiyuCultivationNodeDef node, out string message)
        {
            message = Check(pawn, node);
            if (message != null) return false;
            var stacks = new List<List<Thing>>(node.costs.Count);
            for (int i = 0; i < node.costs.Count; i++)
            {
                var cost = node.costs[i];
                List<Thing> available = Materials(pawn, cost.thingDef, out int count, cost.count);
                if (count < cost.count)
                {
                    message = CultivationText.Get("MaterialsMissing", "可用材料不足。");
                    return false;
                }
                stacks.Add(available);
            }
            Hediff_BiographyTracker tracker = BiographyFrameworkUtility.GetOrCreateTracker(pawn);
            if (tracker == null)
            {
                message = CultivationText.Get("Unavailable", "角色当前无法修行。");
                return false;
            }
            // Stage pieces before committing progress. A split failure restores every staged
            // piece; nothing is destroyed until all costs have been collected successfully.
            var staged = new List<Thing>();
            var cells = new List<IntVec3>();
            try
            {
                for (int i = 0; i < node.costs.Count; i++)
                {
                    int remaining = node.costs[i].count;
                    foreach (Thing stack in stacks[i])
                    {
                        if (remaining <= 0) break;
                        int take = Math.Min(remaining, stack.stackCount);
                        IntVec3 cell = stack.Position;
                        Thing piece = stack.SplitOff(take);
                        if (piece == null) throw new InvalidOperationException("Material split returned no item.");
                        staged.Add(piece);
                        cells.Add(cell);
                        remaining -= piece.stackCount;
                    }
                    if (remaining != 0) throw new InvalidOperationException("Material availability changed.");
                }
            }
            catch (Exception exception)
            {
                for (int i = 0; i < staged.Count; i++)
                    if (!staged[i].Destroyed && !staged[i].Spawned)
                        GenPlace.TryPlaceThing(staged[i], cells[i], pawn.Map, ThingPlaceMode.Near);
                Log.Warning("[MiliraXian] Cultivation materials restored: " + exception.Message);
                message = CultivationText.Get("Retry", "材料状态已变化，请重新尝试。");
                return false;
            }
            tracker.SetProgress(RankKeys[(int)node.branch], node.rank);
            foreach (Thing piece in staged) piece.Destroy(DestroyMode.Vanish);
            CultivationPower.InvalidateStats(pawn);
            pawn.health.capacities.Notify_CapacityLevelsDirty();
            message = CultivationText.Get("Learned", "已领悟") + " · " + node.LabelCap;
            return true;
        }
    }
}

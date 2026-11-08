using System;
using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Common.PerkSystem
{
    public static class PerkCostUtility
    {
        public static bool UsableMaterial(Pawn pawn, Thing thing) => thing != null && thing.Spawned
            && !thing.Destroyed && thing.stackCount > 0 && thing.IsInAnyStorage()
            && !thing.IsForbidden(pawn) && pawn.CanReserveAndReach(thing, PathEndMode.Touch, Danger.None);

        public static List<Thing> Materials(Pawn pawn, ThingDef def, out int count, int stopAt = int.MaxValue)
        {
            List<Thing> result = new();
            count = 0;
            if (pawn?.Map == null || def == null) return result;
            foreach (Thing thing in pawn.Map.listerThings.ThingsOfDef(def))
            {
                if (!UsableMaterial(pawn, thing)) continue;
                result.Add(thing);
                count += thing.stackCount;
                if (count >= stopAt) break;
            }
            return result;
        }

        public static bool HasCosts(Pawn pawn, List<ThingDefCountClass> costs)
        {
            if (costs.NullOrEmpty()) return true;
            Dictionary<ThingDef, int> totals = new();
            foreach (var cost in costs)
            {
                if (cost?.thingDef == null || cost.count <= 0) return false;
                totals.TryGetValue(cost.thingDef, out int current);
                totals[cost.thingDef] = checked(current + cost.count);
            }
            foreach (var cost in totals)
            {
                Materials(pawn, cost.Key, out int count, cost.Value);
                if (count < cost.Value) return false;
            }
            return true;
        }

        public static bool TryStageCosts(Pawn pawn, List<ThingDefCountClass> costs,
            out PerkCostTransaction transaction)
        {
            transaction = new PerkCostTransaction(pawn?.Map);
            if (costs.NullOrEmpty()) return true;
            if (pawn?.Map == null) { transaction.Dispose(); return false; }
            try
            {
                Dictionary<ThingDef, int> totals = new();
                foreach (var cost in costs)
                {
                    if (cost?.thingDef == null || cost.count <= 0) { transaction.Dispose(); return false; }
                    totals.TryGetValue(cost.thingDef, out int current);
                    totals[cost.thingDef] = checked(current + cost.count);
                }
                foreach (var cost in totals)
                {
                    List<Thing> stacks = Materials(pawn, cost.Key, out int count, cost.Value);
                    if (count < cost.Value) { transaction.Dispose(); return false; }
                    int remaining = cost.Value;
                    foreach (Thing stack in stacks)
                    {
                        if (remaining <= 0) break;
                        IntVec3 cell = stack.Position;
                        Thing piece = stack.SplitOff(Math.Min(remaining, stack.stackCount));
                        if (piece == null) throw new InvalidOperationException("Material split returned no item.");
                        transaction.Add(piece, cell);
                        remaining -= piece.stackCount;
                    }
                    if (remaining != 0) throw new InvalidOperationException("Material availability changed.");
                }
                return true;
            }
            catch (Exception exception)
            {
                transaction.Dispose();
                Log.Warning("[MiliraXian] Perk materials restored: " + exception.Message);
                return false;
            }
        }
    }

    /// <summary>Owns split pieces until the stored node commits; disposal restores an incomplete purchase.</summary>
    public sealed class PerkCostTransaction : IDisposable
    {
        private readonly Map map;
        private readonly List<(Thing thing, IntVec3 cell)> pieces = new();
        private bool completed;
        internal PerkCostTransaction(Map map) => this.map = map;
        internal void Add(Thing thing, IntVec3 cell) => pieces.Add((thing, cell));
        public void Commit()
        {
            completed = true;
            foreach (var piece in pieces) piece.thing.Destroy(DestroyMode.Vanish);
            pieces.Clear();
        }
        public void Dispose()
        {
            if (completed) return;
            completed = true;
            foreach (var piece in pieces)
                if (!piece.thing.Destroyed && !piece.thing.Spawned)
                    GenPlace.TryPlaceThing(piece.thing, piece.cell, map, ThingPlaceMode.Near);
            pieces.Clear();
        }
    }
}

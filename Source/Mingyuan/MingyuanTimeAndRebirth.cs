using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan
{
    public class MingyuanTimeLockRecord : IExposable
    {
        public Thing thing;
        public int endTick;
        public HediffDef markerHediff;
        public bool restoreOnEnd;

        public MingyuanTimeLockRecord()
        {
        }

        public MingyuanTimeLockRecord(Thing thing, int endTick, HediffDef markerHediff, bool restoreOnEnd)
        {
            this.thing = thing;
            this.endTick = endTick;
            this.markerHediff = markerHediff;
            this.restoreOnEnd = restoreOnEnd;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref thing, "thing");
            Scribe_Values.Look(ref endTick, "endTick", 0);
            Scribe_Defs.Look(ref markerHediff, "markerHediff");
            Scribe_Values.Look(ref restoreOnEnd, "restoreOnEnd", false);
        }
    }

    public class GameComponent_MingyuanTimeLock : GameComponent
    {
        private List<MingyuanTimeLockRecord> locks = new();

        public GameComponent_MingyuanTimeLock(Game game)
        {
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            if (locks.Count == 0)
            {
                return;
            }

            GameComponent_MingyuanRebirth rebirth = Current.Game?.GetComponent<GameComponent_MingyuanRebirth>();
            for (int i = locks.Count - 1; i >= 0; i--)
            {
                MingyuanTimeLockRecord record = locks[i];
                if (record?.thing == null || record.thing.Destroyed)
                {
                    locks.RemoveAt(i);
                    continue;
                }

                Pawn pawn = record.thing as Pawn;
                if (pawn == null)
                {
                    locks.RemoveAt(i);
                    continue;
                }

                if (record.markerHediff != null)
                {
                    Hediff marker = pawn.health?.hediffSet?.GetFirstHediffOfDef(record.markerHediff);
                    if (marker != null)
                    {
                        pawn.health.RemoveHediff(marker);
                    }
                }

                int tick = Find.TickManager.TicksGame;
                if (tick < record.endTick && pawn.Spawned && pawn.Map != null && rebirth != null)
                {
                    Map map = pawn.Map;
                    IntVec3 cell = pawn.Position;
                    pawn.DeSpawn();
                    if (!pawn.IsWorldPawn())
                    {
                        Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
                    }

                    Thing marker = MingyuanRebirthUtility.SpawnRebirthMarker(map, cell);
                    rebirth.RegisterPendingRebirth(pawn, map, cell, record.endTick, marker);
                }
                else if (record.restoreOnEnd)
                {
                    MingyuanUtility.RestorePawnToBestCondition(pawn, false);
                }

                locks.RemoveAt(i);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref locks, "mingyuanTimeLocks", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                locks ??= new();
            }
        }
    }

    public static class MingyuanRebirthUtility
    {
        public const int EternalBurningTicks = 1800;

        public static bool TryScheduleRebirth(Pawn pawn)
        {
            if (pawn == null || pawn.Discarded || !pawn.Dead || !MingyuanUtility.IsMingyuan(pawn))
            {
                return false;
            }

            GameComponent_MingyuanRebirth component = Current.Game?.GetComponent<GameComponent_MingyuanRebirth>();
            if (component == null || component.IsPending(pawn))
            {
                return false;
            }

            Map map = pawn.Corpse?.MapHeld ?? pawn.MapHeld;
            IntVec3 cell = pawn.Corpse?.PositionHeld ?? pawn.PositionHeld;
            if (map == null || !cell.IsValid)
            {
                return false;
            }

            DoRebirthExplosion(pawn, map, cell);
            PreparePawnForPendingRebirth(pawn);
            Thing marker = SpawnRebirthMarker(map, cell);
            component.RegisterPendingRebirth(pawn, map, cell, Find.TickManager.TicksGame + EternalBurningTicks, marker);
            return true;
        }

        private static void DoRebirthExplosion(Pawn pawn, Map map, IntVec3 cell)
        {
            GenExplosion.DoExplosion(cell, map, 8f, DamageDefOf.Bomb, pawn, 999, 999f);

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(cell, map, 8f, true))
            {
                Pawn target = thing as Pawn;
                if (target != null && target != pawn && !target.Dead && target.HostileTo(pawn))
                {
                    target.stances?.stunner?.StunFor(600, pawn, false, true, false);
                }
            }
        }

        public static Thing SpawnRebirthMarker(Map map, IntVec3 cell)
        {
            ThingDef markerDef = MX_MingyuanDefOf.MX_Mingyuan_RebirthMarker;
            if (map == null || markerDef == null || !cell.IsValid || !cell.InBounds(map))
            {
                return null;
            }

            return GenSpawn.Spawn(markerDef, cell, map, WipeMode.Vanish);
        }

        private static void PreparePawnForPendingRebirth(Pawn pawn)
        {
            Corpse corpse = pawn.Corpse;
            if (corpse != null)
            {
                corpse.InnerPawn = null;
                corpse.Destroy(DestroyMode.Vanish);
            }

            if (!pawn.Spawned && !pawn.IsWorldPawn())
            {
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            }
        }

        public static bool TryFinishRebirth(Pawn pawn, Map map, IntVec3 cell)
        {
            if (pawn == null || map == null)
            {
                return false;
            }

            if (pawn.Dead)
            {
                bool resurrected = ResurrectionUtility.TryResurrect(pawn, new ResurrectionParams
                {
                    gettingScarsChance = 0f,
                    canKidnap = false,
                    canTimeoutOrFlee = false,
                    sappers = false,
                    useAvoidGridSmart = true,
                    canSteal = false,
                    breachers = false,
                    canPickUpOpportunisticWeapons = false,
                    restoreMissingParts = true,
                    noLord = true,
                    dontSpawn = true,
                    invisibleStun = true,
                    removeDiedThoughts = false
                });
                if (!resurrected)
                {
                    return false;
                }
            }

            if (pawn.IsWorldPawn())
            {
                Find.WorldPawns.RemovePawn(pawn);
            }

            IntVec3 spawnCell = MingyuanUtility.FindStandableCellNear(cell, map, 5);
            if (!pawn.Spawned)
            {
                GenSpawn.Spawn(pawn, spawnCell, map);
            }

            MingyuanUtility.EnsureHediff(pawn, MingyuanUtility.BurningBodyDef);
            MingyuanUtility.EnsureHediff(pawn, MingyuanUtility.ShieldDef);
            MingyuanUtility.EnsureHediff(pawn, MingyuanUtility.RebirthDef);
            MingyuanUtility.RestorePawnToBestCondition(pawn, false);
            return true;
        }
    }

    public class MingyuanPendingRebirth : IExposable
    {
        public Pawn pawn;
        public Map map;
        public IntVec3 cell;
        public int rebirthTick;
        public Thing marker;

        public MingyuanPendingRebirth()
        {
        }

        public MingyuanPendingRebirth(Pawn pawn, Map map, IntVec3 cell, int rebirthTick, Thing marker)
        {
            this.pawn = pawn;
            this.map = map;
            this.cell = cell;
            this.rebirthTick = rebirthTick;
            this.marker = marker;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref rebirthTick, "rebirthTick", 0);
            Scribe_References.Look(ref marker, "marker");
        }
    }

    public class GameComponent_MingyuanRebirth : GameComponent
    {
        private List<MingyuanPendingRebirth> pendingRebirths = new();
        private int nextProcessTick = int.MaxValue;

        public GameComponent_MingyuanRebirth(Game game)
        {
        }

        public bool IsPending(Pawn pawn)
        {
            for (int i = 0; i < pendingRebirths.Count; i++)
            {
                if (pendingRebirths[i]?.pawn == pawn)
                {
                    return true;
                }
            }

            return false;
        }

        public void RegisterPendingRebirth(Pawn pawn, Map map, IntVec3 cell, int rebirthTick, Thing marker)
        {
            if (pawn == null || map == null)
            {
                return;
            }

            pendingRebirths.Add(new MingyuanPendingRebirth(pawn, map, cell, rebirthTick, marker));
            nextProcessTick = Mathf.Min(nextProcessTick, rebirthTick);
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Current.ProgramState != ProgramState.Playing || pendingRebirths.Count == 0)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;
            if (tick < nextProcessTick)
            {
                return;
            }

            nextProcessTick = int.MaxValue;
            for (int i = pendingRebirths.Count - 1; i >= 0; i--)
            {
                MingyuanPendingRebirth pending = pendingRebirths[i];
                if (pending?.pawn == null || pending.pawn.Destroyed)
                {
                    DestroyMarker(pending?.marker);
                    pendingRebirths.RemoveAt(i);
                    continue;
                }

                if (tick < pending.rebirthTick)
                {
                    nextProcessTick = Mathf.Min(nextProcessTick, pending.rebirthTick);
                    continue;
                }

                if (MingyuanRebirthUtility.TryFinishRebirth(pending.pawn, pending.map, pending.cell))
                {
                    DestroyMarker(pending.marker);
                    pendingRebirths.RemoveAt(i);
                }
                else
                {
                    pending.rebirthTick = tick + 60;
                    nextProcessTick = Mathf.Min(nextProcessTick, pending.rebirthTick);
                }
            }
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            int tick = Find.TickManager.TicksGame;
            for (int i = 0; i < pendingRebirths.Count; i++)
            {
                MingyuanPendingRebirth pending = pendingRebirths[i];
                if (pending?.pawn == null || pending.map == null || !pending.cell.IsValid)
                {
                    continue;
                }

                bool missingMarker = pending.marker == null || pending.marker.Destroyed;
                if (!missingMarker)
                {
                    continue;
                }

                pending.marker = MingyuanRebirthUtility.SpawnRebirthMarker(pending.map, pending.cell);
                if (pending.pawn.Dead && pending.rebirthTick <= tick + 1)
                {
                    pending.rebirthTick = tick + MingyuanRebirthUtility.EternalBurningTicks;
                }
            }

            RecalculateNextProcessTick();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pendingRebirths, "mingyuanPendingRebirths", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pendingRebirths ??= new();
            }

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                RecalculateNextProcessTick();
            }
        }

        private void RecalculateNextProcessTick()
        {
            nextProcessTick = int.MaxValue;
            for (int index = 0; index < pendingRebirths.Count; index++)
            {
                MingyuanPendingRebirth pending = pendingRebirths[index];
                if (pending != null)
                {
                    nextProcessTick = Mathf.Min(nextProcessTick, pending.rebirthTick);
                }
            }
        }

        private static void DestroyMarker(Thing marker)
        {
            if (marker != null && !marker.Destroyed)
            {
                marker.Destroy(DestroyMode.Vanish);
            }
        }
    }
}

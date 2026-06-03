using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan
{
    public static class MingyuanTimeLockUtility
    {
        public static bool IsLocked(Thing thing)
        {
            return Current.Game?.GetComponent<GameComponent_MingyuanTimeLock>()?.IsLocked(thing) ?? false;
        }

        public static bool IsEternalBurning(Pawn pawn)
        {
            return MingyuanUtility.HasHediff(pawn, MingyuanUtility.EternalBurningDef);
        }

        public static void RegisterLock(Thing thing, int durationTicks, HediffDef markerHediff, bool restoreOnEnd)
        {
            if (thing == null || durationTicks <= 0)
            {
                return;
            }

            Current.Game?.GetComponent<GameComponent_MingyuanTimeLock>()?.Register(thing, Find.TickManager.TicksGame + durationTicks, markerHediff, restoreOnEnd);
        }
    }

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
        private List<MingyuanTimeLockRecord> locks = new List<MingyuanTimeLockRecord>();

        public GameComponent_MingyuanTimeLock(Game game)
        {
        }

        public bool IsLocked(Thing thing)
        {
            if (thing == null)
            {
                return false;
            }

            int tick = Find.TickManager.TicksGame;
            for (int i = 0; i < locks.Count; i++)
            {
                MingyuanTimeLockRecord record = locks[i];
                if (record?.thing == thing && tick < record.endTick)
                {
                    return true;
                }
            }

            return false;
        }

        public void Register(Thing thing, int endTick, HediffDef markerHediff, bool restoreOnEnd)
        {
            if (thing == null)
            {
                return;
            }

            for (int i = 0; i < locks.Count; i++)
            {
                MingyuanTimeLockRecord record = locks[i];
                if (record?.thing == thing)
                {
                    record.endTick = Mathf.Max(record.endTick, endTick);
                    record.markerHediff = markerHediff ?? record.markerHediff;
                    record.restoreOnEnd = record.restoreOnEnd || restoreOnEnd;
                    return;
                }
            }

            locks.Add(new MingyuanTimeLockRecord(thing, endTick, markerHediff, restoreOnEnd));
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Current.ProgramState != ProgramState.Playing || locks.Count == 0)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;
            for (int i = locks.Count - 1; i >= 0; i--)
            {
                MingyuanTimeLockRecord record = locks[i];
                if (record?.thing == null || record.thing.Destroyed)
                {
                    locks.RemoveAt(i);
                    continue;
                }

                if (tick < record.endTick)
                {
                    continue;
                }

                Pawn pawn = record.thing as Pawn;
                if (pawn != null)
                {
                    if (record.restoreOnEnd)
                    {
                        MingyuanUtility.RestorePawnToBestCondition(pawn, false);
                    }

                    if (record.markerHediff != null)
                    {
                        Hediff hediff = pawn.health?.hediffSet?.GetFirstHediffOfDef(record.markerHediff);
                        if (hediff != null)
                        {
                            pawn.health.RemoveHediff(hediff);
                        }
                    }
                }

                locks.RemoveAt(i);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref locks, "mingyuanTimeLocks", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && locks == null)
            {
                locks = new List<MingyuanTimeLockRecord>();
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
            component.RegisterPendingRebirth(pawn, map, cell, Find.TickManager.TicksGame + 1);
            return true;
        }

        private static void DoRebirthExplosion(Pawn pawn, Map map, IntVec3 cell)
        {
            for (int i = 0; i < 5; i++)
            {
                GenExplosion.DoExplosion(cell, map, 8f, DamageDefOf.Bomb, pawn, 999, 999f);
            }

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(cell, map, 8f, true))
            {
                Pawn target = thing as Pawn;
                if (target != null && target != pawn && !target.Dead && target.HostileTo(pawn))
                {
                    target.stances?.stunner?.StunFor(600, pawn, false, true, false);
                }
            }
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
            MingyuanUtility.EnsureHediff(pawn, MingyuanUtility.EternalBurningDef);
            MingyuanUtility.RestorePawnToBestCondition(pawn, false);
            MingyuanTimeLockUtility.RegisterLock(pawn, EternalBurningTicks, MingyuanUtility.EternalBurningDef, true);
            return true;
        }
    }

    public class MingyuanPendingRebirth : IExposable
    {
        public Pawn pawn;
        public Map map;
        public IntVec3 cell;
        public int rebirthTick;

        public MingyuanPendingRebirth()
        {
        }

        public MingyuanPendingRebirth(Pawn pawn, Map map, IntVec3 cell, int rebirthTick)
        {
            this.pawn = pawn;
            this.map = map;
            this.cell = cell;
            this.rebirthTick = rebirthTick;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref cell, "cell");
            Scribe_Values.Look(ref rebirthTick, "rebirthTick", 0);
        }
    }

    public class GameComponent_MingyuanRebirth : GameComponent
    {
        private List<MingyuanPendingRebirth> pendingRebirths = new List<MingyuanPendingRebirth>();

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

        public void RegisterPendingRebirth(Pawn pawn, Map map, IntVec3 cell, int rebirthTick)
        {
            if (pawn == null || map == null)
            {
                return;
            }

            pendingRebirths.Add(new MingyuanPendingRebirth(pawn, map, cell, rebirthTick));
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Current.ProgramState != ProgramState.Playing || pendingRebirths.Count == 0)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;
            for (int i = pendingRebirths.Count - 1; i >= 0; i--)
            {
                MingyuanPendingRebirth pending = pendingRebirths[i];
                if (pending?.pawn == null || pending.pawn.Destroyed)
                {
                    pendingRebirths.RemoveAt(i);
                    continue;
                }

                if (tick < pending.rebirthTick)
                {
                    continue;
                }

                if (MingyuanRebirthUtility.TryFinishRebirth(pending.pawn, pending.map, pending.cell))
                {
                    pendingRebirths.RemoveAt(i);
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pendingRebirths, "mingyuanPendingRebirths", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && pendingRebirths == null)
            {
                pendingRebirths = new List<MingyuanPendingRebirth>();
            }
        }
    }
}

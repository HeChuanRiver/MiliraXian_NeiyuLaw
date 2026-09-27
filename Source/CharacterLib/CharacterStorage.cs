using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using RimWorld.Planet;
using Verse;

namespace MiliraXian.CharacterLib
{
    // Always owns our stored pawns, including those displayed in SCM. Removing the
    // optional adapter therefore cannot remove the only serialized copy of a pawn.
    public class GameComponent_CharacterStorage : GameComponent, IThingHolder
    {
        private ThingOwner<Pawn> pawns;
        private Dictionary<string, int> readyTicks = new();
        private int nextReturnTick = int.MaxValue;
        public IThingHolder ParentHolder => null;
        public int Count => pawns.Count;

        public GameComponent_CharacterStorage(Game game) { pawns = new ThingOwner<Pawn>(this); }
        public ThingOwner GetDirectlyHeldThings() => pawns;
        public void GetChildHolders(List<IThingHolder> children) => ThingOwnerUtility.AppendThingHoldersFromThings(children, GetDirectlyHeldThings());
        public bool Contains(Pawn pawn) => pawn != null && pawns.Contains(pawn);
        public Pawn Find(string id)
        {
            for (int i = 0; i < pawns.Count; i++) if (pawns[i].ThingID == id) return pawns[i];
            return null;
        }
        public void AppendTo(List<Pawn> list)
        {
            for (int i = 0; i < pawns.Count; i++) if (!list.Contains(pawns[i])) list.Add(pawns[i]);
        }

        public bool Store(Pawn pawn)
        {
            if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Discarded) return false;
            if (Contains(pawn)) return true;
            // Same SCM rule: do not detach a caravan member behind the caravan's back.
            if (pawn.GetCaravan() != null) return false;
            Map map = pawn.Map;
            IntVec3 cell = pawn.Position;
            bool worldPawn = pawn.IsWorldPawn();
            if (pawn.Spawned) pawn.DeSpawn();
            if (worldPawn) Verse.Find.WorldPawns.RemovePawn(pawn);
            if (!pawns.TryAddOrTransfer(pawn))
            {
                if (map != null && !pawn.Spawned && pawn.holdingOwner == null) GenSpawn.Spawn(pawn, cell, map);
                else if (worldPawn) KeepInWorld(pawn);
                return false;
            }
            SetCooldown(pawn.ThingID, 0);
            return true;
        }

        public Pawn Take(string id)
        {
            Pawn pawn = Find(id);
            if (pawn == null) return null;
            Pawn result = pawns.Take(pawn);
            readyTicks.Remove(id);
            return result;
        }
        public void NotifySpawned(Pawn pawn)
        {
            if (pawn?.Spawned != true) return;
            if (Contains(pawn)) pawns.Remove(pawn);
            readyTicks.Remove(pawn.ThingID);
        }
        public void SetCooldown(string id, int duration)
        {
            if (Find(id) == null) return;
            int tick = Verse.Find.TickManager.TicksGame + System.Math.Max(0, duration);
            readyTicks[id] = tick;
            if (tick < nextReturnTick) nextReturnTick = tick;
        }
        public int CooldownRemaining(string id) => readyTicks.TryGetValue(id, out int tick) ? System.Math.Max(0, tick - Verse.Find.TickManager.TicksGame) : 0;

        public override void GameComponentTick()
        {
            if (pawns.Count == 0 || (CharacterServices.Scm != null && (CharacterServices.ScmEnabled?.Invoke() ?? true))) return;
            int now = Verse.Find.TickManager.TicksGame;
            if (now < nextReturnTick) return;
            nextReturnTick = now + 600;
            Map map = Verse.Find.AnyPlayerHomeMap;
            if (map == null) return;
            for (int i = pawns.Count - 1; i >= 0; i--)
            {
                Pawn pawn = pawns[i];
                if (CooldownRemaining(pawn.ThingID) > 0) continue;
                if (!CellFinder.TryFindRandomEdgeCellWith(c => c.Standable(map) && !c.Fogged(map), map, CellFinder.EdgeRoadChance_Neutral, out IntVec3 cell)) continue;
                // GenSpawn transfers from the owning container; retain ownership until it succeeds.
                GenSpawn.Spawn(pawn, cell, map);
                NotifySpawned(pawn);
                Messages.Message(pawn.LabelShortCap + " 已恢复并返回殖民地。", pawn, MessageTypeDefOf.PositiveEvent);
            }
        }

        public override void ExposeData()
        {
            Scribe_Deep.Look(ref pawns, "mxStoredCharacters", this);
            Scribe_Collections.Look(ref readyTicks, "mxStoredCharacterReadyTicks", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pawns ??= new ThingOwner<Pawn>(this);
                readyTicks ??= new();
                nextReturnTick = 0;
            }
        }
        internal static void KeepInWorld(Pawn pawn)
        {
            Faction faction = pawn.Faction;
            PawnKindDef kind = pawn.kindDef;
            Verse.Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            if (pawn.Faction != faction) pawn.SetFaction(faction);
            if (pawn.kindDef != kind) pawn.ChangeKind(kind);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class Patch_CharacterDeathRecovery
    {
        private static readonly HashSet<Pawn> recovering = new();
        [HarmonyPriority(Priority.High)]
        private static bool Prefix(Pawn __instance)
        {
            Pawn pawn = __instance;
            if (pawn == null || pawn.Dead || pawn.Destroyed || pawn.Discarded) return true;
            if (recovering.Contains(pawn)) return false;
            var rule = CharacterProtection.For(pawn);
            if (rule?.recoverOnDeath != true || pawn.Faction != Faction.OfPlayer) return true;
            recovering.Add(pawn);
            try
            {
                // Gameplay-specific rebirth has first choice, without an AL registration check.
                if (CharacterServices.InterceptDeath?.Invoke(pawn) == true) return false;
                var hediffs = pawn.health?.hediffSet?.hediffs;
                if (hediffs == null) return true;
                for (int i = hediffs.Count - 1; i >= 0; i--)
                    if (i < hediffs.Count && hediffs[i].def.isBad) pawn.health.RemoveHediff(hediffs[i]);
                pawn.mindState?.mentalStateHandler?.Reset();
                pawn.health.hediffSet.DirtyCache();
                CharacterServices.Registry?.Register(pawn, synchronizeScm: false);
                bool scm = CharacterServices.ScmEnabled?.Invoke() ?? true;
                if (scm && CharacterServices.Scm?.StoreRecovered(pawn, rule.recoveryTicks) == true) return false;
                if (CharacterServices.Storage?.Store(pawn) == true)
                    CharacterServices.Storage.SetCooldown(pawn.ThingID, rule.recoveryTicks);
                // If a holder prevents transfer, retain the healed, living pawn in that holder.
                return false;
            }
            finally { recovering.Remove(pawn); }
        }
    }
}

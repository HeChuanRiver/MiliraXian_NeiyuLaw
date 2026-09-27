using AriandelLibrary;
using MiliraXian.CharacterLib;
using Verse;
using HarmonyLib;
using System.Collections.Generic;

namespace MiliraXian.ScmCompat
{
    [StaticConstructorOnStartup]
    internal static class ScmBootstrap
    {
        static ScmBootstrap()
        {
            CharacterServices.Scm = new ScmIntegration();
            new Harmony("MiliraXian.ScmCompat").PatchAll(typeof(ScmBootstrap).Assembly);
        }
    }

    internal sealed class ScmIntegration : ICharacterScm
    {
        private static SpecialPawnManager Manager => AriandelLibrary_GameComponent.Instance?.SpecialPawns;
        public string GetRegisteredId(string characterId)
        {
            SpecialPawnManager manager = Manager;
            if (manager?.GetRealID(characterId) == null) return null;
            Pawn pawn = manager.GetPawnByStaticID(characterId);
            return pawn != null && !pawn.Discarded && CharacterServices.IdFor(pawn) == characterId ? pawn.ThingID : null;
        }

        public bool Register(string characterId, Pawn pawn)
        {
            SpecialPawnManager manager = Manager;
            if (manager == null) return false;
            string existing = manager.GetRealID(characterId);
            if (existing != null && existing != pawn.ThingID)
            {
                // Repair only an orphan mapping, never replace a live/stored character.
                if (!manager.CanClearStaticID(characterId) || manager.GetPawnByStaticID(characterId) != null
                    || !manager.TryClearStaticID(characterId, out _)) return false;
            }
            manager.RegisterSpecialPawn(characterId, pawn);
            return manager.GetRealID(characterId) == pawn.ThingID;
        }

        public bool StoreRecovered(Pawn pawn, int cooldownTicks)
        {
            string id = CharacterServices.IdFor(pawn);
            if (id == null || !Register(id, pawn) || !Manager.SendPawnToVoid(pawn)) return false;
            Manager.SetCooldown(pawn.ThingID, cooldownTicks);
            return true;
        }
        public void SetUnavailable(Pawn pawn, bool unavailable, string reason)
        {
            if (Manager == null || pawn == null) return;
            if (unavailable) Manager.TryAcquireExternalActivityLock(pawn, "MiliraXian.CharacterLib", reason, out _);
            else Manager.TryReleaseExternalActivityLock(pawn, "MiliraXian.CharacterLib");
        }
    }

    public class GameComponent_ScmMigration : GameComponent
    {
        public GameComponent_ScmMigration(Game game) { }
        public override void LoadedGame()
        {
            var library = AriandelLibrary_GameComponent.Instance;
            var storage = CharacterServices.Storage;
            if (library == null || storage == null) return;
            foreach (Pawn pawn in library.SpecialPawns.GetAllStoredPawns())
            {
                if (CharacterServices.IdFor(pawn) == null) continue;
                int remaining = library.SpecialPawns.GetCooldownRemaining(pawn.ThingID);
                if (storage.Contains(pawn))
                {
                    // AL may have been disabled and enabled again since the last save.
                    remaining = System.Math.Max(remaining, storage.CooldownRemaining(pawn.ThingID));
                    if (CharacterServices.Registry?.Register(pawn, synchronizeScm: true) == true)
                        library.SpecialPawns.SetCooldown(pawn.ThingID, remaining);
                    continue;
                }
                // Transfer the existing object and ThingID; never generate or clone a replacement.
                if (storage.Store(pawn))
                {
                    library.VirtualPawns.NotifyPawnRemoved(pawn);
                    storage.SetCooldown(pawn.ThingID, remaining);
                    CharacterServices.Registry?.Register(pawn, synchronizeScm: false);
                }
            }
        }
    }

    // SCM has no storage-provider API. These narrow patches redirect only our four
    // characters; all other AL pawns retain their original owner and behavior.
    [HarmonyPatch(typeof(VirtualPawnContainer), nameof(VirtualPawnContainer.StorePawn))]
    internal static class Patch_ScmStore
    {
        private static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (CharacterServices.IdFor(pawn) == null) return true;
            __result = CharacterServices.Storage?.Store(pawn) == true;
            return false;
        }
    }
    [HarmonyPatch(typeof(VirtualPawnContainer), nameof(VirtualPawnContainer.GetStoredPawnByID))]
    internal static class Patch_ScmFind
    {
        private static void Postfix(string pawnID, ref Pawn __result) => __result ??= CharacterServices.Storage?.Find(pawnID);
    }
    [HarmonyPatch(typeof(VirtualPawnContainer), nameof(VirtualPawnContainer.GetAllStoredPawns))]
    internal static class Patch_ScmList
    {
        private static void Postfix(List<Pawn> __result) => CharacterServices.Storage?.AppendTo(__result);
    }
    [HarmonyPatch(typeof(VirtualPawnContainer), nameof(VirtualPawnContainer.Count), MethodType.Getter)]
    internal static class Patch_ScmCount
    {
        private static void Postfix(ref int __result) => __result += CharacterServices.Storage?.Count ?? 0;
    }
    [HarmonyPatch(typeof(VirtualPawnContainer), nameof(VirtualPawnContainer.Contains))]
    internal static class Patch_ScmContains
    {
        private static void Postfix(Pawn pawn, ref bool __result) => __result |= CharacterServices.Storage?.Contains(pawn) == true;
    }
    [HarmonyPatch(typeof(VirtualPawnContainer), nameof(VirtualPawnContainer.TryTakeStoredPawn))]
    internal static class Patch_ScmTake
    {
        private static bool Prefix(string pawnID, ref Pawn pawn, ref bool __result)
        {
            var storage = CharacterServices.Storage;
            if (storage?.Find(pawnID) == null) return true;
            pawn = storage.Take(pawnID);
            __result = pawn != null;
            return false;
        }
    }
    [HarmonyPatch(typeof(VirtualPawnContainer), nameof(VirtualPawnContainer.NotifyPawnRemoved))]
    internal static class Patch_ScmRemoved
    {
        private static void Postfix(Pawn pawn) => CharacterServices.Storage?.NotifySpawned(pawn);
    }
    [HarmonyPatch(typeof(SpecialPawnManager), nameof(SpecialPawnManager.SetCooldown))]
    internal static class Patch_ScmCooldown
    {
        private static void Postfix(string pawnID, int durationTicks) => CharacterServices.Storage?.SetCooldown(pawnID, durationTicks);
    }
}

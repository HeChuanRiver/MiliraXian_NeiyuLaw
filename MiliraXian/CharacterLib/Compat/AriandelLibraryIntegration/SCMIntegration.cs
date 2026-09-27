using AriandelLibrary;
using Verse;

namespace MiliraXian.CharacterLib.Compat.AriandelLibraryIntegration
{
    [StaticConstructorOnStartup]
    internal static class SCMBootstrap
    {
        static SCMBootstrap() => CharacterSCM.Registry = new SCMIntegration();
    }

    internal sealed class SCMIntegration : ICharacterSCMRegistry
    {
        private const string LockOwner = "MiliraXian.CharacterLib";

        private static SpecialPawnManager Manager => AriandelLibrary_GameComponent.Instance?.SpecialPawns;

        public string GetRegisteredPawnId(string characterId)
        {
            SpecialPawnManager manager = Manager;
            if (manager?.GetRealID(characterId) == null) return null;
            Pawn pawn = manager.GetPawnByStaticID(characterId);
            return pawn != null && !pawn.Discarded && CharacterSCM.CharacterIdFor(pawn) == characterId ? pawn.ThingID : null;
        }

        public bool Register(string characterId, Pawn pawn)
        {
            SpecialPawnManager manager = Manager;
            if (manager == null) return false;
            string existing = manager.GetRealID(characterId);
            if (existing != null && existing != pawn.ThingID)
            {
                // RegisterSpecialPawn is first-write-wins, so rebinding requires clearing first.
                // Repair only an orphan mapping, never replace a live/stored character.
                if (!manager.CanClearStaticID(characterId) || manager.GetPawnByStaticID(characterId) != null
                    || !manager.TryClearStaticID(characterId, out _)) return false;
            }
            manager.RegisterSpecialPawn(characterId, pawn);
            return manager.GetRealID(characterId) == pawn.ThingID;
        }

        public void SetUnavailable(Pawn pawn, bool unavailable, string reason)
        {
            SpecialPawnManager manager = Manager;
            if (manager == null || pawn == null) return;
            if (unavailable) manager.TryAcquireExternalActivityLock(pawn, LockOwner, reason, out _);
            else manager.TryReleaseExternalActivityLock(pawn, LockOwner);
        }
    }
}

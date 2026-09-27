using AriandelLibrary;
using MiliraXian.CharacterLib;
using Verse;

namespace MiliraXian.ScmCompat
{
    [StaticConstructorOnStartup]
    internal static class ScmBootstrap
    {
        static ScmBootstrap() { CharacterServices.Scm = new ScmIntegration(); }
    }

    internal sealed class ScmIntegration : ICharacterScm
    {
        private static SpecialPawnManager Manager => AriandelLibrary_GameComponent.Instance?.SpecialPawns;
        public string GetRegisteredId(string characterId) => Manager?.GetRealID(characterId);

        public bool Register(string characterId, Pawn pawn)
        {
            SpecialPawnManager manager = Manager;
            if (manager == null) return false;
            string existing = manager.GetRealID(characterId);
            if (existing != null && existing != pawn.ThingID) return false;
            manager.RegisterSpecialPawn(characterId, pawn);
            return manager.GetRealID(characterId) == pawn.ThingID;
        }
    }
}

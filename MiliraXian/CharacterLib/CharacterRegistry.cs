using System.Collections.Generic;
using System;
using RimWorld;
using Verse;

namespace MiliraXian.CharacterLib
{
    // Identity belongs to this save. SCM is an optional consumer of the binding.
    public class CharacterRegistrationExtension : DefModExtension
    {
        public string characterId;
    }

    public interface ICharacterScm
    {
        string GetRegisteredId(string characterId);
        bool Register(string characterId, Pawn pawn);
        bool StoreRecovered(Pawn pawn, int cooldownTicks);
        void SetUnavailable(Pawn pawn, bool unavailable, string reason);
    }

    public static class CharacterServices
    {
        public static ICharacterScm Scm { get; set; }
        public static Func<Pawn, bool> InterceptDeath { get; set; }
        public static Func<bool> ScmEnabled { get; set; }
        public static string IdFor(Pawn pawn) => pawn?.kindDef?.GetModExtension<CharacterRegistrationExtension>()?.characterId;
        public static GameComponent_CharacterRegistry Registry => Current.Game?.GetComponent<GameComponent_CharacterRegistry>();
        public static GameComponent_CharacterStorage Storage => Current.Game?.GetComponent<GameComponent_CharacterStorage>();
    }

    public class GameComponent_CharacterRegistry : GameComponent
    {
        private Dictionary<string, string> bindings = new();
        private readonly HashSet<int> warnedDuplicates = new();

        public GameComponent_CharacterRegistry(Game game) { }

        public string GetPawnId(string characterId) => characterId != null && bindings.TryGetValue(characterId, out var id) ? id : null;

        public bool IsBound(Pawn pawn)
        {
            string id = CharacterServices.IdFor(pawn);
            return id != null && GetPawnId(id) == pawn.ThingID;
        }

        public bool Register(Pawn pawn, bool synchronizeScm)
        {
            string id = CharacterServices.IdFor(pawn);
            if (string.IsNullOrEmpty(id) || pawn.Dead || pawn.Destroyed || pawn.Discarded || pawn.Faction != Faction.OfPlayer)
                return false;

            if (!bindings.TryGetValue(id, out string boundId))
            {
                // Import old saves once, even when the original pawn is currently in SCM.
                boundId = CharacterServices.Scm?.GetRegisteredId(id) ?? pawn.ThingID;
                bindings.Add(id, boundId);
            }
            if (boundId != pawn.ThingID)
            {
                if (warnedDuplicates.Add(pawn.thingIDNumber))
                    Log.Warning("[MiliraXian.CharacterLib] Identity already bound: " + id + " -> " + boundId + "; ignored " + pawn.ThingID);
                return false;
            }
            if (synchronizeScm) CharacterServices.Scm?.Register(id, pawn);
            return true;
        }

        public override void ExposeData()
        {
            Scribe_Collections.Look(ref bindings, "mxCharacterBindings", LookMode.Value, LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit) bindings ??= new();
        }
    }
}

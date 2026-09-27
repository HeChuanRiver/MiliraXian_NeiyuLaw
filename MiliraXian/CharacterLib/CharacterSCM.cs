using Verse;

namespace MiliraXian.CharacterLib
{
    // Identity belongs to this save. SCM is an optional consumer of the binding.
    public class CharacterRegistrationExtension : DefModExtension
    {
        public string characterId;
    }

    /// <summary>
    /// Implemented by the AriandelLibrary compatibility assembly. Registration lets the Special
    /// Character Manager discover a character; it grants SCM no control over generation, death
    /// or anything else.
    /// </summary>
    public interface ICharacterSCMRegistry
    {
        string GetRegisteredPawnId(string characterId);
        bool Register(string characterId, Pawn pawn);

        /// <summary>
        /// Marks the character as temporarily unavailable in the manager's own UI, with a reason
        /// shown to the player. Whether a character can be recalled from the manager is the
        /// manager's own concern, so the call is forwarded rather than reimplemented here.
        /// </summary>
        void SetUnavailable(Pawn pawn, bool unavailable, string reason);
    }

    public static class CharacterSCM
    {
        /// <summary>Null when AriandelLibrary is not loaded.</summary>
        public static ICharacterSCMRegistry Registry { get; set; }

        public static string CharacterIdFor(Pawn pawn) =>
            pawn?.kindDef?.GetModExtension<CharacterRegistrationExtension>()?.characterId;

        /// <summary>
        /// Registers the pawn under its kind's characterId. The caller chooses when — this
        /// library holds no state and makes no decision about it. Returns false when AL is
        /// absent, the kind has no characterId, or the id is already taken.
        /// </summary>
        public static bool TryRegister(Pawn pawn)
        {
            string id = CharacterIdFor(pawn);
            return id != null && Registry?.Register(id, pawn) == true;
        }

        /// <summary>The ThingID SCM has bound to this characterId, or null.</summary>
        public static string RegisteredPawnId(string characterId) => Registry?.GetRegisteredPawnId(characterId);

        /// <summary>No-op when AriandelLibrary is not loaded.</summary>
        public static void SetUnavailable(Pawn pawn, bool unavailable, string reason) =>
            Registry?.SetUnavailable(pawn, unavailable, reason);
    }
}

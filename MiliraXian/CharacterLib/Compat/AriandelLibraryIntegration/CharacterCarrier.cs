using HarmonyLib;
using Verse;

namespace MiliraXian.CharacterLib.Compat.AriandelLibraryIntegration
{
    /// <summary>
    /// Marks a PawnKindDef that exists only to carry display data for the Special Character
    /// Manager's UI. A carrier has no apparel, backstory, gender or head type of its own, so
    /// generating one directly can only produce a malformed pawn. Since a carrier stands for a
    /// real character, any generation request is redirected to that character instead.
    /// </summary>
    public class CharacterCarrierExtension : DefModExtension
    {
        /// <summary>The character this carrier represents. Generated in the carrier's place.</summary>
        public PawnKindDef representedKind;
    }

    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), typeof(PawnGenerationRequest))]
    internal static class Patch_CarrierGenerationGuard
    {
        [HarmonyPriority(Priority.First)]
        private static void Prefix(ref PawnGenerationRequest request)
        {
            var carrier = request.KindDef?.GetModExtension<CharacterCarrierExtension>();
            if (carrier == null) return;

            Log.Error("[MiliraXian.CharacterLib] " + request.KindDef.defName + " is a display-only "
                + "carrier and cannot be generated; generating "
                + (carrier.representedKind?.defName ?? "nothing, request left unchanged")
                + " instead. Some Mod is generating pawns by scanning PawnKindDefs.");

            if (carrier.representedKind == null) return;
            // Clear the getter first: it would otherwise win over KindDef further down.
            request.PawnKindDefGetter = null;
            request.KindDef = carrier.representedKind;
        }
    }
}

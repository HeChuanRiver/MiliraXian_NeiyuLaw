using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters.Common
{
    internal static class CharacterHealthUtility
    {
        internal static void ClearControlStates(Pawn pawn)
        {
            if (pawn == null) return;
            pawn.stances?.stunner?.StopStun();
            pawn.mindState?.mentalStateHandler?.Reset();
            Thing fire = pawn.GetAttachment(RimWorld.ThingDefOf.Fire);
            if (fire != null && !fire.Destroyed) fire.Destroy(DestroyMode.Vanish);
        }

        // Shared by living-pawn fatal interception. State containers, abilities and their
        // cooldowns survive; the caller decides which harmful effect must also be preserved.
        internal static void RestoreForRebirth(Pawn pawn, HediffDef preserved = null)
        {
            if (pawn?.health?.hediffSet == null || pawn.Dead) return;
            List<Hediff_MissingPart> missingParts = new(pawn.health.hediffSet.GetMissingPartsCommonAncestors());
            foreach (Hediff_MissingPart missing in missingParts)
                if (missing.Part != null) pawn.health.RestorePart(missing.Part);
            List<Hediff> toRemove = new();
            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
                if (hediff != null && hediff.def != preserved
                    && (hediff is Hediff_Injury || hediff is Hediff_MissingPart || hediff.def.isBad || hediff.def.defName == "BloodLoss"))
                    toRemove.Add(hediff);
            foreach (Hediff hediff in toRemove) pawn.health.RemoveHediff(hediff);
        }
    }
}

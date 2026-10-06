using LudeonTK;
using RimWorld;
using Verse;

namespace MiliraXian.CharacterLib
{
    internal static class CharacterDebugActions
    {
        [DebugAction("Milira Xian - Common", "Rebuild starting hediffs from kind", false, false, false, false, false, 0, false,
            actionType = DebugActionType.ToolMapForPawns, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RebuildStartingHediffs(Pawn pawn)
        {
            if (CharacterSCM.CharacterIdFor(pawn) == null)
            {
                Messages.Message(pawn.LabelShortCap + " has no characterId, skipped.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (pawn.kindDef.startingHediffs.NullOrEmpty())
            {
                Messages.Message(pawn.kindDef.defName + " declares no startingHediffs.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            int before = pawn.health.hediffSet.hediffs.Count;
            HealthUtility.AddStartingHediffs(pawn, pawn.kindDef.startingHediffs);
            Messages.Message(pawn.LabelShortCap + ": added " + (pawn.health.hediffSet.hediffs.Count - before) + " starting hediff(s).",
                MessageTypeDefOf.PositiveEvent, false);
        }
    }
}

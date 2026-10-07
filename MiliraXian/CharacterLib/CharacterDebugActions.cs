using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using RimWorld;
using Verse;

namespace MiliraXian.CharacterLib
{
    internal static class CharacterDebugActions
    {
        [DebugAction("Milira Xian - Common", "Spawn character in player faction", false, false, false, false, false, 0, false,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static List<DebugActionNode> SpawnCharacterInPlayerFaction()
        {
            List<DebugActionNode> nodes = new List<DebugActionNode>();
            foreach (PawnKindDef kind in DefDatabase<PawnKindDef>.AllDefs
                .Where(kd => !string.IsNullOrEmpty(kd.GetModExtension<CharacterRegistrationExtension>()?.characterId))
                .OrderBy(kd => kd.defName))
            {
                PawnKindDef localKind = kind;
                // The kind's label names the role a character fills, not the character; their
                // own name comes from the identity extension's nickname.
                string nickKey = localKind.GetModExtension<CharacterIdentityExtension>()?.nickNameKey;
                string name = !string.IsNullOrEmpty(nickKey) && nickKey.CanTranslate()
                    ? nickKey.Translate().ToString()
                    : localKind.LabelCap.ToString();

                nodes.Add(new DebugActionNode(name + " (" + localKind.defName + ")", DebugActionType.ToolMap)
                {
                    action = delegate
                    {
                        Pawn pawn = PawnGenerator.GeneratePawn(localKind, Faction.OfPlayer, Find.CurrentMap.Tile);
                        GenSpawn.Spawn(pawn, UI.MouseCell(), Find.CurrentMap);
                        pawn.Rotation = Rot4.South;
                    }
                });
            }
            return nodes;
        }

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

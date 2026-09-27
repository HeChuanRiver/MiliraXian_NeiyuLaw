using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.CharacterLib
{
    public class CharacterPossession
    {
        public PawnKindDef pawnKind;
        public ThingDef thingDef;
        public IntRange count = IntRange.One;
    }

    /// <summary>
    /// Gives named characters their own starting possessions. Vanilla decides possessions per
    /// pawn with no scenario-level say beyond ScenPart_NoPossessions, which is all-or-nothing
    /// for every starting pawn; this narrows that down to the characters a scenario names.
    /// </summary>
    public class ScenPart_Possessions_CL_Character : ScenPart
    {
        public List<CharacterPossession> possessions = new();

        /// <summary>
        /// Drops what vanilla rolled for these characters. Their own entries are still granted,
        /// so this removes only the random backstory/trait/global picks.
        /// </summary>
        public bool replaceGenerated;

        public override string Summary(Scenario scen) =>
            ScenSummaryList.SummaryWithList(scen, "PlayerStartsWith", ScenPart_StartingThing_Defined.PlayerStartWithIntro);

        public override IEnumerable<string> GetSummaryListEntries(string tag)
        {
            if (tag != "PlayerStartsWith") yield break;
            foreach (CharacterPossession possession in possessions)
                yield return GenLabel.ThingLabel(possession.thingDef, null, possession.count.max).CapitalizeFirst()
                    + " (" + possession.pawnKind.LabelCap + ")";
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            foreach (CharacterPossession possession in possessions)
            {
                if (possession.pawnKind == null) yield return "possession entry has no pawnKind.";
                if (possession.thingDef == null) yield return "possession entry has no thingDef.";
            }
        }

        internal void ApplyTo(Pawn pawn, List<ThingDefCount> current)
        {
            bool cleared = false;
            foreach (CharacterPossession possession in possessions)
            {
                if (possession.pawnKind != pawn.kindDef || possession.thingDef == null) continue;
                if (replaceGenerated && !cleared)
                {
                    current.Clear();
                    cleared = true;
                }
                int count = Mathf.Clamp(possession.count.RandomInRange, 1, possession.thingDef.stackLimit);
                current.Add(new ThingDefCount(possession.thingDef, count));
            }
        }
    }

    [HarmonyPatch(typeof(StartingPawnUtility), nameof(StartingPawnUtility.GeneratePossessions))]
    internal static class Patch_CharacterPossessions
    {
        // A postfix, because GeneratePossessions clears the pawn's list before filling it, and
        // Scenario.Notify_PawnGenerated fires earlier still, from inside GeneratePawn.
        private static void Postfix(Pawn pawn)
        {
            if (pawn == null || Find.Scenario == null) return;
            if (!Find.GameInitData.startingPossessions.TryGetValue(pawn, out var current)) return;
            foreach (ScenPart part in Find.Scenario.AllParts)
                (part as ScenPart_Possessions_CL_Character)?.ApplyTo(pawn, current);
        }
    }
}

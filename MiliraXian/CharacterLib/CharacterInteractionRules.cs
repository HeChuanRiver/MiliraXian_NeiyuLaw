using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraXian.CharacterLib
{
    [HarmonyPatch(typeof(FloatMenuOptionProvider_RescuePawn), "GetSingleOptionFor")]
    internal static class Patch_CharacterRescue
    {
        private static bool Prefix(Pawn clickedPawn, ref FloatMenuOption __result)
        {
            if (CharacterProtection.For(clickedPawn)?.blockRescue != true || clickedPawn.Faction == Faction.OfPlayer) return true;
            __result = null;
            return false;
        }
    }
    [HarmonyPatch(typeof(FloatMenuOptionProvider_CapturePawn), "GetSingleOptionFor")]
    internal static class Patch_CharacterCapture
    {
        private static bool Prefix(Pawn clickedPawn, ref FloatMenuOption __result)
        {
            if (CharacterProtection.For(clickedPawn)?.blockCapture != true) return true;
            __result = null;
            return false;
        }
    }
    [HarmonyPatch(typeof(FloatMenuOptionProvider_Arrest), "GetSingleOptionFor")]
    internal static class Patch_CharacterArrest
    {
        private static bool Prefix(Pawn clickedPawn, ref FloatMenuOption __result)
        {
            if (CharacterProtection.For(clickedPawn)?.blockArrest != true) return true;
            __result = null;
            return false;
        }
    }
    [HarmonyPatch(typeof(Building_SubcoreScanner), nameof(Building_SubcoreScanner.CanAcceptPawn))]
    internal static class Patch_CharacterScanner
    {
        private static bool Prefix(Pawn selPawn, ref AcceptanceReport __result)
        {
            if (CharacterProtection.For(selPawn)?.blockScanner != true) return true;
            __result = "无法扫描此角色的脑组织。";
            return false;
        }
    }
    [HarmonyPatch(typeof(Recipe_InstallArtificialBodyPart), nameof(Recipe_InstallArtificialBodyPart.GetPartsToApplyOn))]
    internal static class Patch_CharacterLowTechSurgery
    {
        private static void Postfix(Pawn pawn, RecipeDef recipe, ref IEnumerable<BodyPartRecord> __result)
        {
            if (CharacterProtection.For(pawn)?.blockLowTech != true || recipe?.addsHediff == null) return;
            string name = recipe.defName;
            if (recipe.addsHediff.addedPartProps?.betterThanNatural == false || name == "InstallPegLeg"
                || name == "InstallWoodenHand" || name == "InstallWoodenFoot" || name == "InstallDenture")
                __result = Array.Empty<BodyPartRecord>();
        }
    }
    [HarmonyPatch(typeof(Recipe_RemoveBodyPart), nameof(Recipe_RemoveBodyPart.GetPartsToApplyOn))]
    internal static class Patch_CharacterHarvest
    {
        private static void Postfix(Pawn pawn, ref IEnumerable<BodyPartRecord> __result)
        {
            if (CharacterProtection.For(pawn)?.blockHarvest == true) __result = Filter(__result, pawn);
        }
        private static IEnumerable<BodyPartRecord> Filter(IEnumerable<BodyPartRecord> parts, Pawn pawn)
        {
            foreach (BodyPartRecord part in parts)
                if (!MedicalRecipesUtility.IsClean(pawn, part) || (!MedicalRecipesUtility.IsCleanAndDroppable(pawn, part) && !part.def.forceAlwaysRemovable))
                    yield return part;
        }
    }
}

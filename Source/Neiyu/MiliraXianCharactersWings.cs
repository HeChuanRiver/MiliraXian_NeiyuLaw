using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu
{
    [StaticConstructorOnStartup]
    public static class MiliraXianCharactersWingsBootstrap
    {
        static MiliraXianCharactersWingsBootstrap()
        {
            var harmony = new Harmony("HeChuanRiver.MiliraXian.Characters.Neiyu");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    internal sealed class MiliraXianCharacterWingEntry
    {
        private bool loaded;
        private bool failed;

        public MiliraXianCharacterWingEntry(
            string pawnKindDefName,
            string leftWingTextureRoot,
            string rightWingTextureRoot,
            string flyNorthDefName,
            string flyEastDefName,
            string flySouthDefName,
            string flyWestDefName)
        {
            PawnKindDefName = pawnKindDefName;
            LeftWingTextureRoot = leftWingTextureRoot;
            RightWingTextureRoot = rightWingTextureRoot;
            FlyNorthDefName = flyNorthDefName;
            FlyEastDefName = flyEastDefName;
            FlySouthDefName = flySouthDefName;
            FlyWestDefName = flyWestDefName;
        }

        public string PawnKindDefName { get; }
        public string LeftWingTextureRoot { get; }
        public string RightWingTextureRoot { get; }
        public string FlyNorthDefName { get; }
        public string FlyEastDefName { get; }
        public string FlySouthDefName { get; }
        public string FlyWestDefName { get; }

        public Graphic LeftWingFront { get; private set; }
        public Graphic LeftWingBehind { get; private set; }
        public Graphic RightWingFront { get; private set; }
        public Graphic RightWingBehind { get; private set; }

        public void EnsureLoaded()
        {
            if (loaded || failed)
            {
                return;
            }

            loaded = true;

            try
            {
                LeftWingFront = GraphicDatabase.Get<Graphic_Multi>(
                    LeftWingTextureRoot + "/LeftWingFront",
                    ShaderDatabase.Cutout,
                    Vector2.one,
                    Color.white);
                LeftWingBehind = GraphicDatabase.Get<Graphic_Multi>(
                    LeftWingTextureRoot + "/LeftWingBehind",
                    ShaderDatabase.Cutout,
                    Vector2.one,
                    Color.white);
                RightWingFront = GraphicDatabase.Get<Graphic_Multi>(
                    RightWingTextureRoot + "/RightWingFront",
                    ShaderDatabase.Cutout,
                    Vector2.one,
                    Color.white);
                RightWingBehind = GraphicDatabase.Get<Graphic_Multi>(
                    RightWingTextureRoot + "/RightWingBehind",
                    ShaderDatabase.Cutout,
                    Vector2.one,
                    Color.white);
            }
            catch (Exception ex)
            {
                failed = true;
                Log.Error("[MiliraXian.Characters.Neiyu] Failed to load wing graphics for " + PawnKindDefName + ".\n" + ex);
            }
        }
    }

    internal static class MiliraXianCharactersWingRegistry
    {
        private const string LeftWingLabel = "left wing";
        private const string LeftWingBehindLabel = "left wing behind";
        private const string RightWingLabel = "right wing";
        private const string RightWingBehindLabel = "right wing behind";

        private static readonly Dictionary<string, MiliraXianCharacterWingEntry> Entries =
            new Dictionary<string, MiliraXianCharacterWingEntry>
            {
                {
                    "MiliraXian_Neiyu",
                    new MiliraXianCharacterWingEntry(
                        "MiliraXian_Neiyu",
                        "MiliraXianNeiyu/PawnNeiyu/LeftWingNew_Neiyu",
                        "MiliraXianNeiyu/PawnNeiyu/RightWingNew_Neiyu",
                        "Milira_FlyNorth_Neiyu",
                        "Milira_FlyEast_Neiyu",
                        "Milira_FlySouth_Neiyu",
                        "Milira_FlyWest_Neiyu")
                },
                {
                    "MiliraXian_Zhaoli",
                    new MiliraXianCharacterWingEntry(
                        "MiliraXian_Zhaoli",
                        // TODO：这里记得改翅膀贴图 
                        "MiliraXianNeiyu/PawnNeiyu/LeftWingNew_Neiyu",
                        "MiliraXianNeiyu/PawnNeiyu/RightWingNew_Neiyu",
                        "Milira_FlyNorth_Zhaoli",
                        "Milira_FlyEast_Zhaoli",
                        "Milira_FlySouth_Zhaoli",
                        "Milira_FlyWest_Zhaoli")
                },
                {
                    "MiliraXian_Qinghe",
                    new MiliraXianCharacterWingEntry(
                        "MiliraXian_Qinghe",
                        // TODO：这里记得改翅膀贴图 (for 清荷
                        "MiliraXianNeiyu/PawnNeiyu/LeftWingNew_Neiyu",
                        "MiliraXianNeiyu/PawnNeiyu/RightWingNew_Neiyu",
                        "Milira_FlyNorth_Qinghe",
                        "Milira_FlyEast_Qinghe",
                        "Milira_FlySouth_Qinghe",
                        "Milira_FlyWest_Qinghe")
                }
            };

        public static bool TryGetEntry(Pawn pawn, out MiliraXianCharacterWingEntry entry)
        {
            entry = null;
            return pawn?.kindDef != null && Entries.TryGetValue(pawn.kindDef.defName, out entry);
        }

        public static bool TryGetGraphic(Pawn pawn, string label, out Graphic graphic)
        {
            graphic = null;
            if (!TryGetEntry(pawn, out var entry) || string.IsNullOrEmpty(label))
            {
                return false;
            }

            entry.EnsureLoaded();

            switch (label)
            {
                case LeftWingLabel:
                    graphic = entry.LeftWingFront;
                    break;
                case LeftWingBehindLabel:
                    graphic = entry.LeftWingBehind;
                    break;
                case RightWingLabel:
                    graphic = entry.RightWingFront;
                    break;
                case RightWingBehindLabel:
                    graphic = entry.RightWingBehind;
                    break;
            }

            return graphic != null;
        }

        public static bool TryGetFlyAnimation(Pawn pawn, Rot4? facingOverride, out AnimationDef animationDef)
        {
            animationDef = null;
            if (!TryGetEntry(pawn, out var entry))
            {
                return false;
            }

            string defName;
            switch ((facingOverride ?? pawn.Rotation).AsInt)
            {
                case 0:
                    defName = entry.FlyNorthDefName;
                    break;
                case 1:
                    defName = entry.FlyEastDefName;
                    break;
                case 2:
                    defName = entry.FlySouthDefName;
                    break;
                case 3:
                    defName = entry.FlyWestDefName;
                    break;
                default:
                    defName = entry.FlyEastDefName;
                    break;
            }

            animationDef = DefDatabase<AnimationDef>.GetNamedSilentFail(defName);
            return animationDef != null;
        }
    }

    [HarmonyPatch(typeof(PawnRenderNode), nameof(PawnRenderNode.GraphicFor))]
    [HarmonyAfter("Ariandel.MiliraImperiumHarmonyPatch")]
    internal static class Patch_PawnRenderNode_GraphicFor_MiliraXianCharactersWings
    {
        [HarmonyPostfix]
        private static void Postfix(PawnRenderNode __instance, Pawn pawn, ref Graphic __result)
        {
            string label = __instance?.Props?.debugLabel;
            if (label == null)
            {
                return;
            }

            if (MiliraXianCharactersWingRegistry.TryGetGraphic(pawn, label, out var graphic))
            {
                __result = graphic;
            }
        }
    }

    [HarmonyPatch(typeof(Pawn_FlightTracker), nameof(Pawn_FlightTracker.GetBestFlyAnimation))]
    [HarmonyAfter("Ariandel.MiliraImperiumHarmonyPatch")]
    internal static class Patch_Pawn_FlightTracker_GetBestFlyAnimation_MiliraXianCharactersWings
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn pawn, Rot4? facingOverride, ref AnimationDef __result)
        {
            if (MiliraXianCharactersWingRegistry.TryGetFlyAnimation(pawn, facingOverride, out var anim))
            {
                __result = anim;
            }
        }
    }


    [HarmonyPatch]
    [HarmonyAfter("Ariandel.MiliraImperiumHarmonyPatch")]
    internal static class Patch_Milira_CompAbilityEffect_HungerRestCost_GetBestFlyAnimation_MiliraXianCharactersWings
    {
        private static MethodBase TargetMethod()
        {
            Type t = AccessTools.TypeByName("Milira.CompAbilityEffect_HungerRestCost");
            return t != null ? AccessTools.Method(t, "GetBestFlyAnimation") : null;
        }

        [HarmonyPostfix]
        private static void Postfix(Pawn pawn, Rot4? facingOverride, ref AnimationDef __result)
        {
            if (MiliraXianCharactersWingRegistry.TryGetFlyAnimation(pawn, facingOverride, out var anim))
            {
                __result = anim;
            }
        }
    }
}

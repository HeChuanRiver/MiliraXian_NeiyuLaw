// MiliraXian.NeiyuLaw - Wing replacement + fly animation selection
// Put the compiled DLL into: YourMod/1.6/Assemblies/
// Textures you must prepare for STATIC wings (Graphic_Multi):
//   Content/Textures/MiliraXian/PawnNeiyu/LeftWingNew_Neiyu/LeftWingFront_[north/east/south/west].png
//   Content/Textures/MiliraXian/PawnNeiyu/LeftWingNew_Neiyu/LeftWingBehind_[north/east/south/west].png
//   Content/Textures/MiliraXian/PawnNeiyu/RightWingNew_Neiyu/RightWingFront_[north/east/south/west].png
//   Content/Textures/MiliraXian/PawnNeiyu/RightWingNew_Neiyu/RightWingBehind_[north/east/south/west].png
//
// Fly animation textures/defs are already in your XML (GraphicStateDef + AnimationDef).
// This DLL only tells the game "use my AnimationDef when THIS pawn is flying".

using System;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.NeiyuLaw
{
    [StaticConstructorOnStartup]
    public static class MiliraXian_NeiyuLaw_HarmonyBootstrap
    {
        static MiliraXian_NeiyuLaw_HarmonyBootstrap()
        {
            var harmony = new Harmony("HeChuanRiver.MiliraXian.NeiyuLaw");
            harmony.PatchAll(Assembly.GetExecutingAssembly());
        }
    }

    internal static class MX_Neiyu_Ids
    {
        // IMPORTANT: change this to your actual PawnKindDef defName if you renamed it
        public const string NeiyuPawnKindDefName = "MiliraXian_Neiyu";

        // IMPORTANT: these must match your XML <AnimationDef><defName>...</defName>
        public const string FlyNorth = "Milira_FlyNorth_Neiyu";
        public const string FlyEast  = "Milira_FlyEast_Neiyu";
        public const string FlySouth = "Milira_FlySouth_Neiyu";
        public const string FlyWest  = "Milira_FlyWest_Neiyu";
    }

    internal static class MX_Neiyu_Util
    {
        public static bool IsNeiyu(Pawn pawn)
        {
            return pawn?.kindDef != null && pawn.kindDef.defName == MX_Neiyu_Ids.NeiyuPawnKindDefName;
        }

        public static bool TryGetNeiyuFlyAnimation(Pawn pawn, Rot4? facingOverride, out AnimationDef animationDef)
        {
            animationDef = null;
            if (!IsNeiyu(pawn)) return false;

            Rot4 rot = facingOverride ?? pawn.Rotation;
            string defName;
            switch (rot.AsInt)
            {
                case 0: defName = MX_Neiyu_Ids.FlyNorth; break;
                case 1: defName = MX_Neiyu_Ids.FlyEast;  break;
                case 2: defName = MX_Neiyu_Ids.FlySouth; break;
                case 3: defName = MX_Neiyu_Ids.FlyWest;  break;
                default: defName = MX_Neiyu_Ids.FlyEast; break;
            }

            animationDef = DefDatabase<AnimationDef>.GetNamedSilentFail(defName);
            return animationDef != null;
        }
    }

    internal static class MX_Neiyu_WingGraphics
    {
        private static bool _loaded;
        private static bool _failed;

        public static Graphic LeftWingFront;
        public static Graphic LeftWingBehind;
        public static Graphic RightWingFront;
        public static Graphic RightWingBehind;

        public static void EnsureLoaded()
        {
            if (_loaded || _failed) return;
            _loaded = true;

            try
            {
                // IMPORTANT: these base paths must match your texture folders (WITHOUT file extension & direction suffix)
                const string leftBase  = "MiliraXianNeiyu/PawnNeiyu/LeftWingNew_Neiyu";
                const string rightBase = "MiliraXianNeiyu/PawnNeiyu/RightWingNew_Neiyu";

                // Graphic_Multi will automatically pick _north/_east/_south/_west.
                LeftWingFront   = GraphicDatabase.Get<Graphic_Multi>(leftBase + "/LeftWingFront",   ShaderDatabase.Cutout, Vector2.one, Color.white);
                LeftWingBehind  = GraphicDatabase.Get<Graphic_Multi>(leftBase + "/LeftWingBehind",  ShaderDatabase.Cutout, Vector2.one, Color.white);
                RightWingFront  = GraphicDatabase.Get<Graphic_Multi>(rightBase + "/RightWingFront", ShaderDatabase.Cutout, Vector2.one, Color.white);
                RightWingBehind = GraphicDatabase.Get<Graphic_Multi>(rightBase + "/RightWingBehind",ShaderDatabase.Cutout, Vector2.one, Color.white);
            }
            catch (Exception ex)
            {
                _failed = true;
                Log.Error("[MiliraXian.NeiyuLaw] Failed to load Neiyu wing graphics. Check your texture paths & file names.\n" + ex);
            }
        }
    }

    // =========================
    // 1) Replace STATIC wing graphics
    // =========================
    //
    // Milira Race's renderTree has wing nodes with debugLabel:
    //   "left wing", "left wing behind", "right wing", "right wing behind"
    //
    // We override GraphicFor() ONLY when pawn is Neiyu.
    //
    [HarmonyPatch(typeof(PawnRenderNode), nameof(PawnRenderNode.GraphicFor))]
    [HarmonyAfter("Ariandel.MiliraImperiumHarmonyPatch")] // If Milira Imperium is installed, let their patch run first.
    internal static class Patch_PawnRenderNode_GraphicFor_MX_Neiyu
    {
        [HarmonyPostfix]
        private static void Postfix(PawnRenderNode __instance, Pawn pawn, ref Graphic __result)
        {
            if (!MX_Neiyu_Util.IsNeiyu(pawn)) return;

            string label = __instance?.Props?.debugLabel;
            if (label == null) return;

            // Lazy load graphics only when needed
            MX_Neiyu_WingGraphics.EnsureLoaded();

            switch (label)
            {
                case "left wing":
                    if (MX_Neiyu_WingGraphics.LeftWingFront != null)
                        __result = MX_Neiyu_WingGraphics.LeftWingFront;
                    break;

                case "left wing behind":
                    if (MX_Neiyu_WingGraphics.LeftWingBehind != null)
                        __result = MX_Neiyu_WingGraphics.LeftWingBehind;
                    break;

                case "right wing":
                    if (MX_Neiyu_WingGraphics.RightWingFront != null)
                        __result = MX_Neiyu_WingGraphics.RightWingFront;
                    break;

                case "right wing behind":
                    if (MX_Neiyu_WingGraphics.RightWingBehind != null)
                        __result = MX_Neiyu_WingGraphics.RightWingBehind;
                    break;
            }
        }
    }

    // =========================
    // 2) Use Neiyu-specific FLY animation
    // =========================

    [HarmonyPatch(typeof(Pawn_FlightTracker), nameof(Pawn_FlightTracker.GetBestFlyAnimation))]
    [HarmonyAfter("Ariandel.MiliraImperiumHarmonyPatch")]
    internal static class Patch_Pawn_FlightTracker_GetBestFlyAnimation_MX_Neiyu
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn pawn, Rot4? facingOverride, ref AnimationDef __result)
        {
            if (MX_Neiyu_Util.TryGetNeiyuFlyAnimation(pawn, facingOverride, out var anim))
                __result = anim;
        }
    }

    // Milira's ability comp also queries fly animation. Patch via reflection so you don't need a hard compile-time dependency.
    [HarmonyPatch]
    [HarmonyAfter("Ariandel.MiliraImperiumHarmonyPatch")]
    internal static class Patch_Milira_CompAbilityEffect_HungerRestCost_GetBestFlyAnimation_MX_Neiyu
    {
        private static MethodBase TargetMethod()
        {
            // Class name from your earlier stacktrace: Milira.CompAbilityEffect_HungerRestCost
            Type t = AccessTools.TypeByName("Milira.CompAbilityEffect_HungerRestCost");
            return t != null ? AccessTools.Method(t, "GetBestFlyAnimation") : null;
        }

        [HarmonyPostfix]
        private static void Postfix(Pawn pawn, Rot4? facingOverride, ref AnimationDef __result)
        {
            if (MX_Neiyu_Util.TryGetNeiyuFlyAnimation(pawn, facingOverride, out var anim))
                __result = anim;
        }
    }
}

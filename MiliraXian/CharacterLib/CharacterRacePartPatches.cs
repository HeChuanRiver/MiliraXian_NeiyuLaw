using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraXian.CharacterLib
{
    [HarmonyPatch(typeof(PawnRenderNode), nameof(PawnRenderNode.GraphicFor))]
    internal static class Patch_PawnRenderNode_GraphicFor
    {
        [HarmonyPostfix]
        private static void Postfix(PawnRenderNode __instance, Pawn pawn, ref Graphic __result)
        {
            Graphic graphic = CharacterRaceParts.GraphicFor(pawn, __instance?.Props?.texPath);
            if (graphic != null) __result = graphic;
        }
    }

    /// <summary>
    /// Swaps the frames of an animated part. These graphics come from the race's
    /// GraphicStateDefs rather than from the node, so GraphicFor never sees them.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderNode), "StateGraphicsFor")]
    internal static class Patch_PawnRenderNode_StateGraphicsFor
    {
        [HarmonyPostfix]
        private static IEnumerable<(GraphicStateDef state, Graphic graphic)> Postfix(
            IEnumerable<(GraphicStateDef state, Graphic graphic)> values, Pawn pawn)
        {
            foreach (var entry in values)
            {
                Graphic replacement = CharacterRaceParts.GraphicLike(pawn, entry.graphic);
                yield return replacement != null ? (entry.state, replacement) : entry;
            }
        }
    }

    /// <summary>
    /// Swaps a character's flight animation at the flight system's decision point. The flight
    /// tracker records the def it requested and later compares the renderer's current animation
    /// against it by reference to decide when to stop; substituting downstream at SetAnimation
    /// would break that identity and leave the wings animating after landing. Vanilla offers no
    /// flight animation for humanlikes — the race supplies it through this method — so a
    /// non-null result is always the race's own def, ours to swap.
    /// </summary>
    [HarmonyPatch(typeof(Pawn_FlightTracker), nameof(Pawn_FlightTracker.GetBestFlyAnimation))]
    [HarmonyPriority(Priority.Last)]
    internal static class Patch_PawnFlightTracker_GetBestFlyAnimation
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn pawn, ref AnimationDef __result)
        {
            if (__result == null) return;
            AnimationDef replacement = CharacterRaceParts.FlyAnimationFor(pawn, __result);
            if (replacement != null) __result = replacement;
        }
    }
}

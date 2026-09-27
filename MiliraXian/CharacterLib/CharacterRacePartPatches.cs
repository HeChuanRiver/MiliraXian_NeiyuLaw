using System.Collections.Generic;
using HarmonyLib;
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
    /// Substitutes a character's animation as the animation is applied, rather than where the
    /// race decided on it. Vanilla leaves humanlike flight animations entirely to the race, so
    /// each race mod picks them its own way and there is no shared decision point to patch; this
    /// is the one place every animation must pass through.
    /// </summary>
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.SetAnimation))]
    internal static class Patch_PawnRenderer_SetAnimation
    {
        [HarmonyPrefix]
        private static void Prefix(Pawn ___pawn, ref AnimationDef animation)
        {
            AnimationDef replacement = CharacterRaceParts.AnimationFor(___pawn, animation);
            if (replacement != null) animation = replacement;
        }
    }
}

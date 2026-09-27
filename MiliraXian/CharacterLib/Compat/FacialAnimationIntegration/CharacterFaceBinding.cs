using System;
using System.Collections.Generic;
using System.Reflection;
using FacialAnimation;
using HarmonyLib;
using Verse;

namespace MiliraXian.CharacterLib.Compat.FacialAnimationIntegration
{
    /// <summary>
    /// Assigns this face part to a character instead of leaving it to Facial Animation's random
    /// draw. Goes on any FaceTypeDef: eyeball, lid, brow, mouth, skin, head, and the rest.
    /// Set probability to 0 alongside it so the part cannot also be drawn at random — as long as
    /// the race keeps at least one part of that kind with a probability above 0, since a pool
    /// summing to 0 falls back to its first entry.
    /// </summary>
    public class CharacterFaceExtension : DefModExtension
    {
        public PawnKindDef pawnKind;
    }

    [StaticConstructorOnStartup]
    internal static class CharacterFaceBinding
    {
        /// <summary>
        /// Every part a character claims, keyed by the FaceTypeDef subclass it belongs to.
        /// Facial Animation declares one controller per subclass, so the subclass is what pairs
        /// a declared part with the controller that can wear it.
        /// </summary>
        private static readonly Dictionary<PawnKindDef, Dictionary<Type, FaceTypeDef>> parts = new();

        static CharacterFaceBinding()
        {
            foreach (Type type in typeof(FaceTypeDef).AllSubclassesNonAbstract())
                foreach (Def def in GenDefDatabase.GetAllDefsInDatabaseForDef(type))
                {
                    if (def is not FaceTypeDef faceType) continue;
                    PawnKindDef kind = faceType.GetModExtension<CharacterFaceExtension>()?.pawnKind;
                    if (kind == null) continue;
                    if (!parts.TryGetValue(kind, out var byType))
                        parts[kind] = byType = new Dictionary<Type, FaceTypeDef>();
                    byType[type] = faceType;
                }
        }

        public static bool HasFace(Pawn pawn) => pawn?.kindDef != null && parts.ContainsKey(pawn.kindDef);

        /// <summary>
        /// Assigns the character's own parts, then hands control straight back to Facial
        /// Animation. Loading textures here would be wrong: LidControllerComp builds its path
        /// from the head controller's face type, which is only filled once the head's own node
        /// is created, so a part cannot be reloaded on its own.
        /// </summary>
        public static void Apply(Pawn pawn)
        {
            if (pawn?.kindDef == null || !parts.TryGetValue(pawn.kindDef, out var byType)) return;

            foreach (ThingComp comp in pawn.AllComps)
            {
                if (comp is not IFacialAnimationController controller) continue;

                // ControllerBaseComp's first type argument says which part this controller wears.
                // The base chain is walked because a no-save controller sits one level below it.
                Type controllerBase = null;
                for (Type type = comp.GetType(); type != null; type = type.BaseType)
                    if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(ControllerBaseComp<,>))
                    {
                        controllerBase = type;
                        break;
                    }
                if (controllerBase == null) continue;

                if (!byType.TryGetValue(controllerBase.GetGenericArguments()[0], out FaceTypeDef faceType)) continue;
                if (controller.FaceTypeDefName == faceType.defName) continue;

                // Set through the interface: its setter resolves the def against the controller's
                // own database, so one call serves every part type.
                controller.FaceTypeDefName = faceType.defName;

                // FaceColor sits on the generic base rather than the interface, so it is reached
                // through that base; FaceSecondColor belongs to the eyeball alone.
                controllerBase.GetProperty("FaceColor")?.SetValue(comp, faceType.minColor);
                if (comp is EyeballControllerComp eyes) eyes.FaceSecondColor = faceType.minColor;
                controller.SetDirty();
            }
        }
    }

    /// <summary>
    /// Binds the parts as the face's render nodes are built. Everything else runs too late or
    /// not at all: a pawn on the starting-pawn page has no game object to tick and no job to
    /// change, which is why an identity that depends on either shows a random face there until
    /// something else forces a refresh.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_CharacterFaceNodes
    {
        // DrawFaceGraphicsComp is internal, so the target is resolved at runtime.
        [HarmonyTargetMethod]
        internal static MethodBase TargetMethod() =>
            AccessTools.TypeByName("FacialAnimation.DrawFaceGraphicsComp")?.GetMethod("CompRenderNodes", AccessTools.all);

        [HarmonyPrefix]
        private static void Prefix(ThingComp __instance) => CharacterFaceBinding.Apply(__instance.parent as Pawn);
    }

    [HarmonyPatch(typeof(FacialAnimationModSettings), nameof(FacialAnimationModSettings.ShouldDrawRaceXenoType))]
    internal static class Patch_CharacterFaceVisible
    {
        // CompRenderNodes drops the whole face when this is false, so the parts above would
        // never be built for a character whose race/xenotype the player disabled.
        private static void Postfix(Pawn pawn, ref bool __result)
        {
            if (CharacterFaceBinding.HasFace(pawn)) __result = true;
        }
    }
}

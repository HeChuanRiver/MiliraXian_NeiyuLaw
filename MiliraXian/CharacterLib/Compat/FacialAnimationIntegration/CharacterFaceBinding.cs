using System.Collections.Generic;
using System.Reflection;
using FacialAnimation;
using HarmonyLib;
using Verse;

namespace MiliraXian.CharacterLib.Compat.FacialAnimationIntegration
{
    public class CharacterFaceExtension : DefModExtension
    {
        public PawnKindDef pawnKind;
    }

    [StaticConstructorOnStartup]
    internal static class CharacterFaceBinding
    {
        private static readonly Dictionary<PawnKindDef, EyeballTypeDef> Eyes = Index<EyeballTypeDef>();
        private static readonly Dictionary<PawnKindDef, LidTypeDef> Lids = Index<LidTypeDef>();
        private static readonly Dictionary<PawnKindDef, BrowTypeDef> Brows = Index<BrowTypeDef>();

        private static Dictionary<PawnKindDef, T> Index<T>() where T : FaceTypeDef, new()
        {
            var result = new Dictionary<PawnKindDef, T>();
            foreach (T def in DefDatabase<T>.AllDefsListForReading)
            {
                PawnKindDef kind = def.GetModExtension<CharacterFaceExtension>()?.pawnKind;
                if (kind != null) result[kind] = def;
            }
            return result;
        }

        public static bool HasFace(Pawn pawn) => pawn?.kindDef != null &&
            (Eyes.ContainsKey(pawn.kindDef) || Lids.ContainsKey(pawn.kindDef) || Brows.ContainsKey(pawn.kindDef));

        /// <summary>
        /// Assigns the character's own parts, then hands control straight back to Facial
        /// Animation. Loading textures here would be wrong: LidControllerComp builds its path
        /// from the head controller's face type, which is only filled once the head's own node
        /// is created, so a part cannot be reloaded on its own.
        /// </summary>
        public static void Apply(Pawn pawn)
        {
            if (!HasFace(pawn)) return;
            Bind(pawn.TryGetComp<EyeballControllerComp>(), Eyes, pawn.kindDef);
            Bind(pawn.TryGetComp<LidControllerComp>(), Lids, pawn.kindDef);
            Bind(pawn.TryGetComp<BrowControllerComp>(), Brows, pawn.kindDef);
        }

        private static void Bind<T, S>(ControllerBaseComp<T, S> controller, Dictionary<PawnKindDef, T> table, PawnKindDef kind)
            where T : FaceTypeDef, new() where S : Def, IFaceShapeDef, new()
        {
            if (controller == null || !table.TryGetValue(kind, out T faceType) || controller.FaceType == faceType) return;
            controller.FaceType = faceType;
            controller.FaceColor = faceType.minColor;
            if (controller is EyeballControllerComp eyes) eyes.FaceSecondColor = faceType.minColor;
            controller.SetDirty();
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

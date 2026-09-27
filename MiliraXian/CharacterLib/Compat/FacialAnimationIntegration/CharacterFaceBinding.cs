using System.Collections.Generic;
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

        public static void Apply(Pawn pawn)
        {
            if (!HasFace(pawn)) return;
            var eyes = pawn.TryGetComp<EyeballControllerComp>();
            if (eyes != null && Eyes.TryGetValue(pawn.kindDef, out var eye) && eyes.FaceType != eye)
            {
                eyes.InitializeIfNeed();
                eyes.FaceType = eye;
                eyes.FaceColor = eye.minColor;
                eyes.FaceSecondColor = eye.minColor;
                eyes.ReloadIfNeed();
            }
            var lids = pawn.TryGetComp<LidControllerComp>();
            if (lids != null && Lids.TryGetValue(pawn.kindDef, out var lid) && lids.FaceType != lid)
            {
                lids.InitializeIfNeed();
                lids.FaceType = lid;
                lids.FaceColor = lid.minColor;
                lids.ReloadIfNeed();
            }
            var brows = pawn.TryGetComp<BrowControllerComp>();
            if (brows != null && Brows.TryGetValue(pawn.kindDef, out var brow) && brows.FaceType != brow)
            {
                brows.InitializeIfNeed();
                brows.FaceType = brow;
                brows.FaceColor = brow.minColor;
                brows.ReloadIfNeed();
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class Patch_CharacterFaceSpawn
    {
        private static void Postfix(Pawn __instance) => CharacterFaceBinding.Apply(__instance);
    }

    [HarmonyPatch(typeof(FacialAnimationModSettings), nameof(FacialAnimationModSettings.ShouldDrawRaceXenoType))]
    internal static class Patch_CharacterFaceVisible
    {
        private static void Postfix(Pawn pawn, ref bool __result)
        {
            if (CharacterFaceBinding.HasFace(pawn)) __result = true;
        }
    }
}

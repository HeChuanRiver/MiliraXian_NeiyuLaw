using HarmonyLib;
using MiliraXian.Characters.Neiyu;
using MiliraXian.Characters.Zhaoli;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    [StaticConstructorOnStartup]
    internal static class MXShieldRenderUtility
    {
        private static readonly System.Collections.Generic.Dictionary<string, Material> BaseMaterials =
            new System.Collections.Generic.Dictionary<string, Material>();

        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        public static void Draw(string texturePath, Matrix4x4 matrix, Color color)
        {
            if (texturePath.NullOrEmpty() || color.a <= 0.001f)
            {
                return;
            }

            if (!BaseMaterials.TryGetValue(texturePath, out Material material))
            {
                material = MaterialPool.MatFrom(texturePath, ShaderDatabase.Transparent, Color.white);
                BaseMaterials[texturePath] = material;
            }

            if (material == null)
            {
                return;
            }

            PropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0, null, 0, PropertyBlock);
        }
    }

    [HarmonyPatch(typeof(StatExtension), nameof(StatExtension.GetStatValue))]
    internal static class Patch_MXCharacterStatModifiers_GetStatValue
    {
        [HarmonyPostfix]
        private static void Postfix(Thing thing, StatDef stat, ref float __result)
        {
            Pawn pawn = thing as Pawn;
            if (pawn == null || stat == null)
            {
                return;
            }

            bool appliesToNeiyu = MXNeiyuShieldUtility.IsAffectedStat(stat);
            bool appliesToZhaoli = ZhaoliProgressionUtility.IsAffectedStat(stat);
            if (!appliesToNeiyu && !appliesToZhaoli)
            {
                return;
            }

            if (appliesToNeiyu)
            {
                MXNeiyuShieldUtility.ApplyStatModifiers(pawn, stat, ref __result);
            }

            if (appliesToZhaoli)
            {
                ZhaoliProgressionUtility.ApplyStatModifiers(pawn, stat, ref __result);
            }
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
    internal static class Patch_MXCharacterShields_RenderPawnAt
    {
        private static readonly AccessTools.FieldRef<PawnRenderer, Pawn> PawnRef =
            AccessTools.FieldRefAccess<PawnRenderer, Pawn>("pawn");

        [HarmonyPostfix]
        private static void Postfix(PawnRenderer __instance, Vector3 drawLoc)
        {
            if (__instance == null)
            {
                return;
            }

            Pawn pawn = PawnRef(__instance);
            if (NeiyuEquipmentUtility.IsNeiyu(pawn))
            {
                DrawNeiyuShield(pawn, drawLoc);
            }
            else if (ZhaoliKarmaUtility.IsZhaoli(pawn))
            {
                DrawZhaoliShield(pawn, drawLoc);
            }
        }

        private static void DrawNeiyuShield(Pawn pawn, Vector3 drawLoc)
        {
            if (!MXNeiyuShieldUtility.TryGetShieldComp(pawn, out HediffComp_MXNeiyuCountShield shield)
                || !shield.ShouldDrawActiveShield)
            {
                return;
            }

            HediffCompProperties_MXNeiyuCountShield props = shield.Props;
            float pulseScale = shield.ActiveShieldPulseScale;
            Vector3 pos = drawLoc;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            pos += Altitudes.AltIncVect * props.activeShieldAltitudeOffset;

            Matrix4x4 matrix = Matrix4x4.TRS(
                pos,
                Quaternion.identity,
                new Vector3(props.activeShieldDrawSize.x * pulseScale, 1f, props.activeShieldDrawSize.y * pulseScale));

            MXShieldRenderUtility.Draw(
                props.activeShieldTexPath,
                matrix,
                new Color(1f, 1f, 1f, Mathf.Clamp01(props.activeShieldAlpha)));
        }

        private static void DrawZhaoliShield(Pawn pawn, Vector3 drawLoc)
        {
            HediffComp_ZhaoliShieldLayers shield = ZhaoliShieldLayerUtility.GetShieldComp(pawn);
            if (shield == null || !shield.ShouldDrawActiveShield)
            {
                return;
            }

            HediffCompProperties_ZhaoliShieldLayers props = shield.PropsShield;
            float pulseScale = shield.ActiveShieldPulseScale;
            Vector3 pos = drawLoc;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            pos += Altitudes.AltIncVect * props.activeShieldAltitudeOffset;

            Matrix4x4 matrix = Matrix4x4.TRS(
                pos,
                Quaternion.AngleAxis(shield.ActiveShieldAngle, Vector3.up),
                new Vector3(props.activeShieldDrawSize.x * pulseScale, 1f, props.activeShieldDrawSize.y * pulseScale));

            MXShieldRenderUtility.Draw(
                props.activeShieldTexPath,
                matrix,
                new Color(1f, 1f, 1f, Mathf.Clamp01(props.activeShieldAlpha)));
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class Patch_MXShieldCache_PawnKill
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn __instance)
        {
            MXNeiyuShieldUtility.Invalidate(__instance);
            ZhaoliShieldLayerUtility.Invalidate(__instance);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DeSpawn), typeof(DestroyMode))]
    internal static class Patch_MXShieldCache_PawnDeSpawn
    {
        [HarmonyPostfix]
        private static void Postfix(Pawn __instance)
        {
            MXNeiyuShieldUtility.Invalidate(__instance);
            ZhaoliShieldLayerUtility.Invalidate(__instance);
        }
    }

    [HarmonyPatch(typeof(Verse.Profile.MemoryUtility), nameof(Verse.Profile.MemoryUtility.ClearAllMapsAndWorld))]
    internal static class Patch_MXShieldCache_ClearAllMapsAndWorld
    {
        [HarmonyPostfix]
        private static void Postfix()
        {
            MXNeiyuShieldUtility.ClearCache();
            ZhaoliShieldLayerUtility.ClearCache();
            NeiyuSpecialPawnIntegration.ClearRuntimeState();
            NeiyuEarAnimationRuntime.Reset();
            SpecialHaloAnimationRuntime.Reset();
            CharacterUnityVfxRuntime.Reset();
        }
    }

    internal static class MXVisualBudget
    {
        public static bool ShouldEmit(Thing source, int baseIntervalTicks, int viewPaddingCells = 1)
        {
            if (source == null || !source.Spawned || source.Map == null || Find.TickManager == null)
            {
                return false;
            }

            if (Find.CameraDriver != null
                && !Find.CameraDriver.CurrentViewRect.ExpandedBy(Mathf.Max(0, viewPaddingCells)).Contains(source.Position))
            {
                return false;
            }

            int activeEmitters = source.Map.listerThings.ThingsOfDef(source.def).Count;
            int loadMultiplier = activeEmitters > 64 ? 4 : activeEmitters > 32 ? 2 : 1;
            return source.IsHashIntervalTick(Mathf.Max(1, baseIntervalTicks) * loadMultiplier);
        }
    }
}

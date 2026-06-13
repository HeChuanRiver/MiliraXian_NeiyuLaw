using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class Mote_AccumulationBar : MoteDualAttached
    {
        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        public HediffDef SourceHediffDef { get; set; }

        public float Progress { get; set; }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            MX_AccumulationBarProperties props = def.GetModExtension<MX_AccumulationBarProperties>() ?? MX_AccumulationBarProperties.Default;
            UpdatePositionAndRotation();
            if (Find.UIRoot.HideMotes || Find.ScreenshotModeHandler.Active)
            {
                return;
            }

            GenDraw.FillableBarRequest request = default(GenDraw.FillableBarRequest);
            request.center = exactPosition;
            request.center.x += props.offsetX;
            request.center.z += props.offsetZ + GetStackIndex(props) * props.stackSpacing;
            request.center.y += props.altitudeOffset;
            request.size = props.size;
            request.fillPercent = Mathf.Clamp01(Progress);
            request.filledMat = BuildMat(props.fillTexPath, props.fillShaderType, props.fillColor);
            request.unfilledMat = BuildMat(props.backgroundTexPath, props.backgroundShaderType, props.backgroundColor);
            request.margin = props.margin;
            request.rotation = Rot4.North;
            GenDraw.DrawFillableBar(request);
            DrawIcon(request.center, props);
        }

        private int GetStackIndex(MX_AccumulationBarProperties props)
        {
            Pawn pawn = link1.Target.Thing as Pawn;
            if (pawn?.health?.hediffSet?.hediffs == null || SourceHediffDef == null)
            {
                return 0;
            }

            int index = 0;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                IAccumulationHediff accumulation = hediffs[i] as IAccumulationHediff;
                if (accumulation == null)
                {
                    continue;
                }

                if (accumulation.Def == SourceHediffDef)
                {
                    return props.stackDownward ? -index : index;
                }

                index++;
            }

            return 0;
        }

        private void DrawIcon(Vector3 barCenter, MX_AccumulationBarProperties props)
        {
            if (props.iconTexPath.NullOrEmpty() || props.iconSize <= 0f)
            {
                return;
            }

            Vector3 iconCenter = barCenter;
            iconCenter.x -= props.size.x * 0.5f + props.iconGap + props.iconSize * 0.5f;
            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(iconCenter + Vector3.up * props.iconAltitudeOffset, Quaternion.identity, new Vector3(props.iconSize, 1f, props.iconSize));
            Graphics.DrawMesh(MeshPool.plane10, matrix, BuildMat(props.iconTexPath, props.iconShaderType, props.iconColor), 0);
        }

        private static Material BuildMat(string texPath, ShaderTypeDef shaderType, Color color)
        {
            string key = $"{texPath}|{shaderType?.defName}|{color}";
            Material material;
            if (MaterialCache.TryGetValue(key, out material))
            {
                return material;
            }

            Shader shader = (shaderType ?? ShaderTypeDefOf.MetaOverlay).Shader;
            material = !texPath.NullOrEmpty()
                ? MaterialPool.MatFrom(texPath, shader, color)
                : SolidColorMaterials.NewSolidColorMaterial(color, shader);
            MaterialCache[key] = material;
            return material;
        }
    }

}

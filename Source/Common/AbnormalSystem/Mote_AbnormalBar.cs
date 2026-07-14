using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class AbnormalBarProperties : DefModExtension
    {
        public string backgroundTexPath;
        public string fillTexPath;
        public ShaderTypeDef backgroundShaderType;
        public ShaderTypeDef fillShaderType;
        public string iconTexPath;
        public ShaderTypeDef iconShaderType;
        public Color iconColor = Color.white;
        public Color backgroundColor = new Color(0.05f, 0.10f, 0.14f, 0.72f);
        public Color fillColor = new Color(0.56f, 0.88f, 1f, 0.86f);
        public Vector2 size = new Vector2(0.92f, 0.12f);
        public float margin = 0.05f;
        public float offsetX;
        public float offsetZ = -0.55f;
        public float altitudeOffset = 0.06f;
        public float stackSpacing = 0.16f;
        public bool stackDownward = true;
        public float iconSize = 0.16f;
        public float iconGap = 0.04f;
        public float iconAltitudeOffset = 0.02f;

        public static readonly AbnormalBarProperties Default = new AbnormalBarProperties();
    }

    public class Mote_AbnormalBar : MoteDualAttached
    {
        private static readonly Dictionary<AbnormalBarProperties, MaterialBundle> MaterialCache =
            new Dictionary<AbnormalBarProperties, MaterialBundle>();

        public HediffDef_Abnormal SourceAbnormalDef { get; set; }

        public float Progress { get; set; }

        public int StackIndex { get; set; }

        protected override void Tick()
        {
            base.Tick();
            SyncMapPositionToAttachment();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            AbnormalBarProperties props = def.GetModExtension<AbnormalBarProperties>() ?? AbnormalBarProperties.Default;
            UpdatePositionAndRotation();
            SyncMapPositionToAttachment();
            if (Find.UIRoot.HideMotes || Find.ScreenshotModeHandler.Active)
            {
                return;
            }

            GenDraw.FillableBarRequest request = default(GenDraw.FillableBarRequest);
            request.center = exactPosition;
            request.center.x += props.offsetX;
            int stackOffset = props.stackDownward ? -StackIndex : StackIndex;
            request.center.z += props.offsetZ + stackOffset * props.stackSpacing;
            request.center.y += props.altitudeOffset;
            request.size = props.size;
            request.fillPercent = Mathf.Clamp01(Progress);
            MaterialBundle materials = GetMaterials(props);
            request.filledMat = materials.fill;
            request.unfilledMat = materials.background;
            request.margin = props.margin;
            request.rotation = Rot4.North;
            GenDraw.DrawFillableBar(request);
            DrawIcon(request.center, props, materials.icon);
        }

        private void DrawIcon(Vector3 barCenter, AbnormalBarProperties props, Material iconMaterial)
        {
            if (iconMaterial == null || props.iconSize <= 0f)
            {
                return;
            }

            Vector3 iconCenter = barCenter;
            iconCenter.x -= props.size.x * 0.5f + props.iconGap + props.iconSize * 0.5f;
            Matrix4x4 matrix = default(Matrix4x4);
            matrix.SetTRS(iconCenter + Vector3.up * props.iconAltitudeOffset, Quaternion.identity, new Vector3(props.iconSize, 1f, props.iconSize));
            Graphics.DrawMesh(MeshPool.plane10, matrix, iconMaterial, 0);
        }

        private void SyncMapPositionToAttachment()
        {
            if (!Spawned || Map == null)
            {
                return;
            }

            IntVec3 cell = exactPosition.ToIntVec3();
            if (cell.InBounds(Map))
            {
                Position = cell;
            }
        }

        private static MaterialBundle GetMaterials(AbnormalBarProperties props)
        {
            if (MaterialCache.TryGetValue(props, out MaterialBundle materials))
            {
                return materials;
            }

            materials = new MaterialBundle
            {
                background = BuildMat(props.backgroundTexPath, props.backgroundShaderType, props.backgroundColor),
                fill = BuildMat(props.fillTexPath, props.fillShaderType, props.fillColor),
                icon = props.iconTexPath.NullOrEmpty()
                    ? null
                    : BuildMat(props.iconTexPath, props.iconShaderType, props.iconColor)
            };
            MaterialCache[props] = materials;
            return materials;
        }

        private static Material BuildMat(string texPath, ShaderTypeDef shaderType, Color color)
        {
            Shader shader = (shaderType ?? ShaderTypeDefOf.MetaOverlay).Shader;
            return !texPath.NullOrEmpty()
                ? MaterialPool.MatFrom(texPath, shader, color)
                : SolidColorMaterials.NewSolidColorMaterial(color, shader);
        }

        private sealed class MaterialBundle
        {
            public Material background;
            public Material fill;
            public Material icon;
        }
    }
}

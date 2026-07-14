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
        private static readonly Dictionary<string, Material> MaterialCache = new Dictionary<string, Material>();

        public HediffDef_Abnormal SourceAbnormalDef { get; set; }

        public float Progress { get; set; }

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

        private int GetStackIndex(AbnormalBarProperties props)
        {
            Pawn pawn = link1.Target.Thing as Pawn;
            if (pawn?.health?.hediffSet?.hediffs == null || SourceAbnormalDef == null)
            {
                return 0;
            }

            int index = 0;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (!(hediffs[i] is Hediff_Abnormal abnormal))
                {
                    continue;
                }

                if (abnormal.def == SourceAbnormalDef)
                {
                    return props.stackDownward ? -index : index;
                }

                index++;
            }

            return 0;
        }

        private void DrawIcon(Vector3 barCenter, AbnormalBarProperties props)
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

        private static Material BuildMat(string texPath, ShaderTypeDef shaderType, Color color)
        {
            string key = $"{texPath}|{shaderType?.defName}|{color}";
            if (MaterialCache.TryGetValue(key, out Material material))
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

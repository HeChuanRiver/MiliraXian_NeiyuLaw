using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Vfx;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things
{
    /// <summary>
    /// The Spring Flow field's visual: a cached texture marks which cells are in the field,
    /// the shader does edge detection and draws the boundary as a light curtain.
    ///
    /// The texture is lazily initialized: one pixel per cell, alpha = 1 if in field, 0 if not.
    /// A single quad covers the whole field area; the shader samples the texture and detects edges.
    /// </summary>
    [StaticConstructorOnStartup]
    internal sealed class SpringFlowVfx : IQingheVfxRenderer
    {
        private const float CurtainHeight = 0.4f;      // cells
        private const float LineSigma = 0.024f;        // 线的模糊尺度（格），FWHM ≈ 0.04 格
        private const float CurtainGain = 4.23f;       // 光幕增益（与预览一致）
        private static readonly Color SpringPink = new(1.0f, 0.62f, 0.80f);

        private static readonly ScreenBloomProperties Bloom = new()
        {
            group = DefDatabase<ScreenBloomGroupDef>.GetNamedSilentFail("MX_ScreenBloom_Glow"),
            tint = Color.white,
            intensity = 0.8f,
        };

        private static readonly int ParamsId = Shader.PropertyToID("_Params");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
        private static readonly int FieldCenterId = Shader.PropertyToID("_FieldCenter");
        private static readonly int FieldSizeId = Shader.PropertyToID("_FieldSize");

        private readonly HashSet<CompSpringFlowField> fields = new();
        private static readonly Dictionary<Map, SpringFlowVfx> Instances = new();

        private Map map;
        private Material boundaryMat;
        private Mesh quadMesh;

        public bool Active
        {
            get
            {
                if (map == null)
                {
                    return false;
                }
                fields.RemoveWhere(f => f == null || f.parent == null || f.parent.Destroyed);
                return fields.Count > 0;
            }
        }

        internal static void Add(Map map, CompSpringFlowField field)
        {
            if (map == null || field == null)
            {
                return;
            }
            Ensure(map)?.fields.Add(field);
        }

        internal static void Remove(Map map, CompSpringFlowField field)
        {
            if (map != null && Instances.TryGetValue(map, out SpringFlowVfx renderer))
            {
                renderer.fields.Remove(field);
            }
        }

        internal static SpringFlowVfx Ensure(Map map)
        {
            if (map == null || map.GetComponent<MapComponent_QingheVfx>() == null)
            {
                return null;
            }
            if (Instances.TryGetValue(map, out SpringFlowVfx existing) && existing.map == map)
            {
                return existing;
            }

            SpringFlowVfx renderer = new() { map = map };
            Instances[map] = renderer;
            MapComponent_QingheVfx.Register(map, renderer);
            return renderer;
        }

        public void Update(Map map)
        {
        }

        public void Draw(Map map)
        {
            if (Find.CurrentMap != map)
            {
                return;
            }

            fields.RemoveWhere(f => f == null || f.parent == null || f.parent.Destroyed);
            if (fields.Count == 0)
            {
                return;
            }

            boundaryMat ??= MatFor("MX_QH_SpringFlowBoundary");
            if (boundaryMat == null)
            {
                return;
            }

            quadMesh ??= MapComponent_QingheVfx.NewMesh("MX_QH_SpringFlowBoundary");
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;

            foreach (CompSpringFlowField field in fields)
            {
                float strength = field.VisualAlpha;
                if (strength <= 0.01f)
                {
                    continue;
                }

                // Get or create the cached field texture
                Texture2D fieldTex = GetFieldTexture(field);
                if (fieldTex == null)
                {
                    continue;
                }

                // Build a single quad covering the whole field area
                MapComponent_QingheVfx.ClearBuffers();
                AddFieldQuad(field);
                MapComponent_QingheVfx.Upload(quadMesh);

                block.Clear();
                block.SetColor(GlowId, SpringPink);
                block.SetVector(ParamsId, new Vector4(CurtainHeight, strength, LineSigma, CurtainGain));
                block.SetTexture(MainTexId, fieldTex);
                block.SetVector(FieldCenterId, new Vector4(field.parent.Position.x, field.parent.Position.z, 0f, 0f));
                block.SetFloat(FieldSizeId, fieldTex.width);

                ScreenBloom.Submit(Bloom, quadMesh, Matrix4x4.identity, boundaryMat, block);
                Graphics.DrawMesh(quadMesh, Matrix4x4.identity, boundaryMat, 0, null, 0, block);
            }
            block.Clear();
        }

        /// <summary>
        /// Get or create the cached field texture. One pixel per cell, alpha = 1 if in field, 0 if not.
        /// The texture is lazily initialized and cached per field.
        /// </summary>
        private Texture2D GetFieldTexture(CompSpringFlowField field)
        {
            // TODO: Cache the texture per field (or per radius) to avoid regenerating every frame
            int radius = Mathf.CeilToInt(field.CurrentRadius);
            int size = radius * 2 + 3;  // 15x15，留一圈黑边

            Texture2D tex = new(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Point;  // 着色器用 texelFetch 直接读格，不需要过滤
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            IntVec3 center = field.parent.Position;

            // Collect all cells in the field using vanilla GenRadial
            HashSet<IntVec3> fieldCells = new();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, field.CurrentRadius, true))
            {
                fieldCells.Add(cell);
            }

            // Fill the texture: alpha = 1 if in field, 0 if not
            for (int z = 0; z < size; z++)
            {
                for (int x = 0; x < size; x++)
                {
                    // x ∈ [0, size-1], 映射到 cell 坐标 x - size/2 ∈ [-size/2, size/2]
                    IntVec3 cell = center + new IntVec3(x - size / 2, 0, z - size / 2);
                    bool inField = fieldCells.Contains(cell);
                    pixels[z * size + x] = new Color(0f, 0f, 0f, inField ? 1f : 0f);
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();
            return tex;
        }

        /// <summary>
        /// Add a single quad covering the whole field area.
        /// The quad is centered at the field's position, with size = radius * 2 + 1 cells.
        /// vUv maps to the texture coordinates.
        /// </summary>
        private static void AddFieldQuad(CompSpringFlowField field)
        {
            IntVec3 center = field.parent.Position;
            int radius = Mathf.CeilToInt(field.CurrentRadius);
            int size = radius * 2 + 3;  // 15x15，留一圈黑边

            float y = AltitudeLayer.MoteLow.AltitudeFor();
            float halfSize = size / 2f;

            // Quad vertices: centered at field position, size = size x size cells
            Vector3 p0 = new(center.x - halfSize, y, center.z - halfSize);
            Vector3 p1 = new(center.x + halfSize, y, center.z - halfSize);
            Vector3 p2 = new(center.x + halfSize, y, center.z + halfSize + CurtainHeight);
            Vector3 p3 = new(center.x - halfSize, y, center.z + halfSize + CurtainHeight);

            int n = MapComponent_QingheVfx.VertexCount;
            MapComponent_QingheVfx.AddVertex(p0, new Vector2(0f, 0f), Color.white);
            MapComponent_QingheVfx.AddVertex(p1, new Vector2(1f, 0f), Color.white);
            MapComponent_QingheVfx.AddVertex(p2, new Vector2(1f, 1f), Color.white);
            MapComponent_QingheVfx.AddVertex(p3, new Vector2(0f, 1f), Color.white);
            MapComponent_QingheVfx.AddTriangle(n, n + 1, n + 2);
            MapComponent_QingheVfx.AddTriangle(n, n + 2, n + 3);
        }

        private static Material MatFor(string shaderDef)
        {
            Shader shader = DefDatabase<ShaderDef>.GetNamedSilentFail(shaderDef)?.Shader;
            if (shader == null)
            {
                Log.ErrorOnce("[MiliraXian] Spring Flow: missing shader '" + shaderDef + "'.",
                    GenText.StableStringHash(shaderDef));
                return null;
            }
            return MaterialPool.MatFrom(BaseContent.WhiteTex, shader, Color.white);
        }

        public void Dispose()
        {
            if (quadMesh != null)
            {
                Object.Destroy(quadMesh);
                quadMesh = null;
            }
            fields.Clear();
            if (map != null && Instances.TryGetValue(map, out SpringFlowVfx self) && ReferenceEquals(self, this))
            {
                Instances.Remove(map);
            }
            map = null;
        }
    }
}

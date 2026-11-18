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
        private static readonly int FieldCenterId = Shader.PropertyToID("_FieldCenter");
        private static readonly int FieldSizeId = Shader.PropertyToID("_FieldSize");
        private static readonly int FieldRadiusId = Shader.PropertyToID("_FieldRadius");

        private readonly HashSet<CompSpringFlowField> fields = new();
        private static readonly Dictionary<Map, SpringFlowVfx> Instances = new();

        private Map map;
        private Material boundaryMat;
        private readonly List<Mesh> quadMeshes = new();

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

            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            int meshIndex = 0;

            foreach (CompSpringFlowField field in fields)
            {
                float strength = field.VisualAlpha;
                if (strength <= 0.01f)
                {
                    continue;
                }

                // The shader derives the cell set from the radius threshold, so the field
                // only needs its centre and extent; no occupancy texture is built.
                int radius = Mathf.CeilToInt(field.CurrentRadius);
                int size = radius * 2 + 3;

                // One mesh per field: DrawMesh is deferred to the end of the frame, so
                // sharing a single mesh would leave only the last field's quad visible.
                Mesh mesh = MeshAt(meshIndex++);

                // Build a single quad covering the whole field area
                MapComponent_QingheVfx.ClearBuffers();
                AddFieldQuad(field);
                MapComponent_QingheVfx.Upload(mesh);

                block.Clear();
                block.SetColor(GlowId, SpringPink);
                block.SetVector(ParamsId, new Vector4(CurtainHeight, strength, LineSigma, CurtainGain));
                block.SetVector(FieldCenterId, new Vector4(field.parent.Position.x + 0.5f, field.parent.Position.z + 0.5f, 0f, 0f));
                block.SetFloat(FieldSizeId, size);
                block.SetFloat(FieldRadiusId, field.CurrentRadius);

                ScreenBloom.Submit(Bloom, mesh, Matrix4x4.identity, boundaryMat, block);
                Graphics.DrawMesh(mesh, Matrix4x4.identity, boundaryMat, 0, null, 0, block);
            }
            block.Clear();
        }

        private Mesh MeshAt(int index)
        {
            while (quadMeshes.Count <= index)
            {
                quadMeshes.Add(MapComponent_QingheVfx.NewMesh("MX_QH_SpringFlowBoundary"));
            }
            return quadMeshes[index];
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

            // IntVec3 是格子的左下角，世界坐标里的格子中心是 +0.5
            float cx = center.x + 0.5f;
            float cz = center.z + 0.5f;

            // Quad vertices: centered at field position, size = size x size cells
            Vector3 p0 = new(cx - halfSize, y, cz - halfSize);
            Vector3 p1 = new(cx + halfSize, y, cz - halfSize);
            Vector3 p2 = new(cx + halfSize, y, cz + halfSize + CurtainHeight);
            Vector3 p3 = new(cx - halfSize, y, cz + halfSize + CurtainHeight);

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
            foreach (Mesh mesh in quadMeshes)
            {
                if (mesh != null)
                {
                    Object.Destroy(mesh);
                }
            }
            quadMeshes.Clear();
            fields.Clear();
            if (map != null && Instances.TryGetValue(map, out SpringFlowVfx self) && ReferenceEquals(self, this))
            {
                Instances.Remove(map);
            }
            map = null;
        }
    }
}

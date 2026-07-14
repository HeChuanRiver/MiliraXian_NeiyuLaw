using UnityEngine;
using MiliraXian.Characters.QingHe.Vfx;
using MiliraXian.Characters.Vfx;
using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Mote
{
    public class Mote_SpiritBurstCurvedTrail : MoteDualAttached
    {
        private const float MinDrawDist = 0.18f;
        private const float MinSegLen = 0.01f;
        private const int MaxLayerMeshes = 7;

        private FleckDef lineDef;
        private FleckDef distortDef;
        private Material lineMat;
        private Material distortMat;
        private Color lineColor = Color.white;
        private Color distortColor = Color.white;
        private float width = 0.06f;
        private float distortWidth = 4.2f;
        private float distortAlpha = 0.68f;
        private float widthMul = 3.1f;
        private float segDensity = 8.8f;
        private int distortStep = 3;
        private float waveLen = 4.8f;
        private int animTicks = 90;
        private int growTicks = 18;
        private float alphaMul = 2.1f;
        private int afterLayers = 9;
        private int afterGap = 1;
        private float afterAlpha = 0.62f;
        private int minSegs = 28;
        private int maxSegs = 96;
        private float rnd;
        private Vector3 fixedStart;
        private Vector3 fixedEnd;
        private bool hasFixedEndpoints;
        private readonly Mesh[] lineMeshes = new Mesh[MaxLayerMeshes];
        private Mesh distortMesh;
        private readonly List<Vector3> meshVertices = new List<Vector3>(1280);
        private readonly List<Vector2> meshUvs = new List<Vector2>(1280);
        private readonly List<int> meshTriangles = new List<int>(1920);
        private readonly List<Vector3> distortVertices = new List<Vector3>(2560);
        private readonly List<Vector2> distortUvs = new List<Vector2>(2560);
        private readonly List<int> distortTriangles = new List<int>(3840);

        protected override bool EndOfLife
        {
            get
            {
                int ageTicks = Mathf.RoundToInt(AgeSecsPausable * 60f);
                return ageTicks >= Mathf.Max(1, animTicks);
            }
        }

        public void Setup(
            TargetInfo source,
            TargetInfo target,
            FleckDef line,
            FleckDef distort,
            float lineWidth,
            float distortWidthMul,
            float distortAlphaMul,
            float lineWidthMul,
            float densityMul,
            float waveLenCells,
            int totalTicks,
            int growTicksIn,
            float lineAlphaMul,
            int afterLayerCount,
            int afterGapTicks,
            float afterAlphaMul,
            int minSegments,
            int maxSegments,
            int distortStride)
        {
            Attach(source, target);
            fixedStart = source.CenterVector3;
            fixedEnd = target.CenterVector3;
            hasFixedEndpoints = source.IsValid && target.IsValid;
            lineDef = line;
            distortDef = distort;
            width = Mathf.Max(0.02f, lineWidth);
            distortWidth = Mathf.Clamp(distortWidthMul, 1f, 8f);
            distortAlpha = Mathf.Clamp01(distortAlphaMul);
            widthMul = Mathf.Clamp(lineWidthMul, 0.8f, 4f);
            segDensity = Mathf.Clamp(densityMul, 2f, 16f);
            distortStep = Mathf.Clamp(distortStride, 1, 10);
            waveLen = Mathf.Clamp(waveLenCells, 0.8f, 30f);
            animTicks = Mathf.Max(1, totalTicks);
            growTicks = Mathf.Clamp(growTicksIn, 1, Mathf.Max(1, animTicks - 1));
            alphaMul = Mathf.Clamp(lineAlphaMul, 0.25f, 3f);
            afterLayers = Mathf.Clamp(afterLayerCount, 0, 6);
            afterGap = Mathf.Clamp(afterGapTicks, 1, 8);
            afterAlpha = Mathf.Clamp(afterAlphaMul, 0.1f, 0.95f);
            minSegs = Mathf.Clamp(minSegments, 6, 72);
            maxSegs = Mathf.Clamp(maxSegments, minSegs, 160);
            rnd = Rand.Range(0f, 100000f);
            LoadMats();
        }

        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            LoadMats();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (paused || Find.UIRoot.HideMotes)
            {
                return;
            }

            if (lineMat == null)
            {
                LoadMats();
                if (lineMat == null)
                {
                    return;
                }
            }

            if (!hasFixedEndpoints && (!link1.Linked || !link2.Linked))
            {
                return;
            }

            Vector3 start;
            Vector3 end;
            if (hasFixedEndpoints)
            {
                start = fixedStart;
                end = fixedEnd;
                exactPosition = (start + end) * 0.5f;
                exactPosition.y = def.altitudeLayer.AltitudeFor();
            }
            else
            {
                UpdatePositionAndRotation();
                start = link1.LastDrawPos;
                end = link2.LastDrawPos;
            }

            float dist = (end - start).MagnitudeHorizontal();
            if (dist < MinDrawDist)
            {
                return;
            }

            if (!IntersectsView(start, end))
            {
                return;
            }

            int segs = Mathf.Clamp(Mathf.CeilToInt(dist * segDensity), minSegs, maxSegs);
            int ageNow = Mathf.Clamp(Mathf.RoundToInt(AgeSecsPausable * 60f), 0, Mathf.Max(1, animTicks));
            int grow = Mathf.Clamp(growTicks, 1, Mathf.Max(1, animTicks - 1));
            int settle = Mathf.Max(1, animTicks - grow);

            for (int layer = afterLayers; layer >= 0; layer--)
            {
                int age = ageNow - layer * afterGap;
                if (age < 0)
                {
                    continue;
                }

                float curve;
                float alpha;
                if (age <= grow)
                {
                    float t = Mathf.Clamp01(grow <= 1 ? 1f : age / (float)grow);
                    curve = Mathf.SmoothStep(0f, 1f, t);
                    alpha = Mathf.Lerp(0.9f, 1f, t);
                }
                else
                {
                    float t = Mathf.Clamp01((age - grow) / (float)settle);
                    curve = Mathf.SmoothStep(1f, 0.5f, t);
                    alpha = Mathf.Lerp(1f, 0f, t);
                }

                float layerAlpha = Mathf.Clamp01(alpha * alphaMul * Mathf.Pow(afterAlpha, layer));
                if (layerAlpha > 0.001f)
                {
                    DrawLayer(start, end, dist, segs, curve, layerAlpha, age, 1f + 0.035f * layer, layer == 0, layer);
                }
            }
        }

        private void DrawLayer(Vector3 start, Vector3 end, float dist, int segs, float curve, float alpha, int ageTicks, float layerWidthMul, bool drawDistort, int layerIndex)
        {
            float amp = Mathf.Clamp(dist * 0.092f, 0.1f, 0.78f) * curve;
            Vector3 dir = (end - start).normalized;
            Vector3 perp = Vector3.Cross(dir, Vector3.up);
            if (perp.sqrMagnitude < 1e-5f)
            {
                perp = Vector3.right;
            }
            perp.Normalize();

            float y = def.altitudeLayer.AltitudeFor();
            start.y = y;
            end.y = y;
            float ageSecs = Mathf.Max(0f, ageTicks / 60f);
            float segW = Mathf.Max(0.02f, width * widthMul * layerWidthMul);
            float bends = Mathf.Clamp(dist / Mathf.Max(0.8f, waveLen), 0.35f, 24f);
            Vector3 prev = start;
            Vector3 distortFrom = start;
            ClearGeometry(meshVertices, meshUvs, meshTriangles);
            if (drawDistort)
            {
                ClearGeometry(distortVertices, distortUvs, distortTriangles);
            }

            for (int i = 1; i <= segs; i++)
            {
                float t = i / (float)segs;
                Vector3 basePoint = Vector3.Lerp(start, end, t);
                float envelope = Mathf.Sin(Mathf.PI * t);
                float oscillation = Mathf.Sin(t * Mathf.PI * bends);
                Vector3 cur = basePoint + perp * (oscillation * amp * envelope);
                cur.y = y;
                AddSegmentQuad(prev, cur, segW, meshVertices, meshUvs, meshTriangles);

                if (drawDistort && distortMat != null && (i % distortStep == 0 || i == segs))
                {
                    Vector3 distortDelta = cur - distortFrom;
                    Vector3 distortPerp = Vector3.Cross(distortDelta.normalized, Vector3.up);
                    if (distortPerp.sqrMagnitude < 1e-5f)
                    {
                        distortPerp = perp;
                    }
                    distortPerp.Normalize();

                    float dw = Mathf.Max(segW * distortWidth * 0.62f, segW + 0.01f);
                    float offset = Mathf.Max(segW * 1.12f, dw * 0.48f);
                    Vector3 sideOffset = distortPerp * offset;
                    AddSegmentQuad(distortFrom + sideOffset, cur + sideOffset, dw, distortVertices, distortUvs, distortTriangles);
                    AddSegmentQuad(distortFrom - sideOffset, cur - sideOffset, dw, distortVertices, distortUvs, distortTriangles);
                    distortFrom = cur;
                }

                prev = cur;
            }

            UploadAndDraw(GetLineMesh(layerIndex), meshVertices, meshUvs, meshTriangles, lineMat, lineColor, alpha, ageSecs);
            if (drawDistort && distortMat != null && distortVertices.Count > 0)
            {
                if (distortMesh == null)
                {
                    distortMesh = CreateDynamicMesh("MX_SpiritBurstDistortionTrail");
                }

                UploadAndDraw(distortMesh, distortVertices, distortUvs, distortTriangles, distortMat, distortColor, alpha * distortAlpha, ageSecs);
            }
        }

        private static void AddSegmentQuad(
            Vector3 a,
            Vector3 b,
            float segmentWidth,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles)
        {
            Vector3 delta = b - a;
            float len = delta.MagnitudeHorizontal();
            if (len < MinSegLen)
            {
                return;
            }

            Vector3 direction = new Vector3(delta.x / len, 0f, delta.z / len);
            Vector3 side = new Vector3(-direction.z, 0f, direction.x) * (Mathf.Max(0.001f, segmentWidth) * 0.5f);
            Vector3 trim = direction * Mathf.Min(0.001f, len * 0.25f);
            a += trim;
            b -= trim;

            int vertexStart = vertices.Count;
            vertices.Add(a - side);
            vertices.Add(a + side);
            vertices.Add(b + side);
            vertices.Add(b - side);
            uvs.Add(new Vector2(0f, 0f));
            uvs.Add(new Vector2(1f, 0f));
            uvs.Add(new Vector2(1f, 1f));
            uvs.Add(new Vector2(0f, 1f));
            triangles.Add(vertexStart);
            triangles.Add(vertexStart + 1);
            triangles.Add(vertexStart + 2);
            triangles.Add(vertexStart);
            triangles.Add(vertexStart + 2);
            triangles.Add(vertexStart + 3);
        }

        private static void ClearGeometry(List<Vector3> vertices, List<Vector2> uvs, List<int> triangles)
        {
            vertices.Clear();
            uvs.Clear();
            triangles.Clear();
        }

        private Mesh GetLineMesh(int layerIndex)
        {
            layerIndex = Mathf.Clamp(layerIndex, 0, MaxLayerMeshes - 1);
            Mesh mesh = lineMeshes[layerIndex];
            if (mesh == null)
            {
                mesh = CreateDynamicMesh("MX_SpiritBurstTrail_" + layerIndex);
                lineMeshes[layerIndex] = mesh;
            }

            return mesh;
        }

        private static Mesh CreateDynamicMesh(string name)
        {
            Mesh mesh = new Mesh { name = name };
            mesh.MarkDynamic();
            return mesh;
        }

        private void UploadAndDraw(
            Mesh mesh,
            List<Vector3> vertices,
            List<Vector2> uvs,
            List<int> triangles,
            Material material,
            Color baseColor,
            float alpha,
            float ageSecs)
        {
            if (mesh == null || material == null || vertices.Count == 0)
            {
                return;
            }

            mesh.Clear(false);
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();

            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            baseColor.a *= Mathf.Clamp01(alpha);
            block.SetColor(ShaderPropertyIDs.Color, baseColor);
            block.SetFloat(ShaderPropertyIDs.AgeSecs, ageSecs);
            block.SetFloat(ShaderPropertyIDs.AgeSecsPausable, ageSecs);
            block.SetFloat(ShaderPropertyIDs.RandomPerObject, rnd);
            Graphics.DrawMesh(mesh, Matrix4x4.identity, material, 0, null, 0, block);
            block.Clear();
        }

        private static bool IntersectsView(Vector3 start, Vector3 end)
        {
            if (Find.CameraDriver == null)
            {
                return true;
            }

            CellRect view = Find.CameraDriver.CurrentViewRect.ExpandedBy(2);
            float minX = Mathf.Min(start.x, end.x);
            float maxX = Mathf.Max(start.x, end.x);
            float minZ = Mathf.Min(start.z, end.z);
            float maxZ = Mathf.Max(start.z, end.z);
            return maxX >= view.minX && minX <= view.maxX && maxZ >= view.minZ && minZ <= view.maxZ;
        }

        public override void DeSpawn(DestroyMode mode = DestroyMode.Vanish)
        {
            ReleaseMeshes();
            base.DeSpawn(mode);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            ReleaseMeshes();
            base.Destroy(mode);
        }

        private void ReleaseMeshes()
        {
            for (int i = 0; i < lineMeshes.Length; i++)
            {
                if (lineMeshes[i] != null)
                {
                    Object.Destroy(lineMeshes[i]);
                    lineMeshes[i] = null;
                }
            }

            if (distortMesh != null)
            {
                Object.Destroy(distortMesh);
                distortMesh = null;
            }
        }

        private void LoadMats()
        {
            lineMat = null;
            distortMat = null;
            lineColor = Color.white;
            distortColor = Color.white;

            if (lineDef?.graphicData?.Graphic != null)
            {
                Graphic graphic = lineDef.graphicData.Graphic;
                lineMat = graphic.MatSingle;
                lineColor = graphic.Color;
            }

            if (distortDef?.graphicData?.Graphic != null)
            {
                Graphic graphic = distortDef.graphicData.Graphic;
                distortMat = graphic.MatSingle;
                distortColor = graphic.Color;
            }
        }
    }
}


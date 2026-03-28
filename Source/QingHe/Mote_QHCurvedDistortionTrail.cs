using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class Mote_QHCurvedDistortionTrail : MoteDualAttached
    {
        private const float MinDrawDist = 0.18f;
        private const float MinSegLen = 0.01f;

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

            UpdatePositionAndRotation();
            if (!link1.Linked || !link2.Linked)
            {
                return;
            }

            Vector3 start = link1.LastDrawPos;
            Vector3 end = link2.LastDrawPos;
            float dist = (end - start).MagnitudeHorizontal();
            if (dist < MinDrawDist)
            {
                return;
            }

            int segs = Mathf.Clamp(Mathf.CeilToInt(dist * segDensity), minSegs, maxSegs);
            int ageNow = Mathf.Clamp(Mathf.RoundToInt(AgeSecsPausable * 60f), 0, Mathf.Max(1, animTicks));
            int total = Mathf.Max(1, animTicks);
            int grow = Mathf.Clamp(growTicks, 1, Mathf.Max(1, total - 1));
            int settle = Mathf.Max(1, total - grow);

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
                if (layerAlpha <= 0.001f)
                {
                    continue;
                }

                DrawLayer(start, end, dist, segs, curve, layerAlpha, age, 1f + 0.035f * layer, layer == 0);
            }
        }

        private void DrawLayer(Vector3 start, Vector3 end, float dist, int segs, float curve, float alpha, int ageTicks, float layerWidthMul, bool drawDistort)
        {
            float ampMax = Mathf.Clamp(dist * 0.092f, 0.1f, 0.78f);
            float amp = ampMax * curve;

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
            for (int i = 1; i <= segs; i++)
            {
                float t = i / (float)segs;
                Vector3 basePoint = Vector3.Lerp(start, end, t);
                float envelope = Mathf.Sin(Mathf.PI * t);
                float oscillation = Mathf.Sin(t * Mathf.PI * bends);
                Vector3 cur = basePoint + perp * (oscillation * amp * envelope);
                cur.y = y;

                DrawSeg(prev, cur, lineMat, lineColor, segW, alpha, ageSecs);

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
                    float da = alpha * distortAlpha;
                    float offset = Mathf.Max(segW * 1.12f, dw * 0.48f);
                    Vector3 sideOffset = distortPerp * offset;

                    DrawSeg(distortFrom + sideOffset, cur + sideOffset, distortMat, distortColor, dw, da, ageSecs);
                    DrawSeg(distortFrom - sideOffset, cur - sideOffset, distortMat, distortColor, dw, da, ageSecs);
                    distortFrom = cur;
                }

                prev = cur;
            }
        }

        private void DrawSeg(Vector3 a, Vector3 b, Material mat, Color baseColor, float w, float alpha, float ageSecs)
        {
            Vector3 delta = b - a;
            float len = delta.MagnitudeHorizontal();
            if (len < MinSegLen || mat == null)
            {
                return;
            }

            float rot = Mathf.Atan2(-delta.z, delta.x) * 57.29578f + 90f;
            float drawLen = Mathf.Max(MinSegLen, len - 0.002f);

            Vector3 pos = a + delta * 0.5f;
            pos.y = def.altitudeLayer.AltitudeFor();

            Matrix4x4 trs = default(Matrix4x4);
            trs.SetTRS(pos, Quaternion.AngleAxis(rot, Vector3.up), new Vector3(w, 1f, drawLen));

            MaterialPropertyBlock block = MX_QHRenderStatics.SharedPropertyBlock;
            block.Clear();

            Color c = baseColor;
            c.a *= Mathf.Clamp01(alpha);
            block.SetColor(ShaderPropertyIDs.Color, c);
            block.SetFloat(ShaderPropertyIDs.AgeSecs, ageSecs);
            block.SetFloat(ShaderPropertyIDs.AgeSecsPausable, ageSecs);
            block.SetFloat(ShaderPropertyIDs.RandomPerObject, rnd);

            Graphics.DrawMesh(MeshPool.plane10, trs, mat, 0, null, 0, block);
        }

        private void LoadMats()
        {
            lineMat = null;
            distortMat = null;
            lineColor = Color.white;
            distortColor = Color.white;

            if (lineDef?.graphicData?.Graphic != null)
            {
                Graphic g = lineDef.graphicData.Graphic;
                lineMat = g.MatSingle;
                lineColor = g.Color;
            }

            if (distortDef?.graphicData?.Graphic != null)
            {
                Graphic g = distortDef.graphicData.Graphic;
                distortMat = g.MatSingle;
                distortColor = g.Color;
            }
        }
    }
}

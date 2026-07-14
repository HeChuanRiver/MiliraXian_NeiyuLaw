using System.Collections.Generic;
using MiliraXian.Characters.Vfx;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Jobs;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Vfx
{
    public class MapComponent_QingheMirrorSlashVisuals : MapComponent
    {
        private const int MaxVisuals = 16;
        private const int DistortionMeshSegments = 32;
        private const int DistortionCapSegments = 8;
        private const float ArcCenterX = 0.5f;
        private const float ArcCenterY = 0.61f;
        private const float ArcStartDegrees = -155f;
        private const float ArcSweepDegrees = 130f;
        private const float DistortionInnerRadius = 0.32f;
        private const float DistortionOuterRadius = 0.62f;
        private const float DistortionAnglePaddingDegrees = 4f;
        private static readonly int EffectTimeProperty = Shader.PropertyToID("_EffectTime");
        private static readonly int RevealDurationProperty = Shader.PropertyToID("_RevealDuration");
        private static readonly int HoldDurationProperty = Shader.PropertyToID("_HoldDuration");
        private static readonly int FadeDurationProperty = Shader.PropertyToID("_FadeDuration");
        private static readonly int ReverseProperty = Shader.PropertyToID("_Reverse");
        private static readonly int OpacityProperty = Shader.PropertyToID("_Opacity");
        private static readonly int HeadWidthProperty = Shader.PropertyToID("_HeadWidth");
        private static readonly int HeadIntensityProperty = Shader.PropertyToID("_HeadIntensity");
        private static readonly int AdditiveIntensityProperty = Shader.PropertyToID("_AdditiveIntensity");
        private static readonly int ColorProperty = Shader.PropertyToID("_Color");
        private static readonly int DistortionTexProperty = Shader.PropertyToID("_DistortionTex");
        private static readonly int DistortionStrengthProperty = Shader.PropertyToID("_distortionIntensity");
        private static readonly int DistortionScaleProperty = Shader.PropertyToID("_distortionScale");
        private static readonly int DistortionScrollSpeedProperty = Shader.PropertyToID("_distortionScrollSpeed");
        private static readonly int WorldSpaceDistortionProperty = Shader.PropertyToID("_wordSpaceDistortionToggle");

        private static Material slashMaterial;
        private static Material distortionMaterial;
        private static string loadedTexPath;
        private static bool warnedMissingShader;

        private readonly List<MirrorSlashVisual> visuals = new List<MirrorSlashVisual>();

        public MapComponent_QingheMirrorSlashVisuals(Map map) : base(map)
        {
        }

        public void AddSlash(
            Vector3 origin,
            Vector3 forward,
            float radius,
            string texPath,
            float revealSeconds,
            float holdSeconds,
            float fadeSeconds,
            float drawSizeFactor,
            float forwardOffsetFactor,
            float angleOffsetDegrees,
            float headWidth,
            float headIntensity,
            float additiveIntensity,
            float distortionStrength,
            float distortionScale,
            float distortionScrollX,
            float distortionScrollY,
            float distortionOpacity,
            bool reverse)
        {
            if (map == null || forward.sqrMagnitude < 0.001f || texPath.NullOrEmpty())
            {
                return;
            }

            while (visuals.Count >= MaxVisuals)
            {
                RemoveVisualAt(0);
            }

            forward.y = 0f;
            forward.Normalize();
            visuals.Add(new MirrorSlashVisual
            {
                origin = origin,
                forward = forward,
                radius = Mathf.Max(0.5f, radius),
                texPath = texPath,
                startRealTime = Time.realtimeSinceStartup,
                revealSeconds = Mathf.Max(0.001f, revealSeconds),
                holdSeconds = Mathf.Max(0f, holdSeconds),
                fadeSeconds = Mathf.Max(0.001f, fadeSeconds),
                drawSizeFactor = Mathf.Max(0.1f, drawSizeFactor),
                forwardOffsetFactor = forwardOffsetFactor,
                angleOffsetDegrees = angleOffsetDegrees,
                headWidth = Mathf.Max(0f, headWidth),
                headIntensity = Mathf.Max(0f, headIntensity),
                additiveIntensity = Mathf.Max(0f, additiveIntensity),
                distortionStrength = Mathf.Max(0f, distortionStrength),
                distortionScale = Mathf.Max(0.001f, distortionScale),
                distortionScroll = new Vector2(distortionScrollX, distortionScrollY),
                distortionOpacity = Mathf.Clamp01(distortionOpacity),
                reverse = reverse
            });
        }

        public override void MapComponentDraw()
        {
            base.MapComponentDraw();
            if (visuals.Count == 0)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            for (int i = visuals.Count - 1; i >= 0; i--)
            {
                MirrorSlashVisual visual = visuals[i];
                float age = now - visual.startRealTime;
                float totalSeconds = visual.revealSeconds + visual.holdSeconds + visual.fadeSeconds;
                if (age < 0)
                {
                    continue;
                }
                if (age > totalSeconds)
                {
                    RemoveVisualAt(i);
                    continue;
                }

                DrawSlash(visual, now);
            }
        }

        private static void DrawSlash(MirrorSlashVisual visual, float now)
        {
            Material material = ResolveMaterial(visual.texPath);
            if (material == null)
            {
                return;
            }

            Vector3 center = visual.origin + visual.forward * (visual.radius * visual.forwardOffsetFactor);
            center.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            float angle = Mathf.Atan2(visual.forward.x, visual.forward.z) * Mathf.Rad2Deg
                + 180f
                + visual.angleOffsetDegrees;
            float drawSize = visual.radius * visual.drawSizeFactor;
            Matrix4x4 matrix = Matrix4x4.TRS(
                center,
                Quaternion.AngleAxis(angle, Vector3.up),
                new Vector3(drawSize, 1f, drawSize));

            float effectTime = Mathf.Max(0f, now - visual.startRealTime);
            DrawDistortion(visual, matrix, effectTime);

            material.SetFloat(EffectTimeProperty, effectTime);
            material.SetFloat(RevealDurationProperty, visual.revealSeconds);
            material.SetFloat(HoldDurationProperty, visual.holdSeconds);
            material.SetFloat(FadeDurationProperty, visual.fadeSeconds);
            material.SetFloat(ReverseProperty, visual.reverse ? 1f : 0f);
            material.SetFloat(OpacityProperty, 1f);
            material.SetFloat(HeadWidthProperty, visual.headWidth);
            material.SetFloat(HeadIntensityProperty, visual.headIntensity);
            material.SetFloat(AdditiveIntensityProperty, visual.additiveIntensity);

            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetFloat(EffectTimeProperty, effectTime);
            block.SetFloat(RevealDurationProperty, visual.revealSeconds);
            block.SetFloat(HoldDurationProperty, visual.holdSeconds);
            block.SetFloat(FadeDurationProperty, visual.fadeSeconds);
            block.SetFloat(ReverseProperty, visual.reverse ? 1f : 0f);
            block.SetFloat(OpacityProperty, 1f);
            block.SetFloat(HeadWidthProperty, visual.headWidth);
            block.SetFloat(HeadIntensityProperty, visual.headIntensity);
            block.SetFloat(AdditiveIntensityProperty, visual.additiveIntensity);
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0, null, 0, block);
            block.Clear();
        }

        private static void DrawDistortion(MirrorSlashVisual visual, Matrix4x4 matrix, float effectTime)
        {
            Material material = ResolveDistortionMaterial();
            if (material == null || visual.distortionStrength <= 0f || visual.distortionOpacity <= 0f)
            {
                return;
            }

            float revealProgress = Smooth01(effectTime / visual.revealSeconds);
            if (revealProgress <= 0.001f)
            {
                return;
            }

            float fadeProgress = (effectTime - visual.revealSeconds - visual.holdSeconds) / visual.fadeSeconds;
            float lifetimeOpacity = 1f - Smooth01(fadeProgress);
            if (lifetimeOpacity <= 0.001f)
            {
                return;
            }

            Mesh mesh = UpdateDistortionMesh(visual, revealProgress);
            float effectiveOpacity = visual.distortionOpacity * lifetimeOpacity;
            float distortionIntensity = visual.distortionStrength * effectiveOpacity;
            DrawDistortionSubmesh(visual, matrix, material, mesh, 0, -distortionIntensity, effectiveOpacity);
            DrawDistortionSubmesh(visual, matrix, material, mesh, 1, distortionIntensity, effectiveOpacity);
        }

        private static void DrawDistortionSubmesh(
            MirrorSlashVisual visual,
            Matrix4x4 matrix,
            Material material,
            Mesh mesh,
            int submeshIndex,
            float distortionIntensity,
            float opacity)
        {
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetColor(ColorProperty, new Color(1f, 1f, 1f, opacity));
            block.SetTexture(DistortionTexProperty, TexGame.RippleTex);
            block.SetFloat(DistortionStrengthProperty, distortionIntensity);
            block.SetFloat(DistortionScaleProperty, visual.distortionScale);
            block.SetVector(
                DistortionScrollSpeedProperty,
                new Vector4(visual.distortionScroll.x, visual.distortionScroll.y, 0f, 0f));
            block.SetFloat(WorldSpaceDistortionProperty, 0f);
            Graphics.DrawMesh(mesh, matrix, material, 0, null, submeshIndex, block);
            block.Clear();
        }

        private static float Smooth01(float value)
        {
            value = Mathf.Clamp01(value);
            return value * value * (3f - 2f * value);
        }

        private static Mesh UpdateDistortionMesh(MirrorSlashVisual visual, float progress)
        {
            if (visual.distortionMesh == null)
            {
                visual.distortionMesh = new Mesh
                {
                    name = "MX_QH_MirrorSlashDistortion"
                };
                visual.distortionMesh.MarkDynamic();
                int ribbonVertexCount = (DistortionMeshSegments + 1) * 3;
                int capVertexCount = DistortionCapSegments + 2;
                visual.distortionVertices = new Vector3[ribbonVertexCount + capVertexCount * 2];
                visual.distortionUvs = new Vector2[visual.distortionVertices.Length];
                int ribbonTriangleCount = DistortionMeshSegments * 6;
                int capTrianglesPerEndPerHalf = DistortionCapSegments / 2 * 3;
                visual.distortionInnerTriangles = new int[ribbonTriangleCount + capTrianglesPerEndPerHalf * 2];
                visual.distortionOuterTriangles = new int[ribbonTriangleCount + capTrianglesPerEndPerHalf * 2];
            }

            Vector2 arcCenter = new Vector2(ArcCenterX, ArcCenterY);
            float startDegrees = visual.reverse
                ? ArcStartDegrees + ArcSweepDegrees + DistortionAnglePaddingDegrees
                : ArcStartDegrees - DistortionAnglePaddingDegrees;
            float sweepDegrees = visual.reverse
                ? -(ArcSweepDegrees * progress + DistortionAnglePaddingDegrees * 2f)
                : ArcSweepDegrees * progress + DistortionAnglePaddingDegrees * 2f;
            for (int i = 0; i <= DistortionMeshSegments; i++)
            {
                float angle = startDegrees + sweepDegrees * i / DistortionMeshSegments;
                float radians = angle * Mathf.Deg2Rad;
                Vector2 direction = new Vector2(Mathf.Cos(radians), Mathf.Sin(radians));
                float maxRadius = DistanceToUnitSquare(arcCenter, direction);
                Vector2 innerUv = arcCenter + direction * Mathf.Min(DistortionInnerRadius, maxRadius);
                Vector2 outerUv = arcCenter + direction * Mathf.Min(DistortionOuterRadius, maxRadius);
                Vector2 middleUv = (innerUv + outerUv) * 0.5f;
                int vertexIndex = i * 3;
                visual.distortionVertices[vertexIndex] = UvToVertex(innerUv);
                visual.distortionVertices[vertexIndex + 1] = UvToVertex(middleUv);
                visual.distortionVertices[vertexIndex + 2] = UvToVertex(outerUv);
                visual.distortionUvs[vertexIndex] = innerUv;
                visual.distortionUvs[vertexIndex + 1] = middleUv;
                visual.distortionUvs[vertexIndex + 2] = outerUv;
            }

            bool positiveSweep = sweepDegrees >= 0f;
            for (int i = 0; i < DistortionMeshSegments; i++)
            {
                int inner = i * 3;
                int middle = inner + 1;
                int outer = inner + 2;
                int nextInner = inner + 3;
                int nextMiddle = inner + 4;
                int nextOuter = inner + 5;
                int triangleIndex = i * 6;
                WriteRibbonQuad(
                    visual.distortionInnerTriangles,
                    triangleIndex,
                    inner,
                    middle,
                    nextInner,
                    nextMiddle,
                    positiveSweep);
                WriteRibbonQuad(
                    visual.distortionOuterTriangles,
                    triangleIndex,
                    middle,
                    outer,
                    nextMiddle,
                    nextOuter,
                    positiveSweep);
            }

            int capVertexOffset = (DistortionMeshSegments + 1) * 3;
            int capTriangleOffset = DistortionMeshSegments * 6;
            Vector2 startInner = visual.distortionUvs[0];
            Vector2 startOuter = visual.distortionUvs[2];
            int endVertexIndex = DistortionMeshSegments * 3;
            Vector2 endInner = visual.distortionUvs[endVertexIndex];
            Vector2 endOuter = visual.distortionUvs[endVertexIndex + 2];
            Vector2 startDirection = (startOuter - startInner).normalized;
            Vector2 endDirection = (endOuter - endInner).normalized;
            float sweepSign = positiveSweep ? 1f : -1f;
            Vector2 startTangent = new Vector2(-startDirection.y, startDirection.x) * sweepSign;
            Vector2 endTangent = new Vector2(-endDirection.y, endDirection.x) * sweepSign;
            WriteRoundedCap(
                visual,
                capVertexOffset,
                capTriangleOffset,
                capTriangleOffset,
                startInner,
                startOuter,
                -startTangent);
            int capHalfTriangleCount = DistortionCapSegments / 2 * 3;
            WriteRoundedCap(
                visual,
                capVertexOffset + DistortionCapSegments + 2,
                capTriangleOffset + capHalfTriangleCount,
                capTriangleOffset + capHalfTriangleCount,
                endInner,
                endOuter,
                endTangent);

            visual.distortionMesh.Clear();
            visual.distortionMesh.vertices = visual.distortionVertices;
            visual.distortionMesh.uv = visual.distortionUvs;
            visual.distortionMesh.subMeshCount = 2;
            visual.distortionMesh.SetTriangles(visual.distortionInnerTriangles, 0);
            visual.distortionMesh.SetTriangles(visual.distortionOuterTriangles, 1);
            visual.distortionMesh.RecalculateBounds();
            return visual.distortionMesh;
        }

        private static void WriteRibbonQuad(
            int[] triangles,
            int triangleIndex,
            int inner,
            int outer,
            int nextInner,
            int nextOuter,
            bool positiveSweep)
        {
            triangles[triangleIndex] = inner;
            triangles[triangleIndex + 1] = positiveSweep ? nextInner : nextOuter;
            triangles[triangleIndex + 2] = positiveSweep ? nextOuter : nextInner;
            triangles[triangleIndex + 3] = inner;
            triangles[triangleIndex + 4] = positiveSweep ? nextOuter : outer;
            triangles[triangleIndex + 5] = positiveSweep ? outer : nextOuter;
        }

        private static void WriteRoundedCap(
            MirrorSlashVisual visual,
            int vertexOffset,
            int innerTriangleOffset,
            int outerTriangleOffset,
            Vector2 inner,
            Vector2 outer,
            Vector2 extensionDirection)
        {
            Vector2 center = (inner + outer) * 0.5f;
            Vector2 radial = (outer - inner).normalized;
            float radius = Vector2.Distance(inner, outer) * 0.5f;
            visual.distortionVertices[vertexOffset] = UvToVertex(center);
            visual.distortionUvs[vertexOffset] = center;

            for (int i = 0; i <= DistortionCapSegments; i++)
            {
                float angle = Mathf.PI * (1f - i / (float)DistortionCapSegments);
                Vector2 uv = center + radius * (
                    Mathf.Cos(angle) * radial
                    + Mathf.Sin(angle) * extensionDirection);
                int index = vertexOffset + i + 1;
                visual.distortionVertices[index] = UvToVertex(uv);
                visual.distortionUvs[index] = uv;
            }

            for (int i = 0; i < DistortionCapSegments; i++)
            {
                int current = vertexOffset + i + 1;
                int next = current + 1;
                Vector3 fromCenter = visual.distortionVertices[current] - visual.distortionVertices[vertexOffset];
                Vector3 toNext = visual.distortionVertices[next] - visual.distortionVertices[vertexOffset];
                bool upward = Vector3.Cross(fromCenter, toNext).y >= 0f;
                bool innerHalf = i < DistortionCapSegments / 2;
                int localTriangle = innerHalf ? i : i - DistortionCapSegments / 2;
                int index = (innerHalf ? innerTriangleOffset : outerTriangleOffset) + localTriangle * 3;
                int[] triangles = innerHalf
                    ? visual.distortionInnerTriangles
                    : visual.distortionOuterTriangles;
                triangles[index] = vertexOffset;
                triangles[index + 1] = upward ? current : next;
                triangles[index + 2] = upward ? next : current;
            }
        }

        private static Vector3 UvToVertex(Vector2 uv)
        {
            return new Vector3(uv.x - 0.5f, 0f, uv.y - 0.5f);
        }

        private static float DistanceToUnitSquare(Vector2 origin, Vector2 direction)
        {
            float distanceX = direction.x > 0f
                ? (1f - origin.x) / direction.x
                : direction.x < 0f ? -origin.x / direction.x : float.PositiveInfinity;
            float distanceY = direction.y > 0f
                ? (1f - origin.y) / direction.y
                : direction.y < 0f ? -origin.y / direction.y : float.PositiveInfinity;
            return Mathf.Min(distanceX, distanceY);
        }

        private static Material ResolveMaterial(string texPath)
        {
            if (slashMaterial != null && loadedTexPath == texPath)
            {
                return slashMaterial;
            }

            Shader shader = MX_QHDefOf.MX_QH_MirrorSlash?.Shader;
            if (shader == null || shader == ShaderDatabase.DefaultShader)
            {
                if (!warnedMissingShader)
                {
                    warnedMissingShader = true;
                    Log.Warning("[MiliraXian] Mirror Slash shader could not be loaded from the asset bundle.");
                }
                return null;
            }

            Texture2D texture = ContentFinder<Texture2D>.Get(texPath, reportFailure: false);
            if (texture == null)
            {
                return null;
            }

            loadedTexPath = texPath;
            slashMaterial = MaterialPool.MatFrom(texture, shader, Color.white);
            return slashMaterial;
        }

        private static Material ResolveDistortionMaterial()
        {
            if (distortionMaterial != null)
            {
                return distortionMaterial;
            }

            Texture2D texture = ContentFinder<Texture2D>.Get("Things/Mote/Black", reportFailure: false);
            if (texture == null || ShaderDatabase.MoteGlowDistortBG == null)
            {
                return null;
            }

            distortionMaterial = MaterialPool.MatFrom(texture, ShaderDatabase.MoteGlowDistortBG, Color.white);
            return distortionMaterial;
        }

        private void RemoveVisualAt(int index)
        {
            MirrorSlashVisual visual = visuals[index];
            if (visual.distortionMesh != null)
            {
                UnityEngine.Object.Destroy(visual.distortionMesh);
            }
            visuals.RemoveAt(index);
        }

        private sealed class MirrorSlashVisual
        {
            public Vector3 origin;
            public Vector3 forward;
            public float radius;
            public string texPath;
            public float startRealTime;
            public float revealSeconds;
            public float holdSeconds;
            public float fadeSeconds;
            public float drawSizeFactor;
            public float forwardOffsetFactor;
            public float angleOffsetDegrees;
            public float headWidth;
            public float headIntensity;
            public float additiveIntensity;
            public float distortionStrength;
            public float distortionScale;
            public Vector2 distortionScroll;
            public float distortionOpacity;
            public bool reverse;
            public Mesh distortionMesh;
            public Vector3[] distortionVertices;
            public Vector2[] distortionUvs;
            public int[] distortionInnerTriangles;
            public int[] distortionOuterTriangles;
        }
    }
}

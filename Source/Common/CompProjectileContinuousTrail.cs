using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public interface IProjectileVisualPositionProvider
    {
        Vector3 VisualTrailPosition { get; }
    }

    public class CompProperties_ProjectileContinuousTrail : CompProperties
    {
        public string trailTexPath = "UI/Overlays/ThingLine";
        public int trailMaxPoints = 18;
        public float trailMinPointDistance = 0.10f;
        public int trailSmoothSubdivisions = 3;
        public float trailWidthStart = 0.16f;
        public float trailWidthEnd = 0.05f;
        public float trailAlpha = 0.62f;
        public float trailAltitudeOffset = 0.7f;
        public bool useProjectileGraphicColor = true;
        public bool useGlowShader;
        public Color trailColor = Color.white;

        public CompProperties_ProjectileContinuousTrail()
        {
            compClass = typeof(CompProjectileContinuousTrail);
        }
    }

    [StaticConstructorOnStartup]
    public class CompProjectileContinuousTrail : ThingComp
    {
        private static readonly MaterialPropertyBlock TrailPropertyBlock = new();

        private Vector3[] trailPoints;
        private int trailStart;
        private int trailCount;
        private bool initialized;

        private Mesh trailMesh;
        private readonly List<Vector3> meshVertices = new(256);
        private readonly List<Vector2> meshUvs = new(256);
        private readonly List<Color32> meshColors = new(256);
        private readonly List<int> meshTriangles = new(384);

        private Material trailMaterial;
        private string cachedTrailTexPath;
        private bool cachedUseGlowShader;

        private CompProperties_ProjectileContinuousTrail Props => (CompProperties_ProjectileContinuousTrail)props;
        private Projectile ProjectileParent => parent as Projectile;

        public override void PostExposeData()
        {
            base.PostExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ResetTrail();
                ResetMaterialCache();
                DestroyMesh();
            }
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                ResetTrail();
            }
        }

        public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
        {
            base.PostDeSpawn(map, mode);
            ResetTrail();
            DestroyMesh();
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            ResetTrail();
            DestroyMesh();
        }

        public override void CompTick()
        {
            Projectile projectile = ProjectileParent;
            if (Props == null || projectile == null || !parent.Spawned || parent.Map == null)
            {
                return;
            }

            EnsureTrailBuffer();
            Vector3 point = ResolveTrailPosition(projectile);
            if (!initialized)
            {
                if (projectile.Launcher == null)
                {
                    return;
                }

                initialized = true;
                AddTrailPoint(point);
                return;
            }

            RecordTrailPoint(point);
        }

        public override void PostDraw()
        {
            base.PostDraw();

            Projectile projectile = ProjectileParent;
            if (Props == null || projectile == null || trailCount < 2 || projectile.Map == null)
            {
                return;
            }

            if (Find.CameraDriver != null
                && !Find.CameraDriver.CurrentViewRect.ExpandedBy(2).Contains(projectile.Position))
            {
                return;
            }

            DrawContinuousTrail(projectile);
        }

        private void EnsureTrailBuffer()
        {
            int capacity = Mathf.Max(4, Props.trailMaxPoints);
            if (trailPoints != null && trailPoints.Length == capacity)
            {
                return;
            }

            trailPoints = new Vector3[capacity];
            trailStart = 0;
            trailCount = 0;

            int subdivisions = Mathf.Clamp(Props.trailSmoothSubdivisions, 1, 6);
            int maxSegments = (capacity - 1) * subdivisions;
            EnsureListCapacity(meshVertices, maxSegments * 4);
            EnsureListCapacity(meshUvs, maxSegments * 4);
            EnsureListCapacity(meshColors, maxSegments * 4);
            EnsureListCapacity(meshTriangles, maxSegments * 6);
        }

        private void RecordTrailPoint(Vector3 point)
        {
            float minDist = Mathf.Max(0.01f, Props.trailMinPointDistance);
            if (trailCount > 0)
            {
                Vector3 delta = point - GetTrailPoint(trailCount - 1);
                if (delta.x * delta.x + delta.z * delta.z < minDist * minDist)
                {
                    return;
                }
            }

            AddTrailPoint(point);
        }

        private void AddTrailPoint(Vector3 point)
        {
            if (trailPoints == null || trailPoints.Length == 0)
            {
                return;
            }

            if (trailCount < trailPoints.Length)
            {
                int index = (trailStart + trailCount) % trailPoints.Length;
                trailPoints[index] = point;
                trailCount++;
                return;
            }

            trailPoints[trailStart] = point;
            trailStart = (trailStart + 1) % trailPoints.Length;
        }

        private Vector3 GetTrailPoint(int index)
        {
            return trailPoints[(trailStart + index) % trailPoints.Length];
        }

        private void DrawContinuousTrail(Projectile projectile)
        {
            float baseAlpha = Mathf.Clamp01(Props.trailAlpha);
            if (baseAlpha <= 0.001f || !BuildTrailMesh())
            {
                return;
            }

            Material material = GetTrailMaterial();
            if (material == null)
            {
                return;
            }

            Color color = Props.useProjectileGraphicColor && projectile.def.graphicData != null
                ? projectile.def.graphicData.color
                : Props.trailColor;
            color.a = baseAlpha;
            TrailPropertyBlock.SetColor(ShaderPropertyIDs.Color, color);
            Graphics.DrawMesh(trailMesh, Matrix4x4.identity, material, 0, null, 0, TrailPropertyBlock);
        }

        private bool BuildTrailMesh()
        {
            meshVertices.Clear();
            meshUvs.Clear();
            meshColors.Clear();
            meshTriangles.Clear();

            int subdivisions = Mathf.Clamp(Props.trailSmoothSubdivisions, 1, 6);
            float widthStart = Mathf.Max(0.01f, Props.trailWidthStart);
            float widthEnd = Mathf.Max(0.005f, Props.trailWidthEnd);
            float altitudeOffset = Mathf.Max(0f, Props.trailAltitudeOffset);
            Vector3 altitude = Altitudes.AltIncVect * altitudeOffset;

            for (int i = 0; i < trailCount - 1; i++)
            {
                Vector3 p0 = GetTrailPoint(Mathf.Max(i - 1, 0));
                Vector3 p1 = GetTrailPoint(i);
                Vector3 p2 = GetTrailPoint(i + 1);
                Vector3 p3 = GetTrailPoint(Mathf.Min(i + 2, trailCount - 1));

                Vector3 previous = p1 + altitude;
                float previousProgress = i / Mathf.Max(1f, trailCount - 1f);
                for (int subdivision = 1; subdivision <= subdivisions; subdivision++)
                {
                    float t = subdivision / (float)subdivisions;
                    Vector3 current = CatmullRom(p0, p1, p2, p3, t) + altitude;
                    float progress = (i + t) / Mathf.Max(1f, trailCount - 1f);
                    AddRibbonSegment(
                        previous,
                        current,
                        Mathf.Lerp(widthStart, widthEnd, previousProgress),
                        Mathf.Lerp(widthStart, widthEnd, progress),
                        previousProgress,
                        progress);
                    previous = current;
                    previousProgress = progress;
                }
            }

            if (meshVertices.Count == 0)
            {
                return false;
            }

            if (trailMesh == null)
            {
                trailMesh = new Mesh
                {
                    name = "MX_ProjectileTrail_" + (parent?.thingIDNumber ?? 0)
                };
                trailMesh.MarkDynamic();
            }

            trailMesh.Clear(false);
            trailMesh.SetVertices(meshVertices);
            trailMesh.SetUVs(0, meshUvs);
            trailMesh.SetColors(meshColors);
            trailMesh.SetTriangles(meshTriangles, 0, false);
            trailMesh.RecalculateBounds();
            return true;
        }

        private void AddRibbonSegment(
            Vector3 start,
            Vector3 end,
            float startWidth,
            float endWidth,
            float startAlpha,
            float endAlpha)
        {
            Vector3 direction = (end - start).Yto0();
            if (direction.sqrMagnitude < 0.000001f)
            {
                return;
            }

            direction.Normalize();
            Vector3 perpendicular = new(-direction.z, 0f, direction.x);
            Vector3 startOffset = perpendicular * (startWidth * 0.5f);
            Vector3 endOffset = perpendicular * (endWidth * 0.5f);
            int vertexStart = meshVertices.Count;

            meshVertices.Add(start - startOffset);
            meshVertices.Add(start + startOffset);
            meshVertices.Add(end - endOffset);
            meshVertices.Add(end + endOffset);

            meshUvs.Add(new Vector2(0f, 0f));
            meshUvs.Add(new Vector2(0f, 1f));
            meshUvs.Add(new Vector2(1f, 0f));
            meshUvs.Add(new Vector2(1f, 1f));

            byte startByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(startAlpha) * 255f);
            byte endByte = (byte)Mathf.RoundToInt(Mathf.Clamp01(endAlpha) * 255f);
            meshColors.Add(new Color32(255, 255, 255, startByte));
            meshColors.Add(new Color32(255, 255, 255, startByte));
            meshColors.Add(new Color32(255, 255, 255, endByte));
            meshColors.Add(new Color32(255, 255, 255, endByte));

            meshTriangles.Add(vertexStart);
            meshTriangles.Add(vertexStart + 2);
            meshTriangles.Add(vertexStart + 1);
            meshTriangles.Add(vertexStart + 2);
            meshTriangles.Add(vertexStart + 3);
            meshTriangles.Add(vertexStart + 1);
        }

        private Material GetTrailMaterial()
        {
            string texturePath = Props.trailTexPath.NullOrEmpty() ? "UI/Overlays/ThingLine" : Props.trailTexPath;
            if (trailMaterial != null
                && cachedTrailTexPath == texturePath
                && cachedUseGlowShader == Props.useGlowShader)
            {
                return trailMaterial;
            }

            cachedTrailTexPath = texturePath;
            cachedUseGlowShader = Props.useGlowShader;
            trailMaterial = MaterialPool.MatFrom(
                texturePath,
                Props.useGlowShader ? ShaderDatabase.MoteGlow : ShaderDatabase.Transparent,
                Color.white);
            return trailMaterial;
        }

        private void ResetTrail()
        {
            initialized = false;
            trailStart = 0;
            trailCount = 0;
            meshVertices.Clear();
            meshUvs.Clear();
            meshColors.Clear();
            meshTriangles.Clear();
        }

        private void ResetMaterialCache()
        {
            trailMaterial = null;
            cachedTrailTexPath = null;
            cachedUseGlowShader = false;
        }

        private void DestroyMesh()
        {
            if (trailMesh == null)
            {
                return;
            }

            Object.Destroy(trailMesh);
            trailMesh = null;
        }

        private static void EnsureListCapacity<T>(List<T> list, int capacity)
        {
            if (list.Capacity < capacity)
            {
                list.Capacity = capacity;
            }
        }

        private static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
        {
            float t2 = t * t;
            float t3 = t2 * t;
            return 0.5f * ((2f * p1)
                + (-p0 + p2) * t
                + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
                + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
        }

        private static Vector3 ResolveTrailPosition(Projectile projectile)
        {
            IProjectileVisualPositionProvider provider = projectile as IProjectileVisualPositionProvider;
            return provider?.VisualTrailPosition ?? projectile.ExactPosition;
        }
    }
}

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

    public class CompProjectileContinuousTrail : ThingComp
    {
        private const int FadeMaterialSteps = 24;
        private readonly List<Vector3> trailPoints = new List<Vector3>();
        private readonly Material[] fadeMaterials = new Material[FadeMaterialSteps];
        private Color cachedTrailBaseColor = Color.clear;
        private string cachedTrailTexPath;
        private bool cachedUseGlowShader;
        private bool initialized;

        private CompProperties_ProjectileContinuousTrail Props => (CompProperties_ProjectileContinuousTrail)props;
        private Projectile ProjectileParent => parent as Projectile;

        public override void PostExposeData()
        {
            base.PostExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ResetTrail();
                ResetMaterialCache();
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
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            ResetTrail();
        }

        public override void CompTick()
        {
            Projectile projectile = ProjectileParent;
            if (Props == null || projectile == null || !parent.Spawned || parent.Map == null)
            {
                return;
            }

            if (!initialized)
            {
                if (projectile.Launcher == null)
                {
                    return;
                }

                initialized = true;
                trailPoints.Clear();
                trailPoints.Add(ResolveTrailPosition(projectile));
                return;
            }

            RecordTrailPoint(ResolveTrailPosition(projectile));
        }

        public override void PostDraw()
        {
            base.PostDraw();

            Projectile projectile = ProjectileParent;
            if (Props == null || projectile == null || trailPoints.Count < 2)
            {
                return;
            }

            DrawContinuousTrail(projectile);
        }

        private void RecordTrailPoint(Vector3 point)
        {
            int count = trailPoints.Count;
            float minDist = Mathf.Max(0.01f, Props.trailMinPointDistance);
            if (count > 0)
            {
                Vector3 delta = point - trailPoints[count - 1];
                if (delta.x * delta.x + delta.z * delta.z < minDist * minDist)
                {
                    return;
                }
            }

            trailPoints.Add(point);
            int maxPoints = Mathf.Max(4, Props.trailMaxPoints);
            if (trailPoints.Count > maxPoints)
            {
                trailPoints.RemoveAt(0);
            }
        }

        private void DrawContinuousTrail(Projectile projectile)
        {
            int subdivisions = Mathf.Clamp(Props.trailSmoothSubdivisions, 1, 6);
            float widthStart = Mathf.Max(0.01f, Props.trailWidthStart);
            float widthEnd = Mathf.Max(0.005f, Props.trailWidthEnd);
            float altitudeOffset = Mathf.Max(0f, Props.trailAltitudeOffset);
            float baseAlpha = Mathf.Clamp01(Props.trailAlpha);
            if (baseAlpha <= 0f)
            {
                return;
            }

            int pointsCount = trailPoints.Count;

            for (int i = 0; i < pointsCount - 1; i++)
            {
                Vector3 p0 = trailPoints[Mathf.Max(i - 1, 0)];
                Vector3 p1 = trailPoints[i];
                Vector3 p2 = trailPoints[i + 1];
                Vector3 p3 = trailPoints[Mathf.Min(i + 2, pointsCount - 1)];

                Vector3 prev = p1 + Altitudes.AltIncVect * altitudeOffset;
                for (int s = 1; s <= subdivisions; s++)
                {
                    float t = s / (float)subdivisions;
                    Vector3 cur = CatmullRom(p0, p1, p2, p3, t) + Altitudes.AltIncVect * altitudeOffset;
                    float progress = (i + t) / Mathf.Max(1f, pointsCount - 1f);
                    float width = Mathf.Lerp(widthStart, widthEnd, progress);
                    float alpha = baseAlpha * progress;
                    Material mat = GetTrailMaterial(projectile, alpha);
                    if (mat != null)
                    {
                        GenDraw.DrawLineBetween(prev, cur, mat, width);
                    }
                    prev = cur;
                }
            }
        }

        private Material GetTrailMaterial(Projectile projectile, float alpha)
        {
            if (alpha <= 0.001f)
            {
                return null;
            }

            Color color;
            if (Props.useProjectileGraphicColor && projectile.def.graphicData != null)
            {
                color = projectile.def.graphicData.color;
            }
            else
            {
                color = Props.trailColor;
            }

            color.a = 1f;
            string texPath = Props.trailTexPath;
            if (texPath.NullOrEmpty())
            {
                texPath = "UI/Overlays/ThingLine";
            }

            if (cachedTrailTexPath != texPath || cachedTrailBaseColor != color || cachedUseGlowShader != Props.useGlowShader)
            {
                cachedTrailTexPath = texPath;
                cachedTrailBaseColor = color;
                cachedUseGlowShader = Props.useGlowShader;
                for (int i = 0; i < fadeMaterials.Length; i++)
                {
                    fadeMaterials[i] = null;
                }
            }

            int step = Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01(alpha) * (FadeMaterialSteps - 1)), 1, FadeMaterialSteps - 1);
            Material mat = fadeMaterials[step];
            if (mat == null)
            {
                Color matColor = cachedTrailBaseColor;
                matColor.a = step / (float)(FadeMaterialSteps - 1);
                mat = MaterialPool.MatFrom(
                    cachedTrailTexPath,
                    Props.useGlowShader ? ShaderDatabase.MoteGlow : ShaderDatabase.Transparent,
                    matColor);
                fadeMaterials[step] = mat;
            }

            return mat;
        }

        private void ResetTrail()
        {
            initialized = false;
            trailPoints.Clear();
        }

        private void ResetMaterialCache()
        {
            cachedTrailTexPath = null;
            cachedTrailBaseColor = Color.clear;
            cachedUseGlowShader = false;
            for (int i = 0; i < fadeMaterials.Length; i++)
            {
                fadeMaterials[i] = null;
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

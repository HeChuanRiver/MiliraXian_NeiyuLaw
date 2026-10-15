using System.Collections.Generic;
using HarmonyLib;
using MiliraXian.Characters.Common.Things;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx
{
    internal sealed class ArcaneProjectileVfx
    {
        private const int Segments = 64;
        private static readonly List<ArcaneProjectileVfx> Active = new();
        // 1.6.9676.17735 has public JumpTo but no public zoom setter. Setting the public
        // altitude alone is undone by Update; this verified private field is needed once for framing.
        private static readonly AccessTools.FieldRef<WorldCameraDriver, float> DesiredAltitude =
            AccessTools.FieldRefAccess<WorldCameraDriver, float>("desiredAltitude");
        private readonly Vector3[] vertices = new Vector3[(Segments + 1) * 2];
        private readonly Vector3[] coreVertices = new Vector3[(Segments + 1) * 2];
        private readonly Vector3[] innerVertices = new Vector3[(Segments + 1) * 2];
        private Mesh tailMesh;
        private Mesh coreMesh;
        private Mesh innerMesh;
        private ArcaneParticleBatch sparks;
        private int lastTick = -1;
        private Vector3 lastCamera;
        private Quaternion lastCameraRotation;
        private ArcaneVfxQuality lastQuality;
        internal bool Disposed { get; private set; }

        internal ArcaneProjectileVfx()
        {
            Active.Add(this);
        }

        internal static void Reset()
        {
            while (Active.Count > 0) Active[Active.Count - 1].Dispose();
        }

        internal static void FrameFlight(WorldObject_ArcaneProjectile projectile)
        {
            CameraJumper.TryJump(projectile.SourceTile, CameraJumper.MovementMode.Cut);
            WorldCameraDriver driver = Find.WorldCameraDriver;
            Vector3 lookDirection = (projectile.PositionAt(0.5f) - projectile.Origin).normalized;
            driver.JumpTo(lookDirection);
            Camera camera = Find.WorldCamera;
            float verticalTan = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f);
            float limitingTan = verticalTan * Mathf.Min(1f, camera.aspect);
            float required = WorldCameraDriver.MinAltitude;
            for (int i = 0; i <= 32; i++)
            {
                Vector3 position = projectile.PositionAt(i / 32f) - projectile.Origin;
                float projection = Vector3.Dot(position, lookDirection);
                float side = (position - lookDirection * projection).magnitude;
                required = Mathf.Max(required, projection + side * 1.3f / Mathf.Max(0.05f, limitingTan)
                    + projectile.DrawSize * 8f);
            }
            float altitude = Mathf.Clamp(required - projectile.SourceTile.Layer.ExtraCameraAltitude,
                WorldCameraDriver.MinAltitude, 1100f);
            driver.altitude = altitude;
            DesiredAltitude(driver) = altitude;
            // No camera lock or replacement camera configuration survives this one-time cut.
        }

        internal static void FrameImpact(WorldObject_ArcaneImpact impact)
        {
            CameraJumper.TryJump(impact.Tile, CameraJumper.MovementMode.Cut);
            PlanetLayer layer = impact.Tile.Layer;
            float angle = Find.WorldGrid.TileRadiusToAngle(layer, impact.ImpactRadius) * Mathf.Deg2Rad;
            // Keep the tile-centered native camera cut; the plume follows the same radial axis.
            WorldCameraDriver driver = Find.WorldCameraDriver;
            Camera camera = Find.WorldCamera;
            float tan = Mathf.Tan(camera.fieldOfView * Mathf.Deg2Rad * 0.5f) * Mathf.Min(1f, camera.aspect);
            float width = layer.Radius * Mathf.Sin(angle) * 1.4f;
            float required = layer.Radius + impact.CloudHeight + width * 1.7f / Mathf.Max(0.05f, tan);
            float altitude = Mathf.Clamp(required - layer.ExtraCameraAltitude, WorldCameraDriver.MinAltitude, 1100f);
            driver.altitude = altitude;
            DesiredAltitude(driver) = altitude;
        }

        internal void Draw(WorldObject_ArcaneProjectile projectile)
        {
            if (Disposed || Find.UIRoot.HideMotes || Find.ScreenshotModeHandler.Active || !ArcaneEnergyResources.Ready()) return;
            Camera camera = Find.WorldCamera;
            Vector3 head = projectile.DrawPos;
            float p = projectile.FlightProgress;
            float size = projectile.DrawSize;
            float seconds = projectile.FlightSeconds;
            if (tailMesh == null) CreateMeshes();
            bool update = lastTick != Find.TickManager.TicksGame || lastCamera != camera.transform.position
                || lastCameraRotation != camera.transform.rotation || lastQuality != ArcaneVfxStyle.Quality;
            if (update)
            {
                BuildTail(projectile, camera);
                lastTick = Find.TickManager.TicksGame;
                lastCamera = camera.transform.position;
                lastCameraRotation = camera.transform.rotation;
                lastQuality = ArcaneVfxStyle.Quality;
            }
            int layer = WorldCameraManager.WorldLayer;
            ArcaneEnergyResources.DrawMesh(tailMesh, Matrix4x4.identity, ArcaneEnergyResources.BeamOuter,
                new Color(0.25f, 0.48f, 1.1f, 0.34f), layer, camera, seconds);
            ArcaneEnergyResources.DrawMesh(innerMesh, Matrix4x4.identity, ArcaneEnergyResources.BeamInner,
                new Color(0.35f, 0.83f, 1.15f, 0.62f), layer, camera, seconds);
            ArcaneEnergyResources.DrawMesh(coreMesh, Matrix4x4.identity, ArcaneEnergyResources.BeamCore,
                new Color(0.72f, 1.0f, 1.2f, 0.79f), layer, camera, seconds);
            float pulse = 1f + 0.04f * Mathf.Sin(p * 160f);
            ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, head, size * 4.5f, size * 4.5f,
                new Color(0.26f, 0.51f, 1.1f, 0.36f), camera, layer, seconds);
            ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, head, size * 2.2f, size * 2.2f,
                new Color(0.43f, 0.88f, 1.2f, 0.68f), camera, layer, seconds);
            ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, head, size * pulse, size * pulse,
                new Color(0.9f, 1.03f, 1.14f, 0.9f), camera, layer, seconds, 4f);

            sparks ??= new ArcaneParticleBatch(ArcaneEnergyResources.Spark);
            if (update)
            {
                sparks.Begin();
                for (int i = 0; i < 36; i++)
                {
                    if (!ArcaneVfxStyle.KeepParticle(i, 2, ArcaneVfxStyle.Density)) continue;
                    float age = Mathf.Repeat(i * 0.618034f + p * 5f, 1f);
                    float sample = Mathf.Max(0, p - age * Mathf.Min(p, projectile.TrailLength));
                    float phase = i * 2.399963f + p * 36f;
                    Vector3 center = projectile.PositionAt(sample);
                    float spread = size * (0.5f + age * 1.2f);
                    center += (camera.transform.right * Mathf.Cos(phase) + camera.transform.up * Mathf.Sin(phase)) * spread;
                    float alpha = (1f - age) * Mathf.Clamp01(p * 12f);
                    float diameter = size * (0.11f + Mathf.Repeat(i * 0.38f, 1f) * 0.17f);
                    sparks.Add(center, camera.transform.right, camera.transform.up, diameter,
                        diameter * (1.4f + age * 1.5f), new Color(0.48f, 0.83f, 1.14f, alpha * 0.7f));
                }
                sparks.Upload();
            }
            sparks.Draw(seconds, layer, camera);
        }

        private void CreateMeshes()
        {
            Vector2[] uv = new Vector2[vertices.Length];
            Color32[] colors = new Color32[vertices.Length];
            int[] triangles = new int[Segments * 6];
            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments;
                uv[i * 2] = new Vector2(0, 0.1f + t * 0.8f);
                uv[i * 2 + 1] = new Vector2(1, 0.1f + t * 0.8f);
                byte alpha = (byte)(Mathf.Pow(t, 1.4f) * 255f);
                colors[i * 2] = colors[i * 2 + 1] = new Color32(255, 255, 255, alpha);
                if (i == Segments) continue;
                int v = i * 2;
                int index = i * 6;
                triangles[index] = v; triangles[index + 1] = v + 1; triangles[index + 2] = v + 2;
                triangles[index + 3] = v + 2; triangles[index + 4] = v + 1; triangles[index + 5] = v + 3;
            }
            tailMesh = new Mesh { name = "MX_ArcaneWorldTail", vertices = vertices, uv = uv, colors32 = colors, triangles = triangles };
            coreMesh = new Mesh { name = "MX_ArcaneWorldTailCore", vertices = coreVertices, uv = uv, colors32 = colors, triangles = triangles };
            innerMesh = new Mesh { name = "MX_ArcaneWorldTailInner", vertices = innerVertices, uv = uv, colors32 = colors, triangles = triangles };
            tailMesh.MarkDynamic();
            coreMesh.MarkDynamic();
            innerMesh.MarkDynamic();
        }

        private void BuildTail(WorldObject_ArcaneProjectile projectile, Camera camera)
        {
            float head = projectile.FlightProgress;
            float start = Mathf.Max(0f, head - projectile.TrailLength);
            Vector3 previousSide = Vector3.zero;
            for (int i = 0; i <= Segments; i++)
            {
                float t = i / (float)Segments;
                float p = Mathf.Lerp(start, head, t);
                Vector3 position = projectile.PositionAt(p);
                Vector3 tangent = projectile.PositionAt(Mathf.Min(1, p + 0.001f))
                    - projectile.PositionAt(Mathf.Max(0, p - 0.001f));
                Vector3 side = Vector3.Cross(tangent, position - camera.transform.position);
                if (side.sqrMagnitude < 0.00000001f) side = camera.transform.right;
                side.Normalize();
                if (i > 0 && Vector3.Dot(side, previousSide) < 0f) side = -side;
                previousSide = side;
                Vector3 offset = side * (projectile.DrawSize * 1.1f * Mathf.Lerp(0.06f, 1f, t)
                    * Mathf.Clamp(ArcaneVfxStyle.Tuning.trailWidth, 0.5f, 1.5f));
                vertices[i * 2] = position - offset * 1.65f;
                vertices[i * 2 + 1] = position + offset * 1.65f;
                innerVertices[i * 2] = position - offset * 0.8f;
                innerVertices[i * 2 + 1] = position + offset * 0.8f;
                coreVertices[i * 2] = position - offset * 0.20f;
                coreVertices[i * 2 + 1] = position + offset * 0.20f;
            }
            ArcaneVfxStyle.UploadMesh(tailMesh, vertices);
            ArcaneVfxStyle.UploadMesh(coreMesh, coreVertices);
            ArcaneVfxStyle.UploadMesh(innerMesh, innerVertices);
        }

        internal void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            if (tailMesh != null) Object.Destroy(tailMesh);
            if (coreMesh != null) Object.Destroy(coreMesh);
            if (innerMesh != null) Object.Destroy(innerMesh);
            sparks?.Dispose();
            tailMesh = null;
            coreMesh = null;
            innerMesh = null;
            sparks = null;
            Active.Remove(this);
        }
    }
}

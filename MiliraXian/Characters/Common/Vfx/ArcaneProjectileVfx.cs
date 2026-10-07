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
        private Mesh tailMesh;
        private Mesh coreMesh;
        private int lastTick = -1;
        private Vector3 lastCamera;
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
            if (Disposed || !ArcaneEnergyResources.Ready()) return;
            Camera camera = Find.WorldCamera;
            Vector3 head = projectile.DrawPos;
            float p = projectile.FlightProgress;
            float size = projectile.DrawSize;
            if (tailMesh == null) CreateMeshes();
            if (lastTick != Find.TickManager.TicksGame || lastCamera != camera.transform.position)
            {
                BuildTail(projectile, camera);
                lastTick = Find.TickManager.TicksGame;
                lastCamera = camera.transform.position;
            }
            int layer = WorldCameraManager.WorldLayer;
            ArcaneEnergyResources.DrawMesh(tailMesh, Matrix4x4.identity, ArcaneEnergyResources.BeamOuter,
                new Color(0.12f, 0.6f, 1.4f, 0.8f), layer, camera);
            ArcaneEnergyResources.DrawMesh(coreMesh, Matrix4x4.identity, ArcaneEnergyResources.BeamCore,
                new Color(0.6f, 1.3f, 1.7f, 0.9f), layer, camera);
            float pulse = 1f + 0.04f * Mathf.Sin(p * 160f);
            ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, head, size * 4.5f, size * 4.5f,
                new Color(0.15f, 0.6f, 1.3f, 0.6f), camera, layer);
            ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, head, size * 2.2f, size * 2.2f,
                new Color(0.4f, 1.1f, 1.8f, 0.9f), camera, layer);
            ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, head, size * pulse, size * pulse,
                new Color(1.8f, 1.8f, 1.6f, 1f), camera, layer);

            for (int i = 0; i < 18; i++)
            {
                float age = Mathf.Repeat(i * 0.618034f + p * 5f, 1f);
                float sample = Mathf.Max(0, p - age * Mathf.Min(p, projectile.TrailLength));
                float phase = i * 2.399963f + p * 36f;
                Vector3 center = projectile.PositionAt(sample);
                float spread = size * (0.5f + age * 1.2f);
                center += (camera.transform.right * Mathf.Cos(phase) + camera.transform.up * Mathf.Sin(phase)) * spread;
                float alpha = (1f - age) * Mathf.Clamp01(p * 12f);
                ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, center, size * 0.25f, size * 0.6f,
                    new Color(0.4f, 0.85f, 1.5f, alpha), camera, layer);
            }
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
            tailMesh.MarkDynamic();
            coreMesh.MarkDynamic();
        }

        private void BuildTail(WorldObject_ArcaneProjectile projectile, Camera camera)
        {
            float head = projectile.FlightProgress;
            float start = Mathf.Max(0f, head - projectile.TrailLength);
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
                Vector3 offset = side * (projectile.DrawSize * 1.1f * Mathf.Lerp(0.06f, 1f, t));
                vertices[i * 2] = position - offset;
                vertices[i * 2 + 1] = position + offset;
                coreVertices[i * 2] = position - offset * 0.3f;
                coreVertices[i * 2 + 1] = position + offset * 0.3f;
            }
            tailMesh.vertices = vertices;
            coreMesh.vertices = coreVertices;
            tailMesh.RecalculateBounds();
            coreMesh.RecalculateBounds();
        }

        internal void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            if (tailMesh != null) Object.Destroy(tailMesh);
            if (coreMesh != null) Object.Destroy(coreMesh);
            tailMesh = null;
            coreMesh = null;
            Active.Remove(this);
        }
    }
}

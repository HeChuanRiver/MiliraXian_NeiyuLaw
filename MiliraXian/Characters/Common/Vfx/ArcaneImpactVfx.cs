using System.Collections.Generic;
using MiliraXian.Characters.Common.Things;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx
{
    // All positions, expansion and turbulence are analytical functions of saved game ticks.
    // No ParticleSystem simulation or Unity elapsed-time clock can drift after pause/load.
    [StaticConstructorOnStartup]
    internal sealed class ArcaneImpactVfx
    {
        private const int RingSegments = 128;
        private const int CloudCount = 96;
        private static readonly List<ArcaneImpactVfx> Active = new();
        private static Material cloud, dust;
        private static bool attempted;
        private readonly Puff[] puffs = new Puff[CloudCount];
        private readonly int[] order = new int[CloudCount];
        private readonly Vector3[] vertices = new Vector3[(RingSegments + 1) * 6];
        private readonly Color[] colors = new Color[(RingSegments + 1) * 6];
        private Mesh rings;
        private ArcaneParticleBatch embers;
        private int lastTick = -1;
        private float lastCloudSeconds = -1f;
        private Vector3 lastCloudCamera;
        private Quaternion lastCloudRotation;
        private ArcaneVfxQuality lastQuality;
        internal bool Disposed { get; private set; }

        private struct Puff
        {
            public Vector3 Position;
            public float Width, Height, Distance;
            public Color Color;
            public bool Dust;
        }

        internal ArcaneImpactVfx() { Active.Add(this); }

        internal static void Reset()
        {
            while (Active.Count > 0) Active[Active.Count - 1].Dispose();
        }

        internal static void Preload(ArcaneImpactProperties props)
        {
            // Called by the projectile's first Draw, also after loading: Unity assets stay
            // on the render thread and warm up before impact rather than during settlement.
            Ready();
            if (props?.scorchedBiome != null) _ = props.scorchedBiome.DrawMaterial;
            if (props?.scorchedVacuumBiome != null) _ = props.scorchedVacuumBiome.DrawMaterial;
        }

        private static bool Ready()
        {
            if (!ArcaneEnergyResources.Ready()) return false;
            if (attempted) return cloud != null && dust != null;
            attempted = true;
            if (!CharacterUnityVfxRuntime.TryGetAnimation(CharacterUnityVfxKind.ArcaneImpact, out CharacterVfxDef animation)) return false;
            cloud = animation.MaterialAt("Cloud");
            dust = animation.MaterialAt("Dust");
            if (cloud == null || dust == null)
                Log.ErrorOnce("[WorldArcaneStrike] ArcaneImpact prefab has incomplete cloud materials.", 197631220);
            return cloud != null && dust != null;
        }

        internal void Draw(WorldObject_ArcaneImpact impact)
        {
            if (Disposed || !Ready() || Find.UIRoot.HideMotes || Find.ScreenshotModeHandler.Active) return;
            Camera camera = Find.WorldCamera;
            PlanetLayer layer = impact.Tile.Layer;
            Vector3 normal = Find.WorldGrid.GetTileCenter(impact.Tile).normalized;
            WorldRendererUtility.GetTangentsToPlanet(normal, out Vector3 first, out Vector3 second);
            Vector3 anchor = layer.Origin + normal * (layer.Radius + 0.12f);
            float seconds = impact.Seconds;
            float progress = impact.Progress;
            float radiusAngle = Find.WorldGrid.TileRadiusToAngle(layer, impact.ImpactRadius) * Mathf.Deg2Rad;
            float radius = Mathf.Max(layer.AverageTileSize, layer.Radius * Mathf.Sin(radiusAngle));
            float rise = 1f - Mathf.Exp(-seconds * 0.65f);
            float fade = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.55f, 1f, progress));
            int drawLayer = WorldCameraManager.WorldLayer;
            if (rings == null) CreateRings();
            if (lastTick != Find.TickManager.TicksGame)
            {
                BuildRings(layer, normal, first, second, radiusAngle, seconds);
                lastTick = Find.TickManager.TicksGame;
            }
            ArcaneEnergyResources.DrawMesh(rings, Matrix4x4.identity, ArcaneEnergyResources.BeamInner,
                new Color(0.54f, 0.89f, 1.16f, 0.82f), drawLayer, camera, seconds, 1f);

            float flash = Mathf.Exp(-seconds * 3.8f);
            ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, anchor, radius * 6f, radius * 6f,
                new Color(0.48f, 0.81f, 1.13f, flash * 0.58f), camera, drawLayer, seconds);
            ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, anchor, radius * 2.6f, radius * 2.6f,
                new Color(0.93f, 1.02f, 1.15f, flash * 0.88f), camera, drawLayer, seconds, 4f);
            float fire = Mathf.Exp(-seconds * 0.37f) * fade;
            ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, anchor + normal * radius * 0.35f,
                radius * 3.2f, radius * 3.2f, new Color(1.12f, 0.56f, 0.22f, fire * 0.62f), camera, drawLayer, seconds);

            // Stem, annular cloud crown and ground skirt. Far-to-near sorting gives the
            // premultiplied cloud billboards volume from any orbiting world camera angle.
            bool update = lastCloudSeconds != seconds || lastCloudCamera != camera.transform.position
                || lastCloudRotation != camera.transform.rotation || lastQuality != ArcaneVfxStyle.Quality;
            if (update) BuildCloud(anchor, normal, first, second, radius, impact.CloudHeight, seconds, rise, fade, camera);
            for (int i = 0; i < CloudCount; i++)
            {
                int index = order[i];
                // All three cloud regions remain represented at every quality level.
                if (ArcaneVfxStyle.Quality == ArcaneVfxQuality.Low && index % 3 == 2
                    || ArcaneVfxStyle.Quality == ArcaneVfxQuality.Medium && index % 6 == 5) continue;
                Puff puff = puffs[index];
                if (puff.Color.a <= 0.001f) continue;
                ArcaneEnergyResources.DrawBillboard(puff.Dust ? dust : cloud, puff.Position,
                    puff.Width, puff.Height, puff.Color, camera, drawLayer, seconds, seed: index * 0.618034f);
            }
            // Hot core remains visible through the rolling cloud for the first few seconds.
            Vector3 crown = anchor + normal * impact.CloudHeight * rise;
            ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, crown,
                radius * 1.8f, radius * 1.8f, new Color(0.38f, 0.75f, 1.15f, fire * 0.42f), camera, drawLayer, seconds);
            embers ??= new ArcaneParticleBatch(ArcaneEnergyResources.Spark);
            if (update)
            {
                embers.Begin();
                for (int i = 0; i < 88; i++)
                {
                    if (!ArcaneVfxStyle.KeepParticle(i, 3, ArcaneVfxStyle.Density * ArcaneVfxStyle.Tuning.explosionParticleDensity)) continue;
                    float seed = Mathf.Repeat(i * 0.618034f, 1f);
                    float age = Mathf.Repeat(seconds * 0.23f + seed, 1f);
                    float angle = i * 2.399963f + seconds * 0.25f;
                    Vector3 tangent = first * Mathf.Cos(angle) + second * Mathf.Sin(angle);
                    Vector3 position = anchor + tangent * radius * (0.2f + age * 1.5f)
                        + normal * impact.CloudHeight * (Mathf.Sin(age * Mathf.PI) * 0.55f + rise * 0.1f);
                    float size = layer.AverageTileSize * (0.08f + seed * 0.1f);
                    float alpha = Mathf.Sin(age * Mathf.PI) * rise * fade * Mathf.Lerp(1f, 0.22f, progress) * 0.74f;
                    Color color = i % 3 == 0 ? new Color(0.35f, 0.78f, 1.2f, alpha) : new Color(1.15f, 0.60f, 0.22f, alpha);
                    Vector3 velocity = tangent * radius * 0.35f + normal * (Mathf.Cos(age * Mathf.PI) * impact.CloudHeight * 0.45f);
                    Vector3 direction = Vector3.ProjectOnPlane(velocity, camera.transform.forward).normalized;
                    if (direction.sqrMagnitude < 0.01f) direction = camera.transform.up;
                    Vector3 side = Vector3.Cross(direction, camera.transform.forward).normalized;
                    embers.Add(position, side, direction, size, size * (1.6f + seed * 1.8f), color);
                }
                // A short radial burst emphasizes impact without a persistent white disc.
                for (int i = 0; i < 16 && seconds < 0.9f; i++)
                {
                    if (!ArcaneVfxStyle.KeepParticle(i, 8, ArcaneVfxStyle.Density)) continue;
                    float angle = i * 2.399963f;
                    Vector3 tangent = first * Mathf.Cos(angle) + second * Mathf.Sin(angle);
                    Vector3 position = anchor + tangent * radius * seconds * 2f + normal * radius * 0.2f;
                    Vector3 direction = Vector3.ProjectOnPlane(tangent, camera.transform.forward).normalized;
                    embers.Add(position, Vector3.Cross(direction, camera.transform.forward), direction,
                        radius * 0.016f, radius * 0.24f, new Color(0.56f, 0.88f, 1.12f, (1f - seconds / 0.9f) * 0.6f));
                }
                embers.Upload();
                lastCloudSeconds = seconds;
                lastCloudCamera = camera.transform.position;
                lastCloudRotation = camera.transform.rotation;
                lastQuality = ArcaneVfxStyle.Quality;
            }
            embers.Draw(seconds, drawLayer, camera);
        }

        private void BuildCloud(Vector3 anchor, Vector3 normal, Vector3 first, Vector3 second,
            float radius, float height, float seconds, float rise, float fade, Camera camera)
        {
            for (int i = 0; i < CloudCount; i++)
            {
                float seed = Mathf.Repeat(i * 0.618034f, 1f);
                float angle = i * 2.399963f + seconds * (i % 2 == 0 ? 0.09f : -0.07f);
                Vector3 tangent = first * Mathf.Cos(angle) + second * Mathf.Sin(angle);
                Puff puff = new() { Dust = i >= 72 };
                float width, altitude, spread;
                if (i < 24)
                {
                    float level = i / 24f;
                    altitude = height * rise * level;
                    spread = radius * (0.08f + 0.2f * level) * rise;
                    width = radius * (0.35f + seed * 0.18f) * Mathf.Max(0.02f, rise);
                }
                else if (i < 72)
                {
                    int tier = (i - 24) / 16;
                    spread = radius * (0.7f - tier * 0.2f) * rise * (1f + seconds * 0.017f);
                    altitude = height * rise + radius * (tier * 0.22f + seed * 0.15f);
                    width = radius * (0.63f + seed * 0.22f) * Mathf.Max(0.02f, rise);
                }
                else
                {
                    spread = radius * (0.3f + Mathf.Clamp01(seconds / 4f) * (0.8f + seed * 0.4f));
                    altitude = radius * (0.12f + seed * 0.13f) * rise;
                    width = radius * (0.55f + seed * 0.4f) * Mathf.Max(0.02f, rise);
                }
                puff.Position = anchor + normal * (altitude + Mathf.Sin(seconds * 0.9f + i) * radius * 0.035f * rise) + tangent * spread;
                puff.Width = width;
                puff.Height = width * (puff.Dust ? 0.65f : 0.9f + seed * 0.2f);
                float heat = Mathf.Exp(-seconds * 0.42f);
                Color ash = new Color(0.23f + seed * 0.14f, 0.24f + seed * 0.13f, 0.29f + seed * 0.13f);
                Color hot = i < 24 ? new Color(1.15f, 0.60f, 0.24f) : new Color(1.05f, 0.83f, 0.60f);
                puff.Color = Color.Lerp(ash, hot, heat);
                puff.Color.a = rise * fade * (puff.Dust ? 0.42f : 0.72f);
                puff.Distance = (puff.Position - camera.transform.position).sqrMagnitude;
                puffs[i] = puff;
                order[i] = i;
            }
            // A fixed small array and insertion sort avoid per-frame allocations/delegates.
            for (int i = 1; i < CloudCount; i++)
            {
                int item = order[i], j = i - 1;
                while (j >= 0 && puffs[order[j]].Distance < puffs[item].Distance)
                {
                    order[j + 1] = order[j];
                    j--;
                }
                order[j + 1] = item;
            }
        }

        private void CreateRings()
        {
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[RingSegments * 18];
            for (int ring = 0; ring < 3; ring++)
                for (int i = 0; i <= RingSegments; i++)
                {
                    int v = ring * (RingSegments + 1) * 2 + i * 2;
                    uv[v] = new Vector2(0, 0.1f + 0.8f * i / RingSegments);
                    uv[v + 1] = new Vector2(1, uv[v].y);
                    if (i == RingSegments) continue;
                    int index = (ring * RingSegments + i) * 6;
                    triangles[index] = v; triangles[index + 1] = v + 2; triangles[index + 2] = v + 1;
                    triangles[index + 3] = v + 1; triangles[index + 4] = v + 2; triangles[index + 5] = v + 3;
                }
            rings = new Mesh { name = "MX_ArcaneImpact_SphericalWaves", vertices = vertices, uv = uv, colors = colors, triangles = triangles };
            rings.MarkDynamic();
        }

        private void BuildRings(PlanetLayer layer, Vector3 normal, Vector3 first, Vector3 second, float maximumAngle, float seconds)
        {
            for (int ring = 0; ring < 3; ring++)
            {
                float age = (seconds - ring * 0.28f) / (2.5f + ring * 0.4f);
                float p = Mathf.Clamp01(age);
                float angle = maximumAngle * Mathf.Sqrt(p) * (1f + ring * 0.15f);
                float thickness = layer.AverageTileSize / layer.Radius * (0.18f + ring * 0.08f);
                float alpha = age < 0f || age >= 1f ? 0 : Mathf.Pow(1f - p, 0.8f);
                for (int i = 0; i <= RingSegments; i++)
                {
                    float bearing = i * Mathf.PI * 2f / RingSegments;
                    Vector3 tangent = first * Mathf.Cos(bearing) + second * Mathf.Sin(bearing);
                    int v = ring * (RingSegments + 1) * 2 + i * 2;
                    float inner = Mathf.Max(0, angle - thickness);
                    float outer = Mathf.Min(Mathf.PI, angle + thickness);
                    vertices[v] = layer.Origin + (normal * Mathf.Cos(inner) + tangent * Mathf.Sin(inner)) * (layer.Radius + 0.18f + ring * 0.02f);
                    vertices[v + 1] = layer.Origin + (normal * Mathf.Cos(outer) + tangent * Mathf.Sin(outer)) * (layer.Radius + 0.18f + ring * 0.02f);
                    colors[v] = colors[v + 1] = new Color(1, 1, 1, alpha);
                }
            }
            ArcaneVfxStyle.UploadMesh(rings, vertices, colors);
        }

        internal void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            if (rings != null) Object.Destroy(rings);
            embers?.Dispose();
            rings = null;
            embers = null;
            Active.Remove(this);
        }
    }
}

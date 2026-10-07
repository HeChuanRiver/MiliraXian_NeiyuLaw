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
        private int lastTick = -1;
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
            if (!CharacterUnityVfxRuntime.TryGetPrefab(CharacterUnityVfxKind.ArcaneImpact, out GameObject prefab)) return false;
            cloud = prefab.transform.Find("Cloud")?.GetComponent<SpriteRenderer>()?.sharedMaterial;
            dust = prefab.transform.Find("Dust")?.GetComponent<SpriteRenderer>()?.sharedMaterial;
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
                new Color(0.7f, 1.05f, 1.6f), drawLayer, camera);

            float flash = Mathf.Exp(-seconds * 3.8f);
            ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, anchor, radius * 6f, radius * 6f,
                new Color(1.5f, 1.65f, 1.8f, flash), camera, drawLayer);
            float fire = Mathf.Exp(-seconds * 0.37f) * fade;
            ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, anchor + normal * radius * 0.35f,
                radius * 3.2f, radius * 3.2f, new Color(1.7f, 0.58f, 0.12f, fire), camera, drawLayer);

            // Stem, annular cloud crown and ground skirt. Far-to-near sorting gives the
            // premultiplied cloud billboards volume from any orbiting world camera angle.
            BuildCloud(anchor, normal, first, second, radius, impact.CloudHeight, seconds, rise, fade, camera);
            for (int i = 0; i < CloudCount; i++)
            {
                Puff puff = puffs[order[i]];
                if (puff.Color.a <= 0.001f) continue;
                ArcaneEnergyResources.DrawBillboard(puff.Dust ? dust : cloud, puff.Position,
                    puff.Width, puff.Height, puff.Color, camera, drawLayer);
            }
            // Hot core remains visible through the rolling cloud for the first few seconds.
            Vector3 crown = anchor + normal * impact.CloudHeight * rise;
            ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, crown,
                radius * 1.8f, radius * 1.8f, new Color(0.3f, 0.8f, 1.8f, fire * 0.55f), camera, drawLayer);
            for (int i = 0; i < 64; i++)
            {
                float seed = Mathf.Repeat(i * 0.618034f, 1f);
                float age = Mathf.Repeat(seconds * 0.23f + seed, 1f);
                float angle = i * 2.399963f + seconds * 0.25f;
                Vector3 tangent = first * Mathf.Cos(angle) + second * Mathf.Sin(angle);
                Vector3 position = anchor + tangent * radius * (0.2f + age * 1.5f)
                    + normal * impact.CloudHeight * (Mathf.Sin(age * Mathf.PI) * 0.55f + rise * 0.1f);
                float size = layer.AverageTileSize * (0.08f + seed * 0.1f);
                float alpha = Mathf.Sin(age * Mathf.PI) * rise * fade;
                Color color = i % 3 == 0 ? new Color(0.25f, 0.75f, 1.8f, alpha) : new Color(1.8f, 0.55f, 0.07f, alpha);
                ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, position, size, size * 2.8f, color, camera, drawLayer);
            }
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
                Color hot = i < 24 ? new Color(1.7f, 0.6f, 0.14f) : new Color(1.35f, 0.92f, 0.57f);
                puff.Color = Color.Lerp(ash, hot, heat);
                puff.Color.a = rise * fade * (puff.Dust ? 0.5f : 0.85f);
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
            rings = new Mesh { name = "MX_ArcaneImpact_SphericalWaves", vertices = vertices, uv = uv, triangles = triangles };
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
            rings.vertices = vertices;
            rings.colors = colors;
            rings.RecalculateBounds();
        }

        internal void Dispose()
        {
            if (Disposed) return;
            Disposed = true;
            if (rings != null) Object.Destroy(rings);
            rings = null;
            Active.Remove(this);
        }
    }
}

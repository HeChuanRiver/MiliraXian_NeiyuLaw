using MiliraXian.Characters.Common.Abilities;
using RimWorld.Planet;
using UnityEngine;
using UnityEngine.Rendering;
using Verse;

namespace MiliraXian.Characters.Common.Vfx
{
    // Shared native materials, not instantiated GameObjects. All motion samples saved ticks.
    [StaticConstructorOnStartup]
    internal static class ArcaneEnergyResources
    {
        internal static Material BeamCore, BeamInner, BeamOuter, Spark, Shockwave;
        internal static Mesh Billboard;
        private static bool attempted;
        private static readonly MaterialPropertyBlock Properties = new();

        internal static bool Ready()
        {
            if (attempted) return Billboard != null;
            attempted = true;
            if (!CharacterUnityVfxRuntime.TryGetAnimation(CharacterUnityVfxKind.ArcaneLaunch, out CharacterVfxDef animation)) return false;
            BeamCore = animation.MaterialAt("Beam/BeamCore");
            BeamInner = animation.MaterialAt("Beam/BeamInnerGlow");
            BeamOuter = animation.MaterialAt("Beam/BeamOuterGlow");
            Spark = animation.MaterialAt("Particle/ParticleColumn");
            Shockwave = animation.MaterialAt("Ground/Shockwave");
            if (BeamCore == null || BeamInner == null || BeamOuter == null || Spark == null || Shockwave == null)
            {
                Log.ErrorOnce("[WorldArcaneStrike] ArcaneLaunch prefab has incomplete visual layers.", 197631210);
                return false;
            }
            Billboard = new Mesh
            {
                name = "MX_ArcaneEnergy_Billboard",
                vertices = new[] { new Vector3(-0.5f, -0.5f, 0), new Vector3(0.5f, -0.5f, 0),
                    new Vector3(-0.5f, 0.5f, 0), new Vector3(0.5f, 0.5f, 0) },
                uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) },
                colors = new[] { Color.white, Color.white, Color.white, Color.white },
                triangles = new[] { 0, 2, 1, 1, 2, 3 }
            };
            Billboard.RecalculateBounds();
            return true;
        }

        internal static void DrawBillboard(Material material, Vector3 position, float width, float height,
            Color color, Camera camera, int layer, float seconds = 0f, float profile = -1f, float seed = 0f)
        {
            Quaternion facing = Quaternion.LookRotation(-camera.transform.forward, camera.transform.up);
            DrawMesh(Billboard, Matrix4x4.TRS(position, facing, new Vector3(width, height, 1)), material, color, layer, camera, seconds, profile, seed);
        }

        internal static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, Color color, int layer, Camera camera,
            float seconds = 0f, float profile = -1f, float seed = 0f)
        {
            Properties.Clear();
            Properties.SetColor(ShaderPropertyIDs.Color, color);
            if (profile < 0f) profile = material == Shockwave ? 2f : material == Spark ? 3f : 0f;
            ArcaneVfxStyle.Properties(Properties, seconds, seed, profile);
            SubmitMesh(mesh, matrix, material, layer, camera, Properties);
        }

        internal static void SubmitMesh(Mesh mesh, Matrix4x4 matrix, Material material, int layer,
            Camera camera, MaterialPropertyBlock properties)
        {
            // World terrain draws at 3500. These shared map materials draw near 3150
            // without writing depth, so the planet would paint over them afterwards.
            // Override this submission only; never mutate the shared map material.
            int renderQueue = layer == WorldCameraManager.WorldLayer
                ? WorldMaterials.DynamicObjectRenderQueue + (material.shader == BeamCore.shader ? 1 : 0)
                : 0;
            Graphics.DrawMesh(mesh, matrix, material, layer, camera, 0, properties,
                ShadowCastingMode.Off, false, renderQueue);
        }
    }

    internal static class ArcaneLaunchVfx
    {
        internal static void Draw(ArcaneLaunchState launch)
        {
            if (Find.UIRoot.HideMotes || Find.ScreenshotModeHandler.Active || !ArcaneEnergyResources.Ready()) return;
            Camera camera = Find.Camera;
            Vector3 anchor = launch.Position;
            anchor.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            Vector3 up = camera.transform.up;
            Vector3 right = camera.transform.right;
            float p = launch.Progress;
            float seconds = (Find.TickManager.TicksGame - launch.StartTick) / 60f;
            float rise = Mathf.SmoothStep(0, 1, Mathf.Clamp01(p / 0.13f));
            float fade = 1f - Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.82f, 1f, p));
            float pulse = 1f + 0.045f * Mathf.Sin(seconds * 23f);
            float height = launch.Height * rise;
            float width = launch.Width * pulse * Mathf.Max(0.05f, rise) * fade;
            float intensity = Mathf.Clamp(ArcaneVfxStyle.Tuning.beamIntensity, 0.1f, 1f);
            // A camera-facing column keeps the ascent readable in RimWorld's overhead view.
            Vector3 center = anchor + up * (height * 0.5f);
            if (height > 0.01f && width > 0.01f)
            {
                ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.BeamOuter, center, width * 4f, height,
                    new Color(0.23f, 0.42f, 0.98f, fade * 0.28f * intensity), camera, 0, seconds);
                ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.BeamInner, center, width * 2f, height,
                    new Color(0.40f, 0.84f, 1.12f, fade * 0.68f * intensity), camera, 0, seconds);
                ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.BeamCore, center, width * 0.62f, height,
                    new Color(0.91f, 1.03f, 1.18f, fade * 0.88f * intensity), camera, 0, seconds);
            }
            float flash = Mathf.Exp(-seconds * 6f) * (1f - p);
            ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, anchor, 13f, 13f,
                new Color(0.68f, 0.93f, 1.15f, flash * 0.66f), camera, 0, seconds, 4f);
            DrawShockwave(anchor, Mathf.Clamp01(p / 0.38f), 24f, camera, seconds);
            if (p > 0.1f) DrawShockwave(anchor + Altitudes.AltIncVect, Mathf.Clamp01((p - 0.1f) / 0.45f), 31f, camera, seconds);

            // Bounded, analytical particles reconstruct immediately after loading, including while paused.
            for (int i = 0; i < 64; i++)
            {
                if (!ArcaneVfxStyle.KeepParticle(i, 0, ArcaneVfxStyle.Density)) continue;
                float seed = Mathf.Repeat(i * 0.618034f, 1f);
                float age = Mathf.Repeat(seconds * (0.65f + seed * 0.5f) + seed, 1f);
                float angle = i * 2.399963f + seconds * (i % 2 == 0 ? 2f : -2f);
                float spread = launch.Width * (0.35f + seed * 1.2f) * (1f - age * 0.65f);
                Vector3 position = anchor + up * (age * height) + right * (Mathf.Cos(angle) * spread);
                position += camera.transform.forward * (Mathf.Sin(angle) * spread * 0.15f);
                float alpha = rise * fade * Mathf.Sin(age * Mathf.PI);
                Color color = i % 4 == 0 ? new Color(0.96f, 0.78f, 0.43f, alpha * 0.65f) : new Color(0.4f, 0.86f, 1.15f, alpha * 0.66f);
                ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, position,
                    0.10f + seed * 0.18f, 0.5f + seed * 1.4f, color, camera, 0, seconds);
            }
            // Sparse rising filaments follow a helix around the existing column.
            int filaments = ArcaneVfxStyle.Quality == ArcaneVfxQuality.Low ? 4 : ArcaneVfxStyle.Quality == ArcaneVfxQuality.High ? 10 : 6;
            for (int i = 0; i < filaments; i++)
            {
                float age = Mathf.Repeat(seconds * 0.58f + i * 0.618034f, 1f);
                float angle = seconds * 2.6f + i * 2.399963f + age * 8f;
                Vector3 position = anchor + up * (height * age) + right * (Mathf.Cos(angle) * width * 0.8f);
                position += camera.transform.forward * (Mathf.Sin(angle) * width * 0.25f);
                ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.BeamInner, position,
                    0.06f + width * 0.025f, 1.1f + age * 1.5f,
                    new Color(0.48f, 0.9f, 1.08f, Mathf.Sin(age * Mathf.PI) * fade * rise * 0.46f), camera, 0, seconds, seed: i);
            }
        }

        private static void DrawShockwave(Vector3 anchor, float progress, float maximum, Camera camera, float seconds)
        {
            if (progress >= 1f) return;
            float diameter = Mathf.Lerp(1.5f, maximum, Mathf.Sqrt(progress));
            ArcaneEnergyResources.DrawMesh(MeshPool.plane10,
                Matrix4x4.TRS(anchor, Quaternion.identity, new Vector3(diameter, 1f, diameter)),
                ArcaneEnergyResources.Shockwave, new Color(0.40f, 0.86f, 1.12f, (1f - progress) * 0.72f), 0, camera, seconds);
        }
    }
}

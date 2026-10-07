using MiliraXian.Characters.Common.Abilities;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx
{
    // Shared authored materials, not instantiated GameObjects. All motion samples saved ticks.
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
            if (!CharacterUnityVfxRuntime.TryGetPrefab(CharacterUnityVfxKind.ArcaneLaunch, out GameObject prefab)) return false;
            BeamCore = prefab.transform.Find("Beam/BeamCore")?.GetComponent<SpriteRenderer>()?.sharedMaterial;
            BeamInner = prefab.transform.Find("Beam/BeamInnerGlow")?.GetComponent<SpriteRenderer>()?.sharedMaterial;
            BeamOuter = prefab.transform.Find("Beam/BeamOuterGlow")?.GetComponent<SpriteRenderer>()?.sharedMaterial;
            Spark = prefab.transform.Find("Particle/ParticleColumn")?.GetComponent<SpriteRenderer>()?.sharedMaterial;
            Shockwave = prefab.transform.Find("Ground/Shockwave")?.GetComponent<SpriteRenderer>()?.sharedMaterial;
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
            Color color, Camera camera, int layer)
        {
            Quaternion facing = Quaternion.LookRotation(-camera.transform.forward, camera.transform.up);
            DrawMesh(Billboard, Matrix4x4.TRS(position, facing, new Vector3(width, height, 1)), material, color, layer, camera);
        }

        internal static void DrawMesh(Mesh mesh, Matrix4x4 matrix, Material material, Color color, int layer, Camera camera)
        {
            Properties.Clear();
            Properties.SetColor(ShaderPropertyIDs.Color, color);
            Graphics.DrawMesh(mesh, matrix, material, layer, camera, 0, Properties);
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
            // A camera-facing column keeps the ascent readable in RimWorld's overhead view.
            Vector3 center = anchor + up * (height * 0.5f);
            if (height > 0.01f && width > 0.01f)
            {
                ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.BeamOuter, center, width * 4f, height,
                    new Color(0.18f, 0.3f, 1f, fade * 0.35f), camera, 0);
                ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.BeamInner, center, width * 2f, height,
                    new Color(0.25f, 0.85f, 1.4f, fade * 0.8f), camera, 0);
                ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.BeamCore, center, width * 0.62f, height,
                    new Color(1.5f, 1.6f, 1.8f, fade), camera, 0);
            }
            float flash = Mathf.Exp(-seconds * 6f) * (1f - p);
            ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, anchor, 13f, 13f,
                new Color(0.7f, 1.2f, 1.8f, flash), camera, 0);
            DrawShockwave(anchor, Mathf.Clamp01(p / 0.38f), 24f, camera);
            if (p > 0.1f) DrawShockwave(anchor + Altitudes.AltIncVect, Mathf.Clamp01((p - 0.1f) / 0.45f), 31f, camera);

            // Bounded, analytical particles reconstruct immediately after loading, including while paused.
            for (int i = 0; i < 64; i++)
            {
                float seed = Mathf.Repeat(i * 0.618034f, 1f);
                float age = Mathf.Repeat(seconds * (0.65f + seed * 0.5f) + seed, 1f);
                float angle = i * 2.399963f + seconds * (i % 2 == 0 ? 2f : -2f);
                float spread = launch.Width * (0.35f + seed * 1.2f) * (1f - age * 0.65f);
                Vector3 position = anchor + up * (age * height) + right * (Mathf.Cos(angle) * spread);
                position += camera.transform.forward * (Mathf.Sin(angle) * spread * 0.15f);
                float alpha = rise * fade * Mathf.Sin(age * Mathf.PI);
                Color color = i % 4 == 0 ? new Color(1.3f, 0.8f, 0.25f, alpha) : new Color(0.3f, 1f, 1.6f, alpha);
                ArcaneEnergyResources.DrawBillboard(ArcaneEnergyResources.Spark, position,
                    0.12f + seed * 0.22f, 0.8f + seed * 1.8f, color, camera, 0);
            }
        }

        private static void DrawShockwave(Vector3 anchor, float progress, float maximum, Camera camera)
        {
            if (progress >= 1f) return;
            float diameter = Mathf.Lerp(1.5f, maximum, Mathf.Sqrt(progress));
            ArcaneEnergyResources.DrawMesh(MeshPool.plane10,
                Matrix4x4.TRS(anchor, Quaternion.identity, new Vector3(diameter, 1f, diameter)),
                ArcaneEnergyResources.Shockwave, new Color(0.35f, 0.9f, 1.5f, 1f - progress), 0, camera);
        }
    }
}

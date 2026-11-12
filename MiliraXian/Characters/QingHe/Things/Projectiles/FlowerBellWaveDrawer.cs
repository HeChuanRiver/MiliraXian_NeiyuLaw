using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.QingHe.Abilities.SakuraDance;
using MiliraXian.Characters.QingHe.Vfx;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Projectiles
{
    /// <summary>
    /// The shock wave picture — an open curtain on a flattened dome, the ground ring and its
    /// refraction — shared by every wave on the map: a note's impact and the frost retaliation.
    /// Radii and timing come from <see cref="FlowerBellWave"/>, so the ring drawn here is the ring
    /// that deals the damage. One instance per picture; the mesh pool is reused between frames.
    /// </summary>
    internal sealed class FlowerBellWaveDrawer
    {
        private const float WaveSaturation = 1.8f;
        private const float WaveAlpha = 0.55f;
        /// <summary>Shell thickness (3 px at 60 px a cell).</summary>
        private const float ShellThickness = 0.05f;
        private const float ShellCap = 4f;
        private const float ShellBack = 0.5f;
        /// <summary>Curtain brightness seen face on (the soft part is left to the bloom).</summary>
        private const float ShellGain = 0.56f;
        private const float DistortCells = 10f / 60f;
        private const float DistortGain = 1.5f;
        /// <summary>Central difference step; also smooths the offset into a ramp just outside the edge.</summary>
        private const float DistortStep = 0.05f;

        private static readonly Color White = new(1f, 0.95f, 0.98f, 1f);
        private static readonly int WaveId = Shader.PropertyToID("_Wave");
        private static readonly int ShellId = Shader.PropertyToID("_Shell");
        private static readonly int FootId = Shader.PropertyToID("_Foot");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int CoreId = Shader.PropertyToID("_Core");
        private static readonly int FadePowId = Shader.PropertyToID("_FadePow");
        private static readonly int DistortScaleId = Shader.PropertyToID("_DistortScale");

        /// <summary>The bloom the Flower Bell's whole picture joins; a wave of another projectile joins it too.</summary>
        internal static readonly ScreenBloomProperties Bloom = new()
        {
            group = DefDatabase<ScreenBloomGroupDef>.GetNamedSilentFail("MX_ScreenBloom_Glow"),
            tint = Color.white,
            intensity = 1.35f,
        };

        private static Material waveMat;
        private static Material distortMat;

        private readonly List<Mesh> waveMeshes = new();
        private readonly List<Mesh> distortMeshes = new();
        private int used;
        private Vector4 screenPerCell = Vector4.zero;

        /// <summary>Starts a draw pass: the mesh pool and the refraction's screen scale are reset.</summary>
        public void Begin()
        {
            used = 0;
            waveMat ??= MatFor("MX_QH_BellWave");
            distortMat ??= MatFor("MX_QH_BellWaveDistort");
            Camera cam = Find.Camera;
            screenPerCell = Vector4.zero;
            if (cam != null && cam.orthographicSize > 0f)
            {
                float h = 2f * cam.orthographicSize;
                // The grab image's rows run down the screen, north is up: y is negative.
                screenPerCell = new Vector4(1f / (h * Mathf.Max(0.1f, cam.aspect)), -1f / h, DistortGain, DistortStep);
            }
        }

        /// <summary>
        /// Draws one wave that reached <paramref name="radius"/> over <paramref name="ringSeconds"/>
        /// and has been going for <paramref name="seconds"/>.
        /// </summary>
        public void Draw(Vector3 centre, float radius, Color color, float seconds, float ringSeconds)
        {
            float life = ringSeconds + FlowerBellWave.OutSeconds;
            if (waveMat == null || seconds < 0f || seconds > life)
            {
                return;
            }
            float e = FlowerBellWave.Expansion(seconds, ringSeconds);
            float r = radius * e;
            float fade = FlowerBellWave.Fade(seconds, life);
            if (r < 0.02f || fade <= 0f)
            {
                return;
            }

            FlowerBellWave.Shape(e, out float kappa, out float cut);
            float y = AltitudeLayer.MoteOverhead.AltitudeFor() - 0.01f;
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            Mesh mesh = MeshAt(waveMeshes, used);
            BuildBox(mesh, centre, r, kappa, y);
            Color glow = Saturate(color, WaveSaturation);
            Color core = Mix(White, glow, 0.65f);
            block.Clear();
            block.SetVector(WaveId, new Vector4(r, kappa, cut, WaveAlpha * fade));
            block.SetVector(ShellId, new Vector4(ShellThickness, ShellCap, ShellBack, ShellGain));
            // The ground line: core blur 0.8 px (drawn at 1.2 px, energy kept) and a 3.5 px halo, at 60 px a cell.
            block.SetVector(FootId, new Vector4(0.02f, 0.53f, 0.058f, 0.164f));
            block.SetColor(GlowId, glow);
            block.SetColor(CoreId, core);
            block.SetFloat(FadePowId, 1f);
            ScreenBloom.Submit(Bloom, mesh, Matrix4x4.identity, waveMat, block);
            Graphics.DrawMesh(mesh, Matrix4x4.identity, waveMat, 0, null, 0, block);

            if (distortMat != null && screenPerCell.x > 0f)
            {
                Mesh dm = MeshAt(distortMeshes, used);
                BuildBox(dm, centre, r, kappa, y);
                block.Clear();
                block.SetVector(WaveId, new Vector4(r, kappa, cut, DistortCells * fade));
                block.SetVector(DistortScaleId, screenPerCell);
                Graphics.DrawMesh(dm, Matrix4x4.identity, distortMat, 0, null, 0, block);
            }
            block.Clear();
            used++;
        }

        public void Dispose()
        {
            foreach (Mesh m in waveMeshes)
            {
                Object.Destroy(m);
            }
            foreach (Mesh m in distortMeshes)
            {
                Object.Destroy(m);
            }
            waveMeshes.Clear();
            distortMeshes.Clear();
        }

        private static Material MatFor(string shaderDef)
        {
            Shader shader = DefDatabase<ShaderDef>.GetNamedSilentFail(shaderDef)?.Shader;
            if (shader == null)
            {
                Log.ErrorOnce($"[MiliraXian] Flower Bell: missing shader '{shaderDef}'.", GenText.StableStringHash(shaderDef));
                return null;
            }
            return MaterialPool.MatFrom(BaseContent.WhiteTex, shader, Color.white);
        }

        private static Mesh MeshAt(List<Mesh> pool, int i)
        {
            while (pool.Count <= i)
            {
                pool.Add(MapComponent_SakuraDanceVisuals.NewMesh());
            }
            return pool[i];
        }

        /// <summary>A quad over the dome; the uv is the map offset from the centre in cells.</summary>
        private static void BuildBox(Mesh mesh, Vector3 c, float r, float kappa, float y)
        {
            const float pad = 0.2f;
            Vector2 lo = new(-r - pad, -r - pad);
            Vector2 hi = new(r + pad, r * Mathf.Sqrt(1f + kappa * kappa) + pad);
            Color w = Color.white;
            MapComponent_QingheVfx.ClearBuffers();
            MapComponent_QingheVfx.AddVertex(new Vector3(c.x + lo.x, y, c.z + lo.y), new Vector2(lo.x, lo.y), w);
            MapComponent_QingheVfx.AddVertex(new Vector3(c.x + lo.x, y, c.z + hi.y), new Vector2(lo.x, hi.y), w);
            MapComponent_QingheVfx.AddVertex(new Vector3(c.x + hi.x, y, c.z + hi.y), new Vector2(hi.x, hi.y), w);
            MapComponent_QingheVfx.AddVertex(new Vector3(c.x + hi.x, y, c.z + lo.y), new Vector2(hi.x, lo.y), w);
            MapComponent_QingheVfx.AddTriangle(0, 1, 2);
            MapComponent_QingheVfx.AddTriangle(0, 2, 3);
            MapComponent_QingheVfx.Upload(mesh);
        }

        private static Color Saturate(Color c, float k)
        {
            float g = (c.r + c.g + c.b) / 3f;
            return new Color(Mathf.Clamp(g + (c.r - g) * k, 0f, 1.5f), Mathf.Clamp(g + (c.g - g) * k, 0f, 1.5f), Mathf.Clamp(g + (c.b - g) * k, 0f, 1.5f), 1f);
        }

        private static Color Mix(Color a, Color b, float t) => new(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f);
    }
}

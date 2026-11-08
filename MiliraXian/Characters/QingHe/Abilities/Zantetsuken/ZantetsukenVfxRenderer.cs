using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.QingHe.Abilities.SakuraDance;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Vfx;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities.Zantetsuken
{
    /// <summary>The lotus, light bars, shock rings and flashes of one cast.</summary>
    [StaticConstructorOnStartup]
    internal sealed class ZantetsukenVfxRenderer : IQingheVfxRenderer
    {
        private const float AlphaScale = 4f;
        private static readonly int LotusId = Shader.PropertyToID("_Lotus");
        private static readonly int Lotus2Id = Shader.PropertyToID("_Lotus2");
        private static readonly int GlowId = Shader.PropertyToID("_Glow");
        private static readonly int WhiteId = Shader.PropertyToID("_White");
        private static readonly int FillId = Shader.PropertyToID("_Fill");
        private static readonly int RimWidthId = Shader.PropertyToID("_RimWidth");
        private static Material lotusMat;

        private readonly CompProperties_AbilityZantetsuken props;
        private readonly ZantetsukenVisualProps vis;
        private readonly Pawn pawn;
        private readonly int kickTick;
        private readonly Vector3 centre;
        private readonly bool gaeshi;
        private readonly System.Func<bool> stillOn;
        private Mesh lotusMesh;
        private Mesh lineMesh;
        private bool active = true;

        public ZantetsukenVfxRenderer(CompProperties_AbilityZantetsuken props, Pawn pawn, int kickTick, IntVec3 lotusCell,
            Vector3 aim, bool gaeshi, System.Func<bool> stillOn)
        {
            this.props = props;
            vis = props.visuals;
            this.pawn = pawn;
            this.kickTick = kickTick;
            centre = lotusCell.ToVector3Shifted();
            this.gaeshi = gaeshi;
            this.stillOn = stillOn;
        }

        public bool Active => active;

        internal static ZantetsukenVfxRenderer Find(Map map, Pawn pawn, int kickTick) =>
            MapComponent_QingheVfx.Find<ZantetsukenVfxRenderer>(map, r => r.pawn == pawn && r.kickTick == kickTick);

        private float Seconds => VisualClock.TicksSince(kickTick) / 60f;

        private float LifeSeconds => gaeshi
            ? Mathf.Max(props.GaeshiEnd, props.ClickTime + props.burstSeconds)
            : props.HoldEnd + props.plainFadeSeconds;

        public void Update(Map map)
        {
            float t = Seconds;
            // An interrupted cast drops its effect at once instead of playing it out.
            if ((t < props.EndSeconds(gaeshi) && !stillOn()) || t > LifeSeconds)
            {
                active = false;
            }
        }

        public void Dispose()
        {
            Object.Destroy(lotusMesh);
            Object.Destroy(lineMesh);
        }

        private float BlueK(float t) => gaeshi && t >= props.HoldEnd ? Smooth((t - props.HoldEnd) / vis.blueRamp) : 0f;

        private Color Glow(float k) => Color.Lerp(vis.pink, vis.blue, k);
        private Color White(float k) => Color.Lerp(vis.white, vis.blueWhite, k);
        private Color Fill(float k) => Color.Lerp(vis.red, vis.blueDeep, k);

        public void Draw(Map map)
        {
            float t = Seconds;
            if (t < 0f)
            {
                return;
            }
            float k = BlueK(t);
            DrawLotus(t, k);
            DrawLines(t, k);
        }

        private void LotusState(float t, out float bright, out float scale, out float rise)
        {
            bright = scale = 1f;
            rise = 0f;
            if (t < props.takeoffSeconds)
            {
                bright = 0f;
                return;
            }
            bright = vis.lotusBase * Smooth((t - props.takeoffSeconds) / (props.apexSeconds - props.takeoffSeconds));
            float h = vis.curtainHeight;
            if (t >= props.landSeconds)
            {
                float u = (t - props.landSeconds) / vis.landFlareSeconds;
                if (u < 1f)
                {
                    bright = vis.lotusBase + 0.9f * Mathf.Exp(-u * 3f);
                    rise = h * Mathf.Pow(1f - u, 0.6f);
                }
            }
            if (gaeshi && t >= props.HoldEnd)
            {
                float kk = Smooth((t - props.HoldEnd) / (props.ScoopEnd - props.HoldEnd));
                bright = Mathf.Max(bright, vis.lotusBase + 0.55f * kk);
                rise = Mathf.Max(rise, h * kk);
                if (t >= props.ClickTime)
                {
                    float u = Mathf.Clamp01((t - props.ClickTime) / props.burstSeconds);
                    scale = 1f + 0.3f * EaseOut(u);
                    bright = Mathf.Pow(1f - u, 1.3f);
                    rise = h * (1f - u) * (1f - u);
                }
            }
            if (!gaeshi && t >= props.HoldEnd)
            {
                bright *= 1f - Mathf.Clamp01((t - props.HoldEnd) / props.plainFadeSeconds);
            }
        }

        private void DrawLotus(float t, float k)
        {
            LotusState(t, out float bright, out float scale, out float rise);
            if (bright <= 0.005f)
            {
                return;
            }
            Material mat = lotusMat ??= MaterialPool.MatFrom(BaseContent.WhiteTex, MX_QHDefOf.MX_QH_Lotus.Shader, Color.white);
            float depth = props.depthToScreen;
            float r = props.lotusRadius * scale * 1.1f;
            float top = rise * vis.curtainLift;
            float y = AltitudeLayer.MoteLow.AltitudeFor();
            MapComponent_QingheVfx.ClearBuffers();
            // The uv is the offset from the centre in cells, which the shader draws the lotus from.
            Vector2 lo = new(-r, -r * depth);
            Vector2 hi = new(r, r * depth + top);
            Color c = Color.white;
            MapComponent_QingheVfx.AddVertex(new Vector3(centre.x + lo.x, y, centre.z + lo.y), new Vector2(lo.x, lo.y), c);
            MapComponent_QingheVfx.AddVertex(new Vector3(centre.x + lo.x, y, centre.z + hi.y), new Vector2(lo.x, hi.y), c);
            MapComponent_QingheVfx.AddVertex(new Vector3(centre.x + hi.x, y, centre.z + hi.y), new Vector2(hi.x, hi.y), c);
            MapComponent_QingheVfx.AddVertex(new Vector3(centre.x + hi.x, y, centre.z + lo.y), new Vector2(hi.x, lo.y), c);
            MapComponent_QingheVfx.AddTriangle(0, 1, 2);
            MapComponent_QingheVfx.AddTriangle(0, 2, 3);
            lotusMesh ??= MapComponent_QingheVfx.NewMesh("MX_QH_Lotus");
            MapComponent_QingheVfx.Upload(lotusMesh);

            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetVector(LotusId, new Vector4(bright, scale, top, props.lotusRadius));
            block.SetVector(Lotus2Id, new Vector4(depth, 0.07f, vis.rimStrength, 0.25f * vis.rimStrength));
            block.SetFloat(RimWidthId, vis.rimWidth);
            block.SetColor(GlowId, Glow(k));
            block.SetColor(WhiteId, White(k));
            block.SetColor(FillId, Fill(k));
            ScreenBloom.Submit(vis.bloom, lotusMesh, Matrix4x4.identity, mat, block);
            Graphics.DrawMesh(lotusMesh, Matrix4x4.identity, mat, 0, null, 0, block);
            block.Clear();
        }

        private void DrawLines(float t, float k)
        {
            Material mat = MapComponent_SakuraDanceVisuals.Material(vis.glowTexPath, "MX_QH_SakuraTrailAdd");
            MapComponent_QingheVfx.ClearBuffers();
            Color glow = Glow(k);
            Color white = White(k);
            float y = AltitudeLayer.MoteOverhead.AltitudeFor();
            AddRing(t, props.landSeconds, 1f, glow, white, y);
            if (gaeshi)
            {
                AddRing(t, props.ClickTime, 0.8f, glow, white, y);
            }
            AddFlash(t, props.landSeconds, centre, 1.4f, 0.05f, 1f, white, y);
            if (MapComponent_QingheVfx.BuffersEmpty)
            {
                return;
            }
            lineMesh ??= MapComponent_QingheVfx.NewMesh("MX_QH_ZantetsukenLines");
            MapComponent_QingheVfx.Upload(lineMesh);
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetFloat(MapComponent_SakuraDanceVisuals.BrightnessId, 1f);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AlphaScaleId, AlphaScale);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AdditiveId, 1f);
            ScreenBloom.Submit(vis.bloom, lineMesh, Matrix4x4.identity, mat, block);
            Graphics.DrawMesh(lineMesh, Matrix4x4.identity, mat, 0, null, 0, block);
            block.Clear();
        }

        /// <summary>Vertex alpha is stored divided by <see cref="AlphaScale"/>, so intensities up to 4 survive the 8-bit colour.</summary>
        private static Color WithIntensity(Color c, float intensity) => new(c.r, c.g, c.b, Mathf.Clamp01(intensity / AlphaScale));

        private Vector3 MapOf(Vector2 g, float y) => new(centre.x + g.x, y, centre.z + g.y * props.depthToScreen);

        private void AddRing(float t, float t0, float strength, Color glow, Color white, float y)
        {
            const float life = 0.35f;
            float u = (t - t0) / life;
            if (u < 0f || u > 1f)
            {
                return;
            }
            float r = props.lotusRadius * 1.1f * EaseOut(u);
            Color c = WithIntensity(Color.Lerp(glow, white, 0.5f), 0.65f * Mathf.Pow(1f - u, 1.5f) * strength);
            const int n = 48;
            Vector3 prev = MapOf(new Vector2(r, 0f), y);
            for (int i = 1; i <= n; i++)
            {
                float a = i / (float)n * Mathf.PI * 2f;
                Vector3 p = MapOf(new Vector2(Mathf.Cos(a) * r, Mathf.Sin(a) * r), y);
                MapComponent_QingheVfx.AddLine(prev, p, 0.24f, c, c);
                prev = p;
            }
        }

        private static void AddFlash(float t, float t0, Vector3 at, float size, float life, float strength, Color white, float y)
        {
            if (t < t0 - 0.01f)
            {
                return;
            }
            float x = (t - t0) / life;
            float k = Mathf.Exp(-x * x) * strength;
            if (k < 0.01f)
            {
                return;
            }
            MapComponent_QingheVfx.AddQuad(new Vector3(at.x, y, at.z), size * 2f, WithIntensity(white, k * 2f));
        }

        private static float Smooth(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * (3f - 2f * x);
        }

        private static float EaseOut(float u) => 1f - (1f - u) * (1f - u);
    }
}

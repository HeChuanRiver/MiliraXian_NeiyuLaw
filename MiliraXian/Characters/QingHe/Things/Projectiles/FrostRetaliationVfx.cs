using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.QingHe.Abilities.SakuraDance;
using MiliraXian.Characters.QingHe.Vfx;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Projectiles
{
    /// <summary>
    /// The frost retaliation's impact picture: the shared shock wave (see
    /// <see cref="FlowerBellWaveDrawer"/>) plus the short impact flash the bell notes also get.
    /// Purely visual and not saved; a wave on the map in the middle of a save simply ends.
    /// </summary>
    [StaticConstructorOnStartup]
    internal sealed class FrostRetaliationVfx : IQingheVfxRenderer
    {
        private const float FlashRise = 0.03f;
        private const float FlashHold = 0.06f;
        private const float FlashFall = 0.33f;
        private const float AlphaScale = 4f;
        private const float PreviewPixelsPerCell = 60f;

        /// <summary>The wave's colour: the winter season tint, so the frost reads apart from the notes.</summary>
        private static readonly Color FrostColor = new(0.60f, 0.85f, 1.00f, 1f);
        private static readonly Color White = new(1f, 0.95f, 0.98f, 1f);

        private static Material glowMat;

        private sealed class Burst
        {
            public Vector3 centre;
            public float radius;
            public int startTick;
        }

        private readonly Map map;
        private readonly List<Burst> bursts = new();
        private readonly FlowerBellWaveDrawer waveDrawer = new();
        private Mesh flashMesh;

        private FrostRetaliationVfx(Map map)
        {
            this.map = map;
        }

        public bool Active => bursts.Count > 0;

        /// <summary>Starts a wave on the map; called by the projectile on impact.</summary>
        internal static void Notify(Map map, Vector3 centre, float radius, int now)
        {
            if (map == null || map.GetComponent<MapComponent_QingheVfx>() == null)
            {
                return;
            }
            FrostRetaliationVfx vfx = MapComponent_QingheVfx.Find<FrostRetaliationVfx>(map, v => v.map == map);
            if (vfx == null)
            {
                vfx = new FrostRetaliationVfx(map);
                MapComponent_QingheVfx.Register(map, vfx);
            }
            vfx.bursts.Add(new Burst { centre = centre, radius = radius, startTick = now });
        }

        public void Update(Map map)
        {
            float vt = VisualClock.Ticks;
            for (int i = bursts.Count - 1; i >= 0; i--)
            {
                if ((vt - bursts[i].startTick) / 60f > FlowerBellWave.FrostLifeSeconds)
                {
                    bursts.RemoveAt(i);
                }
            }
        }

        public void Draw(Map map)
        {
            if (bursts.Count == 0 || Find.CurrentMap != map)
            {
                return;
            }
            float vt = VisualClock.Ticks;
            waveDrawer.Begin();
            DrawFlashes(vt);
            foreach (Burst b in bursts)
            {
                waveDrawer.Draw(b.centre, b.radius, FrostColor, (vt - b.startTick) / 60f, FlowerBellWave.FrostRingSeconds);
            }
        }

        public void Dispose()
        {
            waveDrawer.Dispose();
            Object.Destroy(flashMesh);
            flashMesh = null;
            bursts.Clear();
        }

        // --- the impact flash: a short white spot inside a longer season-coloured one, as for a note
        private void DrawFlashes(float vt)
        {
            glowMat ??= MapComponent_SakuraDanceVisuals.Material("MiliraXianQinghe/Effect/SakuraDance/Glow", "MX_QH_SakuraTrailAdd");
            if (glowMat == null)
            {
                return;
            }
            float y = AltitudeLayer.MoteOverhead.AltitudeFor() + 0.03f;
            float life = FlashRise + FlashHold + FlashFall;
            MapComponent_SakuraDanceVisuals.ClearBuffers();
            foreach (Burst b in bursts)
            {
                float age = (vt - b.startTick) / 60f;
                if (age < 0f || age >= life)
                {
                    continue;
                }
                float fl = age < FlashRise ? age / FlashRise
                    : age < FlashRise + FlashHold ? 1f
                    : Mathf.Pow(1f - (age - FlashRise - FlashHold) / FlashFall, 2f);
                float grow = 1f + 0.6f * Mathf.Min(1f, age / life);
                AddFlash(b.centre, y, 0.20f * grow, fl * 110f, White);
                AddFlash(b.centre, y, 0.45f * grow, fl * 140f, FrostColor);
            }
            if (MapComponent_SakuraDanceVisuals.BuffersEmpty)
            {
                return;
            }
            flashMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
            MapComponent_SakuraDanceVisuals.Upload(flashMesh);
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetFloat(MapComponent_SakuraDanceVisuals.BrightnessId, 1f);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AlphaScaleId, AlphaScale);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AdditiveId, 1f);
            ScreenBloom.Submit(FlowerBellWaveDrawer.Bloom, flashMesh, Matrix4x4.identity, glowMat, block);
            Graphics.DrawMesh(flashMesh, Matrix4x4.identity, glowMat, 0, null, 0, block);
            block.Clear();
        }

        /// <summary>A gaussian spot (sigma in cells, amp in px units at 60 px a cell), as a soft quad.</summary>
        private static void AddFlash(Vector3 at, float y, float sigma, float amp, Color color)
        {
            float sigmaPx = sigma * PreviewPixelsPerCell;
            float peak = amp / (2f * Mathf.PI * sigmaPx * sigmaPx);
            if (peak < 0.002f)
            {
                return;
            }
            float alpha = Mathf.Clamp01(peak / AlphaScale);
            MapComponent_SakuraDanceVisuals.AddQuad(new Vector3(at.x, y, at.z), sigma * 4f, 0f, Vector2.zero, Vector2.one,
                new Color(color.r, color.g, color.b, alpha));
        }
    }
}

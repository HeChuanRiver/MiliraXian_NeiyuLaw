using MiliraXian.Characters.Common.Vfx.Emitter;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Projectiles
{
    /// <summary>Timing and flash toggle carried by <c>MX_Vfx_BellWave</c> and <c>MX_Vfx_FrostRetaliation</c>.</summary>
    public class BellWaveVfxExtension : DefModExtension
    {
        public float ringSeconds = FlowerBellWave.RingSeconds;
        public bool emitFlash;
    }

    /// <summary>
    /// Shared shock wave emitter for both Flower Bell notes and Lunar Mirror frost retaliations:
    /// an expanding flattened dome curtain, ground ring, refraction lens, and (optionally) a
    /// two-layer impact flash. Radii and curves match <see cref="FlowerBellWave"/> so the drawn ring
    /// stays aligned with the damage ring.
    /// </summary>
    internal sealed class BellWaveEmitter : VfxEmitter
    {
        private const float WaveSaturation = 1.8f;
        private const float WaveAlpha = 0.55f;
        private const float DistortCells = 10f / 60f;
        private const float QuadPad = 0.2f;

        private const float FlashRise = 0.03f;
        private const float FlashHold = 0.06f;
        private const float FlashFall = 0.33f;
        private const float FlashLife = FlashRise + FlashHold + FlashFall;
        private const float FlashAlphaScale = 4f;
        private const float PreviewPixelsPerCell = 60f;

        private static readonly Color White = new(1f, 0.95f, 0.98f, 1f);
        private static readonly Color FrostColor = new(0.60f, 0.85f, 1.00f, 1f);
        private static readonly BellWaveVfxExtension DefaultProps = new();

        private float radius = FlowerBellWave.Radius;
        private float ringSeconds = FlowerBellWave.RingSeconds;
        private Color baseColor = FrostColor;
        private Color glowColor;
        private Color coreColor;
        private bool emitFlash;

        private int quadSlot;
        private int waveSlot;
        private int distortWaveSlot;
        private int glowSlot;
        private int coreSlot;

        public BellWaveEmitter()
        {
        }

        internal static void SpawnNoteWave(Map map, Vector3 centre, float radius, Color color)
        {
            Spawn(map, VfxDefOf.MX_Vfx_BellWave, centre, radius, color, FlowerBellWave.RingSeconds);
        }

        internal static void SpawnFrostRetaliation(Map map, Vector3 centre, float radius)
        {
            Spawn(map, VfxDefOf.MX_Vfx_FrostRetaliation, centre, radius, FrostColor, FlowerBellWave.FrostRingSeconds);
        }

        private static void Spawn(Map map, VfxDef def, Vector3 centre, float radius, Color color, float ringSeconds)
        {
            MapComponent_VfxEmitterHost host = map?.GetComponent<MapComponent_VfxEmitterHost>();
            if (host == null || def == null)
            {
                return;
            }
            VfxEmitterHandle handle = host.Spawn(def, VfxAnchor.At(centre));
            if (host.TryGetEmitter(handle, out BellWaveEmitter emitter))
            {
                emitter.Configure(radius, color, ringSeconds);
            }
        }

        internal override void Reset(VfxEffect effect, VfxAnchor anchor, int version, int nowTick, Vector3 position)
        {
            base.Reset(effect, anchor, version, nowTick, position);

            BellWaveVfxExtension ext = effect.Def?.GetModExtension<BellWaveVfxExtension>() ?? DefaultProps;
            radius = FlowerBellWave.Radius;
            ringSeconds = ext.ringSeconds;
            emitFlash = ext.emitFlash;
            SetColor(FrostColor);

            quadSlot = effect.RequireVectorSlot("bellWaveQuad");
            waveSlot = effect.RequireVectorSlot("bellWaveWave");
            distortWaveSlot = effect.RequireVectorSlot("bellWaveDistortWave");
            glowSlot = effect.RequireVectorSlot("bellWaveGlow");
            coreSlot = effect.RequireVectorSlot("bellWaveCore");

            if (emitFlash && Particles != null && Particles.Length >= 2)
            {
                Emit(position, 0f, 0f, Color.clear, particleGroup: 0);
                Emit(position, 0f, 0f, Color.clear, particleGroup: 0);
                Particles[0].Alive = false;
                Particles[1].Alive = false;
            }
        }

        private void Configure(float waveRadius, Color color, float expandSeconds)
        {
            radius = waveRadius;
            ringSeconds = expandSeconds;
            SetColor(color);
        }

        private void SetColor(Color color)
        {
            baseColor = color;
            glowColor = Saturate(color, WaveSaturation);
            coreColor = Mix(White, glowColor, 0.65f);
        }

        internal override bool Advance(float deltaSeconds, int nowTick, Map map)
        {
            if (!base.Advance(deltaSeconds, nowTick, map))
            {
                return false;
            }

            float t = AgeSeconds;
            UpdateWave(t);
            if (emitFlash)
            {
                UpdateFlashes(t);
            }
            return true;
        }

        private void UpdateWave(float seconds)
        {
            float life = ringSeconds + FlowerBellWave.OutSeconds;
            if (seconds < 0f || seconds > life)
            {
                SetVectorChannel(quadSlot, Vector4.zero);
                return;
            }

            float e = FlowerBellWave.Expansion(seconds, ringSeconds);
            float r = radius * e;
            float fade = FlowerBellWave.Fade(seconds, life);
            if (r < 0.02f || fade <= 0f)
            {
                SetVectorChannel(quadSlot, Vector4.zero);
                return;
            }

            FlowerBellWave.Shape(e, out float kappa, out float cut);
            float extraTop = r * (Mathf.Sqrt(1f + kappa * kappa) - 1f);

            SetVectorChannel(quadSlot, new Vector4(r + QuadPad, r + QuadPad, extraTop, 0f));
            SetVectorChannel(waveSlot, new Vector4(r, kappa, cut, WaveAlpha * fade));
            SetVectorChannel(distortWaveSlot, new Vector4(r, kappa, cut, DistortCells * fade));
            SetVectorChannel(glowSlot, glowColor);
            SetVectorChannel(coreSlot, coreColor);
        }

        private void UpdateFlashes(float age)
        {
            if (Count < 2)
            {
                return;
            }
            if (age < 0f || age >= FlashLife)
            {
                Particles[0].Alive = false;
                Particles[1].Alive = false;
                return;
            }

            float fl = age < FlashRise ? age / FlashRise
                : age < FlashRise + FlashHold ? 1f
                : Mathf.Pow(1f - (age - FlashRise - FlashHold) / FlashFall, 2f);
            float grow = 1f + 0.6f * Mathf.Min(1f, age / FlashLife);

            SetFlashParticle(0, 0.20f * grow, fl * 110f, White);
            SetFlashParticle(1, 0.45f * grow, fl * 140f, baseColor);
        }

        private void SetFlashParticle(int index, float sigma, float amp, Color color)
        {
            float sigmaPx = sigma * PreviewPixelsPerCell;
            float peak = amp / (2f * Mathf.PI * sigmaPx * sigmaPx);
            VfxParticle p = Particles[index];
            if (peak < 0.002f)
            {
                p.Alive = false;
                Particles[index] = p;
                return;
            }
            p.Alive = true;
            p.Position = Position;
            p.Size = sigma * 4f;
            p.Color = new Color(color.r, color.g, color.b, Mathf.Clamp01(peak / FlashAlphaScale));
            Particles[index] = p;
        }

        private static Color Saturate(Color c, float k)
        {
            float g = (c.r + c.g + c.b) / 3f;
            return new Color(
                Mathf.Clamp(g + (c.r - g) * k, 0f, 1.5f),
                Mathf.Clamp(g + (c.g - g) * k, 0f, 1.5f),
                Mathf.Clamp(g + (c.b - g) * k, 0f, 1.5f),
                1f);
        }

        private static Color Mix(Color a, Color b, float t) =>
            new(a.r + (b.r - a.r) * t, a.g + (b.g - a.g) * t, a.b + (b.b - a.b) * t, 1f);
    }
}

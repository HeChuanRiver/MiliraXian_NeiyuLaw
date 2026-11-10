using System.Collections.Generic;
using UnityEngine;

namespace MiliraXian.Characters.QingHe.Things.Projectiles
{
    /// <summary>
    /// Time curves of a note's shock wave, shared by the damage ring and the picture (owner 11-09/10,
    /// see RangedRework.md 2.11). Seconds are game seconds (60 ticks).
    /// </summary>
    public static class FlowerBellWave
    {
        /// <summary>The damage (and ground ring) radius; the same at every charge tier (owner 11-10).</summary>
        public const float Radius = 1.6f;
        /// <summary>Expansion to the full radius.</summary>
        public const float RingSeconds = 0.9f;
        /// <summary>After reaching the full radius the wave fades out over this.</summary>
        public const float OutSeconds = 0.18f;
        public const float LifeSeconds = RingSeconds + OutSeconds;
        /// <summary>r = R (1 - (1 - u)^p): fast at first, slowing to 0 at R.</summary>
        private const float EasePow = 5f;
        /// <summary>Full brightness for this share of the life, then (1 - u)^FadePow down to 0.</summary>
        private const float Hold = 0.06f;
        private const float FadePow = 1.5f;
        // The curtain lies on a flattened hemisphere x² + z² + (h/κ)² = r² and reaches c·κ·r; both
        // follow the expansion: nearly a whole bubble at first, lower and straighter at full size.
        private const float KappaStart = 0.9f;
        private const float KappaEnd = 0.4f;
        private const float CutStart = 0.95f;
        private const float CutEnd = 0.6f;

        /// <summary>Share of the full radius after <paramref name="seconds"/>.</summary>
        public static float Expansion(float seconds)
        {
            float u = Mathf.Clamp01(seconds / RingSeconds);
            return 1f - Mathf.Pow(1f - u, EasePow);
        }

        /// <summary>Brightness (and distortion strength) after <paramref name="seconds"/>, by time, not by radius.</summary>
        public static float Fade(float seconds)
        {
            float u = seconds / LifeSeconds;
            if (u < 0f || u >= 1f)
            {
                return 0f;
            }
            return u < Hold ? 1f : Mathf.Pow(1f - (u - Hold) / (1f - Hold), FadePow);
        }

        /// <summary>Ellipsoid height / radius (κ) and the share of it the curtain reaches (c) at expansion <paramref name="e"/>.</summary>
        public static void Shape(float e, out float kappa, out float cut)
        {
            float s = Mathf.Clamp01(e);
            s = s * s * (3f - 2f * s);
            kappa = Mathf.Lerp(KappaStart, KappaEnd, s);
            cut = Mathf.Lerp(CutStart, CutEnd, s);
        }

        /// <summary>The damage ring's radius at every tick of the expansion (tick 0 = impact).</summary>
        public static List<float> Radii(float radius)
        {
            int n = Mathf.RoundToInt(RingSeconds * 60f);
            List<float> list = new(n + 1);
            for (int i = 0; i <= n; i++)
            {
                list.Add(radius * Expansion(i / 60f));
            }
            return list;
        }
    }
}

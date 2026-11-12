using System.Collections.Generic;
using UnityEngine;

namespace MiliraXian.Characters.QingHe.Things.Projectiles
{
    /// <summary>
    /// Time curves of a note's shock wave, shared by the damage ring and the picture.
    /// Seconds are game seconds (60 ticks).
    /// </summary>
    public static class FlowerBellWave
    {
        /// <summary>The default damage (and ground ring) radius; a charged note sets its own.</summary>
        public const float Radius = 1.6f;
        /// <summary>Expansion to the full radius.</summary>
        public const float RingSeconds = 0.9f;
        /// <summary>After reaching the full radius the wave fades out over this.</summary>
        public const float OutSeconds = 0.18f;
        public const float LifeSeconds = RingSeconds + OutSeconds;
        /// <summary>The frost retaliation's wave: a smaller ring, opening in half the time of a note's.</summary>
        public const float FrostRadius = 1f;
        public const float FrostRingSeconds = 0.5f;
        public const float FrostLifeSeconds = FrostRingSeconds + OutSeconds;
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
            return Expansion(seconds, RingSeconds);
        }

        /// <summary>Share of the full radius after <paramref name="seconds"/>, over a given expansion time.</summary>
        public static float Expansion(float seconds, float ringSeconds)
        {
            float u = Mathf.Clamp01(seconds / Mathf.Max(0.01f, ringSeconds));
            return 1f - Mathf.Pow(1f - u, EasePow);
        }

        /// <summary>Brightness (and distortion strength) after <paramref name="seconds"/>, by time, not by radius.</summary>
        public static float Fade(float seconds)
        {
            return Fade(seconds, LifeSeconds);
        }

        /// <summary>Brightness after <paramref name="seconds"/>, over a given total life.</summary>
        public static float Fade(float seconds, float lifeSeconds)
        {
            float u = seconds / Mathf.Max(0.01f, lifeSeconds);
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
            return Radii(radius, RingSeconds);
        }

        /// <summary>The damage ring's radius at every tick of an expansion lasting <paramref name="ringSeconds"/>.</summary>
        public static List<float> Radii(float radius, float ringSeconds)
        {
            int n = Mathf.RoundToInt(Mathf.Max(0.01f, ringSeconds) * 60f);
            List<float> list = new(n + 1);
            for (int i = 0; i <= n; i++)
            {
                list.Add(radius * Expansion(i / 60f, ringSeconds));
            }
            return list;
        }
    }
}

using System;
using Verse;

namespace MiliraXian.Characters
{
    // Tier two changes magnitude, not targeting, damage semantics or resource rules.
    // Evaluate against the post-patch baseline once, never against an already tuned value.
    internal static class ConservativePowerTuning
    {
        public const float Damage = .85f;
        public const float Bonus = .85f;
        public const float Defense = .95f;
        public const float Cooldown = 1.20f;

        public static float Scale(float original, float scale, float neutral = 0f)
        {
            return neutral + (original - neutral) * scale;
        }

        public static object Number(object original, float scale, float neutral = 0f)
        {
            if (original is float f) return Scale(f, scale, neutral);
            if (original is int i)
                return (int)Math.Round(Scale(i, scale, neutral), MidpointRounding.AwayFromZero);
            if (original is IntRange range)
                return new IntRange((int)Number(range.min, scale, neutral), (int)Number(range.max, scale, neutral));
            throw new ArgumentException("Unsupported numeric tuning: " + original?.GetType().FullName);
        }

        public static int RemapCooldown(int remaining, int previousTotal, int nextTotal)
        {
            double progress = Math.Max(0d, Math.Min(1d, remaining / (double)Math.Max(1, previousTotal)));
            return (int)Math.Ceiling(progress * Math.Max(0, nextTotal));
        }
    }
}

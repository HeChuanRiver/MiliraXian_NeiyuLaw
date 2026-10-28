using System;

namespace MiliraXian.Characters.Neiyu;

// Engine-independent arithmetic shared by the live system and its focused checks.
internal static class NeiyuSwordCombatRules
{
    internal static float Finite(float value, float fallback = 0f) => float.IsFinite(value) ? value : fallback;
    internal static float Health(float health) => Math.Clamp(Finite(health, 1f), 0f, 1f);
    internal static float Maximum(float basis, float minimumFraction, float health) =>
        Math.Max(1f, Finite(basis, 100f)) *
        (Math.Clamp(Finite(minimumFraction, .45f), .01f, 1f)
        + (1f - Math.Clamp(Finite(minimumFraction, .45f), .01f, 1f)) * Health(health));
    internal static float Recovery(float rate, float minimumFraction, float health) =>
        Math.Max(0f, Finite(rate, 4f)) *
        (Math.Clamp(Finite(minimumFraction, .1f), 0f, 1f)
        + (1f - Math.Clamp(Finite(minimumFraction, .1f), 0f, 1f)) * Health(health));

    // One roll: 0 = failed, 1 = normal, 2 = perfect. Perfect is an absolute
    // probability, bounded by total success rather than rolled a second time.
    internal static int Deflection(float roll, float success, float perfect)
    {
        success = Math.Clamp(Finite(success), 0f, 1f);
        perfect = Math.Clamp(Finite(perfect), 0f, success);
        return roll < perfect ? 2 : roll < success ? 1 : 0;
    }
}

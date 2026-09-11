using UnityEngine;

namespace MiliraXian.Characters
{
    internal static class CountShieldUtility
    {
        public static int CalculateCost(float damageAmount, float damagePerLayer)
        {
            return Mathf.Max(1, Mathf.CeilToInt(damageAmount / Mathf.Max(0.1f, damagePerLayer)));
        }
    }
}

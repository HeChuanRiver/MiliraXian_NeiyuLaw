using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    internal static class CountShieldUtility
    {
        public static int CalculateCost(float damageAmount, float damagePerLayer, bool freeBelowThreshold)
        {
            float threshold = Mathf.Max(0.1f, damagePerLayer);
            if (damageAmount <= 0f || (freeBelowThreshold && damageAmount < threshold)) return 0;
            return Mathf.Max(1, Mathf.CeilToInt(damageAmount / threshold));
        }

        public static string RuleDescription(float damagePerLayer, bool freeBelowThreshold)
        {
            string key = freeBelowThreshold ? "MX_CountShield_FreeSmallHits" : "MX_CountShield_ChargedSmallHits";
            string source = freeBelowThreshold
                ? "单次伤害低于{0}点时不消耗盾层；达到{0}点后，每{0}点伤害消耗1层，向上取整。"
                : "每{0}点伤害消耗1层，向上取整；正伤害至少消耗1层。";
            return key.CanTranslate() ? key.Translate(damagePerLayer).ToString() : string.Format(source, damagePerLayer);
        }
    }
}

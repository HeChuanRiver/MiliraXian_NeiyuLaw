using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public static class PawnResourceScaleUtility
    {
        public static float Evaluate(float baseValue, float min, float max, float percent, string method)
        {
            switch (method?.ToLower() ?? "linear")
            {
                case "linear":
                default:
                    return baseValue * Mathf.Lerp(min, max, percent);
            }
        }
    }
}

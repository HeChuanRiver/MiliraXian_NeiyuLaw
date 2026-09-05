using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public static class PawnResourceScaleUtility
    {
        public static float Evaluate(float baseValue, float min, float max, float percent, string method)
        {
            return (method?.ToLower() ?? "linear") switch
            {
                _ => baseValue * Mathf.Lerp(min, max, percent),
            };
        }
    }
}

using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class ScaledValue : IExposable
    {
        public float baseValue;
        public float min = 1f;
        public float max = 1f;
        public HediffDef resourceDef;
        public string method = "linear";

        public void ExposeData()
        {
            Scribe_Values.Look(ref baseValue, "baseValue", 0f);
            Scribe_Values.Look(ref min, "min", 1f);
            Scribe_Values.Look(ref max, "max", 1f);
            Scribe_Defs.Look(ref resourceDef, "resourceDef");
            Scribe_Values.Look(ref method, "method", "linear");
        }

        public float GetValue(Pawn pawn)
        {
            if (resourceDef == null || pawn == null)
                return baseValue;

            float percent = PawnSpecialResourceUtility.GetResourcePercent(pawn, resourceDef);

            switch (method?.ToLower() ?? "linear")
            {
                case "linear":
                default:
                    return baseValue * Mathf.Lerp(min, max, percent);
            }
        }
    }
}

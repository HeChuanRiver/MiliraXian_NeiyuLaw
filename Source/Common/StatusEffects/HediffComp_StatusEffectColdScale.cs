using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_StatusEffectColdScale : HediffCompProperties_AccumulationScaling
    {
        public float comfyTemperatureScaleStart;
        public float comfyTemperatureScaleZero = -100f;
        public float mechSeverityMultiplier = 0.3f;
        public SimpleCurve mechBodySizeResistanceCurve;
        public float mechMaxBodySizeResistance = 0.9f;

        public HediffCompProperties_StatusEffectColdScale()
        {
            compClass = typeof(HediffComp_StatusEffectColdScale);
        }
    }

    public class HediffComp_StatusEffectColdScale : HediffComp_AccumulationScaling
    {
        private HediffCompProperties_StatusEffectColdScale PropsCold => (HediffCompProperties_StatusEffectColdScale)props;

        public override float Scaled(Pawn caster, float severityOffset)
        {
            if (Pawn == null)
            {
                return severityOffset;
            }

            if (Pawn.RaceProps?.IsMechanoid == true)
            {
                float bodySizeResistance = MechBodySizeResistance(Pawn);
                return severityOffset * Mathf.Clamp01(PropsCold.mechSeverityMultiplier) * Mathf.Clamp01(1f - bodySizeResistance);
            }

            return severityOffset * TemperatureMultiplier(
                Pawn.GetStatValue(StatDefOf.ComfyTemperatureMin, true, -1),
                PropsCold.comfyTemperatureScaleStart,
                PropsCold.comfyTemperatureScaleZero);
        }

        private static float TemperatureMultiplier(float comfyTemperatureMin, float startTemp, float zeroTemp)
        {
            if (Mathf.Approximately(startTemp, zeroTemp))
            {
                return comfyTemperatureMin <= zeroTemp ? 0f : 1f;
            }

            if (startTemp > zeroTemp)
            {
                if (comfyTemperatureMin >= startTemp)
                {
                    return 1f;
                }

                if (comfyTemperatureMin <= zeroTemp)
                {
                    return 0f;
                }
            }
            else
            {
                if (comfyTemperatureMin <= startTemp)
                {
                    return 1f;
                }

                if (comfyTemperatureMin >= zeroTemp)
                {
                    return 0f;
                }
            }

            return Mathf.Clamp01(Mathf.InverseLerp(zeroTemp, startTemp, comfyTemperatureMin));
        }

        private float MechBodySizeResistance(Pawn pawn)
        {
            SimpleCurve curve = PropsCold.mechBodySizeResistanceCurve;
            if (curve == null)
            {
                return 0f;
            }

            float bodySize = Mathf.Max(0f, pawn.BodySize);
            float resistance = curve.Evaluate(bodySize);
            return Mathf.Clamp(resistance, 0f, Mathf.Clamp01(PropsCold.mechMaxBodySizeResistance));
        }
    }
}

using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerBellColdScale : HediffCompProperties_AccumulationScaling
    {
        public float comfyTemperatureScaleStart;
        public float comfyTemperatureScaleZero = -100f;
        public float mechSeverityMultiplier = 0.4f;

        public HediffCompProperties_FlowerBellColdScale()
        {
            compClass = typeof(HediffComp_FlowerBellColdScale);
        }
    }

    public class HediffComp_FlowerBellColdScale : HediffComp_AccumulationScaling
    {
        private HediffCompProperties_FlowerBellColdScale PropsCold => (HediffCompProperties_FlowerBellColdScale)props;

        public override float Scaled(Pawn caster, float severityOffset)
        {
            if (Pawn == null)
            {
                return severityOffset;
            }

            if (Pawn.RaceProps?.IsMechanoid == true)
            {
                return severityOffset * Mathf.Clamp01(PropsCold.mechSeverityMultiplier);
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
    }
}

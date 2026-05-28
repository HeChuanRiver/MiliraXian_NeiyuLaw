using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_AccumulationResistance : HediffCompProperties
    {
        public int maxStage = 5;
        public List<float> accumulationMultipliers;

        public HediffCompProperties_AccumulationResistance()
        {
            compClass = typeof(HediffComp_AccumulationResistance);
        }

        public float GetAccumulationMultiplier(int stage)
        {
            int clampedStage = Mathf.Clamp(stage, 1, maxStage);
            if (accumulationMultipliers != null && accumulationMultipliers.Count >= clampedStage)
            {
                return Mathf.Clamp01(accumulationMultipliers[clampedStage - 1]);
            }

            return Mathf.Clamp01(1f - (float)clampedStage / Mathf.Max(1, maxStage));
        }
    }

    public class HediffComp_AccumulationResistance : HediffComp
    {
        private HediffCompProperties_AccumulationResistance PropsResistance => (HediffCompProperties_AccumulationResistance)props;

        public int MaxStage => PropsResistance.maxStage;

        public int CurrentStage => Mathf.Clamp(Mathf.RoundToInt(parent.Severity), 1, MaxStage);

        public float AccumulationMultiplier => PropsResistance.GetAccumulationMultiplier(CurrentStage);

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            parent.Severity = CurrentStage;
        }
    }
}

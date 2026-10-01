using UnityEngine;
using Verse;
using MiliraXian.Characters.Common;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_SwordPressure : HediffCompProperties_PawnSpecialResource
    {
        public int decayDelayTicks = 360;
        public int decayIntervalTicks = 120;
        public float decayPerInterval = 0.1f;

        public HediffCompProperties_SwordPressure()
        {
            compClass = typeof(HediffComp_SwordPressure);
        }
    }

    public class HediffComp_SwordPressure : HediffComp_PawnSpecialResource
    {
        private int ticksSinceGain;
        private int ticksSinceDecay;

        public HediffCompProperties_SwordPressure PropsPressure => (HediffCompProperties_SwordPressure)props;

        /// <summary>
        /// Completed points are a sword-pressure concept: a filled point is already earned.
        /// The resource itself knows nothing about segments.
        /// </summary>
        public int CompletedPoints => Mathf.FloorToInt(CurrentValue);

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksSinceGain, "mx_qh_swordPressure_ticksSinceGain", 0);
            Scribe_Values.Look(ref ticksSinceDecay, "mx_qh_swordPressure_ticksSinceDecay", 0);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            ticksSinceGain++;

            if (ticksSinceGain < PropsPressure.decayDelayTicks)
            {
                return;
            }

            ticksSinceDecay++;
            if (ticksSinceDecay < PropsPressure.decayIntervalTicks)
            {
                return;
            }

            ticksSinceDecay = 0;

            // Only the unfinished remainder drains; a completed point is never lost. That floor is
            // why decay is an external consume rather than a negative recovery rate.
            float remainder = CurrentValue - Mathf.Floor(CurrentValue);
            if (remainder > 0.0001f)
            {
                TryConsume(Mathf.Min(PropsPressure.decayPerInterval, remainder));
            }
        }

        public override void AddValue(float value)
        {
            if (value <= 0f)
            {
                return;
            }

            base.AddValue(value);
            ticksSinceGain = 0;
            ticksSinceDecay = 0;
        }

        public float ConsumeAll()
        {
            float consumed = CurrentValue;
            SetValue(0f);
            return consumed;
        }
    }
}

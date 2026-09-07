using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_SwordPressure : HediffCompProperties_PawnSpecialResource
    {
        public float valuePerPoint = 100f;
        public int decayDelayTicks = 300;
        public float decayPerSecond = 12f;

        public HediffCompProperties_SwordPressure()
        {
            compClass = typeof(HediffComp_SwordPressure);
        }
    }

    public class HediffComp_SwordPressure : HediffComp_PawnSpecialResource, ISpecialResourceAddHandler, ISpecialResourceValueAdapter
    {
        private int ticksSinceGain;
        private int recoveryTicksLeft;
        private float recoveryPerTick;

        public HediffCompProperties_SwordPressure PropsPressure => (HediffCompProperties_SwordPressure)props;

        public float ValuePerPoint => Mathf.Max(1f, PropsPressure.valuePerPoint);

        public float CurrentResourceValue => CurrentValue / ValuePerPoint;

        public float MaxResourceValue => MaxValue / ValuePerPoint;

        public float PartialProgress => Mathf.Repeat(CurrentValue, ValuePerPoint);

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref ticksSinceGain, "mx_qh_swordPressure_ticksSinceGain", 0);
            Scribe_Values.Look(ref recoveryTicksLeft, "mx_qh_swordPressure_recoveryTicksLeft", 0);
            Scribe_Values.Look(ref recoveryPerTick, "mx_qh_swordPressure_recoveryPerTick", 0f);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            ticksSinceGain++;

            if (recoveryTicksLeft > 0)
            {
                recoveryTicksLeft--;
                AddProgress(recoveryPerTick);
                return;
            }

            if (ticksSinceGain < Mathf.Max(0, PropsPressure.decayDelayTicks))
            {
                return;
            }

            float completedFloor = Mathf.Floor(CurrentValue / ValuePerPoint) * ValuePerPoint;
            float partial = CurrentValue - completedFloor;
            if (partial <= 0.0001f)
            {
                return;
            }

            float decay = Mathf.Max(0f, PropsPressure.decayPerSecond) / 60f;
            SetValue(Mathf.Max(completedFloor, CurrentValue - decay));
        }

        public void AddProgress(float amount)
        {
            if (!QinghePowerBalance.ZeroLevelPassivesEnabled)
            {
                return;
            }

            if (amount <= 0f)
            {
                return;
            }

            AddValue(amount);
            ticksSinceGain = 0;
        }

        public void AddPoints(float points)
        {
            AddProgress(points * ValuePerPoint);
        }

        public int CompletedPoints => Mathf.FloorToInt(CurrentValue / ValuePerPoint);

        public bool TryConsumePoints(int points)
        {
            return TryConsume(Mathf.Max(0, points) * ValuePerPoint);
        }

        public float ConsumeAll()
        {
            float consumed = CurrentValue;
            SetValue(0f);
            recoveryTicksLeft = 0;
            recoveryPerTick = 0f;
            return consumed / ValuePerPoint;
        }

        public void StartRecovery(float totalPoints, int durationTicks)
        {
            int ticks = Mathf.Max(1, durationTicks);
            recoveryTicksLeft = ticks;
            recoveryPerTick = Mathf.Max(0f, totalPoints) * ValuePerPoint / ticks;
        }

        public void AddResourceValue(float value)
        {
            AddPoints(value);
        }

        public bool TryConsumeResourceValue(float value)
        {
            return TryConsume(value * ValuePerPoint);
        }
    }
}

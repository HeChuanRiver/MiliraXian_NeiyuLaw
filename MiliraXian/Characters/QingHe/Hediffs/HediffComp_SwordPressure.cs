using UnityEngine;
using Verse;
using MiliraXian.Characters.Common;
using MiliraXian.Characters.QingHe;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_SwordPressure : HediffCompProperties_PawnSpecialResource
    {
        public int decayDelayTicks = 300;
        public float decayPerSecond = 0.12f;

        public HediffCompProperties_SwordPressure()
        {
            compClass = typeof(HediffComp_SwordPressure);
        }
    }

    public class HediffComp_SwordPressure : HediffComp_PawnSpecialResource
    {
        private int ticksSinceGain;
        private int recoveryTicksLeft;
        private float recoveryPerTick;

        public HediffCompProperties_SwordPressure PropsPressure => (HediffCompProperties_SwordPressure)props;

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
                AddValue(recoveryPerTick);
                return;
            }

            if (ticksSinceGain < Mathf.Max(0, PropsPressure.decayDelayTicks))
            {
                return;
            }

            float completedFloor = Mathf.Floor(CurrentValue);
            float partial = CurrentValue - completedFloor;
            if (partial <= 0.0001f)
            {
                return;
            }

            float decay = Mathf.Max(0f, PropsPressure.decayPerSecond) / 60f;
            SetValue(Mathf.Max(completedFloor, CurrentValue - decay));
        }

        public override void AddValue(float value)
        {
            if (!QinghePowerBalance.ZeroLevelPassivesEnabled || value <= 0f)
            {
                return;
            }

            base.AddValue(value);
            ticksSinceGain = 0;
        }

        public int CompletedPoints => Mathf.FloorToInt(CurrentValue);

        public float ConsumeAll()
        {
            float consumed = CurrentValue;
            SetValue(0f);
            recoveryTicksLeft = 0;
            recoveryPerTick = 0f;
            return consumed;
        }

        public void StartRecovery(float totalPoints, int durationTicks)
        {
            int ticks = Mathf.Max(1, durationTicks);
            recoveryTicksLeft = ticks;
            recoveryPerTick = Mathf.Max(0f, totalPoints) / ticks;
        }
    }
}

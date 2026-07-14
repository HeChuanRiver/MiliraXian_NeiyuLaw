using UnityEngine;
using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerDecree : HediffCompProperties_PawnSpecialResource
    {
        public int baseRecoveryTicksPerDecree = 1200;
        public float valuePerDecree = 100f;
        public int highlightTicks = 90;

        public HediffCompProperties_FlowerDecree()
        {
            compClass = typeof(HediffComp_FlowerDecree);
        }
    }

    public class HediffComp_FlowerDecree : HediffComp_PawnSpecialResource, ISpecialResourceAddHandler, ISpecialResourceValueAdapter
    {
        private const int RecoveryFlushIntervalTicks = 10;

        private int highlightTicksLeft;
        private int pendingRecoveryTicks;
        private bool flushingRecovery;

        public HediffCompProperties_FlowerDecree PropsDecree => (HediffCompProperties_FlowerDecree)props;

        public float ValuePerDecree => Mathf.Max(1f, PropsDecree.valuePerDecree);

        public override float CurrentValue
        {
            get
            {
                if (!flushingRecovery)
                {
                    FlushAccumulatedRecovery();
                }
                return base.CurrentValue;
            }
        }

        public override float MaxValue
        {
            get
            {
                float maxDecrees = PropsResource.maxValue / ValuePerDecree
                    + GetStatValue(MX_QHDefOf.MX_QH_FlowerDecreeMaxOffset, 0f);
                return Mathf.Max(ValuePerDecree, maxDecrees * ValuePerDecree);
            }
        }

        public float CurrentResourceValue => CurrentValue / ValuePerDecree;

        public float MaxResourceValue => MaxValue / ValuePerDecree;

        public float CurrentRecoveryFactor => ResolveRecoveryFactor();

        public float RecoveryProgress
        {
            get
            {
                if (CurrentValue >= MaxValue - 0.0001f)
                {
                    return 0f;
                }

                return Mathf.Repeat(CurrentValue, ValuePerDecree);
            }
        }

        public float RecoveryProgressMax => ValuePerDecree;

        public float RecoveryProgressPercent => Mathf.Clamp01(RecoveryProgress / RecoveryProgressMax);

        public float CurrentRecoveryProgressPerSecond => ResolveRecoveryValuePerTick() * 60f;

        public float HighlightPercent => Mathf.Clamp01(highlightTicksLeft / (float)Mathf.Max(1, PropsDecree.highlightTicks));

        public override void CompExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                FlushAccumulatedRecovery();
            }

            base.CompExposeData();
            Scribe_Values.Look(ref highlightTicksLeft, "highlightTicksLeft", 0);
            Scribe_Values.Look(ref pendingRecoveryTicks, "pendingRecoveryTicks", 0);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (highlightTicksLeft > 0)
            {
                highlightTicksLeft--;
            }

            if (Pawn != null && !Pawn.Dead)
            {
                pendingRecoveryTicks++;
                if (pendingRecoveryTicks >= RecoveryFlushIntervalTicks)
                {
                    FlushAccumulatedRecovery();
                }
            }
        }

        public void AddDecree(float amount)
        {
            FlushAccumulatedRecovery();
            if (amount <= 0f)
            {
                return;
            }

            AddValueWithHighlight(amount * ValuePerDecree);
        }

        public void AddRecoveryProgress(float amount)
        {
            FlushAccumulatedRecovery();
            AddValueWithHighlight(amount);
        }

        public bool TryConsumeDecree(float amount)
        {
            FlushAccumulatedRecovery();
            return TryConsume(Mathf.Max(0f, amount) * ValuePerDecree);
        }

        public bool TryConsumeRawValue(float amount)
        {
            FlushAccumulatedRecovery();
            return TryConsume(amount);
        }

        public void AddResourceValue(float value)
        {
            AddDecree(value);
        }

        public bool TryConsumeResourceValue(float value)
        {
            return TryConsumeDecree(value);
        }

        private void TriggerHighlight()
        {
            highlightTicksLeft = Mathf.Max(1, PropsDecree.highlightTicks);
        }

        private void AddValueWithHighlight(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            float before = CurrentValue;
            int beforeDecrees = Mathf.FloorToInt(before / ValuePerDecree);
            AddValue(amount);
            int afterDecrees = Mathf.FloorToInt(CurrentValue / ValuePerDecree);
            if (afterDecrees > beforeDecrees)
            {
                TriggerHighlight();
            }
        }

        private float ResolveRecoveryValuePerTick()
        {
            int baseTicks = Mathf.Max(1, PropsDecree.baseRecoveryTicksPerDecree);
            float basePerSecond = ValuePerDecree / baseTicks * 60f;
            float valuePerSecond = basePerSecond * ResolveRecoveryFactor();
            return Mathf.Max(0f, valuePerSecond) / 60f;
        }

        private void FlushAccumulatedRecovery()
        {
            if (flushingRecovery || pendingRecoveryTicks <= 0)
            {
                return;
            }

            int elapsedTicks = pendingRecoveryTicks;
            pendingRecoveryTicks = 0;
            flushingRecovery = true;
            try
            {
                if (Pawn == null || Pawn.Dead || base.CurrentValue >= MaxValue - 0.0001f)
                {
                    return;
                }

                AddValueWithHighlight(ResolveRecoveryValuePerTick() * elapsedTicks);
            }
            finally
            {
                flushingRecovery = false;
            }
        }

        private float ResolveRecoveryFactor()
        {
            return GetStatValue(MX_QHDefOf.MX_QH_FlowerDecreeRegenFactor, 1f);
        }

        private float GetStatValue(StatDef statDef, float fallback)
        {
            if (Pawn == null || statDef == null)
            {
                return fallback;
            }

            return Pawn.GetStatValue(statDef, true, 1);
        }
    }
}

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
        private int lastRecoveryTick = -1;
        private float cachedMaxValue = -1f;
        private float cachedRecoveryValuePerTick;

        public HediffCompProperties_FlowerDecree PropsDecree => (HediffCompProperties_FlowerDecree)props;

        public float ValuePerDecree => Mathf.Max(1f, PropsDecree.valuePerDecree);

        public override float MaxValue
        {
            get
            {
                FlushRecovery(force: false);
                return cachedMaxValue > 0f ? cachedMaxValue : ResolveMaxValue();
            }
        }

        public float CurrentResourceValue
        {
            get
            {
                FlushRecovery(force: false);
                return CurrentValue / ValuePerDecree;
            }
        }

        public float MaxResourceValue => MaxValue / ValuePerDecree;

        public float CurrentRecoveryFactor
        {
            get
            {
                FlushRecovery(force: false);
                float basePerTick = ValuePerDecree / Mathf.Max(1, PropsDecree.baseRecoveryTicksPerDecree);
                return basePerTick > 0f ? cachedRecoveryValuePerTick / basePerTick : 0f;
            }
        }

        public float RecoveryProgress
        {
            get
            {
                FlushRecovery(force: false);
                if (CurrentValue >= MaxValue - 0.0001f)
                {
                    return 0f;
                }

                return Mathf.Repeat(CurrentValue, ValuePerDecree);
            }
        }

        public float RecoveryProgressMax => ValuePerDecree;

        public float RecoveryProgressPercent => Mathf.Clamp01(RecoveryProgress / RecoveryProgressMax);

        public float CurrentRecoveryProgressPerSecond
        {
            get
            {
                FlushRecovery(force: false);
                return cachedRecoveryValuePerTick * 60f;
            }
        }

        public float HighlightPercent => Mathf.Clamp01(highlightTicksLeft / (float)Mathf.Max(1, PropsDecree.highlightTicks));

        public override void CompExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                FlushRecovery(force: true);
            }

            base.CompExposeData();
            Scribe_Values.Look(ref highlightTicksLeft, "highlightTicksLeft", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                lastRecoveryTick = CurrentTick;
                RefreshCachedRates();
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (highlightTicksLeft > 0)
            {
                highlightTicksLeft--;
            }

            FlushRecovery(force: false);
        }

        public void AddDecree(float amount)
        {
            FlushRecovery(force: true);
            if (amount <= 0f)
            {
                return;
            }

            AddValueWithHighlight(amount * ValuePerDecree);
        }

        public void AddRecoveryProgress(float amount)
        {
            FlushRecovery(force: true);
            AddValueWithHighlight(amount);
        }

        public bool TryConsumeDecree(float amount)
        {
            FlushRecovery(force: true);
            return TryConsume(Mathf.Max(0f, amount) * ValuePerDecree);
        }

        public bool TryConsumeRawValue(float amount)
        {
            FlushRecovery(force: true);
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

        private float ResolveRecoveryFactor()
        {
            return GetStatValue(MX_QHDefOf.MX_QH_FlowerDecreeRegenFactor, 1f);
        }

        private int CurrentTick => Find.TickManager != null ? Find.TickManager.TicksGame : 0;

        private void FlushRecovery(bool force)
        {
            int currentTick = CurrentTick;
            if (lastRecoveryTick < 0)
            {
                lastRecoveryTick = currentTick;
                RefreshCachedRates();
                return;
            }

            int elapsedTicks = Mathf.Max(0, currentTick - lastRecoveryTick);
            if (!force && elapsedTicks < RecoveryFlushIntervalTicks)
            {
                return;
            }

            RefreshCachedRates();
            lastRecoveryTick = currentTick;
            if (elapsedTicks > 0 && Pawn != null && !Pawn.Dead && CurrentValue < cachedMaxValue - 0.0001f)
            {
                AddValueWithHighlight(cachedRecoveryValuePerTick * elapsedTicks);
            }
        }

        private void RefreshCachedRates()
        {
            cachedMaxValue = ResolveMaxValue();
            cachedRecoveryValuePerTick = ResolveRecoveryValuePerTick();
        }

        private float ResolveMaxValue()
        {
            float maxDecrees = PropsResource.maxValue / ValuePerDecree
                + GetStatValue(MX_QHDefOf.MX_QH_FlowerDecreeMaxOffset, 0f);
            return Mathf.Max(ValuePerDecree, maxDecrees * ValuePerDecree);
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

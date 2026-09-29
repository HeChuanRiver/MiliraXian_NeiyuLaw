using UnityEngine;
using RimWorld;
using Verse;
using MiliraXian.Characters.Common;
using MiliraXian.Characters.QingHe;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerDecree : HediffCompProperties_PawnSpecialResource
    {
        public int baseRecoveryTicksPerDecree = 1200;
        public int highlightTicks = 90;

        public HediffCompProperties_FlowerDecree()
        {
            compClass = typeof(HediffComp_FlowerDecree);
        }
    }

    public class HediffComp_FlowerDecree : HediffComp_PawnSpecialResource
    {
        private const int RecoveryFlushIntervalTicks = 10;

        private int highlightTicksLeft;
        private int lastRecoveryTick = -1;
        private float cachedMaxValue = -1f;
        private float cachedRecoveryValuePerTick;

        public HediffCompProperties_FlowerDecree PropsDecree => (HediffCompProperties_FlowerDecree)props;

        public override float MaxValue
        {
            get
            {
                FlushRecovery(force: false);
                return cachedMaxValue > 0f ? cachedMaxValue : base.MaxValue;
            }
        }

        public float CurrentRecoveryProgressPerSecond
        {
            get
            {
                FlushRecovery(force: false);
                float perSecond = cachedRecoveryValuePerTick * 60f;
                StatDef gainStat = PawnSpecialResourceStats.GainFactorStatFor(parent?.def);
                if (gainStat != null && Pawn != null)
                {
                    perSecond *= Pawn.GetStatValue(gainStat);
                }
                return perSecond;
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

        public override void AddValue(float value)
        {
            if (!QinghePowerBalance.ZeroLevelPassivesEnabled || value <= 0f)
            {
                return;
            }

            FlushRecovery(force: true);
            int decreesBefore = Mathf.FloorToInt(CurrentValue);
            base.AddValue(value);
            if (Mathf.FloorToInt(CurrentValue) > decreesBefore)
            {
                TriggerHighlight();
            }
        }

        public override bool TryConsume(float value)
        {
            FlushRecovery(force: true);
            return base.TryConsume(value);
        }

        private void TriggerHighlight()
        {
            highlightTicksLeft = Mathf.Max(1, PropsDecree.highlightTicks);
        }

        private float ResolveRecoveryValuePerTick()
        {
            int baseTicks = Mathf.Max(1, PropsDecree.baseRecoveryTicksPerDecree);
            return 1f / baseTicks;
        }

        private int CurrentTick => Find.TickManager != null ? Find.TickManager.TicksGame : 0;

        private void FlushRecovery(bool force)
        {
            if (!QinghePowerBalance.ZeroLevelPassivesEnabled)
            {
                return;
            }

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
            // Reach the exact cap so flooring the displayed decree count cannot lose a full decree.
            if (elapsedTicks > 0 && Pawn != null && !Pawn.Dead && CurrentValue < cachedMaxValue)
            {
                AddValue(cachedRecoveryValuePerTick * elapsedTicks);
            }
        }

        private void RefreshCachedRates()
        {
            cachedMaxValue = base.MaxValue;
            ClampCurrentValueTo(cachedMaxValue);

            cachedRecoveryValuePerTick = ResolveRecoveryValuePerTick();
        }
    }
}

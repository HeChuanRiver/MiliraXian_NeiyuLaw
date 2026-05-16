using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerDecree : HediffCompProperties_PawnSpecialResource
    {
        public float recoveryProgressMax = 100f;
        public int baseRecoveryTicksPerDecree = 1200;
        public float flowerTidingsRecoverySpeedBonusAtMax = 1f;
        public int highlightTicks = 90;

        public HediffCompProperties_FlowerDecree()
        {
            compClass = typeof(HediffComp_FlowerDecree);
        }
    }

    public class HediffComp_FlowerDecree : HediffComp_PawnSpecialResource, ISpecialResourceAddHandler
    {
        private float recoveryProgress;
        private int highlightTicksLeft;

        public HediffCompProperties_FlowerDecree PropsDecree => (HediffCompProperties_FlowerDecree)props;

        public float RecoveryProgress
        {
            get
            {
                ClampRecoveryProgressIfFull();
                return recoveryProgress;
            }
        }

        public float RecoveryProgressMax => Mathf.Max(1f, PropsDecree.recoveryProgressMax);

        public float RecoveryProgressPercent => Mathf.Clamp01(RecoveryProgress / RecoveryProgressMax);

        public float CurrentRecoveryProgressPerSecond => ResolveRecoveryProgressPerTick() * 60f;

        public float HighlightPercent => Mathf.Clamp01(highlightTicksLeft / (float)Mathf.Max(1, PropsDecree.highlightTicks));

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref recoveryProgress, "recoveryProgress", 0f);
            Scribe_Values.Look(ref highlightTicksLeft, "highlightTicksLeft", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ClampRecoveryProgressIfFull();
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (highlightTicksLeft > 0)
            {
                highlightTicksLeft--;
            }

            if (Pawn != null && !Pawn.Dead && CurrentValue < MaxValue - 0.0001f)
            {
                AddRecoveryProgress(ResolveRecoveryProgressPerTick());
            }

            ClampRecoveryProgressIfFull();
        }

        public void AddDecree(float amount)
        {
            int decreeCount = Mathf.FloorToInt(amount);
            if (decreeCount <= 0)
            {
                return;
            }

            float before = CurrentValue;
            AddValue(decreeCount);
            if (CurrentValue > before + 0.0001f)
            {
                TriggerHighlight();
            }

            ClampRecoveryProgressIfFull();
        }

        public void AddRecoveryProgress(float amount)
        {
            if (CurrentValue >= MaxValue - 0.0001f)
            {
                recoveryProgress = 0f;
                return;
            }

            recoveryProgress += amount;
            recoveryProgress = Mathf.Max(0f, recoveryProgress);
            if (recoveryProgress >= RecoveryProgressMax && CurrentValue < MaxValue - 0.0001f)
            {
                AddDecree(1f);
                recoveryProgress = 0f;
            }

            ClampRecoveryProgressIfFull();
        }

        public bool TryConsumeDecree(float amount)
        {
            return TryConsume(Mathf.CeilToInt(amount));
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

        private void ClampRecoveryProgressIfFull()
        {
            if (CurrentValue >= MaxValue - 0.0001f)
            {
                recoveryProgress = 0f;
            }
            else
            {
                recoveryProgress = Mathf.Clamp(recoveryProgress, 0f, RecoveryProgressMax);
            }
        }

        private float ResolveRecoveryProgressPerTick()
        {
            int baseTicks = Mathf.Max(1, PropsDecree.baseRecoveryTicksPerDecree);
            float baseProgress = RecoveryProgressMax / baseTicks;
            float flowerTidingsFactor = PawnSpecialResourceUtility.GetResourcePercent(Pawn, MX_QHDefOf.MX_QH_FlowerTidings);
            float speedFactor = 1f + Mathf.Max(0f, PropsDecree.flowerTidingsRecoverySpeedBonusAtMax) * flowerTidingsFactor;
            return baseProgress * speedFactor;
        }
    }
}

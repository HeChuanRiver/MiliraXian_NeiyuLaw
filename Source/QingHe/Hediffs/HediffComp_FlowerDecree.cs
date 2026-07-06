using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerDecree : HediffCompProperties_PawnSpecialResource
    {
        public int baseRecoveryTicksPerDecree = 1200;
        public float valuePerDecree = 100f;
        public float maxResourceBonusPerSkillNode = 1f;
        public int highlightTicks = 90;

        public HediffCompProperties_FlowerDecree()
        {
            compClass = typeof(HediffComp_FlowerDecree);
        }
    }

    public class HediffComp_FlowerDecree : HediffComp_PawnSpecialResource, ISpecialResourceAddHandler, ISpecialResourceValueAdapter
    {
        private int highlightTicksLeft;

        public HediffCompProperties_FlowerDecree PropsDecree => (HediffCompProperties_FlowerDecree)props;

        public float ValuePerDecree => Mathf.Max(1f, PropsDecree.valuePerDecree);

        public override float MaxValue => (PropsResource.maxValue / ValuePerDecree + SkillTreeMaxResourceBonus) * ValuePerDecree;

        public float CurrentResourceValue => CurrentValue / ValuePerDecree;

        public float MaxResourceValue => MaxValue / ValuePerDecree;

        public float SkillTreeMaxResourceBonus => FlowerCourtUtility.GetDivineFortune(Pawn)?.FlowerDecreeMaxBonus * PropsDecree.maxResourceBonusPerSkillNode ?? 0f;

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
            base.CompExposeData();
            Scribe_Values.Look(ref highlightTicksLeft, "highlightTicksLeft", 0);
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
                AddValueWithHighlight(ResolveRecoveryValuePerTick());
            }
        }

        public void AddDecree(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            AddValueWithHighlight(amount * ValuePerDecree);
        }

        public void AddRecoveryProgress(float amount)
        {
            AddValueWithHighlight(amount);
        }

        public bool TryConsumeDecree(float amount)
        {
            return TryConsume(Mathf.Max(0f, amount) * ValuePerDecree);
        }

        public bool TryConsumeRawValue(float amount)
        {
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
            return ValuePerDecree / baseTicks * ResolveRecoveryFactor();
        }

        private float ResolveRecoveryFactor()
        {
            return FlowerCourtUtility.GetDivineFortune(Pawn)?.FlowerDecreeRegenMultiplier ?? 1f;
        }
    }
}

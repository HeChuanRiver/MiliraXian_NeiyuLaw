using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.AbnormalSystem
{
    public enum AbnormalApplyStatus
    {
        Applied,
        Triggered,
        InvalidTarget,
        InvalidDefinition,
        InvalidAmount,
        DisabledForTarget,
        /// <summary>The effect is active and the abnormal has no aggravation: only the on-hit extras were given.</summary>
        EffectAlreadyActive,
        /// <summary>The effect is active: the hit filled the aggravation bar (and its on-hit extras were given).</summary>
        AggravationApplied,
        /// <summary>The aggravation bar was filled: the effect aggravated.</summary>
        Aggravated
    }

    public struct AbnormalApplyResult
    {
        public AbnormalApplyStatus status;
        public float requestedAmount;
        public float efficiencyFactor;
        public float appliedAmount;
        public float accumulationLimit;
        public Hediff_Abnormal abnormal;

        public bool Applied => status == AbnormalApplyStatus.Applied || status == AbnormalApplyStatus.Triggered
            || status == AbnormalApplyStatus.AggravationApplied || status == AbnormalApplyStatus.Aggravated;

        public bool Triggered => status == AbnormalApplyStatus.Triggered;
    }

    public static class AbnormalSystem
    {
        public static AbnormalApplyResult ApplyAccumulation(
            Pawn source,
            Pawn target,
            HediffDef_Abnormal abnormalDef,
            float amount)
        {
            AbnormalApplyResult result = new()
            {
                status = AbnormalApplyStatus.InvalidTarget,
                requestedAmount = amount,
                efficiencyFactor = 1f
            };

            if (target == null || target.Dead || target.Destroyed || target.health?.hediffSet == null)
            {
                return result;
            }

            if (abnormalDef == null || abnormalDef.accumulationLimitFactorStat == null || abnormalDef.applicationEfficiencyFactorStat == null)
            {
                result.status = AbnormalApplyStatus.InvalidDefinition;
                return result;
            }

            if (amount <= 0f)
            {
                result.status = AbnormalApplyStatus.InvalidAmount;
                return result;
            }

            Hediff existing = target.health.hediffSet.GetFirstHediffOfDef(abnormalDef);
            float accumulationLimit = GetAccumulationLimit(target, abnormalDef);
            result.accumulationLimit = accumulationLimit;
            if (accumulationLimit <= 0f)
            {
                if (existing != null)
                {
                    target.health.RemoveHediff(existing);
                }

                result.status = AbnormalApplyStatus.DisabledForTarget;
                return result;
            }

            // While the effect is active the hit goes on: its on-hit extras are given and
            // it fills the aggravation bar (Hediff_Abnormal.ApplyAccumulation decides which).
            float efficiencyFactor = source != null
                ? Mathf.Max(0f, source.GetStatValue(abnormalDef.applicationEfficiencyFactorStat, true, -1))
                : 1f;
            float appliedAmount = amount * efficiencyFactor;
            result.efficiencyFactor = efficiencyFactor;
            result.appliedAmount = appliedAmount;
            if (appliedAmount <= 0f)
            {
                result.status = AbnormalApplyStatus.InvalidAmount;
                return result;
            }

            Hediff_Abnormal abnormal = existing as Hediff_Abnormal;
            if (existing != null && abnormal == null)
            {
                Log.Error($"Abnormal hediff {abnormalDef.defName} does not inherit {nameof(Hediff_Abnormal)}.");
                result.status = AbnormalApplyStatus.InvalidDefinition;
                return result;
            }

            if (abnormal == null)
            {
                abnormal = HediffMaker.MakeHediff(abnormalDef, target) as Hediff_Abnormal;
                if (abnormal == null)
                {
                    Log.Error($"Failed to create abnormal hediff {abnormalDef.defName}.");
                    result.status = AbnormalApplyStatus.InvalidDefinition;
                    return result;
                }

                abnormal.Severity = 0f;
                target.health.AddHediff(abnormal);
            }

            result.status = abnormal.ApplyAccumulation(source, appliedAmount, accumulationLimit);
            result.abnormal = abnormal;
            return result;
        }

        public static float GetAccumulationLimit(Pawn target, HediffDef_Abnormal abnormalDef)
        {
            if (target == null || abnormalDef?.accumulationLimitFactorStat == null || abnormalDef.baseAccumulationLimit <= 0f)
            {
                return 0f;
            }

            float factor = target.GetStatValue(abnormalDef.accumulationLimitFactorStat, true, -1);
            return factor > 0f ? abnormalDef.baseAccumulationLimit * factor : 0f;
        }

    }
}

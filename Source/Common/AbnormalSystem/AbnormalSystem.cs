using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public enum AbnormalApplyStatus
    {
        Applied,
        Triggered,
        InvalidTarget,
        InvalidDefinition,
        InvalidAmount,
        DisabledForTarget,
        EffectAlreadyActive
    }

    public struct AbnormalApplyResult
    {
        public AbnormalApplyStatus status;
        public float requestedAmount;
        public float efficiencyFactor;
        public float appliedAmount;
        public float accumulationLimit;
        public Hediff_Abnormal abnormal;

        public bool Applied => status == AbnormalApplyStatus.Applied || status == AbnormalApplyStatus.Triggered;

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

            if (abnormalDef.effectHediff != null && target.health.hediffSet.GetFirstHediffOfDef(abnormalDef.effectHediff) != null)
            {
                result.status = AbnormalApplyStatus.EffectAlreadyActive;
                return result;
            }

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

            bool triggered = abnormal.ApplyAccumulation(source, appliedAmount, accumulationLimit);
            result.abnormal = abnormal;
            result.status = triggered ? AbnormalApplyStatus.Triggered : AbnormalApplyStatus.Applied;
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

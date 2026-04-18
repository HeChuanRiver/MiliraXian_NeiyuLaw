using System;
using System.Collections.Generic;
using System.Reflection;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_PawnResourceScaling : HediffCompProperties
    {
        public ScaledValue painOffset;
        public ScaledValue painFactor;
        public ScaledValue bleedRate;

        public ScaledValue statOffsetMultiplier;
        public ScaledValue statFactorMultiplier;

        public ScaledValue damageAmount;
        public ScaledValue armorPenetration;
        public ScaledValue radius;
        public ScaledValue knockbackDistance;
        public ScaledValue durationTicks;
        public ScaledValue stunDurationTicks;
        public ScaledValue cooldownTicks;
        public ScaledValue healAmount;
        public ScaledValue shieldValue;
        public ScaledValue maxEnergy;
        public ScaledValue damagePerShieldPoint;
        public ScaledValue regenPerSecond;
        public ScaledValue slowSeverity;
        public ScaledValue bleedSeverity;
        public ScaledValue resourceGain;
        public ScaledValue resourceCost;
        public ScaledValue severityPerPulse;
        public ScaledValue hediffSeverityFactor;

        public HediffCompProperties_PawnResourceScaling()
        {
            compClass = typeof(HediffComp_PawnResourceScaling);
        }
    }

    public class HediffComp_PawnResourceScaling : HediffComp
    {
        public HediffCompProperties_PawnResourceScaling Props
            => (HediffCompProperties_PawnResourceScaling)props;

        public float PainOffset => Props.painOffset?.GetValue(parent.pawn) ?? 0f;
        public float PainFactor => Props.painFactor?.GetValue(parent.pawn) ?? 1f;
        public float BleedRate => Props.bleedRate?.GetValue(parent.pawn) ?? 0f;

        public float StatOffsetMultiplier => Props.statOffsetMultiplier?.GetValue(parent.pawn) ?? 1f;
        public float StatFactorMultiplier => Props.statFactorMultiplier?.GetValue(parent.pawn) ?? 1f;

        public float DamageAmount => Props.damageAmount?.GetValue(parent.pawn) ?? 0f;
        public float ArmorPenetration => Props.armorPenetration?.GetValue(parent.pawn) ?? 0f;
        public float Radius => Props.radius?.GetValue(parent.pawn) ?? 0f;
        public float KnockbackDistance => Props.knockbackDistance?.GetValue(parent.pawn) ?? 0f;
        public float DurationTicks => Props.durationTicks?.GetValue(parent.pawn) ?? 0f;
        public float StunDurationTicks => Props.stunDurationTicks?.GetValue(parent.pawn) ?? 0f;
        public float CooldownTicks => Props.cooldownTicks?.GetValue(parent.pawn) ?? 0f;
        public float HealAmount => Props.healAmount?.GetValue(parent.pawn) ?? 0f;
        public float ShieldValue => Props.shieldValue?.GetValue(parent.pawn) ?? 0f;
        public float MaxEnergy => Props.maxEnergy?.GetValue(parent.pawn) ?? 0f;
        public float DamagePerShieldPoint => Props.damagePerShieldPoint?.GetValue(parent.pawn) ?? 0f;
        public float RegenPerSecond => Props.regenPerSecond?.GetValue(parent.pawn) ?? 0f;
        public float SlowSeverity => Props.slowSeverity?.GetValue(parent.pawn) ?? 0f;
        public float BleedSeverity => Props.bleedSeverity?.GetValue(parent.pawn) ?? 0f;
        public float ResourceGain => Props.resourceGain?.GetValue(parent.pawn) ?? 0f;
        public float ResourceCost => Props.resourceCost?.GetValue(parent.pawn) ?? 0f;
        public float SeverityPerPulse => Props.severityPerPulse?.GetValue(parent.pawn) ?? 0f;
        public float HediffSeverityFactor => Props.hediffSeverityFactor?.GetValue(parent.pawn) ?? 0f;

        private HediffStage cachedScaledStage;
        private float cachedResourcePercent = -1f;

        public HediffStage GetScaledStage(HediffStage original)
        {
            if (original == null) return null;

            bool hasStatOffsetScale = Props.statOffsetMultiplier != null;
            bool hasStatFactorScale = Props.statFactorMultiplier != null;
            if (!hasStatOffsetScale && !hasStatFactorScale) return original;

            float percent = GetResourcePercent();
            if (Mathf.Approximately(percent, cachedResourcePercent) && cachedScaledStage != null)
                return cachedScaledStage;

            cachedScaledStage = CloneAndScaleStage(original, hasStatOffsetScale, hasStatFactorScale);
            cachedResourcePercent = percent;
            return cachedScaledStage;
        }

        private float GetResourcePercent()
        {
            var resourceDef = Props.statOffsetMultiplier?.resourceDef ?? Props.statFactorMultiplier?.resourceDef;
            if (resourceDef == null || parent?.pawn == null) return 0f;
            return PawnSpecialResourceUtility.GetResourcePercent(parent.pawn, resourceDef);
        }

        private static readonly MethodInfo memberwiseCloneMethod = typeof(HediffStage).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic);

        private HediffStage CloneAndScaleStage(HediffStage original, bool scaleOffsets, bool scaleFactors)
        {
            HediffStage clone = memberwiseCloneMethod != null
                ? (HediffStage)memberwiseCloneMethod.Invoke(original, null)
                : new HediffStage();

            if (scaleOffsets && original.statOffsets != null)
            {
                float factor = StatOffsetMultiplier;
                clone.statOffsets = new List<StatModifier>(original.statOffsets.Count);
                for (int i = 0; i < original.statOffsets.Count; i++)
                {
                    var sm = original.statOffsets[i];
                    clone.statOffsets.Add(new StatModifier { stat = sm.stat, value = sm.value * factor });
                }
            }

            if (scaleFactors && original.statFactors != null)
            {
                float factor = StatFactorMultiplier;
                clone.statFactors = new List<StatModifier>(original.statFactors.Count);
                for (int i = 0; i < original.statFactors.Count; i++)
                {
                    var sm = original.statFactors[i];
                    clone.statFactors.Add(new StatModifier { stat = sm.stat, value = 1f + (sm.value - 1f) * factor });
                }
            }

            return clone;
        }
    }
}

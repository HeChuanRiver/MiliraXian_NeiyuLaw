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

        // Snapshot cache: values are frozen at creation / severity-increase time.
        private Dictionary<string, float> snapshot;
        private float lastSeverity = -1f;

        public void CaptureSnapshot()
        {
            if (snapshot == null)
                snapshot = new Dictionary<string, float>();

            if (Props.painOffset != null)
                snapshot["painOffset"] = Props.painOffset.GetValue(parent.pawn);
            if (Props.painFactor != null)
                snapshot["painFactor"] = Props.painFactor.GetValue(parent.pawn);
            if (Props.bleedRate != null)
                snapshot["bleedRate"] = Props.bleedRate.GetValue(parent.pawn);
            if (Props.statOffsetMultiplier != null)
                snapshot["statOffsetMultiplier"] = Props.statOffsetMultiplier.GetValue(parent.pawn);
            if (Props.statFactorMultiplier != null)
                snapshot["statFactorMultiplier"] = Props.statFactorMultiplier.GetValue(parent.pawn);
            if (Props.damageAmount != null)
                snapshot["damageAmount"] = Props.damageAmount.GetValue(parent.pawn);
            if (Props.armorPenetration != null)
                snapshot["armorPenetration"] = Props.armorPenetration.GetValue(parent.pawn);
            if (Props.radius != null)
                snapshot["radius"] = Props.radius.GetValue(parent.pawn);
            if (Props.knockbackDistance != null)
                snapshot["knockbackDistance"] = Props.knockbackDistance.GetValue(parent.pawn);
            if (Props.durationTicks != null)
                snapshot["durationTicks"] = Props.durationTicks.GetValue(parent.pawn);
            if (Props.stunDurationTicks != null)
                snapshot["stunDurationTicks"] = Props.stunDurationTicks.GetValue(parent.pawn);
            if (Props.cooldownTicks != null)
                snapshot["cooldownTicks"] = Props.cooldownTicks.GetValue(parent.pawn);
            if (Props.healAmount != null)
                snapshot["healAmount"] = Props.healAmount.GetValue(parent.pawn);
            if (Props.shieldValue != null)
                snapshot["shieldValue"] = Props.shieldValue.GetValue(parent.pawn);
            if (Props.maxEnergy != null)
                snapshot["maxEnergy"] = Props.maxEnergy.GetValue(parent.pawn);
            if (Props.damagePerShieldPoint != null)
                snapshot["damagePerShieldPoint"] = Props.damagePerShieldPoint.GetValue(parent.pawn);
            if (Props.regenPerSecond != null)
                snapshot["regenPerSecond"] = Props.regenPerSecond.GetValue(parent.pawn);
            if (Props.slowSeverity != null)
                snapshot["slowSeverity"] = Props.slowSeverity.GetValue(parent.pawn);
            if (Props.bleedSeverity != null)
                snapshot["bleedSeverity"] = Props.bleedSeverity.GetValue(parent.pawn);
            if (Props.resourceGain != null)
                snapshot["resourceGain"] = Props.resourceGain.GetValue(parent.pawn);
            if (Props.resourceCost != null)
                snapshot["resourceCost"] = Props.resourceCost.GetValue(parent.pawn);
            if (Props.severityPerPulse != null)
                snapshot["severityPerPulse"] = Props.severityPerPulse.GetValue(parent.pawn);
            if (Props.hediffSeverityFactor != null)
                snapshot["hediffSeverityFactor"] = Props.hediffSeverityFactor.GetValue(parent.pawn);
        }

        private float GetSnapshotOrRealtime(string key, float fallback, ScaledValue scaled)
        {
            if (snapshot != null && snapshot.TryGetValue(key, out float cached))
                return cached;
            return scaled != null ? scaled.GetValue(parent.pawn) : fallback;
        }

        public float PainOffset => GetSnapshotOrRealtime("painOffset", 0f, Props.painOffset);
        public float PainFactor => GetSnapshotOrRealtime("painFactor", 1f, Props.painFactor);
        public float BleedRate => GetSnapshotOrRealtime("bleedRate", 0f, Props.bleedRate);

        public float StatOffsetMultiplier => GetSnapshotOrRealtime("statOffsetMultiplier", 1f, Props.statOffsetMultiplier);
        public float StatFactorMultiplier => GetSnapshotOrRealtime("statFactorMultiplier", 1f, Props.statFactorMultiplier);

        public float DamageAmount => GetSnapshotOrRealtime("damageAmount", 0f, Props.damageAmount);
        public float ArmorPenetration => GetSnapshotOrRealtime("armorPenetration", 0f, Props.armorPenetration);
        public float Radius => GetSnapshotOrRealtime("radius", 0f, Props.radius);
        public float KnockbackDistance => GetSnapshotOrRealtime("knockbackDistance", 0f, Props.knockbackDistance);
        public float DurationTicks => GetSnapshotOrRealtime("durationTicks", 0f, Props.durationTicks);
        public float StunDurationTicks => GetSnapshotOrRealtime("stunDurationTicks", 0f, Props.stunDurationTicks);
        public float CooldownTicks => GetSnapshotOrRealtime("cooldownTicks", 0f, Props.cooldownTicks);
        public float HealAmount => GetSnapshotOrRealtime("healAmount", 0f, Props.healAmount);
        public float ShieldValue => GetSnapshotOrRealtime("shieldValue", 0f, Props.shieldValue);
        public float MaxEnergy => GetSnapshotOrRealtime("maxEnergy", 0f, Props.maxEnergy);
        public float DamagePerShieldPoint => GetSnapshotOrRealtime("damagePerShieldPoint", 0f, Props.damagePerShieldPoint);
        public float RegenPerSecond => GetSnapshotOrRealtime("regenPerSecond", 0f, Props.regenPerSecond);
        public float SlowSeverity => GetSnapshotOrRealtime("slowSeverity", 0f, Props.slowSeverity);
        public float BleedSeverity => GetSnapshotOrRealtime("bleedSeverity", 0f, Props.bleedSeverity);
        public float ResourceGain => GetSnapshotOrRealtime("resourceGain", 0f, Props.resourceGain);
        public float ResourceCost => GetSnapshotOrRealtime("resourceCost", 0f, Props.resourceCost);
        public float SeverityPerPulse => GetSnapshotOrRealtime("severityPerPulse", 0f, Props.severityPerPulse);
        public float HediffSeverityFactor => GetSnapshotOrRealtime("hediffSeverityFactor", 0f, Props.hediffSeverityFactor);

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            lastSeverity = parent.Severity;
            CaptureSnapshot();
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            if (parent.Severity > lastSeverity)
            {
                CaptureSnapshot();
            }
            lastSeverity = parent.Severity;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref lastSeverity, "lastSeverity", -1f);
            // snapshot cannot be saved; it will be recaptured on load via CompPostPostAdd
            // because severity is serialized and re-applied.
        }

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

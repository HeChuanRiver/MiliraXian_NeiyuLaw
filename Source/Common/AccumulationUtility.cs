using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public static class AccumulationUtility
    {
        public static bool CanAccumulate(Pawn target, HediffDef accumulationHediff)
        {
            if (target == null || target.Dead || target.health?.hediffSet == null || accumulationHediff == null)
            {
                return false;
            }

            if (accumulationHediff.CompProps<HediffCompProperties_Accumulation>() == null)
            {
                return false;
            }

            HediffCompProperties_Accumulation effectProps = accumulationHediff.CompProps<HediffCompProperties_Accumulation>();
            HediffDef effectHediff = effectProps != null ? ResolveEffectHediff(target, effectProps) : null;
            if (effectHediff != null && target.health.hediffSet.GetFirstHediffOfDef(effectHediff) != null)
            {
                return false;
            }

            return true;
        }

        public static bool TryApplyAccumulation(Pawn caster, Pawn target, HediffDef accumulationHediff, float severityOffset)
        {
            return TryApplyAccumulation(caster, target, accumulationHediff, severityOffset, out _);
        }

        public static bool TryApplyAccumulation(Pawn caster, Pawn target, HediffDef accumulationHediff, float severityOffset, out float finalSeverityOffset)
        {
            return TryApplyAccumulation(caster, target, accumulationHediff, severityOffset, out finalSeverityOffset, out _);
        }

        public static bool TryApplyAccumulation(Pawn caster, Pawn target, HediffDef accumulationHediff, float severityOffset, out float finalSeverityOffset, out Hediff appliedHediff)
        {
            if (!CanAccumulate(target, accumulationHediff) || severityOffset <= 0f)
            {
                finalSeverityOffset = 0f;
                appliedHediff = null;
                return false;
            }

            Hediff existing = target.health.hediffSet.GetFirstHediffOfDef(accumulationHediff);
            bool createdHediff = false;
            IAccumulationHediff accumulation = existing as IAccumulationHediff;
            if (existing != null && accumulation == null)
            {
                Log.Error($"Tried to apply status accumulation hediff {accumulationHediff.defName}, but it does not implement {nameof(IAccumulationHediff)}.");
                finalSeverityOffset = 0f;
                appliedHediff = null;
                return false;
            }

            if (existing == null)
            {
                existing = HediffMaker.MakeHediff(accumulationHediff, target);
                existing.Severity = 0f;
                target.health.AddHediff(existing);
                createdHediff = true;
                accumulation = existing as IAccumulationHediff;
            }

            if (accumulation == null)
            {
                Log.Error($"Tried to apply status accumulation hediff {accumulationHediff.defName}, but it does not implement {nameof(IAccumulationHediff)}.");
                finalSeverityOffset = 0f;
                appliedHediff = null;
                return false;
            }

            finalSeverityOffset = ScaleSeverityOffset(caster, existing, severityOffset) * GetResistanceMultiplier(target, accumulationHediff);
            if (finalSeverityOffset <= 0f)
            {
                if (createdHediff && target.health.hediffSet.hediffs.Contains(existing))
                {
                    target.health.RemoveHediff(existing);
                }

                appliedHediff = null;
                return false;
            }

            accumulation.AddAccumulation(caster, finalSeverityOffset);
            appliedHediff = existing;
            return true;
        }

        public static HediffDef ResolveEffectHediff(Pawn target, HediffCompProperties_Accumulation props)
        {
            if (target?.RaceProps?.IsMechanoid == true)
            {
                return props.mechEffectHediff ?? props.effectHediff;
            }

            return props.effectHediff;
        }

        public static int GetResistanceStage(Pawn target, HediffDef accumulationHediff)
        {
            Hediff resistance = GetResistanceHediff(target, accumulationHediff);
            if (resistance == null)
            {
                return 0;
            }

            Hediff_AccumulationResistance typedResistance = resistance as Hediff_AccumulationResistance;
            if (typedResistance != null)
            {
                return typedResistance.CurrentStage;
            }

            return Mathf.Max(0, Mathf.RoundToInt(resistance.Severity));
        }

        public static float GetResistanceMultiplier(Pawn target, HediffDef accumulationHediff)
        {
            Hediff resistance = GetResistanceHediff(target, accumulationHediff);
            if (resistance == null)
            {
                return 1f;
            }

            Hediff_AccumulationResistance typedResistance = resistance as Hediff_AccumulationResistance;
            if (typedResistance != null)
            {
                return typedResistance.AccumulationMultiplier;
            }

            HediffComp_AccumulationResistance comp = (resistance as HediffWithComps)?.TryGetComp<HediffComp_AccumulationResistance>();
            return comp?.AccumulationMultiplier ?? 1f;
        }

        public static void ApplyResistance(Pawn target, HediffDef accumulationHediff, int stage)
        {
            if (target == null || target.Dead || target.health?.hediffSet == null || accumulationHediff == null || stage <= 0)
            {
                return;
            }

            HediffCompProperties_Accumulation accumulationProps = accumulationHediff.CompProps<HediffCompProperties_Accumulation>();
            HediffDef resistanceHediff = accumulationProps?.resistanceHediff;
            if (resistanceHediff == null)
            {
                return;
            }

            HediffCompProperties_AccumulationResistance resistanceProps = resistanceHediff.CompProps<HediffCompProperties_AccumulationResistance>();
            int maxStage = resistanceProps?.maxStage ?? stage;
            int clampedStage = Mathf.Clamp(stage, 1, Mathf.Max(1, maxStage));
            Hediff resistance = target.health.hediffSet.GetFirstHediffOfDef(resistanceHediff);
            if (resistance == null)
            {
                resistance = HediffMaker.MakeHediff(resistanceHediff, target);
                resistance.Severity = clampedStage;
                target.health.AddHediff(resistance);
            }
            else
            {
                resistance.Severity = Mathf.Max(resistance.Severity, clampedStage);
            }

            resistance.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
        }

        private static float ScaleSeverityOffset(Pawn caster, Hediff accumulationHediff, float severityOffset)
        {
            if (accumulationHediff == null)
            {
                return 0f;
            }

            HediffWithComps hediffWithComps = accumulationHediff as HediffWithComps;
            HediffComp_AccumulationScaling comp = hediffWithComps?.TryGetComp<HediffComp_AccumulationScaling>();
            return comp != null ? comp.Scaled(caster, severityOffset) : severityOffset;
        }

        private static Hediff GetResistanceHediff(Pawn target, HediffDef accumulationHediff)
        {
            if (target == null || target.Dead || target.health?.hediffSet == null || accumulationHediff == null)
            {
                return null;
            }

            HediffCompProperties_Accumulation props = accumulationHediff.CompProps<HediffCompProperties_Accumulation>();
            if (props?.resistanceHediff == null)
            {
                return null;
            }

            return target.health.hediffSet.GetFirstHediffOfDef(props.resistanceHediff);
        }
    }
}

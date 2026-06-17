using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_StatusEffectPsychicScale : HediffCompProperties_AccumulationScaling
    {
        public SimpleCurve psychicSensitivityMultiplierCurve;

        public HediffCompProperties_StatusEffectPsychicScale()
        {
            compClass = typeof(HediffComp_StatusEffectPsychicScale);
        }
    }

    public class HediffComp_StatusEffectPsychicScale : HediffComp_AccumulationScaling
    {
        private static readonly SimpleCurve DefaultPsychicSensitivityMultiplierCurve = new SimpleCurve
        {
            new CurvePoint(0f, 0f),
            new CurvePoint(1f, 1f),
            new CurvePoint(2f, 1.5f),
            new CurvePoint(3f, 2f)
        };

        private HediffCompProperties_StatusEffectPsychicScale PropsPsychicScale => (HediffCompProperties_StatusEffectPsychicScale)props;

        public override float Scaled(Pawn caster, float severityOffset)
        {
            if (Pawn == null)
            {
                return severityOffset;
            }

            SimpleCurve curve = PropsPsychicScale.psychicSensitivityMultiplierCurve ?? DefaultPsychicSensitivityMultiplierCurve;
            float psychicSensitivity = Mathf.Max(0f, Pawn.GetStatValue(StatDefOf.PsychicSensitivity, true, -1));
            return severityOffset * Mathf.Max(0f, curve.Evaluate(psychicSensitivity));
        }
    }
}

using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_StatusEffectElectricLeakScale : HediffCompProperties_AccumulationScaling
    {
        public SimpleCurve bodySizeResistanceCurve;
        public float maxResistance = 0.9f;
        public float milianResistance = 0.7f;

        public HediffCompProperties_StatusEffectElectricLeakScale()
        {
            compClass = typeof(HediffComp_StatusEffectElectricLeakScale);
        }
    }

    public class HediffComp_StatusEffectElectricLeakScale : HediffComp_AccumulationScaling
    {
        private static readonly Func<Pawn, bool> isMilian = CreateIsMilianDelegate();

        private HediffCompProperties_StatusEffectElectricLeakScale PropsElectricLeakScale => (HediffCompProperties_StatusEffectElectricLeakScale)props;

        public override float Scaled(Pawn caster, float severityOffset)
        {
            if (Pawn == null)
            {
                return severityOffset;
            }

            float resistance = IsMilian(Pawn)
                ? PropsElectricLeakScale.milianResistance
                : BodySizeResistance(Pawn);
            return severityOffset * Mathf.Clamp01(1f - resistance);
        }

        private float BodySizeResistance(Pawn pawn)
        {
            float bodySize = Mathf.Max(0f, pawn.BodySize);
            SimpleCurve curve = PropsElectricLeakScale.bodySizeResistanceCurve;
            float resistance = curve != null ? curve.Evaluate(bodySize) : 0f;
            return Mathf.Clamp(resistance, 0f, Mathf.Clamp01(PropsElectricLeakScale.maxResistance));
        }

        private static bool IsMilian(Pawn pawn)
        {
            return isMilian != null && isMilian(pawn);
        }

        private static Func<Pawn, bool> CreateIsMilianDelegate()
        {
            try
            {
                Type utilityType = AccessTools.TypeByName("Milira.MilianUtility");
                if (utilityType == null)
                {
                    return null;
                }

                var method = AccessTools.Method(utilityType, "IsMilian", new[] { typeof(Pawn) });
                if (method == null)
                {
                    return null;
                }

                return (Func<Pawn, bool>)Delegate.CreateDelegate(typeof(Func<Pawn, bool>), method);
            }
            catch (Exception ex)
            {
                Log.Warning("[MiliraXian.Characters.QingHe] Failed to bind optional Milira.MilianUtility.IsMilian: " + ex.Message);
                return null;
            }
        }
    }
}

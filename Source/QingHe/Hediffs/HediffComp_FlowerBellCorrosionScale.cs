using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerBellCorrosionScale : HediffCompProperties_AccumulationScaling
    {
        public SimpleCurve bodySizeResistanceCurve;
        public float maxResistance = 0.9f;
        public float apparelResistance = 0.2f;

        public HediffCompProperties_FlowerBellCorrosionScale()
        {
            compClass = typeof(HediffComp_FlowerBellCorrosionScale);
        }
    }

    public class HediffComp_FlowerBellCorrosionScale : HediffComp_AccumulationScaling
    {
        private HediffCompProperties_FlowerBellCorrosionScale PropsCorrosionScale => (HediffCompProperties_FlowerBellCorrosionScale)props;

        public override float Scaled(Pawn caster, float severityOffset)
        {
            if (Pawn == null)
            {
                return severityOffset;
            }

            float multiplier = Mathf.Clamp01(1f - BodySizeResistance(Pawn));
            if (HasApparel(Pawn))
            {
                multiplier *= Mathf.Clamp01(1f - PropsCorrosionScale.apparelResistance);
            }

            return severityOffset * multiplier;
        }

        private float BodySizeResistance(Pawn pawn)
        {
            float bodySize = Mathf.Max(0f, pawn.BodySize);
            SimpleCurve curve = PropsCorrosionScale.bodySizeResistanceCurve;
            float resistance = curve != null ? curve.Evaluate(bodySize) : 0f;
            return Mathf.Clamp(resistance, 0f, Mathf.Clamp01(PropsCorrosionScale.maxResistance));
        }

        private static bool HasApparel(Pawn pawn)
        {
            return pawn.apparel?.WornApparelCount > 0;
        }
    }
}

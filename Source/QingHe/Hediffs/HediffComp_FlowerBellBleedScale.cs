using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerBellBleedScale : HediffCompProperties_AccumulationScaling
    {
        public SimpleCurve sharpArmorResistanceCurve;
        public SimpleCurve smallBodySizeMultiplierCurve;
        public float nonFleshMultiplier = 0f;

        public HediffCompProperties_FlowerBellBleedScale()
        {
            compClass = typeof(HediffComp_FlowerBellBleedScale);
        }
    }

    public class HediffComp_FlowerBellBleedScale : HediffComp_AccumulationScaling
    {
        private HediffCompProperties_FlowerBellBleedScale PropsBleedScale => (HediffCompProperties_FlowerBellBleedScale)props;

        public override float Scaled(Pawn caster, float severityOffset)
        {
            if (Pawn == null)
            {
                return severityOffset;
            }

            if (Pawn.RaceProps?.IsFlesh != true || Pawn.health?.CanBleed != true)
            {
                return severityOffset * Mathf.Clamp01(PropsBleedScale.nonFleshMultiplier);
            }

            float sharpArmor = Mathf.Max(0f, SharpArmorWithoutApparel(Pawn));
            float armorResistance = PropsBleedScale.sharpArmorResistanceCurve?.Evaluate(sharpArmor) ?? 0f;
            float bodySizeMultiplier = PropsBleedScale.smallBodySizeMultiplierCurve?.Evaluate(Mathf.Max(0f, Pawn.BodySize)) ?? 1f;
            return severityOffset * Mathf.Clamp01(1f - armorResistance) * Mathf.Max(0f, bodySizeMultiplier);
        }

        private static float SharpArmorWithoutApparel(Pawn pawn)
        {
            float armor = pawn.GetStatValue(StatDefOf.ArmorRating_Sharp, true, -1);
            if (pawn.apparel == null)
            {
                return armor;
            }

            var wornApparel = pawn.apparel.WornApparel;
            for (int i = 0; i < wornApparel.Count; i++)
            {
                armor -= StatWorker.StatOffsetFromGear(wornApparel[i], StatDefOf.ArmorRating_Sharp);
            }

            return armor;
        }
    }
}

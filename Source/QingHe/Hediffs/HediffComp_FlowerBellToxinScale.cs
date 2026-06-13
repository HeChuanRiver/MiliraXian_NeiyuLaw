using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerBellToxinScale : HediffCompProperties_AccumulationScaling
    {
        public bool useToxicResistance = true;
        public bool useToxicEnvironmentResistance = true;

        public HediffCompProperties_FlowerBellToxinScale()
        {
            compClass = typeof(HediffComp_FlowerBellToxinScale);
        }
    }

    public class HediffComp_FlowerBellToxinScale : HediffComp_AccumulationScaling
    {
        private HediffCompProperties_FlowerBellToxinScale PropsToxinScale => (HediffCompProperties_FlowerBellToxinScale)props;

        public override float Scaled(Pawn caster, float severityOffset)
        {
            if (Pawn == null)
            {
                return severityOffset;
            }

            float multiplier = 1f;
            if (PropsToxinScale.useToxicResistance)
            {
                multiplier *= ResistanceMultiplier(StatDefOf.ToxicResistance);
            }

            if (PropsToxinScale.useToxicEnvironmentResistance)
            {
                multiplier *= ResistanceMultiplier(StatDefOf.ToxicEnvironmentResistance);
            }

            return severityOffset * multiplier;
        }

        private float ResistanceMultiplier(StatDef statDef)
        {
            return Mathf.Max(1f - Pawn.GetStatValue(statDef, true, -1), 0f);
        }
    }
}

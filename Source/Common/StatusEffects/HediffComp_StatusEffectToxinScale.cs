using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_StatusEffectToxinScale : HediffCompProperties_AccumulationScaling
    {
        public bool useToxicResistance = true;
        public bool useToxicEnvironmentResistance = true;

        public HediffCompProperties_StatusEffectToxinScale()
        {
            compClass = typeof(HediffComp_StatusEffectToxinScale);
        }
    }

    public class HediffComp_StatusEffectToxinScale : HediffComp_AccumulationScaling
    {
        private HediffCompProperties_StatusEffectToxinScale PropsToxinScale => (HediffCompProperties_StatusEffectToxinScale)props;

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

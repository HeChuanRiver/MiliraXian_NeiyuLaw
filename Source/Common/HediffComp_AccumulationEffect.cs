using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_AccumulationEffect : HediffCompProperties
    {
        public HediffDef sourceAccumulationHediff;

        public HediffCompProperties_AccumulationEffect()
        {
            compClass = typeof(HediffComp_AccumulationEffect);
        }
    }

    public class HediffComp_AccumulationEffect : HediffComp
    {
        private HediffDef sourceAccumulationHediff;
        private int resistanceStageBeforeEffect;

        private HediffCompProperties_AccumulationEffect PropsEffect => (HediffCompProperties_AccumulationEffect)props;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            if (sourceAccumulationHediff == null)
            {
                sourceAccumulationHediff = PropsEffect.sourceAccumulationHediff;
            }

            if (resistanceStageBeforeEffect <= 0 && sourceAccumulationHediff != null)
            {
                resistanceStageBeforeEffect = AccumulationUtility.GetResistanceStage(Pawn, sourceAccumulationHediff);
            }
        }

        public void Initialize(HediffDef accumulationHediff, int existingResistanceStage)
        {
            sourceAccumulationHediff = accumulationHediff;
            resistanceStageBeforeEffect = Mathf.Max(0, existingResistanceStage);
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            HediffDef accumulationHediff = sourceAccumulationHediff ?? PropsEffect.sourceAccumulationHediff;
            if (accumulationHediff == null)
            {
                return;
            }

            AccumulationUtility.ApplyResistance(Pawn, accumulationHediff, resistanceStageBeforeEffect + 1);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Defs.Look(ref sourceAccumulationHediff, "sourceAccumulationHediff");
            Scribe_Values.Look(ref resistanceStageBeforeEffect, "resistanceStageBeforeEffect", 0);
        }
    }
}

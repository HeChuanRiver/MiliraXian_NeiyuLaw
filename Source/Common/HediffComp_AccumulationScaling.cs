using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class HediffCompProperties_AccumulationScaling : HediffCompProperties
    {
        public HediffCompProperties_AccumulationScaling()
        {
            compClass = typeof(HediffComp_AccumulationScaling);
        }
    }

    public class HediffComp_AccumulationScaling : HediffComp
    {
        public virtual float Scaled(Pawn caster, float severityOffset)
        {
            return Mathf.Max(0f, severityOffset);
        }
    }
}

using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_AbilitySpringFlow : CompProperties_AbilityEffect
    {
        public ThingDef fieldDef = MX_QHDefOf.SpringFlowField;
        public int fieldDurationTicks = 900;
        public float previewRadius = 6.0f;

        public CompProperties_AbilitySpringFlow()
        {
            compClass = typeof(CompAbilityEffect_SpringFlow);
        }
    }

    public class CompAbilityEffect_SpringFlow : CompAbilityEffect
    {
        public new CompProperties_AbilitySpringFlow Props => (CompProperties_AbilitySpringFlow)props;

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            base.DrawEffectPreview(target);
            GenDraw.DrawRadiusRing(target.Cell, ResolvePreviewRadius(), Color.magenta);
        }

        private float ResolvePreviewRadius()
        {
            if (Props.fieldDef != null && Props.fieldDef.comps != null)
            {
                for (int i = 0; i < Props.fieldDef.comps.Count; i++)
                {
                    if (Props.fieldDef.comps[i] is CompProperties_SpringFlowField fieldProps)
                    {
                        return fieldProps.radius;
                    }
                }
            }

            return Props.previewRadius;
        }
    }
}

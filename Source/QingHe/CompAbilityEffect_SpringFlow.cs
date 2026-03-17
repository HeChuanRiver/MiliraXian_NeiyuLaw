using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_AbilitySpringFlow : CompProperties_AbilityEffect
    {
        public ThingDef fieldDef = MX_QHDefOf.SpringFlowField;
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
            GenDraw.DrawRadiusRing(target.Cell, Props.previewRadius, Color.magenta);
        }
    }
}
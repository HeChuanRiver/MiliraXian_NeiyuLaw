using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_AbilityYangChun : CompProperties_AbilityEffect
    {
        public ThingDef requiredWeapon;
        public int postCastDelayTicks = 6;
        public ThingDef fieldDef;
        public int fieldDurationTicks = 300;
        public float previewRadius = 7.9f;

        public CompProperties_AbilityYangChun()
        {
            compClass = typeof(CompAbilityEffect_YangChun);
        }
    }

    public class CompAbilityEffect_YangChun : CompAbilityEffect
    {
        private new CompProperties_AbilityYangChun Props => (CompProperties_AbilityYangChun)props;
        
        public override bool ShouldHideGizmo => !MX_QHUtility.HasRequiredWeapon(parent?.pawn, Props.requiredWeapon);
        
        public override bool GizmoDisabled(out string reason)
        {
            if (!MX_QHUtility.HasRequiredWeapon(parent?.pawn, Props.requiredWeapon))
            {
                reason = "需要对应武器";
                return true;
            }

            reason = null;
            return false;
        }
        
        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            var caster = parent?.pawn;
            if (caster == null || !caster.Spawned)
            {
                return;
            }

            GenDraw.DrawRadiusRing(caster.Position, ResolvePreviewRadius(), Color.green);
        }
        
        private float ResolvePreviewRadius()
        {
            if (Props.fieldDef?.comps == null) return Props.previewRadius;
            foreach (var t in Props.fieldDef.comps)
            {
                if (t is CompProperties_YangChunField fieldProps)
                {
                    return fieldProps.radius;
                }
            }

            return Props.previewRadius;
        }
    }
}

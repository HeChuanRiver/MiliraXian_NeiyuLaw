using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_AbilityYangChun : CompProperties_AbilityEffect
    {
        public ThingDef requiredWeapon;

        /// <summary>
        /// Extra delay ticks after base cast in the same Job.
        /// </summary>
        public int postCastDelayTicks = 6;

        /// <summary>
        /// YangChun field ThingDef.
        /// </summary>
        public ThingDef fieldDef;

        /// <summary>
        /// Channel duration ticks in Job.
        /// </summary>
        public int fieldDurationTicks = 300;

        /// <summary>
        /// Fallback preview radius when field comp config is unavailable.
        /// </summary>
        public float previewRadius = 7.9f;

        public CompProperties_AbilityYangChun()
        {
            compClass = typeof(CompAbilityEffect_YangChun);
        }
    }

    public class CompAbilityEffect_YangChun : CompAbilityEffect
    {
        private new CompProperties_AbilityYangChun Props
        {
            get { return (CompProperties_AbilityYangChun)props; }
        }

        /// <summary>
        /// Hide gizmo when required weapon is not equipped.
        /// </summary>
        public override bool ShouldHideGizmo
        {
            get
            {
                return !MX_QHUtility.HasRequiredWeapon(parent != null ? parent.pawn : null, Props.requiredWeapon);
            }
        }

        /// <summary>
        /// Disable gizmo when required weapon is not equipped.
        /// </summary>
        public override bool GizmoDisabled(out string reason)
        {
            if (!MX_QHUtility.HasRequiredWeapon(parent != null ? parent.pawn : null, Props.requiredWeapon))
            {
                reason = "需要对应武器";
                return true;
            }

            reason = null;
            return false;
        }

        /// <summary>
        /// Draw YangChun radius preview around caster.
        /// </summary>
        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent != null ? parent.pawn : null;
            if (caster == null || !caster.Spawned)
            {
                return;
            }

            GenDraw.DrawRadiusRing(caster.Position, ResolvePreviewRadius(), Color.green);
        }

        /// <summary>
        /// Actual effect is executed in JobDriver_CastAbility_YangChun.
        /// </summary>
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
        }

        /// <summary>
        /// Prefer radius configured on field ThingDef comp.
        /// </summary>
        private float ResolvePreviewRadius()
        {
            if (Props.fieldDef != null && Props.fieldDef.comps != null)
            {
                for (int i = 0; i < Props.fieldDef.comps.Count; i++)
                {
                    if (Props.fieldDef.comps[i] is CompProperties_YangChunField fieldProps)
                    {
                        return fieldProps.radius;
                    }
                }
            }

            return Props.previewRadius;
        }
    }
}

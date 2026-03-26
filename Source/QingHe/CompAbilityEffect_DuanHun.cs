using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_AbilityDuanHun : CompProperties_AbilityEffect
    {
        public ThingDef requiredWeapon;

        /// <summary>
        /// Extra delay ticks after base cast in the same Job.
        /// </summary>
        public int postCastDelayTicks = 0;

        public float radius = 2.5f;

        public DamageDef damageDef = null;
        public float damageAmount = 16f;
        public float armorPenetration = 0.25f;

        public float stunDamageAmount = 8f;
        public float bleedDamageAmount = 5f;

        public HediffDef slowHediff;
        public float slowSeverity = 1f;
        public int slowDurationTicks = 1800;

        public float brainDestroyChance = 0.08f;

        public float eleganceGainOnCast = 8f;
        public float eleganceGainPerTarget = 2f;

        public CompProperties_AbilityDuanHun()
        {
            compClass = typeof(CompAbilityEffect_DuanHun);
        }
    }

    public class CompAbilityEffect_DuanHun : CompAbilityEffect
    {
        private new CompProperties_AbilityDuanHun Props
        {
            get { return (CompProperties_AbilityDuanHun)props; }
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
        /// Draw radius preview at target cell.
        /// </summary>
        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                return;
            }

            GenDraw.DrawRadiusRing(target.Cell, Props.radius, Color.yellow);
        }

        /// <summary>
        /// Actual effect is executed in JobDriver_CastAbility_DuanHun.
        /// </summary>
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
        }
    }
}

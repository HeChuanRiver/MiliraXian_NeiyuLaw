using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_AbilityHengZhi : CompProperties_AbilityEffect
    {
        public ThingDef requiredWeapon;

        /// <summary>
        /// Extra delay ticks after base cast in the same Job.
        /// </summary>
        public int postCastDelayTicks = 0;

        public float radius = 3.9f;
        public DamageDef damageDef = null;
        public float damageAmount = 24f;
        public float armorPenetration = 0.55f;
        public float knockbackDistance = 4f;
        public float bluntDamageAmount = 10f;
        public float bluntArmorPenetration = 0.15f;

        public float eleganceGainPerTarget = 3f;
        public float eleganceGainMax = 24f;

        public CompProperties_AbilityHengZhi()
        {
            compClass = typeof(CompAbilityEffect_HengZhi);
        }
    }

    public class CompAbilityEffect_HengZhi : CompAbilityEffect
    {
        private new CompProperties_AbilityHengZhi Props
        {
            get { return (CompProperties_AbilityHengZhi)props; }
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
        /// Draw radius preview around caster.
        /// </summary>
        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent != null ? parent.pawn : null;
            if (caster == null || !caster.Spawned)
            {
                return;
            }

            GenDraw.DrawRadiusRing(caster.Position, Props.radius, Color.cyan);
        }

        /// <summary>
        /// Actual effect is executed in JobDriver_CastAbility_HengZhi.
        /// </summary>
        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
        }
    }
}

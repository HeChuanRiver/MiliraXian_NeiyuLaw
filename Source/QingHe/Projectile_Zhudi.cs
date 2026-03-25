using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class Projectile_Zhudi : Bullet, IProjectileHomingCurveHost
    {
        private const float SlowSeverityOnHit = 1.0f;
        private const int SlowDurationTicksOnHit = 240;
        private const float BleedDamageOnHit = 4.0f;

        public override void Launch(
            Thing launcher,
            Vector3 origin,
            LocalTargetInfo usedTarget,
            LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags,
            bool preventFriendlyFire = false,
            Thing equipment = null,
            ThingDef targetCoverDef = null)
        {
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            GetComp<CompProjectileHomingCurve>()?.NotifyLaunch(Find.TickManager.TicksGame);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 impactPos = ExactPosition;
            base.Impact(hitThing, blockedByShield);
            GetComp<CompProjectileEleganceEffect>()?.NotifyImpact(hitThing, blockedByShield);
            TryApplyZhudiHitEffects(hitThing, blockedByShield);
            if (map != null && !blockedByShield)
            {
                FleckMaker.Static(impactPos, map, FleckDefOf.ExplosionFlash, 0.90f);
            }
        }

        private void TryApplyZhudiHitEffects(Thing hitThing, bool blockedByShield)
        {
            Pawn caster = launcher as Pawn;
            Pawn victim = hitThing as Pawn;
            if (blockedByShield || caster == null || victim == null || victim.Dead)
            {
                return;
            }

            if (!caster.HostileTo(victim))
            {
                return;
            }

            if (MX_QHDefOf.MX_QH_Elegance_DuanHunSlow != null && SlowSeverityOnHit > 0f)
            {
                MX_QHUtility.TryApplyOrRefreshHediff(victim, MX_QHDefOf.MX_QH_Elegance_DuanHunSlow, SlowSeverityOnHit, SlowDurationTicksOnHit);
            }

            if (BleedDamageOnHit > 0f)
            {
                MX_QHUtility.ApplyBleed(victim, BleedDamageOnHit);
            }
        }

        public bool AllowHomingUpdate
        {
            get
            {
                return !landed;
            }
        }

        public LocalTargetInfo HomingIntendedTarget => intendedTarget;

        public Vector3 HomingExactPosition => ExactPosition;

        public int HomingTicksToImpact => ticksToImpact;

        public int StartingTicksToImpactCeil()
        {
            return Mathf.CeilToInt(StartingTicksToImpact);
        }

        public void BeginHoming(int minTicksToImpact)
        {
            origin = ExactPosition;
            ticksToImpact = Mathf.Max(minTicksToImpact, StartingTicksToImpactCeil());
            lifetime = ticksToImpact;
        }

        public void LerpHomingDestination(Vector3 desired, float lerp)
        {
            destination = Vector3.Lerp(destination, desired, Mathf.Clamp01(lerp));
        }
    }
}

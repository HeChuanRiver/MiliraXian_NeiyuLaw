using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class Projectile_Pipa : Bullet, IProjectileHomingCurveHost
    {
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
            if (map != null && !blockedByShield)
            {
                FleckMaker.Static(impactPos, map, FleckDefOf.ExplosionFlash, 0.60f);
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

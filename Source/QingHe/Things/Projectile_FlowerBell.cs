using RimWorld;
using UnityEngine;
using Verse;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Verbs;

namespace MiliraXian.Characters.QingHe.Things
{
    public class Projectile_FlowerBell : ProjectileHomingCurveBase
    {
        private bool enhancedOnLaunch;

        public bool EnhancedOnLaunch => enhancedOnLaunch;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref enhancedOnLaunch, "mx_qh_flowerBell_enhancedOnLaunch", defaultValue: false);
        }

        public override void Launch(Thing launcher, Vector3 origin, LocalTargetInfo usedTarget, LocalTargetInfo intendedTarget, ProjectileHitFlags hitFlags, bool preventFriendlyFire = false, Thing equipment = null, ThingDef targetCoverDef = null)
        {
            enhancedOnLaunch = ResolveEnhancedOnLaunch(launcher);
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 impactPos = ExactPosition;
            Thing resolvedHitThing = ResolveImpactHitThing(hitThing, impactPos, map);

            GetComp<CompFlowerBellDivinationAreaOnHit>()?.NotifyImpact(this, resolvedHitThing, blockedByShield, impactPos);
            base.Impact(resolvedHitThing, blockedByShield);
            GetComp<CompProjectileResourceOnHit>()?.NotifyImpact(resolvedHitThing, blockedByShield);
            GetComp<CompFlowerBellStatusOnHit>()?.NotifyImpact(resolvedHitThing, blockedByShield);
            if (map != null && !blockedByShield)
            {
                FleckMaker.Static(impactPos, map, FleckDefOf.ExplosionFlash, 0.60f);
            }
        }

        private static bool ResolveEnhancedOnLaunch(Thing launcher)
        {
            Pawn pawn = launcher as Pawn;
            Verb_ShootFlowerBell verb = (pawn?.stances?.curStance as Stance_Busy)?.verb as Verb_ShootFlowerBell;
            return verb?.EnhancedForCurrentShot == true;
        }
    }
}

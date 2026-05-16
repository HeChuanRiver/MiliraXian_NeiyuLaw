using RimWorld;
using UnityEngine;
using Verse;
using MiliraXian.Characters;

namespace MiliraXian.Characters.QingHe.Things
{
    public class Projectile_FlowerBell : ProjectileHomingCurveBase
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 impactPos = ExactPosition;
            Thing resolvedHitThing = ResolveImpactHitThing(hitThing, impactPos, map);

            base.Impact(resolvedHitThing, blockedByShield);
            GetComp<CompProjectileResourceOnHit>()?.NotifyImpact(resolvedHitThing, blockedByShield);
            if (map != null && !blockedByShield)
            {
                FleckMaker.Static(impactPos, map, FleckDefOf.ExplosionFlash, 0.60f);
            }
        }
    }
}


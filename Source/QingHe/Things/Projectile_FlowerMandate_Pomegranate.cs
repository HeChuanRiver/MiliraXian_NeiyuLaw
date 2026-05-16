using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things
{
    public class Projectile_FlowerMandate_Pomegranate : ProjectileHomingCurveBase
    {
        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 impactPos = ExactPosition;
            Thing resolvedHitThing = ResolveImpactHitThing(hitThing, impactPos, map);

            base.Impact(resolvedHitThing, blockedByShield);
            if (map != null && !blockedByShield)
            {
                FleckMaker.Static(impactPos, map, FleckDefOf.ExplosionFlash, 0.45f);
            }
        }
    }
}

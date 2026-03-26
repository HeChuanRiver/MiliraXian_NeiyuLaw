using RimWorld;
using UnityEngine;
using Verse;
using MiliraXian.Characters;

namespace MiliraXian.Characters.QingHe
{
    public class Projectile_Zhudi : ProjectileHomingCurveBase
    {
        private const float SlowSeverityOnHit = 1.0f;
        private const int SlowDurationTicksOnHit = 240;
        private const float BleedDamageOnHit = 4.0f;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 impactPos = ExactPosition;
            Thing resolvedHitThing = ResolveImpactHitThing(hitThing, impactPos, map);

            base.Impact(resolvedHitThing, blockedByShield);
            GetComp<CompProjectileEleganceEffect>()?.NotifyImpact(resolvedHitThing, blockedByShield);
            TryApplyZhudiHitEffects(resolvedHitThing, blockedByShield);
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
    }
}

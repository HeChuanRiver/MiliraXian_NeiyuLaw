using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things
{
    public class CompProperties_FlowerBellDivinationAreaOnHit : CompProperties
    {
        public float radius = 2.4f;
        public float damageMultiplier = 1f;
        public bool requireHostileTarget = true;
        public ThingDef burstMoteDef;
        public float burstMoteScale = 2.2f;

        public CompProperties_FlowerBellDivinationAreaOnHit()
        {
            compClass = typeof(CompFlowerBellDivinationAreaOnHit);
        }
    }

    public class CompFlowerBellDivinationAreaOnHit : ThingComp
    {
        private const string DefaultBurstMoteDefName = "MX_QH_Mote_FlowerDivinationBurst";

        public CompProperties_FlowerBellDivinationAreaOnHit Props => (CompProperties_FlowerBellDivinationAreaOnHit)props;

        public void NotifyImpact(Projectile_FlowerBell projectile, Thing mainTarget, bool blockedByShield, Vector3 impactPos)
        {
            Pawn caster = projectile?.Launcher as Pawn;
            Map map = projectile?.Map;
            if (blockedByShield || caster == null || map == null || projectile.EnhancedOnLaunch == false)
            {
                return;
            }

            IntVec3 center = impactPos.ToIntVec3();
            if (!center.InBounds(map))
            {
                center = center.ClampInsideMap(map);
            }

            PlayBurstMote(map, impactPos);
            foreach (Pawn target in RadialUtility.CollectHostilePawns(map, center, caster, Mathf.Max(0f, Props.radius)))
            {
                if (target == mainTarget)
                {
                    continue;
                }

                ApplyAreaHit(projectile, caster, target);
            }
        }

        private void ApplyAreaHit(Projectile_FlowerBell projectile, Pawn caster, Pawn target)
        {
            if (Props.requireHostileTarget && !GenHostility.HostileTo(caster, target))
            {
                return;
            }

            float amount = Mathf.Max(0f, projectile.DamageAmount * Props.damageMultiplier);
            if (amount > 0f)
            {
                DamageInfo dinfo = new DamageInfo(
                    projectile.DamageDef,
                    amount,
                    projectile.ArmorPenetration,
                    projectile.ExactRotation.eulerAngles.y,
                    caster,
                    null,
                    projectile.EquipmentDef,
                    DamageInfo.SourceCategory.ThingOrUnknown,
                    target);
                target.TakeDamage(dinfo);
                target.stances?.stagger.Notify_BulletImpact(projectile);
            }

            projectile.GetComp<CompFlowerBellStatusOnHit>()?.NotifyImpact(target, blockedByShield: false);
        }

        private void PlayBurstMote(Map map, Vector3 impactPos)
        {
            ThingDef moteDef = Props.burstMoteDef ?? DefDatabase<ThingDef>.GetNamedSilentFail(DefaultBurstMoteDefName);
            if (moteDef == null)
            {
                return;
            }

            float scale = Props.burstMoteScale > 0f ? Props.burstMoteScale : Mathf.Max(1f, Props.radius);
            MoteMaker.MakeStaticMote(impactPos, map, moteDef, scale, makeOffscreen: true);
        }
    }
}

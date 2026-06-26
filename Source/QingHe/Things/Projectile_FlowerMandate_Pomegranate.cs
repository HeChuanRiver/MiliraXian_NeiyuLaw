using MiliraXian.Characters;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things
{
    public class CompProperties_FlowerMandate_PomegranateAreaHit : CompProperties
    {
        public float radius = 2.4f;
        public float damageMultiplier = 1f;
        public DamageDef toxinDamageDef = MX_StatusEffectsDefOf.MX_StatusEffectToxinAccumulation;
        public float toxinDamageAmount = 0.05f;
        public float toxinArmorPenetration = 2.1f;
        public ThingDef burstMoteDef;
        public float burstMoteScale = 2.2f;

        public CompProperties_FlowerMandate_PomegranateAreaHit()
        {
            compClass = typeof(CompFlowerMandate_PomegranateAreaHit);
        }
    }

    public class CompFlowerMandate_PomegranateAreaHit : ThingComp
    {
        private const string DefaultBurstMoteDefName = "MX_QH_Mote_FlowerDivinationBurst";

        public CompProperties_FlowerMandate_PomegranateAreaHit Props => (CompProperties_FlowerMandate_PomegranateAreaHit)props;

        public void NotifyImpact(Projectile_FlowerMandate_Pomegranate projectile, Thing mainTarget, bool blockedByShield, Vector3 impactPos)
        {
            if (blockedByShield || projectile == null)
            {
                return;
            }

            Pawn caster = projectile.CasterOnLaunch;
            Map map = projectile.Map;
            if (caster == null || map == null)
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
                ApplyAreaDamage(projectile, caster, target, mainTarget);
                ApplyToxinDamage(caster, target, projectile);
            }
        }

        private void ApplyAreaDamage(Projectile_FlowerMandate_Pomegranate projectile, Pawn caster, Pawn target, Thing mainTarget)
        {
            if (target == mainTarget)
            {
                return;
            }

            float amount = Mathf.Max(0f, projectile.DamageAmount * Props.damageMultiplier);
            if (amount <= 0f)
            {
                return;
            }

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

        private void ApplyToxinDamage(Pawn caster, Pawn target, Projectile_FlowerMandate_Pomegranate projectile)
        {
            if (Props.toxinDamageDef == null || Props.toxinDamageAmount <= 0f)
            {
                return;
            }

            DamageInfo dinfo = new DamageInfo(
                Props.toxinDamageDef,
                Props.toxinDamageAmount,
                Props.toxinArmorPenetration,
                projectile.ExactRotation.eulerAngles.y,
                caster);
            target.TakeDamage(dinfo);
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

    public class Verb_ShootFlowerMandatePomegranate : Verb_Shoot
    {
        public override ThingDef Projectile
        {
            get
            {
                CompProperties_FlowerMandate_PomegranateGun props = EquipmentSource?.GetComp<CompFlowerMandate_PomegranateGun>()?.Props;
                if (props?.enhancedProjectileDef != null
                    && caster?.TryGetComp<CompFlowerMandate_PomegranateLifetime>()?.Enhanced == true)
                {
                    return props.enhancedProjectileDef;
                }

                return base.Projectile;
            }
        }
    }

    public class CompProperties_FlowerMandate_PomegranateGun : CompProperties
    {
        public ThingDef enhancedProjectileDef;

        public CompProperties_FlowerMandate_PomegranateGun()
        {
            compClass = typeof(CompFlowerMandate_PomegranateGun);
        }
    }

    public class CompFlowerMandate_PomegranateGun : ThingComp
    {
        public CompProperties_FlowerMandate_PomegranateGun Props => (CompProperties_FlowerMandate_PomegranateGun)props;
    }

    public class Projectile_FlowerMandate_Pomegranate : ProjectileHomingCurveBase
    {
        private Pawn casterOnLaunch;
        private bool enhancedOnLaunch;

        public Pawn CasterOnLaunch => casterOnLaunch;
        public bool EnhancedOnLaunch => enhancedOnLaunch;

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
            CompFlowerMandate_PomegranateLifetime lifetime = launcher?.TryGetComp<CompFlowerMandate_PomegranateLifetime>();
            casterOnLaunch = lifetime?.Caster ?? launcher as Pawn;
            enhancedOnLaunch = lifetime?.Enhanced == true;
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref casterOnLaunch, "casterOnLaunch", false);
            Scribe_Values.Look(ref enhancedOnLaunch, "enhancedOnLaunch", false);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 impactPos = ExactPosition;
            Thing resolvedHitThing = ResolveImpactHitThing(hitThing, impactPos, map);

            GetComp<CompFlowerMandate_PomegranateAreaHit>()?.NotifyImpact(this, resolvedHitThing, blockedByShield, impactPos);
            base.Impact(resolvedHitThing, blockedByShield);
            if (map != null && !blockedByShield)
            {
                FleckMaker.Static(impactPos, map, FleckDefOf.ExplosionFlash, 0.45f);
            }
        }
    }
}

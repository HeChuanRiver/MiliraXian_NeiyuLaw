using MiliraXian.Characters;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things
{
    public class CompProperties_FlowerMandate_WintersweetRetaliationAreaHit : CompProperties
    {
        public float radius = 2.4f;
        public float areaDamageMultiplier = 1f;
        public DamageDef coldDamageDef = MX_StatusEffectsDefOf.MX_StatusEffectColdAccumulation;
        public float coldDamageAmount = 0.08f;
        public float coldArmorPenetration = 2.1f;
        public ThingDef burstMoteDef;
        public float burstMoteScale = 2.2f;

        public CompProperties_FlowerMandate_WintersweetRetaliationAreaHit()
        {
            compClass = typeof(CompFlowerMandate_WintersweetRetaliationAreaHit);
        }
    }

    public class CompFlowerMandate_WintersweetRetaliationAreaHit : ThingComp
    {
        public CompProperties_FlowerMandate_WintersweetRetaliationAreaHit Props => (CompProperties_FlowerMandate_WintersweetRetaliationAreaHit)props;
    }

    public class Projectile_FlowerMandate_WintersweetRetaliation : Bullet
    {
        private CompProperties_FlowerMandate_WintersweetRetaliationAreaHit AreaProps => GetComp<CompFlowerMandate_WintersweetRetaliationAreaHit>()?.Props;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 impactPos = ExactPosition;
            IntVec3 center = Position;
            Pawn caster = Launcher as Pawn;

            base.Impact(hitThing, blockedByShield);
            if (blockedByShield || map == null || caster == null)
            {
                return;
            }

            if (!center.InBounds(map))
            {
                center = impactPos.ToIntVec3().ClampInsideMap(map);
            }

            PlayBurstMote(map, impactPos);
            float radius = AreaProps?.radius ?? 2.4f;
            foreach (Pawn target in RadialUtility.CollectHostilePawns(map, center, caster, Mathf.Max(0f, radius)))
            {
                ApplyAreaDamage(target, hitThing, caster);
                ApplyColdDamage(target, caster);
            }
        }

        private void ApplyAreaDamage(Pawn target, Thing mainTarget, Pawn caster)
        {
            if (target == mainTarget)
            {
                return;
            }

            float multiplier = AreaProps?.areaDamageMultiplier ?? 1f;
            float amount = Mathf.Max(0f, DamageAmount * multiplier);
            if (amount <= 0f)
            {
                return;
            }

            DamageInfo dinfo = new DamageInfo(
                DamageDef,
                amount,
                ArmorPenetration,
                ExactRotation.eulerAngles.y,
                caster,
                null,
                EquipmentDef,
                DamageInfo.SourceCategory.ThingOrUnknown,
                target);
            target.TakeDamage(dinfo);
            target.stances?.stagger.Notify_BulletImpact(this);
        }

        private void ApplyColdDamage(Pawn target, Pawn caster)
        {
            DamageDef coldDamageDef = AreaProps?.coldDamageDef ?? MX_StatusEffectsDefOf.MX_StatusEffectColdAccumulation;
            float coldDamageAmount = AreaProps?.coldDamageAmount ?? 0.08f;
            if (coldDamageDef == null || coldDamageAmount <= 0f)
            {
                return;
            }

            float armorPenetration = AreaProps?.coldArmorPenetration ?? 2.1f;
            DamageInfo dinfo = new DamageInfo(
                coldDamageDef,
                coldDamageAmount,
                armorPenetration,
                ExactRotation.eulerAngles.y,
                caster);
            target.TakeDamage(dinfo);
        }

        private void PlayBurstMote(Map map, Vector3 impactPos)
        {
            float radius = AreaProps?.radius ?? 2.4f;
            ThingDef burstMoteDef = AreaProps?.burstMoteDef;
            ThingDef moteDef = burstMoteDef ?? DefDatabase<ThingDef>.GetNamedSilentFail("MX_QH_Mote_FlowerDivinationBurst");
            if (moteDef == null)
            {
                return;
            }

            float burstMoteScale = AreaProps?.burstMoteScale ?? 2.2f;
            float scale = burstMoteScale > 0f ? burstMoteScale : Mathf.Max(1f, radius);
            MoteMaker.MakeStaticMote(impactPos, map, moteDef, scale, makeOffscreen: true);
        }
    }
}

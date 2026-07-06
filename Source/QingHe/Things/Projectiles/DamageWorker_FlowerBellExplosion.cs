using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Things.Weapons;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Projectiles
{
    public class DamageWorker_FlowerBellExplosion : DamageWorker_AddInjury
    {
        protected override void ExplosionVisualEffectCenter(Explosion explosion)
        {
        }

        protected override void ExplosionDamageThing(Explosion explosion, Thing t, List<Thing> damagedThings, List<Thing> ignoredThings, IntVec3 cell)
        {
            if (explosion == null || t == null || damagedThings == null || damagedThings.Contains(t) || ignoredThings?.Contains(t) == true)
            {
                return;
            }

            damagedThings.Add(t);
            int damageAmount = explosion.GetDamageAmountAt(cell);
            if (damageAmount > 0)
            {
                DamageInfo dinfo = new DamageInfo(
                    def,
                    damageAmount,
                    explosion.GetArmorPenetrationAt(cell),
                    -1f,
                    explosion.instigator,
                    null,
                    explosion.weapon,
                    DamageInfo.SourceCategory.ThingOrUnknown,
                    explosion.intendedTarget);
                t.TakeDamage(dinfo);
            }

            if (t is Pawn pawn)
            {
                ApplyStatusDamage(explosion, pawn);
            }
        }

        private static void ApplyStatusDamage(Explosion explosion, Pawn pawn)
        {
            CompProperties_FlowerBellStatusOnHit props = CompFlowerBellStatusOnHit.PropsFor(explosion.projectile);
            Pawn caster = explosion.instigator as Pawn;
            if (props == null || props.damageDef == null || props.amount <= 0f || pawn == null || pawn.Dead || pawn.Destroyed || caster == null)
            {
                return;
            }

            if (props.requireHostileTarget && !GenHostility.HostileTo(caster, pawn))
            {
                return;
            }

            if (props.chance < 1f && !Rand.Chance(Mathf.Clamp01(props.chance)))
            {
                return;
            }

            float armorPenetration = props.armorPenetration >= 0f ? props.armorPenetration : props.damageDef.defaultArmorPenetration;
            DamageInfo dinfo = new DamageInfo(
                props.damageDef,
                props.amount,
                armorPenetration,
                -1f,
                caster);
            pawn.TakeDamage(dinfo);
        }
    }
}

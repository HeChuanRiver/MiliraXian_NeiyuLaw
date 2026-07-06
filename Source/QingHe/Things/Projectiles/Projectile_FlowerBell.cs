using RimWorld;
using UnityEngine;
using Verse;
using MiliraXian.Characters;

namespace MiliraXian.Characters.QingHe.Things.Projectiles
{
    public class Projectile_FlowerBell : ProjectileHomingCurveBase
    {
        private const float ExplosionRadius = 2f;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 impactPos = ExactPosition;
            Thing resolvedHitThing = ResolveImpactHitThing(hitThing, impactPos, map);

            if (map != null && !blockedByShield)
            {
                IntVec3 center = impactPos.ToIntVec3();
                if (!center.InBounds(map))
                {
                    center = Position;
                }

                GenExplosion.DoExplosion(
                    center,
                    map,
                    ExplosionRadius,
                    DamageDef,
                    launcher,
                    DamageAmount,
                    ArmorPenetration,
                    def.projectile.soundExplode,
                    equipmentDef,
                    def,
                    resolvedHitThing,
                    null,
                    0f,
                    1,
                    null,
                    null,
                    255,
                    applyDamageToExplosionCellsNeighbors: false,
                    null,
                    0f,
                    1,
                    chanceToStartFire: 0f,
                    damageFalloff: false,
                    ExactRotation.eulerAngles.y,
                    null,
                    null,
                    doVisualEffects: true,
                    propagationSpeed: 1f,
                    excludeRadius: 0f,
                    doSoundEffects: true);
            }

            base.Impact(null, blockedByShield);
        }
    }
}

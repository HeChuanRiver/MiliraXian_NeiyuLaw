using MiliraXian.Characters;
using MiliraXian.Characters.QingHe;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Projectiles
{
    public class Projectile_LunarMirrorRetaliation : Bullet
    {
        private const float ExplosionRadius = 1f;

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map map = Map;
            Vector3 impactPos = ExactPosition;

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
                    Mathf.RoundToInt(DamageAmount * MX_QHSkillUtility.GetSpecialAbilityEffectFactor(launcher as Pawn)),
                    ArmorPenetration,
                    null,
                    equipmentDef,
                    def,
                    hitThing,
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
                    doSoundEffects: false);
            }

            base.Impact(null, blockedByShield);
        }
    }
}

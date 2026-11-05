using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.SweepDamage
{
    /// <summary>
    /// Target filter and damage application of a <see cref="SweepDamageDef"/>. One instance per Def.
    /// </summary>
    public class SweepDamageWorker
    {
        public SweepDamageDef def;

        public virtual bool CanTarget(SweepDamageSource source, Thing thing)
        {
            Pawn caster = source.caster;
            if (thing == null || thing == caster || !thing.Spawned || thing.Destroyed)
            {
                return false;
            }
            if (thing is Pawn pawn)
            {
                if (!def.hitPawns || pawn.Dead)
                {
                    return false;
                }
            }
            else if (thing is not Building || !def.hitBuildings)
            {
                return false;
            }

            switch (def.targetFaction)
            {
                case SweepTargetFaction.Hostile:
                    return caster != null && GenHostility.HostileTo(caster, thing);
                case SweepTargetFaction.HostileAndNeutral:
                {
                    Faction own = caster?.Faction;
                    Faction other = thing.Faction;
                    if (own == null || other == null)
                    {
                        return true;
                    }
                    return other != own && other.RelationKindWith(own) != FactionRelationKind.Ally;
                }
                default:
                    return true;
            }
        }

        /// <summary>Called when the damage of a contact lands (after <see cref="SweepDamageDef.damageDelayTicks"/>).</summary>
        public virtual void ApplyHit(SweepDamageSource source, Thing target)
        {
            Vector3 dir = GenThing.TrueCenter(target) - source.center;
            float angle = dir.sqrMagnitude > 0.0001f ? dir.AngleFlat() : -1f;
            DamageInfo dinfo = new(
                def.damageDef ?? DamageDefOf.Cut,
                source.damage,
                source.armorPenetration,
                angle,
                source.caster,
                null,
                source.weaponDef);
            target.TakeDamage(dinfo);
        }
    }
}

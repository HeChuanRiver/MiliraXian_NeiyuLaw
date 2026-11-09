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
                if (!def.hitPawns || pawn.Dead
                    || (def.skipDowned && pawn.Downed && !NeiyuLawMod.QingheSweepHitsDowned))
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
                source.damageDef ?? def.damageDef ?? DamageDefOf.Cut,
                source.damage,
                source.armorPenetration,
                angle,
                source.caster,
                null,
                source.weaponDef);
            // A technique damage def names the cut in the wound and writes it to the combat log.
            DamageWorker.DamageResult result = Combat.TechniqueDamage.Apply(target, dinfo);
            AddPosture(source, target, result);
        }

        /// <summary>Share of a hit's posture that still lands when a shield or another effect absorbed its damage.</summary>
        public const float AbsorbedPostureShare = 0.25f;

        /// <summary>
        /// The sweep's posture on a struck pawn that is still standing: in full, or a quarter when
        /// the damage was absorbed (a shield, a guard skill) and reached no wound.
        /// </summary>
        public static void AddPosture(SweepDamageSource source, Thing target, DamageWorker.DamageResult result)
        {
            if (source.posture <= 0f || source.caster == null || target is not Pawn { Dead: false, Spawned: true } pawn)
            {
                return;
            }
            float amount = Absorbed(result) ? source.posture * AbsorbedPostureShare : source.posture;
            Combat.CombatPosture.Ensure(pawn)?.AddPressure(source.caster, amount);
        }

        /// <summary>
        /// The damage was stopped before any wound: an absorbed hit returns an empty result (vanilla
        /// <c>TakeDamage</c>), with no damage dealt, no hit parts and no armour deflection.
        /// </summary>
        public static bool Absorbed(DamageWorker.DamageResult result) =>
            result == null || (result.totalDamageDealt <= 0f && !result.deflected && result.parts.NullOrEmpty());
    }
}

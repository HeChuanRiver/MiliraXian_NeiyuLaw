using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class MX_QHUtility
    {
        public const string PawnKindDef_Qinghe = "MiliraXian_Qinghe";

        public static bool IsQinghe(Pawn pawn)
        {
            return pawn?.kindDef.defName == PawnKindDef_Qinghe;
        }

        public static bool HasRequiredWeapon(Pawn pawn, ThingDef requiredWeapon)
        {
            if (pawn == null || pawn.equipment == null || pawn.equipment.Primary == null)
            {
                return false;
            }

            if (requiredWeapon == null)
            {
                return true;
            }

            return pawn.equipment.Primary.def == requiredWeapon;
        }

        public static void TryApplyOrRefreshHediff(Pawn pawn, HediffDef hediffDef, float severity, int durationTicks)
        {
            if (pawn == null || hediffDef == null || pawn.health == null || pawn.health.hediffSet == null)
            {
                return;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(hediffDef, pawn);
                pawn.health.AddHediff(hediff);
            }

            if (hediff != null)
            {
                hediff.Severity = Mathf.Max(hediff.Severity, severity);
                HediffComp_Disappears disappears = hediff.TryGetComp<HediffComp_Disappears>();
                if (disappears != null && durationTicks > 0)
                {
                    disappears.SetDuration(durationTicks);
                }
            }
        }

        public static void TryKnockback(Pawn pawn, IntVec3 center, float distance)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead || !pawn.Spawned || pawn.MapHeld == null)
            {
                return;
            }

            Map map = pawn.MapHeld;
            IntVec3 start = pawn.Position;
            Vector3 direction = (start - center).ToVector3();
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = new Vector3(Rand.Range(-1f, 1f), 0f, Rand.Range(-1f, 1f));
            }
            direction.Normalize();

            IntVec3 best = start;
            int steps = Mathf.Max(1, Mathf.RoundToInt(distance));
            for (int i = 1; i <= steps; i++)
            {
                IntVec3 next = start + (direction * i).ToIntVec3();
                if (!ValidKnockbackCell(map, next, pawn))
                {
                    break;
                }
                best = next;
            }

            if (best != start)
            {
                pawn.Position = best;
                if (pawn.pather != null)
                {
                    pawn.pather.StopDead();
                }
                if (pawn.jobs != null)
                {
                    pawn.jobs.StopAll(false, true);
                }
            }
        }

        public static void ApplyBleed(Pawn pawn, float bleedDamage)
        {
            if (pawn == null || pawn.health == null || pawn.health.hediffSet == null || bleedDamage <= 0f)
            {
                return;
            }

            BodyPartRecord part = pawn.health.hediffSet.GetRandomNotMissingPart(DamageDefOf.Cut, BodyPartHeight.Undefined, BodyPartDepth.Outside);
            if (part == null)
            {
                return;
            }

            Hediff_Injury injury = HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, part) as Hediff_Injury;
            if (injury == null)
            {
                return;
            }

            injury.Severity = Mathf.Max(0.1f, bleedDamage);
            pawn.health.AddHediff(injury, part);
        }

        public static void HealInjuries(Pawn pawn, float totalHeal)
        {
            if (pawn == null || totalHeal <= 0f || pawn.health == null || pawn.health.hediffSet == null)
            {
                return;
            }

            float remain = totalHeal;
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0 && remain > 0f; i--)
            {
                Hediff_Injury injury = hediffs[i] as Hediff_Injury;
                if (injury == null || injury.Severity <= 0f)
                {
                    continue;
                }

                float heal = Mathf.Min(remain, injury.Severity);
                injury.Heal(heal);
                remain -= heal;
            }
        }

        public static void ExecuteHengZhiPulseByProps(Pawn caster, CompProperties_AbilityHengZhi props)
        {
            if (caster == null || props == null)
            {
                return;
            }

            Map map = caster.MapHeld;
            if (map == null)
            {
                return;
            }

            DamageDef firstDamageDef = props.damageDef ?? MX_QHDefOf.MX_Dehydrate ?? DamageDefOf.Blunt;
            List<Pawn> victims = RadialUtility.CollectHostilePawns(map, caster.Position, caster, props.radius);

            int hitCount = 0;
            for (int i = 0; i < victims.Count; i++)
            {
                Pawn victim = victims[i];

                DamageInfo firstHit = new DamageInfo(firstDamageDef, props.damageAmount, props.armorPenetration, -1f, caster);
                victim.TakeDamage(firstHit);

                if (props.bluntDamageAmount > 0f)
                {
                    DamageInfo secondHit = new DamageInfo(DamageDefOf.Blunt, props.bluntDamageAmount, props.bluntArmorPenetration, -1f, caster);
                    victim.TakeDamage(secondHit);
                }

                TryKnockback(victim, caster.Position, props.knockbackDistance);
                hitCount++;
            }

            if (hitCount > 0)
            {
                float gain = Mathf.Min(props.eleganceGainMax, props.eleganceGainPerTarget * hitCount);
                EleganceUtility.AddElegance(caster, gain);
            }
        }

        public static void ExecuteDuanHunPulseByProps(Pawn caster, CompProperties_AbilityDuanHun props, IntVec3 center)
        {
            if (caster == null || props == null)
            {
                return;
            }

            Map map = caster.MapHeld;
            if (map == null)
            {
                return;
            }

            DamageDef d = props.damageDef ?? DamageDefOf.Cut;
            List<Pawn> victims = RadialUtility.CollectHostilePawns(map, center, caster, props.radius);
            int affected = 0;

            for (int i = 0; i < victims.Count; i++)
            {
                Pawn victim = victims[i];
                victim.TakeDamage(new DamageInfo(d, props.damageAmount, props.armorPenetration, -1f, caster));

                if (props.stunDamageAmount > 0f)
                {
                    victim.TakeDamage(new DamageInfo(DamageDefOf.Stun, props.stunDamageAmount, 0f, -1f, caster));
                }

                ApplyBleed(victim, props.bleedDamageAmount);
                TryApplyOrRefreshHediff(victim, props.slowHediff, props.slowSeverity, props.slowDurationTicks);

                if (!victim.Dead && Rand.Value < Mathf.Clamp01(props.brainDestroyChance))
                {
                    BodyPartRecord brain = null;
                    foreach (BodyPartRecord part in victim.health.hediffSet.GetNotMissingParts())
                    {
                        if (part.def == DefDatabase<BodyPartDef>.GetNamedSilentFail("Brain"))
                        {
                            brain = part;
                            break;
                        }
                    }

                    if (brain != null)
                    {
                        victim.health.AddHediff(HediffDefOf.MissingBodyPart, brain);
                    }
                }

                affected++;
            }

            EleganceUtility.AddElegance(caster, props.eleganceGainOnCast + props.eleganceGainPerTarget * affected);
        }
        
        private static bool ValidKnockbackCell(Map map, IntVec3 cell, Pawn movingPawn)
        {
            if (!cell.IsValid || !cell.InBounds(map))
            {
                return false;
            }

            if (!cell.Walkable(map) || cell.Impassable(map) || cell.Fogged(map))
            {
                return false;
            }

            Building_Door door = cell.GetEdifice(map) as Building_Door;
            if (door != null && !door.Open)
            {
                return false;
            }

            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Pawn other = things[i] as Pawn;
                if (other != null && other != movingPawn && other.Spawned && !other.Dead)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

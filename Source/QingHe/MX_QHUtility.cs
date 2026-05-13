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
            if (pawn?.equipment?.Primary == null)
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
            if (pawn?.health?.hediffSet == null || hediffDef == null)
            {
                return;
            }

            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(hediffDef, pawn);
                pawn.health.AddHediff(hediff);
            }

            hediff.Severity = Mathf.Max(hediff.Severity, severity);
            var disappears = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappears != null && durationTicks > 0)
            {
                disappears.SetDuration(durationTicks);
            }
        }

        public static void TryKnockback(Pawn pawn, IntVec3 center, float distance)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead || !pawn.Spawned || pawn.MapHeld == null)
            {
                return;
            }

            var map = pawn.MapHeld;
            var start = pawn.Position;
            var direction = (start - center).ToVector3();
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = new Vector3(Rand.Range(-1f, 1f), 0f, Rand.Range(-1f, 1f));
            }
            direction.Normalize();

            var best = start;
            var steps = Mathf.Max(1, Mathf.RoundToInt(distance));
            for (var i = 1; i <= steps; i++)
            {
                var next = start + (direction * i).ToIntVec3();
                if (!ValidKnockbackCell(map, next, pawn))
                {
                    break;
                }
                best = next;
            }

            if (best == start)
            {
                return;
            }

            pawn.Position = best;
            pawn.pather?.StopDead();
            pawn.jobs?.StopAll(false, true);
        }

        public static void ApplyBleed(Pawn pawn, float bleedDamage)
        {
            if (pawn?.health?.hediffSet == null || bleedDamage <= 0f)
            {
                return;
            }

            var part = pawn.health.hediffSet.GetRandomNotMissingPart(DamageDefOf.Cut, BodyPartHeight.Undefined, BodyPartDepth.Outside);
            if (part == null)
            {
                return;
            }

            var injury = HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, part) as Hediff_Injury;
            if (injury == null)
            {
                return;
            }

            injury.Severity = Mathf.Max(0.1f, bleedDamage);
            pawn.health.AddHediff(injury, part);
        }

        public static void HealInjuries(Pawn pawn, float totalHeal)
        {
            if (pawn?.health?.hediffSet == null || totalHeal <= 0f)
            {
                return;
            }

            var remain = totalHeal;
            var hediffs = pawn.health.hediffSet.hediffs;
            for (var i = hediffs.Count - 1; i >= 0 && remain > 0f; i--)
            {
                var injury = hediffs[i] as Hediff_Injury;
                if (injury == null || injury.Severity <= 0f)
                {
                    continue;
                }

                var heal = Mathf.Min(remain, injury.Severity);
                injury.Heal(heal);
                remain -= heal;
            }
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

            if (cell.GetEdifice(map) is Building_Door door && !door.Open)
            {
                return false;
            }

            var things = cell.GetThingList(map);
            for (var i = 0; i < things.Count; i++)
            {
                var other = things[i] as Pawn;
                if (other != null && other != movingPawn && other.Spawned && !other.Dead)
                {
                    return false;
                }
            }

            return true;
        }
    }
}
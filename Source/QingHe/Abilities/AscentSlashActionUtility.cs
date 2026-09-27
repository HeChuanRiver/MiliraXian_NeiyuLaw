using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Abilities
{
    internal static class AscentSlashActionUtility
    {
        public static int CurrentTick => Find.TickManager?.TicksGame ?? 0;

        public static void AddInvulnerability(Pawn caster)
        {
            if (caster?.health == null || MX_QHDefOf.MX_QH_AscentSlashInvulnerable == null)
            {
                return;
            }

            Hediff hediff = caster.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_AscentSlashInvulnerable)
                ?? caster.health.AddHediff(MX_QHDefOf.MX_QH_AscentSlashInvulnerable);
            hediff?.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
        }

        public static void RemoveInvulnerability(Pawn caster)
        {
            if (caster?.health == null || MX_QHDefOf.MX_QH_AscentSlashInvulnerable == null)
            {
                return;
            }

            Hediff hediff = caster.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_AscentSlashInvulnerable);
            if (hediff != null)
            {
                caster.health.RemoveHediff(hediff);
            }
        }

        public static bool CanHit(Pawn caster, Thing thing)
        {
            if (thing == null || thing == caster || thing.Destroyed || !thing.Spawned)
            {
                return false;
            }

            if (thing is Pawn pawn)
            {
                return !pawn.Dead && GenHostility.HostileTo(caster, pawn);
            }

            return thing is Building && thing.HostileTo(caster);
        }

        public static IntVec3 FindNearestLandingCell(Map map, IntVec3 desired, Pawn caster, IntVec3 fallback)
        {
            if (map == null)
            {
                return fallback;
            }

            desired = ClampToMap(desired, map);
            if (ValidLandingCell(map, desired, caster))
            {
                return desired;
            }

            int count = GenRadial.NumCellsInRadius(3.9f);
            for (int i = 0; i < count; i++)
            {
                IntVec3 cell = desired + GenRadial.RadialPattern[i];
                if (ValidLandingCell(map, cell, caster))
                {
                    return cell;
                }
            }

            return fallback.IsValid && fallback.InBounds(map) ? fallback : caster.Position;
        }

        public static IntVec3 ClampToMap(IntVec3 cell, Map map)
        {
            if (map == null)
            {
                return cell;
            }

            return new IntVec3(
                Mathf.Clamp(cell.x, 0, map.Size.x - 1),
                0,
                Mathf.Clamp(cell.z, 0, map.Size.z - 1));
        }

        public static IntVec3 ComputeDirectionCell(IntVec3 origin, IntVec3 landing)
        {
            IntVec3 offset = landing - origin;
            offset.y = 0;
            return offset.x == 0 && offset.z == 0 ? landing + IntVec3.North : landing + offset;
        }

        public static Vector3 ComputeForward(IntVec3 source, IntVec3 target)
        {
            Vector3 forward = (target - source).ToVector3().Yto0();
            if (forward.sqrMagnitude < 0.001f)
            {
                return Vector3.zero;
            }

            return forward.normalized;
        }

        public static void BreakRoofAt(Map map, IntVec3 cell, bool allowThickRoof)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map))
            {
                return;
            }

            RoofDef roof = map.roofGrid.RoofAt(cell);
            if (roof == null || roof.isThickRoof && !allowThickRoof)
            {
                return;
            }

            roof.soundPunchThrough?.PlayOneShot(new TargetInfo(cell, map));
            if (roof.filthLeaving != null)
            {
                FilthMaker.TryMakeFilth(cell, map, roof.filthLeaving);
            }

            map.roofGrid.SetRoof(cell, null);
            FleckMaker.ThrowDustPuff(cell.ToVector3Shifted() + Gen.RandomHorizontalVector(0.6f), map, 2f);
        }

        private static bool ValidLandingCell(Map map, IntVec3 cell, Pawn caster)
        {
            if (!JumpUtility.ValidJumpTarget(caster, map, cell))
            {
                return false;
            }

            foreach (Thing thing in cell.GetThingList(map))
            {
                if (thing is Pawn pawn && pawn != caster && pawn.Spawned && !pawn.Dead)
                {
                    return false;
                }
            }

            return true;
        }
    }
}

using System.Collections.Generic;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Things.Weapons;
using MiliraXian.Characters.QingHe.Vfx;
using MiliraXian.Characters.Vfx;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Abilities
{
    internal readonly struct ASDashResult
    {
        public bool StartsSlash { get; }
        public Thing TrackedTarget { get; }
        public IntVec3 FirstImpactCell { get; }
        public IntVec3 TakeoffCell { get; }
        public IntVec3 DirectionCell { get; }

        public ASDashResult(
            bool startsSlash,
            Thing trackedTarget,
            IntVec3 firstImpactCell,
            IntVec3 takeoffCell,
            IntVec3 directionCell)
        {
            StartsSlash = startsSlash;
            TrackedTarget = trackedTarget;
            FirstImpactCell = firstImpactCell;
            TakeoffCell = takeoffCell;
            DirectionCell = directionCell;
        }
    }

    internal sealed class ASDash
    {
        private readonly CompProperties_AbilityAscentSlash props;

        private IntVec3 dashEndCell = IntVec3.Invalid;
        private IntVec3 lastSafeDashCell = IntVec3.Invalid;
        private Vector3 dashStartPos;
        private Vector3 dashEndPos;
        private Vector3 previousDashPos;
        private int startTick = -1;
        private int endTick = -1;

        public bool Active { get; private set; }

        public ASDash(CompProperties_AbilityAscentSlash props)
        {
            this.props = props;
        }

        public void Start(Pawn caster, IntVec3 destination)
        {
            if (caster == null || !caster.Spawned || caster.MapHeld == null || !destination.IsValid)
            {
                return;
            }

            Active = true;
            dashEndCell = destination;
            lastSafeDashCell = caster.Position;
            dashStartPos = caster.DrawPos;
            dashEndPos = destination.ToVector3Shifted();
            previousDashPos = dashStartPos;

            float speed = Mathf.Max(1f, props.dashSpeedCellsPerSecond);
            int travelTicks = Mathf.CeilToInt((dashEndPos - dashStartPos).Yto0().magnitude / speed * 60f);
            int durationTicks = Mathf.Max(props.dashDurationMinTicks, travelTicks);
            startTick = AscentSlashActionUtility.CurrentTick;
            endTick = startTick + Mathf.Max(1, durationTicks);

            AscentSlashActionUtility.AddInvulnerability(caster);
            caster.pather?.StopDead();
            caster.rotationTracker?.FaceCell(dashEndCell);
            caster.stances?.stunner?.StunFor(durationTicks + 2, caster, addBattleLog: false, showMote: false);
        }

        public bool Tick(Pawn caster, Map map, out ASDashResult result)
        {
            result = default;
            if (!Active)
            {
                return false;
            }

            int now = AscentSlashActionUtility.CurrentTick;
            int predictedTick = Mathf.Min(now + 1, endTick);
            Vector3 predictedPos = ComputeDrawPos(predictedTick);

            if (TryResolveCollision(caster, map, previousDashPos, predictedPos, out Thing hitThing, out bool startsSlash, out IntVec3 impactCell))
            {
                result = Finish(caster, map, lastSafeDashCell, impactCell, hitThing, startsSlash);
                return true;
            }

            previousDashPos = predictedPos;
            TryAddAfterimage(caster, map, predictedPos, now);
            if (predictedTick < endTick)
            {
                return false;
            }

            IntVec3 landingCell = AscentSlashActionUtility.FindNearestLandingCell(map, dashEndCell, caster, lastSafeDashCell);
            result = Finish(caster, map, landingCell, dashEndCell, null, startsSlash: false);
            return true;
        }

        public void Cancel(Pawn caster)
        {
            if (!Active)
            {
                return;
            }

            Complete(caster);
        }

        public void ExposeData()
        {
            bool active = Active;
            Scribe_Values.Look(ref active, "mx_qh_asDash_active", false);
            Scribe_Values.Look(ref dashEndCell, "mx_qh_asDash_endCell", IntVec3.Invalid);
            Scribe_Values.Look(ref lastSafeDashCell, "mx_qh_asDash_lastSafeCell", IntVec3.Invalid);
            Scribe_Values.Look(ref dashStartPos, "mx_qh_asDash_startPos", Vector3.zero);
            Scribe_Values.Look(ref previousDashPos, "mx_qh_asDash_previousPos", Vector3.zero);
            Scribe_Values.Look(ref startTick, "mx_qh_asDash_startTick", -1);
            Scribe_Values.Look(ref endTick, "mx_qh_asDash_endTick", -1);
            Active = active;
        }

        public void RestoreAfterLoad()
        {
            if (Active)
            {
                dashEndPos = dashEndCell.ToVector3Shifted();
            }
        }

        public bool TryApplyDrawPos(ref Vector3 drawPos)
        {
            if (!Active)
            {
                return false;
            }

            int sampledTick = Mathf.Min(AscentSlashActionUtility.CurrentTick + 1, endTick);
            drawPos = ComputeDrawPos(sampledTick);
            return true;
        }

        private ASDashResult Finish(
            Pawn caster,
            Map map,
            IntVec3 desiredLandingCell,
            IntVec3 impactCell,
            Thing directHitThing,
            bool startsSlash)
        {
            IntVec3 landingCell = AscentSlashActionUtility.FindNearestLandingCell(map, desiredLandingCell, caster, caster.Position);
            if (landingCell.IsValid && landingCell.InBounds(map) && landingCell != caster.Position)
            {
                caster.Position = landingCell;
                caster.Notify_Teleported(endCurrentJob: false);
            }

            ResolveImpact(caster, map, impactCell, directHitThing);
            if (startsSlash)
            {
                RoofDef roof = impactCell.InBounds(map) ? map.roofGrid.RoofAt(impactCell) : null;
                startsSlash = roof?.isThickRoof != true;
            }

            ASDashResult result = new(
                startsSlash,
                startsSlash ? directHitThing : null,
                impactCell,
                caster.Position,
                AscentSlashActionUtility.ComputeDirectionCell(caster.Position, impactCell));
            Complete(caster);
            return result;
        }

        private void Complete(Pawn caster)
        {
            AscentSlashActionUtility.RemoveInvulnerability(caster);
            Active = false;
            startTick = -1;
            endTick = -1;
        }

        private bool TryResolveCollision(
            Pawn caster,
            Map map,
            Vector3 from,
            Vector3 to,
            out Thing hitThing,
            out bool startsSlash,
            out IntVec3 impactCell)
        {
            hitThing = null;
            startsSlash = false;
            impactCell = IntVec3.Invalid;
            IntVec3 fromCell = from.ToIntVec3();
            IntVec3 toCell = to.ToIntVec3();

            foreach (IntVec3 cell in GenSight.BresenhamCellsBetween(fromCell, toCell))
            {
                if (cell == dashStartPos.ToIntVec3() || cell == fromCell && fromCell == previousDashPos.ToIntVec3())
                {
                    continue;
                }

                if (!cell.InBounds(map))
                {
                    impactCell = lastSafeDashCell.IsValid ? lastSafeDashCell : caster.Position;
                    return true;
                }

                if (!IsPassable(caster, map, cell))
                {
                    hitThing = FirstBlockingThingAt(map, cell);
                    startsSlash = AscentSlashActionUtility.CanHit(caster, hitThing);
                    impactCell = hitThing?.Position ?? cell;
                    return true;
                }

                Thing hostile = FindNearestTarget(caster, map, cell, props.dashCollisionRadius);
                if (hostile != null)
                {
                    hitThing = hostile;
                    startsSlash = true;
                    impactCell = hostile.Position;
                    return true;
                }

                lastSafeDashCell = cell;
            }

            return false;
        }

        private static Thing FindNearestTarget(Pawn caster, Map map, IntVec3 cell, float collisionRadius)
        {
            Thing nearest = null;
            float nearestDistanceSquared = float.MaxValue;
            Vector3 center = cell.ToVector3Shifted();
            foreach (IntVec3 candidateCell in GenRadial.RadialCellsAround(cell, Mathf.Max(0f, collisionRadius), true))
            {
                if (!candidateCell.InBounds(map))
                {
                    continue;
                }

                foreach (Thing candidate in candidateCell.GetThingList(map))
                {
                    if (!AscentSlashActionUtility.CanHit(caster, candidate)
                        || !GenSight.LineOfSight(cell, candidate.Position, map, true))
                    {
                        continue;
                    }

                    float distanceSquared = (candidate.DrawPos - center).Yto0().sqrMagnitude;
                    if (distanceSquared < nearestDistanceSquared)
                    {
                        nearest = candidate;
                        nearestDistanceSquared = distanceSquared;
                    }
                }
            }

            return nearest;
        }

        private static Thing FirstBlockingThingAt(Map map, IntVec3 cell)
        {
            Building edifice = cell.GetEdifice(map);
            if (edifice != null)
            {
                return edifice;
            }

            foreach (Thing thing in cell.GetThingList(map))
            {
                if (thing?.def.passability == Traversability.Impassable)
                {
                    return thing;
                }
            }

            return null;
        }

        private static bool IsPassable(Pawn caster, Map map, IntVec3 cell)
        {
            if (!cell.WalkableBy(map, caster) || cell.Impassable(map))
            {
                return false;
            }

            return cell.GetEdifice(map) is not Building_Door door || door.Open;
        }

        private void ResolveImpact(Pawn caster, Map map, IntVec3 center, Thing directHitThing)
        {
            if (!center.IsValid || !center.InBounds(map))
            {
                center = caster.Position;
            }

            MX_QHGraphicsUtility.Fx(map, center, props.impactEffecter, 0.8f);
            MX_QHGraphicsUtility.Fleck(map, center, props.impactFleck, 0.85f);
            props.castSound?.PlayOneShot(new TargetInfo(center, map));

            float specialFactor = MX_QHSkillUtility.GetSpecialAbilityEffectFactor(caster);
            List<Thing> victims = new();
            HashSet<Thing> unique = new();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, props.dashImpactRadius, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                foreach (Thing thing in cell.GetThingList(map))
                {
                    if (AscentSlashActionUtility.CanHit(caster, thing) && unique.Add(thing))
                    {
                        victims.Add(thing);
                    }
                }
            }

            foreach (Thing victim in victims)
            {
                ApplyImpactDamage(caster, victim, specialFactor);
            }

            if (directHitThing != null
                && !directHitThing.Destroyed
                && directHitThing.Spawned
                && directHitThing.MapHeld == map
                && unique.Add(directHitThing))
            {
                ApplyImpactDamage(caster, directHitThing, specialFactor);
            }
        }

        private void ApplyImpactDamage(Pawn caster, Thing target, float specialFactor)
        {
            float damage = props.dashDamageAmount * specialFactor;
            if (target is Building)
            {
                damage *= props.buildingDamageMultiplier;
            }

            QingheSwordCombatUtility.ApplySlash(caster, target, damage, props.armorPenetration, empowered: false);
        }

        private static void TryAddAfterimage(Pawn caster, Map map, Vector3 drawPos, int now)
        {
            if (now % 2 != 0)
            {
                return;
            }

            map.GetComponent<MapComponent_PawnAfterimages>()?.AddAfterimage(
                caster,
                drawPos,
                caster.Rotation,
                24,
                0.42f,
                MX_QHRenderStatics.AfterimageTint);
        }

        private Vector3 ComputeDrawPos(int tick)
        {
            float progress = endTick > startTick
                ? Mathf.Clamp01((tick - startTick) / (float)(endTick - startTick))
                : 1f;
            float easedProgress = 1f - Mathf.Pow(1f - progress, 2.6f);
            Vector3 drawPos = Vector3.Lerp(dashStartPos, dashEndPos, easedProgress);
            drawPos.y = dashStartPos.y;
            return drawPos;
        }
    }
}

using RimWorld;
using UnityEngine;
using Verse;
using System.Collections.Generic;

namespace MiliraXian.Characters
{
    public abstract class ProjectileHomingCurveBase : Bullet, IProjectileHomingCurveHost
    {
        private static readonly CompProperties_ProjectileHomingCurve FallbackHomingSettings = new CompProperties_ProjectileHomingCurve();
        private static readonly List<IntVec3> ManualCheckedCells = new List<IntVec3>();

        private Vector3 visualMoveDirection = Vector3.forward;
        private bool hasVisualMoveDirection;
        private bool hasSegmentImpactPosition;
        private Vector3 segmentImpactPosition;
        private float closestIntendedDistanceSqr = float.MaxValue;
        private bool manualHomingActive;
        private Vector3 manualGroundPosition;
        private Vector3 manualMoveDirection = Vector3.forward;
        private int manualTicksLeft;

        public override Vector3 ExactPosition
        {
            get
            {
                if (manualHomingActive)
                {
                    return manualGroundPosition.Yto0() + Vector3.up * def.Altitude;
                }

                return base.ExactPosition;
            }
        }

        public override void Launch(
            Thing launcher,
            Vector3 origin,
            LocalTargetInfo usedTarget,
            LocalTargetInfo intendedTarget,
            ProjectileHitFlags hitFlags,
            bool preventFriendlyFire = false,
            Thing equipment = null,
            ThingDef targetCoverDef = null)
        {
            CompProperties_ProjectileHomingCurve settings = GetHomingSettings();
            if (settings.forceUsedTargetToIntended && intendedTarget.IsValid)
            {
                usedTarget = intendedTarget;
            }

            hitFlags |= ProjectileHitFlags.IntendedTarget;
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            hasSegmentImpactPosition = false;
            closestIntendedDistanceSqr = float.MaxValue;
            manualHomingActive = false;
            manualTicksLeft = 0;
            GetComp<CompProjectileHomingCurve>()?.NotifyLaunch(Find.TickManager.TicksGame);
            InitializeVisualMoveDirection();
        }

        protected override void TickInterval(int delta)
        {
            if (manualHomingActive)
            {
                TickManualHoming(delta);
                return;
            }

            Vector3 before = ExactPosition;
            base.TickInterval(delta);
            if (Destroyed)
            {
                return;
            }

            Vector3 after = ExactPosition;
            UpdateVisualMoveDirection(after - before);
            TryImpactIntendedTargetBySegment(before, after);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref manualHomingActive, "manualHomingActive", false);
            Scribe_Values.Look(ref manualGroundPosition, "manualGroundPosition", default(Vector3));
            Scribe_Values.Look(ref manualMoveDirection, "manualMoveDirection", Vector3.forward);
            Scribe_Values.Look(ref manualTicksLeft, "manualTicksLeft", 0);
        }

        public bool AllowHomingUpdate => !landed;

        public LocalTargetInfo HomingIntendedTarget => intendedTarget;

        public Vector3 HomingExactPosition => ExactPosition;

        public Vector3 HomingCurrentDirection => GetCurrentDirection();

        public int HomingTicksToImpact => ticksToImpact;

        public int StartingTicksToImpactCeil()
        {
            return Mathf.CeilToInt(StartingTicksToImpact);
        }

        public void BeginHoming(int minTicksToImpact)
        {
            CompProperties_ProjectileHomingCurve settings = GetHomingSettings();
            Vector3 currentPos = ExactPosition;
            manualHomingActive = true;
            manualGroundPosition = currentPos.Yto0();
            manualMoveDirection = GetHomingStartDirection(currentPos);
            manualTicksLeft = Mathf.Max(1, minTicksToImpact) + Mathf.Max(0, settings.extraHomingTicks);
            origin = currentPos;
            destination = currentPos + manualMoveDirection * Mathf.Max(def.projectile.SpeedTilesPerTick, 0.001f) * manualTicksLeft;
            lifetime = manualTicksLeft;
            ticksToImpact = manualTicksLeft;
        }

        public void LerpHomingDestination(Vector3 desired, float lerp)
        {
            Vector3 currentPos = ExactPosition;
            Vector3 steeringDirection = ResolveSteeringDirection(currentPos, desired, lerp);

            float speedPerTick = Mathf.Max(def.projectile.SpeedTilesPerTick, 0.001f);
            int remainingTicks = Mathf.Max(1, manualHomingActive ? manualTicksLeft : lifetime);
            manualMoveDirection = steeringDirection;
            ticksToImpact = remainingTicks;
            float remainingDistance = speedPerTick * remainingTicks;

            origin = currentPos;
            destination = currentPos + steeringDirection * remainingDistance;
            UpdateVisualMoveDirection(steeringDirection);
        }

        public override Quaternion ExactRotation
        {
            get
            {
                return Quaternion.LookRotation(GetCurrentDirection());
            }
        }

        protected Thing ResolveImpactHitThing(Thing hitThing, Vector3 impactPos, Map map)
        {
            if (hasSegmentImpactPosition)
            {
                impactPos = segmentImpactPosition;
                hasSegmentImpactPosition = false;
            }

            if (hitThing == null)
            {
                return null;
            }

            Thing intendedThing = intendedTarget.Thing;
            if (intendedThing == null || hitThing != intendedThing)
            {
                return hitThing;
            }

            if (map == null || !intendedThing.Spawned || intendedThing.Destroyed || intendedThing.Map != map)
            {
                return null;
            }

            CompProperties_ProjectileHomingCurve settings = GetHomingSettings();
            float allowedDistance = Mathf.Max(settings.intendedHitMaxDistance, EstimateTargetRadius(intendedThing) + settings.segmentHitRadiusMargin);
            float maxDistSqr = allowedDistance * allowedDistance;
            float distSqr = (impactPos - intendedThing.DrawPos).Yto0().sqrMagnitude;
            if (distSqr > maxDistSqr)
            {
                return null;
            }

            return hitThing;
        }

        protected virtual Vector3 ResolveSteeringDirection(Vector3 currentPos, Vector3 desired, float lerp)
        {
            Vector3 currentDirection = manualHomingActive ? manualMoveDirection.Yto0() : Vector3.zero;
            if (currentDirection.sqrMagnitude < 0.0001f)
            {
                currentDirection = (destination - currentPos).Yto0();
            }

            if (currentDirection.sqrMagnitude < 0.0001f)
            {
                currentDirection = (destination - origin).Yto0();
            }

            if (currentDirection.sqrMagnitude < 0.0001f && hasVisualMoveDirection)
            {
                currentDirection = visualMoveDirection;
            }

            if (currentDirection.sqrMagnitude < 0.0001f)
            {
                currentDirection = Vector3.forward;
            }

            Vector3 desiredDirection = (desired - currentPos).Yto0();
            if (desiredDirection.sqrMagnitude < 0.0001f)
            {
                desiredDirection = currentDirection;
            }

            currentDirection = currentDirection.normalized;
            desiredDirection = desiredDirection.normalized;

            Vector3 blendedDirection = Vector3.Slerp(currentDirection, desiredDirection, Mathf.Clamp01(lerp));
            if (blendedDirection.sqrMagnitude < 0.0001f)
            {
                blendedDirection = desiredDirection;
            }

            if (blendedDirection.sqrMagnitude < 0.0001f)
            {
                blendedDirection = Vector3.forward;
            }

            return blendedDirection.normalized;
        }

        private Vector3 GetHomingStartDirection(Vector3 currentPos)
        {
            if (hasVisualMoveDirection && visualMoveDirection.sqrMagnitude > 0.0001f)
            {
                return visualMoveDirection.normalized;
            }

            Vector3 direction = (destination - currentPos).Yto0();
            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }

            direction = (destination - origin).Yto0();
            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }

            return Vector3.forward;
        }

        private Vector3 GetCurrentDirection()
        {
            if (manualHomingActive && manualMoveDirection.sqrMagnitude > 0.0001f)
            {
                return manualMoveDirection.normalized;
            }

            Vector3 direction = (destination - ExactPosition).Yto0();
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = (destination - origin).Yto0();
            }

            if (direction.sqrMagnitude < 0.0001f && hasVisualMoveDirection)
            {
                direction = visualMoveDirection;
            }

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = Vector3.forward;
            }

            return direction.normalized;
        }

        private void TickManualHoming(int delta)
        {
            foreach (ThingComp comp in AllComps)
            {
                comp.CompTickInterval(delta);
            }

            lifetime -= delta;
            manualTicksLeft -= delta;
            ticksToImpact = Mathf.Max(0, manualTicksLeft);
            if (landed)
            {
                return;
            }

            Vector3 before = ExactPosition;
            Vector3 direction = manualMoveDirection.Yto0();
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = GetCurrentDirection();
            }

            direction = direction.normalized;
            float distance = Mathf.Max(0.001f, def.projectile.SpeedTilesPerTick) * Mathf.Max(1, delta);
            manualGroundPosition = (manualGroundPosition + direction * distance).Yto0();
            Vector3 after = ExactPosition;
            UpdateVisualMoveDirection(after - before);

            Map map = Map;
            if (map == null || !after.InBounds(map))
            {
                Position = after.ToIntVec3();
                Destroy(DestroyMode.Vanish);
                return;
            }

            Position = after.ToIntVec3();
            if (CheckManualFreeInterceptBetween(before, after))
            {
                return;
            }

            TryImpactIntendedTargetBySegment(before, after);
            if (Destroyed)
            {
                return;
            }

            if (manualTicksLeft <= 0)
            {
                if (Position.InBounds(map))
                {
                    ImpactSomething();
                }
                else
                {
                    Destroy(DestroyMode.Vanish);
                }
            }
        }

        private bool CheckManualFreeInterceptBetween(Vector3 lastExactPos, Vector3 newExactPos)
        {
            if (lastExactPos == newExactPos)
            {
                return false;
            }

            Map map = Map;
            if (map == null)
            {
                return false;
            }

            List<Thing> interceptors = map.listerThings.ThingsInGroup(ThingRequestGroup.ProjectileInterceptor);
            for (int i = 0; i < interceptors.Count; i++)
            {
                CompProjectileInterceptor interceptor = interceptors[i].TryGetComp<CompProjectileInterceptor>();
                if (interceptor != null && interceptor.CheckIntercept(this, lastExactPos, newExactPos))
                {
                    Impact(null, true);
                    return true;
                }
            }

            IntVec3 fromCell = lastExactPos.ToIntVec3();
            IntVec3 toCell = newExactPos.ToIntVec3();
            if (toCell == fromCell || !fromCell.InBounds(map) || !toCell.InBounds(map))
            {
                return false;
            }

            if (toCell.AdjacentToCardinal(fromCell))
            {
                return CheckManualFreeIntercept(toCell);
            }

            if (VerbUtility.InterceptChanceFactorFromDistance(origin, toCell) <= 0f)
            {
                return false;
            }

            Vector3 current = lastExactPos;
            Vector3 move = newExactPos - lastExactPos;
            Vector3 step = move.normalized * 0.2f;
            int maxSteps = (int)(move.MagnitudeHorizontal() / 0.2f);
            ManualCheckedCells.Clear();
            int steps = 0;
            while (true)
            {
                current += step;
                IntVec3 cell = current.ToIntVec3();
                if (!ManualCheckedCells.Contains(cell))
                {
                    if (CheckManualFreeIntercept(cell))
                    {
                        return true;
                    }

                    ManualCheckedCells.Add(cell);
                }

                steps++;
                if (steps > maxSteps || cell == toCell)
                {
                    return false;
                }
            }
        }

        private bool CheckManualFreeIntercept(IntVec3 cell)
        {
            Map map = Map;
            if (map == null || !cell.InBounds(map))
            {
                return false;
            }

            List<Thing> thingList = cell.GetThingList(map);
            for (int i = 0; i < thingList.Count; i++)
            {
                Thing thing = thingList[i];
                if (!CanHit(thing))
                {
                    continue;
                }

                Pawn pawn = thing as Pawn;
                if (thing.def.Fillage == FillCategory.Full)
                {
                    Building_Door door = thing as Building_Door;
                    if (door == null || !door.Open)
                    {
                        Position = cell;
                        Impact(thing, false);
                        return true;
                    }
                }

                float chance = 0f;
                if (pawn != null)
                {
                    chance = 0.4f * Mathf.Clamp(pawn.BodySize, 0.1f, 2f);
                    if (pawn.GetPosture() != PawnPosture.Standing)
                    {
                        chance *= 0.1f;
                    }

                    if (launcher != null && pawn.Faction != null && launcher.Faction != null && !pawn.Faction.HostileTo(launcher.Faction))
                    {
                        if (preventFriendlyFire)
                        {
                            chance = 0f;
                        }
                        else
                        {
                            chance *= Find.Storyteller.difficulty.friendlyFireChanceFactor;
                        }
                    }
                }
                else if (thing.def.fillPercent > 0.2f)
                {
                    chance = DestinationCell.AdjacentTo8Way(cell)
                        ? thing.def.fillPercent
                        : thing.def.fillPercent * 0.15f;
                }

                chance *= VerbUtility.InterceptChanceFactorFromDistance(origin, cell);
                if (chance > 0.00001f && Rand.Chance(chance))
                {
                    Position = cell;
                    Impact(thing, false);
                    return true;
                }
            }

            return false;
        }

        private void InitializeVisualMoveDirection()
        {
            Vector3 direction = (destination - origin).Yto0();
            if (direction.sqrMagnitude > 0.0001f)
            {
                visualMoveDirection = direction.normalized;
                hasVisualMoveDirection = true;
                return;
            }

            visualMoveDirection = Vector3.forward;
            hasVisualMoveDirection = false;
        }

        private void UpdateVisualMoveDirection(Vector3 moveDelta)
        {
            Vector3 horizontalMove = moveDelta.Yto0();
            if (horizontalMove.sqrMagnitude > 0.0001f)
            {
                visualMoveDirection = horizontalMove.normalized;
                hasVisualMoveDirection = true;
            }
        }

        private void TryImpactIntendedTargetBySegment(Vector3 fromPos, Vector3 toPos)
        {
            CompProperties_ProjectileHomingCurve settings = GetHomingSettings();
            if (!settings.enableIntendedTargetSegmentHitCheck || landed)
            {
                return;
            }

            Thing intendedThing = intendedTarget.Thing;
            Map map = Map;
            if (intendedThing == null || map == null || intendedThing.Destroyed || !intendedThing.Spawned || intendedThing.Map != map)
            {
                return;
            }

            if (!CanHit(intendedThing))
            {
                return;
            }

            float hitRadius = EstimateTargetRadius(intendedThing) + Mathf.Max(0f, settings.segmentHitRadiusMargin);
            if (hitRadius < 0.05f)
            {
                return;
            }

            float hitRadiusSqr = hitRadius * hitRadius;
            Vector3 targetPos = intendedThing.DrawPos;
            float distSqr = DistancePointToSegmentSqr(targetPos, fromPos, toPos);
            closestIntendedDistanceSqr = Mathf.Min(closestIntendedDistanceSqr, distSqr);
            if (distSqr <= hitRadiusSqr)
            {
                segmentImpactPosition = ClosestPointOnSegment(targetPos, fromPos, toPos);
                hasSegmentImpactPosition = true;
                Position = segmentImpactPosition.ToIntVec3();
                Impact(intendedThing, false);
                return;
            }

            if (!TryImpactIntendedTargetByOvershoot(intendedThing, targetPos, fromPos, toPos, hitRadius, settings))
            {
                return;
            }
        }

        private bool TryImpactIntendedTargetByOvershoot(Thing intendedThing, Vector3 targetPos, Vector3 fromPos, Vector3 toPos, float hitRadius, CompProperties_ProjectileHomingCurve settings)
        {
            if (!settings.enableIntendedTargetOvershootFuse || closestIntendedDistanceSqr == float.MaxValue)
            {
                return false;
            }

            Vector3 moveDirection = (toPos - fromPos).Yto0();
            Vector3 toTargetAfter = (targetPos - toPos).Yto0();
            if (moveDirection.sqrMagnitude < 1E-06f || toTargetAfter.sqrMagnitude < 1E-06f)
            {
                return false;
            }

            if (Vector3.Dot(moveDirection, toTargetAfter) > 0f)
            {
                return false;
            }

            float overshootRadius = hitRadius + Mathf.Max(0f, settings.overshootFuseExtraRadius);
            float overshootRadiusSqr = overshootRadius * overshootRadius;
            if (closestIntendedDistanceSqr > overshootRadiusSqr)
            {
                return false;
            }

            segmentImpactPosition = ClosestPointOnSegment(targetPos, fromPos, toPos);
            hasSegmentImpactPosition = true;
            Position = segmentImpactPosition.ToIntVec3();
            Impact(intendedThing, false);
            return true;
        }

        private CompProperties_ProjectileHomingCurve GetHomingSettings()
        {
            return GetComp<CompProjectileHomingCurve>()?.Settings ?? FallbackHomingSettings;
        }

        private static float EstimateTargetRadius(Thing thing)
        {
            if (thing == null)
            {
                return 0f;
            }

            Pawn pawn = thing as Pawn;
            if (pawn != null)
            {
                float bodySize = Mathf.Clamp(pawn.BodySize, 0.1f, 3.5f);
                return 0.16f + bodySize * 0.20f;
            }

            IntVec2 size = thing.def != null ? thing.def.size : IntVec2.One;
            float halfX = Mathf.Max(0.5f, size.x * 0.5f);
            float halfZ = Mathf.Max(0.5f, size.z * 0.5f);
            return Mathf.Sqrt(halfX * halfX + halfZ * halfZ) * 0.70f;
        }

        private static float DistancePointToSegmentSqr(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ap = (point - a).Yto0();
            Vector3 ab = (b - a).Yto0();
            float abSqr = ab.sqrMagnitude;
            if (abSqr < 1E-06f)
            {
                return ap.sqrMagnitude;
            }

            float t = Mathf.Clamp01(Vector3.Dot(ap, ab) / abSqr);
            Vector3 closest = a + ab * t;
            return (point - closest).Yto0().sqrMagnitude;
        }

        private static Vector3 ClosestPointOnSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = (b - a).Yto0();
            float abSqr = ab.sqrMagnitude;
            if (abSqr < 1E-06f)
            {
                return a;
            }

            float t = Mathf.Clamp01(Vector3.Dot((point - a).Yto0(), ab) / abSqr);
            Vector3 closest = a + ab * t;
            closest.y = Mathf.Max(a.y, b.y);
            return closest;
        }
    }
}

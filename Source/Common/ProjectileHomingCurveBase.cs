using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public abstract class ProjectileHomingCurveBase : Bullet, IProjectileHomingCurveHost
    {
        private static readonly CompProperties_ProjectileHomingCurve FallbackHomingSettings = new CompProperties_ProjectileHomingCurve();

        private Vector3 visualMoveDirection = Vector3.forward;
        private bool hasVisualMoveDirection;
        private bool hasSegmentImpactPosition;
        private Vector3 segmentImpactPosition;
        private float closestIntendedDistanceSqr = float.MaxValue;

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
            GetComp<CompProjectileHomingCurve>()?.NotifyLaunch(Find.TickManager.TicksGame);
            InitializeVisualMoveDirection();
        }

        protected override void TickInterval(int delta)
        {
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
            origin = ExactPosition;
            lifetime = Mathf.Max(1, minTicksToImpact) + Mathf.Max(0, settings.extraHomingTicks);
            ticksToImpact = lifetime;
        }

        public void LerpHomingDestination(Vector3 desired, float lerp)
        {
            Vector3 currentPos = ExactPosition;
            Vector3 steeringDirection = ResolveSteeringDirection(currentPos, desired, lerp);

            float speedPerTick = Mathf.Max(def.projectile.SpeedTilesPerTick, 0.001f);
            int remainingTicks = Mathf.Max(1, lifetime);
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
            Vector3 currentDirection = (destination - currentPos).Yto0();
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

        private Vector3 GetCurrentDirection()
        {
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

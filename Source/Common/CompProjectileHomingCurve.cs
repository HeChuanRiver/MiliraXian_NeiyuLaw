using UnityEngine;
using Verse;
using System.Collections.Generic;
using RimWorld;

namespace MiliraXian.Characters
{
    public interface IProjectileHomingCurveHost
    {
        bool AllowHomingUpdate { get; }
        LocalTargetInfo HomingIntendedTarget { get; }
        Vector3 HomingExactPosition { get; }
        Vector3 HomingCurrentDirection { get; }
        int HomingTicksToImpact { get; }
        int StartingTicksToImpactCeil();
        void BeginHoming(int minTicksToImpact);
        void LerpHomingDestination(Vector3 desired, float lerp);
    }

    public class CompProperties_ProjectileHomingCurve : CompProperties
    {
        public int homingStartDelayTicks = 4;
        public int minHomingTicksToImpact = 9;
        public float homingLifetimeFactor = 1.2f;
        public bool forceUsedTargetToIntended = true;
        public int extraHomingTicks = 2;
        public float intendedHitMaxDistance = 0.55f;
        public bool enableIntendedTargetSegmentHitCheck = true;
        public float segmentHitRadiusMargin = 0.08f;
        public bool enableIntendedTargetOvershootFuse = true;
        public float overshootFuseExtraRadius = 0.22f;
        public float homingTurnLerp = 0.14f;
        public float terminalSnapDistance = 3.0f;
        public float terminalSnapTurnLerp = 0.92f;
        public float curveAmplitudeMin = 0.12f;
        public float curveAmplitudeMax = 0.42f;
        public float curveFrequency = 0.22f;
        public float curveGuideDistanceFactor = 0.35f;
        public float curveGuideDistanceMin = 3.5f;
        public float curveGuideDistanceMax = 7.5f;
        public float terminalLockDistance = 1.05f;
        public float curveWobbleFadeDistance = 4.5f;
        public bool enableTurnStopWhenLikelyHit = true;
        public float turnStopMinTargetRadius = 0.42f;
        public float turnStopMargin = 0.08f;
        public int turnStopPredictTicks = 3;
        public bool enableRetargetOnTargetLost = true;
        public int retargetMaxCount = 2;
        public int retargetRetryIntervalTicks = 8;
        public float retargetSearchRadius = 10f;
        public float retargetPawnWeight = 3.5f;
        public float retargetNonPawnWeight = 1.0f;

        public CompProperties_ProjectileHomingCurve()
        {
            compClass = typeof(CompProjectileHomingCurve);
        }
    }

    public class CompProjectileHomingCurve : ThingComp
    {
        private int homingStartTick;
        private bool homingStarted;
        private float curveSeed;
        private float curveSign = 1f;
        private float curveAmplitude;
        private bool launchInitialized;
        private int retargetCount;
        private int nextRetargetTryTick;

        private CompProperties_ProjectileHomingCurve Props => (CompProperties_ProjectileHomingCurve)props;
        public CompProperties_ProjectileHomingCurve Settings => Props;
        private Projectile ProjectileParent => parent as Projectile;
        private IProjectileHomingCurveHost Host => parent as IProjectileHomingCurveHost;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref homingStartTick, "homingStartTick", 0);
            Scribe_Values.Look(ref homingStarted, "homingStarted", false);
            Scribe_Values.Look(ref curveSeed, "curveSeed", 0f);
            Scribe_Values.Look(ref curveSign, "curveSign", 1f);
            Scribe_Values.Look(ref curveAmplitude, "curveAmplitude", 0f);
            Scribe_Values.Look(ref launchInitialized, "launchInitialized", false);
            Scribe_Values.Look(ref retargetCount, "retargetCount", 0);
            Scribe_Values.Look(ref nextRetargetTryTick, "nextRetargetTryTick", 0);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                ResetState();
            }
        }

        public void NotifyLaunch(int currentTick)
        {
            if (Props == null)
            {
                ResetState();
                return;
            }

            homingStartTick = currentTick + Mathf.Max(0, Props.homingStartDelayTicks);
            homingStarted = false;
            curveSeed = Rand.Range(0f, 999f);
            curveSign = Rand.Chance(0.5f) ? -1f : 1f;
            float minAmp = Props.curveAmplitudeMin;
            float maxAmp = Props.curveAmplitudeMax;
            if (maxAmp < minAmp)
            {
                maxAmp = minAmp;
            }

            curveAmplitude = Rand.Range(minAmp, maxAmp);
            launchInitialized = true;
            retargetCount = 0;
            nextRetargetTryTick = 0;
        }

        public override void CompTick()
        {
            Projectile projectile = ProjectileParent;
            IProjectileHomingCurveHost host = Host;
            if (Props == null || projectile == null || host == null || !parent.Spawned || parent.Map == null)
            {
                return;
            }

            if (!launchInitialized)
            {
                if (projectile.Launcher == null)
                {
                    return;
                }

                NotifyLaunch(Find.TickManager.TicksGame);
            }

            int currentTick = Find.TickManager.TicksGame;
            if (host.AllowHomingUpdate)
            {
                TryRetargetWhenTargetLost(projectile, host, currentTick);
            }

            if (!host.AllowHomingUpdate || !host.HomingIntendedTarget.IsValid)
            {
                return;
            }

            if (ShouldStartHomingNow(currentTick))
            {
                int homingLifetimeTicks = ResolveHomingLifetimeTicks(projectile, host);
                host.BeginHoming(homingLifetimeTicks);
                MarkHomingStarted();
            }

            if (!homingStarted)
            {
                return;
            }

            Vector3 desired;
            float distanceToTarget;
            if (!TryGetDesiredDestination(currentTick, host.HomingIntendedTarget, host.HomingExactPosition, host, projectile, out desired, out distanceToTarget))
            {
                return;
            }

            host.LerpHomingDestination(desired, ResolveTurnLerp(distanceToTarget));
        }

        private bool ShouldStartHomingNow(int currentTick)
        {
            return !homingStarted && currentTick >= homingStartTick;
        }

        private void TryRetargetWhenTargetLost(Projectile projectile, IProjectileHomingCurveHost host, int currentTick)
        {
            if (projectile == null || host == null || Props == null || !Props.enableRetargetOnTargetLost)
            {
                return;
            }

            if (retargetCount >= Mathf.Max(0, Props.retargetMaxCount))
            {
                return;
            }

            if (currentTick < nextRetargetTryTick)
            {
                return;
            }

            if (!IsCurrentTargetLost(host.HomingIntendedTarget))
            {
                return;
            }

            Thing excludeTarget = host.HomingIntendedTarget.HasThing ? host.HomingIntendedTarget.Thing : null;
            Thing newTarget = TryPickRandomHostileTarget(
                projectile.Map,
                projectile.ExactPosition,
                projectile.Launcher,
                Props.retargetSearchRadius,
                Props.retargetPawnWeight,
                Props.retargetNonPawnWeight,
                excludeTarget);

            nextRetargetTryTick = currentTick + Mathf.Max(1, Props.retargetRetryIntervalTicks);
            if (newTarget == null)
            {
                return;
            }

            LocalTargetInfo retargetInfo = new LocalTargetInfo(newTarget);
            projectile.intendedTarget = retargetInfo;
            projectile.usedTarget = retargetInfo;
            retargetCount++;
        }

        private static bool IsCurrentTargetLost(LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                return true;
            }

            if (!target.HasThing)
            {
                return false;
            }

            Thing thing = target.Thing;
            if (thing == null || thing.Destroyed || !thing.Spawned)
            {
                return true;
            }

            Pawn pawn = thing as Pawn;
            return pawn != null && pawn.Dead;
        }

        private void MarkHomingStarted()
        {
            homingStarted = true;
        }

        private float ResolveTurnLerp(float distanceToTarget)
        {
            float baseLerp = Mathf.Clamp01(Props.homingTurnLerp);
            float snapLerp = Mathf.Clamp01(Props.terminalSnapTurnLerp);
            float snapDistance = Mathf.Max(0.05f, Props.terminalSnapDistance);
            if (distanceToTarget >= snapDistance)
            {
                return baseLerp;
            }

            float t = 1f - Mathf.Clamp01(distanceToTarget / snapDistance);
            return Mathf.Lerp(baseLerp, snapLerp, t);
        }

        private int ResolveHomingLifetimeTicks(Projectile projectile, IProjectileHomingCurveHost host)
        {
            float speedPerTick = Mathf.Max(0.001f, projectile.def.projectile.SpeedTilesPerTick);
            float maxRange = ResolveProjectileMaxRange(projectile, host);
            float factor = Mathf.Max(0.05f, Props.homingLifetimeFactor);
            int lifetimeTicks = Mathf.CeilToInt(maxRange / speedPerTick * factor);
            return Mathf.Max(Props.minHomingTicksToImpact, lifetimeTicks);
        }

        private static float ResolveProjectileMaxRange(Projectile projectile, IProjectileHomingCurveHost host)
        {
            float range = 0f;

            range = Mathf.Max(range, ResolveRangeFromVerbList(projectile.EquipmentDef, projectile.def));
            Thing launcher = projectile.Launcher;
            if (launcher != null)
            {
                range = Mathf.Max(range, ResolveRangeFromVerbList(launcher.def, projectile.def));
            }

            if (range > 0.01f)
            {
                return range;
            }

            LocalTargetInfo intendedTarget = host != null ? host.HomingIntendedTarget : LocalTargetInfo.Invalid;
            if (intendedTarget.IsValid)
            {
                Vector3 origin = host != null ? host.HomingExactPosition : projectile.ExactPosition;
                Vector3 targetPos = intendedTarget.HasThing && intendedTarget.Thing != null
                    ? intendedTarget.Thing.DrawPos
                    : intendedTarget.Cell.ToVector3Shifted();
                float directDistance = (targetPos - origin).Yto0().magnitude;
                if (directDistance > 0.01f)
                {
                    return directDistance;
                }
            }

            return 30f;
        }

        private static float ResolveRangeFromVerbList(ThingDef sourceDef, ThingDef projectileDef)
        {
            if (sourceDef == null || sourceDef.Verbs == null || sourceDef.Verbs.Count == 0)
            {
                return 0f;
            }

            float matchedRange = 0f;
            float launchRange = 0f;
            for (int i = 0; i < sourceDef.Verbs.Count; i++)
            {
                VerbProperties verb = sourceDef.Verbs[i];
                if (verb == null)
                {
                    continue;
                }

                if (verb.defaultProjectile == projectileDef)
                {
                    matchedRange = Mathf.Max(matchedRange, verb.range);
                }

                if (verb.LaunchesProjectile)
                {
                    launchRange = Mathf.Max(launchRange, verb.range);
                }
            }

            if (matchedRange > 0.01f)
            {
                return matchedRange;
            }

            return launchRange;
        }

        private bool TryGetDesiredDestination(
            int currentTick,
            LocalTargetInfo intendedTarget,
            Vector3 exactPosition,
            IProjectileHomingCurveHost host,
            Projectile projectile,
            out Vector3 desired,
            out float distanceToTarget)
        {
            desired = Vector3.zero;
            distanceToTarget = float.MaxValue;

            Vector3 targetPos;
            if (!TryResolveTargetPosition(intendedTarget, out targetPos))
            {
                return false;
            }

            Vector3 toTarget = (targetPos - exactPosition).Yto0();
            if (toTarget.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            distanceToTarget = toTarget.magnitude;
            if (distanceToTarget <= Mathf.Max(0.01f, Props.terminalLockDistance))
            {
                desired = targetPos;
                return true;
            }

            Vector3 forward = toTarget.normalized;
            Vector3 lateral = new Vector3(-forward.z, 0f, forward.x) * curveSign;
            float guideDistance = distanceToTarget * Mathf.Max(0f, Props.curveGuideDistanceFactor);
            guideDistance = Mathf.Clamp(guideDistance, Mathf.Max(0.1f, Props.curveGuideDistanceMin), Mathf.Max(0.1f, Props.curveGuideDistanceMax));
            guideDistance = Mathf.Min(guideDistance, distanceToTarget);

            if (distanceToTarget > Mathf.Max(0.05f, Props.terminalSnapDistance)
                && ShouldStopTurningWhenLikelyHit(intendedTarget, targetPos, exactPosition, host, projectile))
            {
                Vector3 holdDirection = NormalizeOrFallback(host.HomingCurrentDirection.Yto0(), forward);
                desired = exactPosition + holdDirection * guideDistance;
                return true;
            }

            float wobbleFadeNearTarget = Mathf.Clamp01((distanceToTarget - Props.terminalLockDistance) / Mathf.Max(0.05f, Props.curveWobbleFadeDistance));
            float wobble = Mathf.Sin((currentTick + curveSeed) * Props.curveFrequency) * curveAmplitude * wobbleFadeNearTarget;

            Vector3 guidePoint = exactPosition + forward * guideDistance;
            desired = guidePoint + lateral * wobble;
            return true;
        }

        private bool ShouldStopTurningWhenLikelyHit(
            LocalTargetInfo intendedTarget,
            Vector3 targetPos,
            Vector3 exactPosition,
            IProjectileHomingCurveHost host,
            Projectile projectile)
        {
            if (!Props.enableTurnStopWhenLikelyHit || host == null || projectile == null || !intendedTarget.HasThing)
            {
                return false;
            }

            float targetRadius = EstimateTargetRadius(intendedTarget.Thing);
            if (targetRadius < Mathf.Max(0.01f, Props.turnStopMinTargetRadius))
            {
                return false;
            }

            Vector3 currentForward = NormalizeOrFallback(host.HomingCurrentDirection.Yto0(), (targetPos - exactPosition).Yto0());
            Vector3 toTarget = (targetPos - exactPosition).Yto0();
            if (Vector3.Dot(currentForward, toTarget) <= 0f)
            {
                return false;
            }

            float speedPerTick = Mathf.Max(0.001f, projectile.def.projectile.SpeedTilesPerTick);
            float predictDistance = speedPerTick * Mathf.Max(1, Props.turnStopPredictTicks);
            predictDistance = Mathf.Max(predictDistance, Mathf.Max(0.8f, targetRadius * 1.35f));

            Vector3 a = exactPosition;
            Vector3 b = exactPosition + currentForward * predictDistance;
            float hitRadius = targetRadius + Mathf.Max(0f, Props.turnStopMargin);
            float distSqr = DistancePointToSegmentSqr(targetPos, a, b);
            return distSqr <= hitRadius * hitRadius;
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

        private static Vector3 NormalizeOrFallback(Vector3 direction, Vector3 fallback)
        {
            if (direction.sqrMagnitude > 0.0001f)
            {
                return direction.normalized;
            }

            if (fallback.sqrMagnitude > 0.0001f)
            {
                return fallback.normalized;
            }

            return Vector3.forward;
        }

        private static bool TryResolveTargetPosition(LocalTargetInfo intendedTarget, out Vector3 targetPos)
        {
            if (intendedTarget.HasThing)
            {
                Thing targetThing = intendedTarget.Thing;
                if (targetThing == null || !targetThing.Spawned || targetThing.Destroyed)
                {
                    targetPos = Vector3.zero;
                    return false;
                }

                targetPos = targetThing.DrawPos;
                return true;
            }

            if (intendedTarget.IsValid)
            {
                targetPos = intendedTarget.Cell.ToVector3Shifted();
                return true;
            }

            targetPos = Vector3.zero;
            return false;
        }

        public static Thing TryPickRandomHostileTarget(
            Map map,
            Vector3 center,
            Thing seeker,
            float radius,
            float pawnWeight,
            float nonPawnWeight,
            Thing excludeTarget = null)
        {
            if (map == null || seeker == null)
            {
                return null;
            }

            float searchRadius = Mathf.Max(1f, radius);
            IntVec3 centerCell = center.ToIntVec3();
            if (!centerCell.InBounds(map))
            {
                centerCell = centerCell.ClampInsideMap(map);
            }

            List<Thing> candidates = new List<Thing>();
            List<float> weights = new List<float>();
            HashSet<int> seenIds = new HashSet<int>();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(centerCell, searchRadius, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                List<Thing> thingList = cell.GetThingList(map);
                for (int i = 0; i < thingList.Count; i++)
                {
                    Thing thing = thingList[i];
                    if (!IsViableRetargetCandidate(seeker, thing, excludeTarget))
                    {
                        continue;
                    }

                    if (!seenIds.Add(thing.thingIDNumber))
                    {
                        continue;
                    }

                    float weight = GetRetargetWeight(thing, pawnWeight, nonPawnWeight);
                    if (weight <= 0.001f)
                    {
                        continue;
                    }

                    candidates.Add(thing);
                    weights.Add(weight);
                }
            }

            if (candidates.Count == 0)
            {
                return null;
            }

            float totalWeight = 0f;
            for (int i = 0; i < weights.Count; i++)
            {
                totalWeight += Mathf.Max(0f, weights[i]);
            }

            if (totalWeight <= 0.001f)
            {
                return null;
            }

            float pick = Rand.Value * totalWeight;
            float accum = 0f;
            for (int i = 0; i < candidates.Count; i++)
            {
                accum += Mathf.Max(0f, weights[i]);
                if (pick <= accum)
                {
                    return candidates[i];
                }
            }

            return candidates[candidates.Count - 1];
        }

        private static bool IsViableRetargetCandidate(Thing seeker, Thing candidate, Thing excludeTarget)
        {
            if (candidate == null || candidate == seeker || candidate == excludeTarget || candidate.Destroyed || !candidate.Spawned)
            {
                return false;
            }

            if (!GenHostility.HostileTo(seeker, candidate))
            {
                return false;
            }

            Pawn pawn = candidate as Pawn;
            if (pawn != null)
            {
                return !pawn.Dead && !pawn.Downed;
            }

            if (candidate.def == null)
            {
                return false;
            }

            return candidate.def.useHitPoints || candidate.def.Fillage == FillCategory.Full;
        }

        private static float GetRetargetWeight(Thing thing, float pawnWeight, float nonPawnWeight)
        {
            if (thing is Pawn)
            {
                return Mathf.Max(0f, pawnWeight);
            }

            return Mathf.Max(0f, nonPawnWeight);
        }

        private void ResetState()
        {
            launchInitialized = false;
            homingStartTick = 0;
            homingStarted = false;
            curveSeed = 0f;
            curveSign = 1f;
            curveAmplitude = 0f;
            retargetCount = 0;
            nextRetargetTryTick = 0;
        }
    }
}

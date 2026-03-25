using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public interface IProjectileHomingCurveHost
    {
        bool AllowHomingUpdate { get; }
        LocalTargetInfo HomingIntendedTarget { get; }
        Vector3 HomingExactPosition { get; }
        int HomingTicksToImpact { get; }
        int StartingTicksToImpactCeil();
        void BeginHoming(int minTicksToImpact);
        void LerpHomingDestination(Vector3 desired, float lerp);
    }

    public class CompProperties_ProjectileHomingCurve : CompProperties
    {
        public bool enableHoming = true;
        public int homingStartDelayTicks = 4;
        public int minHomingTicksToImpact = 9;
        public float homingTurnLerp = 0.14f;
        public float curveAmplitudeMin = 0.12f;
        public float curveAmplitudeMax = 0.42f;
        public float curveFrequency = 0.22f;

        public CompProperties_ProjectileHomingCurve()
        {
            compClass = typeof(CompProjectileHomingCurve);
        }
    }

    public class CompProjectileHomingCurve : ThingComp
    {
        private int homingStartTick;
        private bool homingStarted;
        private int homingDurationTicks;
        private float curveSeed;
        private float curveSign = 1f;
        private float curveAmplitude;
        private bool launchInitialized;

        private CompProperties_ProjectileHomingCurve Props => (CompProperties_ProjectileHomingCurve)props;
        private Projectile ProjectileParent => parent as Projectile;
        private IProjectileHomingCurveHost Host => parent as IProjectileHomingCurveHost;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref homingStartTick, "homingStartTick", 0);
            Scribe_Values.Look(ref homingStarted, "homingStarted", false);
            Scribe_Values.Look(ref homingDurationTicks, "homingDurationTicks", 0);
            Scribe_Values.Look(ref curveSeed, "curveSeed", 0f);
            Scribe_Values.Look(ref curveSign, "curveSign", 1f);
            Scribe_Values.Look(ref curveAmplitude, "curveAmplitude", 0f);
            Scribe_Values.Look(ref launchInitialized, "launchInitialized", false);
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
            if (Props == null || !Props.enableHoming)
            {
                ResetState();
                return;
            }

            homingStartTick = currentTick + Mathf.Max(0, Props.homingStartDelayTicks);
            homingStarted = false;
            homingDurationTicks = 0;
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
        }

        public override void CompTick()
        {
            Projectile projectile = ProjectileParent;
            IProjectileHomingCurveHost host = Host;
            if (Props == null || !Props.enableHoming || projectile == null || host == null || !parent.Spawned || parent.Map == null)
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

            if (!host.AllowHomingUpdate || !host.HomingIntendedTarget.IsValid)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (ShouldStartHomingNow(currentTick))
            {
                host.BeginHoming(Props.minHomingTicksToImpact);
                MarkHomingStarted(host.HomingTicksToImpact);
            }

            if (!homingStarted)
            {
                return;
            }

            Vector3 desired;
            if (!TryGetDesiredDestination(currentTick, host.HomingIntendedTarget, host.HomingExactPosition, host.HomingTicksToImpact, out desired))
            {
                return;
            }

            host.LerpHomingDestination(desired, Mathf.Clamp01(Props.homingTurnLerp));
        }

        private bool ShouldStartHomingNow(int currentTick)
        {
            return !homingStarted && currentTick >= homingStartTick;
        }

        private void MarkHomingStarted(int durationTicks)
        {
            homingStarted = true;
            homingDurationTicks = Mathf.Max(1, durationTicks);
        }

        private bool TryGetDesiredDestination(
            int currentTick,
            LocalTargetInfo intendedTarget,
            Vector3 exactPosition,
            int ticksToImpact,
            out Vector3 desired)
        {
            desired = Vector3.zero;

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

            Vector3 forward = toTarget.normalized;
            Vector3 lateral = new Vector3(-forward.z, 0f, forward.x) * curveSign;
            float progress = 1f - Mathf.Clamp01((float)ticksToImpact / Mathf.Max(1f, homingDurationTicks));
            float wobble = Mathf.Sin((currentTick + curveSeed) * Props.curveFrequency) * curveAmplitude * (1f - progress * 0.65f);
            desired = targetPos + lateral * wobble;
            return true;
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

        private void ResetState()
        {
            launchInitialized = false;
            homingStartTick = 0;
            homingStarted = false;
            homingDurationTicks = 0;
            curveSeed = 0f;
            curveSign = 1f;
            curveAmplitude = 0f;
        }
    }
}

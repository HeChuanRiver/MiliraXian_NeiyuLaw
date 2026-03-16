using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.NeiyuLaw
{
    public class SplitArrowExtension : DefModExtension
    {
        public ThingDef splitProjectileDef;
        public int splitCount = 6;
        public int splitDelayTicks = 8;
        public float scatterAngle = 180f;
        public float scatterDistance = 9f;
        public int homingStartDelayTicks = 10;
        public float homingTurnLerp = 0.18f;
    }

    public class Projectile_BigArrowSplit : Bullet
    {
        private bool splitDone;

        private int splitTick;

        private SplitArrowExtension Ext => def.GetModExtension<SplitArrowExtension>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref splitDone, "splitDone", false);
            Scribe_Values.Look(ref splitTick, "splitTick", 0);
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
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            SplitArrowExtension ext = Ext;
            splitTick = Find.TickManager.TicksGame + Mathf.Max(1, ext?.splitDelayTicks ?? 8);
        }

        protected override void TickInterval(int delta)
        {
            if (ShouldSplitNow(delta))
            {
                SplitIntoShards();
                return;
            }

            base.TickInterval(delta);
        }

        private bool ShouldSplitNow(int delta)
        {
            if (splitDone || landed || Destroyed || !Spawned || Map == null)
            {
                return false;
            }

            SplitArrowExtension ext = Ext;
            if (ext == null || ext.splitProjectileDef == null)
            {
                return false;
            }

            if (Find.TickManager.TicksGame >= splitTick)
            {
                return true;
            }

            return ticksToImpact <= Mathf.Max(1, delta);
        }

        private void SplitIntoShards()
        {
            splitDone = true;

            SplitArrowExtension ext = Ext;
            Map map = Map;
            if (ext == null || ext.splitProjectileDef == null || map == null)
            {
                if (!Destroyed)
                {
                    Destroy();
                }
                return;
            }

            Vector3 forward = (destination - origin).Yto0();
            if (forward.x == 0f && forward.z == 0f)
            {
                forward = Vector3.forward;
            }

            forward = forward.normalized;
            int count = Mathf.Max(1, ext.splitCount);
            Vector3 aimPoint = destination;
            if (intendedTarget.IsValid)
            {
                if (intendedTarget.HasThing && intendedTarget.Thing != null && intendedTarget.Thing.Spawned)
                {
                    aimPoint = intendedTarget.Thing.DrawPos;
                }
                else
                {
                    aimPoint = intendedTarget.Cell.ToVector3Shifted();
                }
            }

            Vector3 aimVector = (aimPoint - ExactPosition).Yto0();
            if (aimVector.sqrMagnitude > 0.0001f)
            {
                forward = aimVector.normalized;
            }

            float desiredDistance = Mathf.Max(4f, aimVector.magnitude);
            float coneAngle = Mathf.Min(90f, Mathf.Abs(ext.scatterAngle));
            float minScatterDistance = Mathf.Max(6f, desiredDistance * 0.55f + ext.scatterDistance * 0.8f);
            float maxScatterDistance = Mathf.Max(minScatterDistance + 2f, desiredDistance * 1.45f + ext.scatterDistance * 2.4f);
            CellRect mapRect = CellRect.WholeMap(map);

            for (int i = 0; i < count; i++)
            {
                Thing spawned = GenSpawn.Spawn(ext.splitProjectileDef, Position, map);
                Projectile child = spawned as Projectile;
                if (child == null)
                {
                    continue;
                }

                float angle = Rand.Range(-coneAngle, coneAngle);
                Vector3 scatterDir = forward.RotatedBy(angle).normalized;
                float travelDistance = Rand.Range(minScatterDistance, maxScatterDistance);
                IntVec3 scatterCell = (ExactPosition + scatterDir * travelDistance).ToIntVec3();
                if (!scatterCell.InBounds(map))
                {
                    scatterCell = mapRect.ClosestCellTo(scatterCell);
                }

                LocalTargetInfo usedTarget = new LocalTargetInfo(scatterCell);
                LocalTargetInfo homingTarget = intendedTarget.IsValid ? intendedTarget : usedTarget;

                Projectile_HomingShard shard = child as Projectile_HomingShard;
                if (shard != null)
                {
                    shard.SetSequentialDelay(Rand.RangeInclusive(2, 6));
                }

                child.Launch(launcher, ExactPosition, usedTarget, homingTarget, HitFlags, preventFriendlyFire, equipment, targetCoverDef);
            }

            if (!Destroyed)
            {
                Destroy();
            }
        }

    }

    public class Projectile_HomingShard : Bullet
    {
        private int homingStartTick;
        private bool homingStarted;
        private int sequentialDelayTicks;

        private SplitArrowExtension Ext => def.GetModExtension<SplitArrowExtension>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref homingStartTick, "homingStartTick", 0);
            Scribe_Values.Look(ref homingStarted, "homingStarted", false);
            Scribe_Values.Look(ref sequentialDelayTicks, "sequentialDelayTicks", 0);
        }

        public void SetSequentialDelay(int delayTicks)
        {
            sequentialDelayTicks = Mathf.Max(1, delayTicks);
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
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            homingStartTick = Find.TickManager.TicksGame + Mathf.Max(1, sequentialDelayTicks);
        }

        protected override void Tick()
        {
            base.Tick();
            if (this.IsHashIntervalTick(2) && Map != null)
            {
                FleckMaker.ThrowLightningGlow(ExactPosition, Map, 0.2f);
            }
        }


        protected override void TickInterval(int delta)
        {
            if (!landed)
            {
                TryStartOrUpdateHoming();
            }

            base.TickInterval(delta);
        }

        protected override void ImpactSomething()
        {
            if (intendedTarget.HasThing)
            {
                Thing targetThing = intendedTarget.Thing;
                if (targetThing == null || targetThing.Destroyed || !targetThing.Spawned)
                {
                    Destroy(DestroyMode.Vanish);
                    return;
                }

                Impact(targetThing);
                return;
            }

            if (intendedTarget.IsValid)
            {
                Impact(null);
                return;
            }

            base.ImpactSomething();
        }

        private void TryStartOrUpdateHoming()
        {
            SplitArrowExtension ext = Ext;
            if (ext == null)
            {
                return;
            }

            Vector3 targetPos;
            if (intendedTarget.HasThing)
            {
                Thing targetThing = intendedTarget.Thing;
                if (targetThing == null || !targetThing.Spawned || targetThing.Destroyed)
                {
                    return;
                }

                targetPos = targetThing.DrawPos;
            }
            else if (intendedTarget.IsValid)
            {
                targetPos = intendedTarget.Cell.ToVector3Shifted();
            }
            else
            {
                return;
            }

            if (!homingStarted && Find.TickManager.TicksGame >= homingStartTick)
            {
                homingStarted = true;
                origin = ExactPosition;
                destination = Vector3.Lerp(destination, targetPos, 0.25f);
                ticksToImpact = Mathf.Max(6, Mathf.CeilToInt(StartingTicksToImpact));
                lifetime = ticksToImpact;
            }

            if (homingStarted)
            {
                Vector3 delta = targetPos - ExactPosition;
                float horizontalDist = Mathf.Sqrt(delta.x * delta.x + delta.z * delta.z);
                float nearFactor = Mathf.InverseLerp(28f, 3f, horizontalDist);
                float minTurn = Mathf.Clamp01(ext.homingTurnLerp * 0.25f);
                float maxTurn = Mathf.Clamp01(ext.homingTurnLerp * 0.65f);
                float t = Mathf.Lerp(minTurn, maxTurn, nearFactor);
                destination = Vector3.Lerp(destination, targetPos, t);
            }
        }
    }
}

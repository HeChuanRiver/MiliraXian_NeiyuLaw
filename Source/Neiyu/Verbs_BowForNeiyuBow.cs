using RimWorld;
using UnityEngine;
using Verse;
using MiliraXian.Characters;

namespace MiliraXian.Characters.Neiyu
{
    public class SplitArrowExtension : DefModExtension
    {
        public ThingDef splitProjectileDef;
        public int splitCount = 6;
        public int splitDelayTicks = 8;
        public float scatterAngle = 120f;
        public float scatterDistance = 9f;
        public bool enableSplitRetarget = true;
        public float splitRetargetChance = 0.45f;
        public float splitRetargetRadius = 10f;
        public float splitRetargetPawnWeight = 3.5f;
        public float splitRetargetNonPawnWeight = 1.0f;
    }

    public class Projectile_BigArrowSplit : Bullet
    {
        private const string DefaultSplitProjectileDefName = "MX_Bullet_HomingShard";
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

            if (ResolveSplitProjectileDef() == null)
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
            ThingDef splitProjectileDef = ResolveSplitProjectileDef();
            Map map = Map;
            if (splitProjectileDef == null || map == null)
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
            int count = Mathf.Max(1, ext?.splitCount ?? 6);
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

            float scatterDistance = ext?.scatterDistance ?? 9f;
            float totalScatterAngle = Mathf.Clamp(Mathf.Abs(ext?.scatterAngle ?? 120f), 1f, 179f);
            float halfConeAngle = totalScatterAngle * 0.5f;
            float minScatterDistance = Mathf.Max(4f, scatterDistance * 0.65f);
            float maxScatterDistance = Mathf.Max(minScatterDistance + 2f, scatterDistance * 1.35f);
            maxScatterDistance = Mathf.Min(maxScatterDistance, 18f);
            CellRect mapRect = CellRect.WholeMap(map);
            System.Collections.Generic.List<Thing> retargetCandidates = SimplePool<System.Collections.Generic.List<Thing>>.Get();
            System.Collections.Generic.HashSet<int> retargetSeenIds = SimplePool<System.Collections.Generic.HashSet<int>>.Get();
            float retargetTotalWeight = 0f;
            bool canRetarget = (ext?.enableSplitRetarget ?? true) && intendedTarget.HasThing;
            if (canRetarget)
            {
                retargetTotalWeight = CompProjectileHomingCurve.GatherHostileTargets(
                    map,
                    aimPoint,
                    launcher,
                    ext?.splitRetargetRadius ?? 10f,
                    ext?.splitRetargetPawnWeight ?? 3.5f,
                    ext?.splitRetargetNonPawnWeight ?? 1.0f,
                    intendedTarget.Thing,
                    retargetCandidates,
                    retargetSeenIds);
            }

            try
            {
                for (int i = 0; i < count; i++)
                {
                    Thing spawned = GenSpawn.Spawn(splitProjectileDef, Position, map);
                    Projectile child = spawned as Projectile;
                    if (child == null)
                    {
                        continue;
                    }

                    float angle = Rand.Range(-halfConeAngle, halfConeAngle);
                    Vector3 scatterDir = forward.RotatedBy(angle).normalized;
                    float travelDistance = Rand.Range(minScatterDistance, maxScatterDistance);
                    IntVec3 scatterCell = (ExactPosition + scatterDir * travelDistance).ToIntVec3();
                    if (!scatterCell.InBounds(map))
                    {
                        scatterCell = mapRect.ClosestCellTo(scatterCell);
                    }

                    LocalTargetInfo usedTarget = new LocalTargetInfo(scatterCell);
                    LocalTargetInfo homingTarget = intendedTarget.IsValid ? intendedTarget : usedTarget;
                    if (canRetarget
                        && retargetTotalWeight > 0.001f
                        && Rand.Chance(Mathf.Clamp01(ext?.splitRetargetChance ?? 0.45f)))
                    {
                        Thing altTarget = CompProjectileHomingCurve.PickRandomHostileTarget(
                            retargetCandidates,
                        ext?.splitRetargetPawnWeight ?? 3.5f,
                        ext?.splitRetargetNonPawnWeight ?? 1.0f,
                            retargetTotalWeight);
                        if (altTarget != null)
                        {
                            homingTarget = new LocalTargetInfo(altTarget);
                        }
                    }

                    Projectile_HomingShard shard = child as Projectile_HomingShard;
                    if (shard != null)
                    {
                        shard.SetSequentialDelay(Rand.RangeInclusive(2, 6));
                    }
                    child.Launch(launcher, ExactPosition, usedTarget, homingTarget, HitFlags, preventFriendlyFire, equipment, targetCoverDef);
                }
            }
            finally
            {
                retargetCandidates.Clear();
                retargetSeenIds.Clear();
                SimplePool<System.Collections.Generic.List<Thing>>.Return(retargetCandidates);
                SimplePool<System.Collections.Generic.HashSet<int>>.Return(retargetSeenIds);
            }

            if (!Destroyed)
            {
                Destroy();
            }
        }

        private ThingDef ResolveSplitProjectileDef()
        {
            ThingDef splitDef = Ext?.splitProjectileDef;
            if (splitDef != null)
            {
                return splitDef;
            }

            return DefDatabase<ThingDef>.GetNamedSilentFail(DefaultSplitProjectileDefName);
        }

    }

    public class Projectile_HomingShard : ProjectileHomingCurveBase
    {
        private int sequentialDelayTicks;

        public override void ExposeData()
        {
            base.ExposeData();
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
            int delay = Mathf.Max(1, sequentialDelayTicks);
            GetComp<CompProjectileHomingCurve>()?.NotifyLaunch(Find.TickManager.TicksGame + delay);
        }

        protected override void Tick()
        {
            base.Tick();
            if (MiliraXian.Characters.MXVisualBudget.ShouldEmit(this, 2))
            {
                FleckMaker.ThrowLightningGlow(ExactPosition, Map, 0.2f);
            }
        }
    }
}

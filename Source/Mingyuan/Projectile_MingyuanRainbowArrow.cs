using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan
{
    public class MingyuanRainbowArrowExtension : DefModExtension
    {
        public int bounceCount = 6;
        public float bounceRadius = 5f;
        public int maxHitsPerPawn = 3;
        public float lifeBurnLayers = 8f;
        public bool scaleLifeBurnWithOverburn = true;
    }

    public class Projectile_MingyuanRainbowArrow : Bullet
    {
        private static readonly List<Pawn> CandidatePawns = new List<Pawn>(32);

        private int remainingBounces = -1;
        private List<Pawn> hitPawns = new List<Pawn>(8);
        private List<int> hitCounts = new List<int>(8);

        private MingyuanRainbowArrowExtension Ext => def.GetModExtension<MingyuanRainbowArrowExtension>();

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref remainingBounces, "remainingBounces", -1);
            Scribe_Collections.Look(ref hitPawns, "hitPawns", LookMode.Reference);
            Scribe_Collections.Look(ref hitCounts, "hitCounts", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (hitPawns == null)
                {
                    hitPawns = new List<Pawn>(8);
                }

                if (hitCounts == null)
                {
                    hitCounts = new List<int>(hitPawns.Count);
                }
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
            base.Launch(launcher, origin, usedTarget, intendedTarget, hitFlags, preventFriendlyFire, equipment, targetCoverDef);
            if (remainingBounces < 0)
            {
                remainingBounces = Mathf.Max(0, Ext?.bounceCount ?? 6);
            }
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map impactMap = Map;
            IntVec3 impactCell = Position;
            Vector3 impactOrigin = ExactPosition;
            ProjectileHitFlags hitFlags = HitFlags;
            Thing launcherThing = launcher;
            Thing equipmentThing = equipment;
            ThingDef coverDef = targetCoverDef;
            Pawn hitPawn = hitThing as Pawn;
            bool directHostileHit = IsValidBounceTarget(hitPawn, launcherThing as Pawn, impactMap);

            if (directHostileHit)
            {
                RecordHit(hitPawn);
            }

            base.Impact(hitThing, blockedByShield);

            if (blockedByShield || impactMap == null || launcherThing == null || !directHostileHit)
            {
                return;
            }

            ApplyExtraLifeBurn(hitPawn, launcherThing as Pawn);
            if (remainingBounces <= 0)
            {
                return;
            }

            Pawn nextTarget = TryFindNextBounceTarget(impactMap, impactCell, hitPawn, launcherThing as Pawn);
            if (nextTarget == null)
            {
                return;
            }

            Projectile_MingyuanRainbowArrow nextArrow = GenSpawn.Spawn(def, impactCell, impactMap) as Projectile_MingyuanRainbowArrow;
            if (nextArrow == null)
            {
                return;
            }

            nextArrow.CopyBounceStateFrom(this, remainingBounces - 1);
            LocalTargetInfo targetInfo = new LocalTargetInfo(nextTarget);
            nextArrow.Launch(launcherThing, impactOrigin, targetInfo, targetInfo, hitFlags, preventFriendlyFire, equipmentThing, coverDef);
        }

        private void CopyBounceStateFrom(Projectile_MingyuanRainbowArrow source, int newRemainingBounces)
        {
            remainingBounces = Mathf.Max(0, newRemainingBounces);
            hitPawns = new List<Pawn>(source.hitPawns.Count);
            hitCounts = new List<int>(source.hitCounts.Count);
            for (int i = 0; i < source.hitPawns.Count; i++)
            {
                Pawn pawn = source.hitPawns[i];
                if (pawn == null || pawn.Destroyed)
                {
                    continue;
                }

                hitPawns.Add(pawn);
                hitCounts.Add(i < source.hitCounts.Count ? source.hitCounts[i] : 1);
            }
        }

        private void ApplyExtraLifeBurn(Pawn pawn, Pawn instigator)
        {
            MingyuanRainbowArrowExtension ext = Ext;
            float layers = ext?.lifeBurnLayers ?? 8f;
            if (pawn == null || pawn.Dead || layers <= 0f)
            {
                return;
            }

            MingyuanUtility.AddLifeBurn(pawn, instigator, layers, scaleWithOverburn: ext?.scaleLifeBurnWithOverburn ?? true);
        }

        private Pawn TryFindNextBounceTarget(Map map, IntVec3 centerCell, Pawn currentTarget, Pawn launcherPawn)
        {
            MingyuanRainbowArrowExtension ext = Ext;
            float radius = Mathf.Max(0f, ext?.bounceRadius ?? 5f);
            if (map == null || !centerCell.IsValid || radius <= 0f)
            {
                return null;
            }

            CandidatePawns.Clear();
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(centerCell, map, radius, true))
            {
                Pawn pawn = thing as Pawn;
                if (IsEligibleBounceCandidate(pawn, launcherPawn, map))
                {
                    CandidatePawns.Add(pawn);
                }
            }

            Pawn best = BestCandidate(centerCell, currentTarget, false);
            if (best == null)
            {
                best = BestCandidate(centerCell, currentTarget, true);
            }

            CandidatePawns.Clear();
            return best;
        }

        private Pawn BestCandidate(IntVec3 centerCell, Pawn currentTarget, bool allowCurrentTarget)
        {
            Pawn best = null;
            int bestHits = int.MaxValue;
            int bestDistance = int.MaxValue;
            for (int i = 0; i < CandidatePawns.Count; i++)
            {
                Pawn candidate = CandidatePawns[i];
                if (!allowCurrentTarget && candidate == currentTarget)
                {
                    continue;
                }

                int hits = GetHitCount(candidate);
                int distance = candidate.Position.DistanceToSquared(centerCell);
                if (hits < bestHits || (hits == bestHits && distance < bestDistance))
                {
                    best = candidate;
                    bestHits = hits;
                    bestDistance = distance;
                }
            }

            return best;
        }

        private bool IsEligibleBounceCandidate(Pawn pawn, Pawn launcherPawn, Map map)
        {
            return IsValidBounceTarget(pawn, launcherPawn, map)
                   && GetHitCount(pawn) < Mathf.Max(1, Ext?.maxHitsPerPawn ?? 3);
        }

        private static bool IsValidBounceTarget(Pawn pawn, Pawn launcherPawn, Map map)
        {
            return pawn != null
                   && launcherPawn != null
                   && map != null
                   && pawn.Spawned
                   && pawn.Map == map
                   && !pawn.Dead
                   && pawn.HostileTo(launcherPawn);
        }

        private void RecordHit(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            int index = hitPawns.IndexOf(pawn);
            if (index >= 0)
            {
                while (hitCounts.Count <= index)
                {
                    hitCounts.Add(0);
                }

                hitCounts[index]++;
                return;
            }

            hitPawns.Add(pawn);
            hitCounts.Add(1);
        }

        private int GetHitCount(Pawn pawn)
        {
            if (pawn == null)
            {
                return 0;
            }

            int index = hitPawns.IndexOf(pawn);
            if (index < 0 || index >= hitCounts.Count)
            {
                return 0;
            }

            return hitCounts[index];
        }
    }
}

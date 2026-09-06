using System.Collections.Generic;
using MiliraXian.Characters;
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
        public float lifeBurnLayers = 300f;
        public bool scaleLifeBurnWithOverburn = true;
        public bool requireBounceLineOfSight = true;
        public bool skipDownedBounceTargets = true;
        public float bounceForwardWeight = 10f;
        public float bounceDistanceWeight = 0.45f;
        public float bounceRepeatPenalty = 18f;
        public float visualArcHeight = 0.9f;
        public float glowScale = 1.28f;
        public Color glowColor = new(1f, 0.62f, 0.24f, 0.58f);
    }

    [StaticConstructorOnStartup]
    public class Projectile_MingyuanRainbowArrow : ProjectileHomingCurveBase, IProjectileVisualPositionProvider
    {
        private static readonly List<Pawn> CandidatePawns = new(32);
        private static Texture2D cachedGlowTexture;
        private static Material cachedGlowMaterial;

        private int remainingBounces = -1;
        private List<Pawn> hitPawns = new(8);
        private List<int> hitCounts = new(8);
        private int visualArcDurationTicks;
        private int visualArcElapsedTicks;

        private MingyuanRainbowArrowExtension Ext => def.GetModExtension<MingyuanRainbowArrowExtension>();

        public Vector3 VisualTrailPosition => ExactPosition + Vector3.forward * VisualArcOffset;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref remainingBounces, "remainingBounces", -1);
            Scribe_Values.Look(ref visualArcDurationTicks, "visualArcDurationTicks", 0);
            Scribe_Values.Look(ref visualArcElapsedTicks, "visualArcElapsedTicks", 0);
            Scribe_Collections.Look(ref hitPawns, "hitPawns", LookMode.Reference);
            Scribe_Collections.Look(ref hitCounts, "hitCounts", LookMode.Value);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                hitPawns ??= new(8);

                hitCounts ??= new(hitPawns.Count);
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
            CompProjectileHomingCurve homing = GetComp<CompProjectileHomingCurve>();
            CompProperties_ProjectileHomingCurve settings = homing?.Settings;
            float lifetimeFactor = Mathf.Max(0.05f, settings?.homingLifetimeFactor ?? 1f);
            int homingDelay = Mathf.Max(0, settings?.homingStartDelayTicks ?? 0);
            int homingTicks = Mathf.Max(
                settings?.minHomingTicksToImpact ?? 1,
                Mathf.CeilToInt(StartingTicksToImpact * lifetimeFactor));
            homingTicks += Mathf.Max(0, settings?.extraHomingTicks ?? 0);
            visualArcDurationTicks = Mathf.Max(1, homingDelay + homingTicks);
            visualArcElapsedTicks = 0;
            if (remainingBounces < 0)
            {
                remainingBounces = Mathf.Max(0, Ext?.bounceCount ?? 6);
            }
        }

        protected override void TickInterval(int delta)
        {
            visualArcElapsedTicks = Mathf.Min(
                Mathf.Max(1, visualArcDurationTicks),
                visualArcElapsedTicks + Mathf.Max(0, delta));
            base.TickInterval(delta);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            Vector3 visualLoc = drawLoc + Vector3.forward * VisualArcOffset;
            DrawGlow(visualLoc);
            base.DrawAt(visualLoc, flip);
        }

        protected override void Impact(Thing hitThing, bool blockedByShield = false)
        {
            Map impactMap = Map;
            Vector3 impactOrigin = ExactPosition;
            IntVec3 impactCell = Position;
            if (impactMap != null)
            {
                IntVec3 exactCell = impactOrigin.ToIntVec3();
                if (exactCell.InBounds(impactMap))
                {
                    impactCell = exactCell;
                }
            }

            Thing resolvedHitThing = ResolveImpactHitThing(hitThing, impactOrigin, impactMap);
            ProjectileHitFlags hitFlags = HitFlags;
            Thing launcherThing = launcher;
            Thing equipmentThing = equipment;
            ThingDef coverDef = targetCoverDef;
            Pawn hitPawn = resolvedHitThing as Pawn;
            bool directHostileHit = IsValidBounceTarget(hitPawn, launcherThing as Pawn, impactMap);
            Vector3 incomingDirection = (impactOrigin - origin).Yto0();

            if (directHostileHit)
            {
                RecordHit(hitPawn);
            }

            bool previousSuppression = MingyuanUtility.SuppressOnHitLifeBurn;
            try
            {
                MingyuanUtility.SuppressOnHitLifeBurn = true;
                base.Impact(resolvedHitThing, blockedByShield);
            }
            finally
            {
                MingyuanUtility.SuppressOnHitLifeBurn = previousSuppression;
            }

            if (blockedByShield || impactMap == null || launcherThing == null || !directHostileHit)
            {
                return;
            }

            if (MingyuanPowerBalance.Sealed) return;
            ApplyExtraLifeBurn(hitPawn, launcherThing as Pawn);
            if (remainingBounces <= 0)
            {
                return;
            }

            Pawn nextTarget = TryFindNextBounceTarget(impactMap, impactCell, impactOrigin, incomingDirection, hitPawn, launcherThing as Pawn);
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
            LocalTargetInfo targetInfo = new(nextTarget);
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
            float layers = (ext?.lifeBurnLayers ?? 300f) * (MingyuanPowerBalance.IsBalanced ? .9f : 1f);
            if (pawn == null || pawn.Dead || layers <= 0f)
            {
                return;
            }

            MingyuanUtility.AddLifeBurn(pawn, instigator, layers, scaleWithOverburn: ext?.scaleLifeBurnWithOverburn ?? true);
        }

        private Pawn TryFindNextBounceTarget(Map map, IntVec3 centerCell, Vector3 centerPosition, Vector3 incomingDirection, Pawn currentTarget, Pawn launcherPawn)
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
                if (IsEligibleBounceCandidate(pawn, launcherPawn, map, centerCell))
                {
                    CandidatePawns.Add(pawn);
                }
            }

            Pawn best = BestCandidate(centerCell, centerPosition, incomingDirection, currentTarget, false) ?? BestCandidate(centerCell, centerPosition, incomingDirection, currentTarget, true);
            CandidatePawns.Clear();
            return best;
        }

        private Pawn BestCandidate(IntVec3 centerCell, Vector3 centerPosition, Vector3 incomingDirection, Pawn currentTarget, bool allowCurrentTarget)
        {
            Pawn best = null;
            float bestScore = float.MinValue;
            for (int i = 0; i < CandidatePawns.Count; i++)
            {
                Pawn candidate = CandidatePawns[i];
                if (!allowCurrentTarget && candidate == currentTarget)
                {
                    continue;
                }

                float score = ScoreBounceCandidate(centerCell, centerPosition, incomingDirection, candidate, currentTarget);
                if (score > bestScore)
                {
                    best = candidate;
                    bestScore = score;
                }
            }

            return best;
        }

        private float ScoreBounceCandidate(IntVec3 centerCell, Vector3 centerPosition, Vector3 incomingDirection, Pawn candidate, Pawn currentTarget)
        {
            MingyuanRainbowArrowExtension ext = Ext;
            int hits = GetHitCount(candidate);
            Vector3 toCandidate = (candidate.DrawPos - centerPosition).Yto0();
            float distance = Mathf.Sqrt(Mathf.Max(0f, candidate.Position.DistanceToSquared(centerCell)));
            float forwardScore = 0.5f;
            if (incomingDirection.sqrMagnitude > 0.0001f && toCandidate.sqrMagnitude > 0.0001f)
            {
                forwardScore = (Vector3.Dot(incomingDirection.normalized, toCandidate.normalized) + 1f) * 0.5f;
            }

            float score = forwardScore * Mathf.Max(0f, ext?.bounceForwardWeight ?? 10f);
            score -= distance * Mathf.Max(0f, ext?.bounceDistanceWeight ?? 0.45f);
            score -= hits * Mathf.Max(0f, ext?.bounceRepeatPenalty ?? 18f);
            if (candidate == currentTarget)
            {
                score -= Mathf.Max(0f, ext?.bounceRepeatPenalty ?? 18f) * 0.75f;
            }

            return score;
        }

        private bool IsEligibleBounceCandidate(Pawn pawn, Pawn launcherPawn, Map map, IntVec3 centerCell)
        {
            return IsValidBounceTarget(pawn, launcherPawn, map)
                   && (!(Ext?.skipDownedBounceTargets ?? true) || !pawn.Downed)
                   && (!(Ext?.requireBounceLineOfSight ?? true) || GenSight.LineOfSight(centerCell, pawn.Position, map, skipFirstCell: true))
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

        private float VisualArcOffset
        {
            get
            {
                if (visualArcDurationTicks <= 0)
                {
                    visualArcDurationTicks = Mathf.Max(1, Mathf.Max(lifetime, ticksToImpact));
                }

                float progress = Mathf.Clamp01(visualArcElapsedTicks / (float)visualArcDurationTicks);
                return Mathf.Max(0f, Ext?.visualArcHeight ?? 0.9f) * GenMath.InverseParabola(progress);
            }
        }

        private void DrawGlow(Vector3 drawLoc)
        {
            Texture2D texture = def.graphic?.MatSingle?.mainTexture as Texture2D;
            if (texture == null)
            {
                return;
            }

            Color glowColor = Ext?.glowColor ?? new Color(1f, 0.62f, 0.24f, 0.58f);
            if (cachedGlowMaterial == null || cachedGlowTexture != texture)
            {
                cachedGlowTexture = texture;
                cachedGlowMaterial = MaterialPool.MatFrom(texture, ShaderDatabase.MoteGlow, glowColor);
            }

            float scale = Mathf.Max(1f, Ext?.glowScale ?? 1.28f);
            Vector2 size = def.graphicData.drawSize * scale;
            Graphics.DrawMesh(
                MeshPool.GridPlane(size),
                drawLoc - Altitudes.AltIncVect * 0.1f,
                ExactRotation,
                cachedGlowMaterial,
                0);
        }
    }
}

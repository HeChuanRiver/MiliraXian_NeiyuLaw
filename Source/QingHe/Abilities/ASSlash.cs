using System.Collections.Generic;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Things.Weapons;
using MiliraXian.Characters.QingHe.Vfx;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Abilities
{
    internal sealed class ASSlash
    {
        private enum SlashStage
        {
            None,
            TakeoffDelay,
            Ascending,
            Hover,
            Descending,
            ImpactDelay,
            ImpactSlashes
        }

        private readonly CompProperties_AbilityAscentSlash props;

        private SlashStage stage;
        private IntVec3 firstImpactCell = IntVec3.Invalid;
        private IntVec3 takeoffCell = IntVec3.Invalid;
        private IntVec3 impactCell = IntVec3.Invalid;
        private IntVec3 landingCell = IntVec3.Invalid;
        private IntVec3 directionCell = IntVec3.Invalid;
        private Thing trackedTarget;
        private int stageStartTick = -1;
        private int stageEndTick = -1;
        private int slashIndex;
        private int slashHitCount;
        private float slashDamage;
        private float slashBaseAngle;

        public bool Active => stage != SlashStage.None;

        private int SlashTotalCount => Mathf.Max(1, props.empoweredSlashCount);

        private int NextSlashTick => stageStartTick
            + slashIndex * Mathf.Max(1, props.empoweredSlashIntervalTicks);

        public ASSlash(CompProperties_AbilityAscentSlash props)
        {
            this.props = props;
        }

        public void Start(Pawn caster, ASDashResult dashResult)
        {
            if (caster == null || !caster.Spawned || caster.MapHeld == null || !dashResult.StartsSlash)
            {
                return;
            }

            firstImpactCell = dashResult.FirstImpactCell;
            takeoffCell = dashResult.TakeoffCell;
            directionCell = dashResult.DirectionCell;
            trackedTarget = dashResult.TrackedTarget;
            impactCell = IntVec3.Invalid;
            landingCell = IntVec3.Invalid;
            ResetSlashState();

            AscentSlashActionUtility.AddInvulnerability(caster);
            int actionTicks = Mathf.Max(1, props.takeoffDelayTicks)
                + Mathf.Max(1, props.ascentTicks)
                + Mathf.Max(1, props.hoverTicks)
                + Mathf.Max(1, props.descentTicks)
                + Mathf.Max(1, props.impactDelayTicks)
                + Mathf.Max(0, props.empoweredSlashCount - 1) * Mathf.Max(1, props.empoweredSlashIntervalTicks);
            caster.stances?.stunner?.StunFor(actionTicks + 5, caster, addBattleLog: false, showMote: false);
            BeginStage(SlashStage.TakeoffDelay, props.takeoffDelayTicks);
        }

        public void Tick(Pawn caster, Map map)
        {
            if (!Active)
            {
                return;
            }

            int now = AscentSlashActionUtility.CurrentTick;
            switch (stage)
            {
                case SlashStage.TakeoffDelay:
                    TickTakeoffDelay(caster, map, now);
                    break;
                case SlashStage.Ascending:
                    TickAscent(caster, now);
                    break;
                case SlashStage.Hover:
                    TickHover(caster, map, now);
                    break;
                case SlashStage.Descending:
                    TickDescent(caster, map, now);
                    break;
                case SlashStage.ImpactDelay:
                    TickImpactDelay(caster, now);
                    break;
                case SlashStage.ImpactSlashes:
                    TickImpactSlashes(caster, map, now);
                    break;
            }
        }

        public void Cancel(Pawn caster)
        {
            if (Active)
            {
                Complete(caster);
            }
        }

        public void ExposeData()
        {
            int stageValue = (int)stage;
            Scribe_Values.Look(ref stageValue, "mx_qh_asSlash_stage", 0);
            Scribe_Values.Look(ref firstImpactCell, "mx_qh_asSlash_firstImpactCell", IntVec3.Invalid);
            Scribe_Values.Look(ref impactCell, "mx_qh_asSlash_impactCell", IntVec3.Invalid);
            Scribe_Values.Look(ref landingCell, "mx_qh_asSlash_landingCell", IntVec3.Invalid);
            Scribe_Values.Look(ref directionCell, "mx_qh_asSlash_directionCell", IntVec3.Invalid);
            Scribe_References.Look(ref trackedTarget, "mx_qh_asSlash_trackedTarget");
            Scribe_Values.Look(ref stageStartTick, "mx_qh_asSlash_stageStartTick", -1);
            Scribe_Values.Look(ref stageEndTick, "mx_qh_asSlash_stageEndTick", -1);
            Scribe_Values.Look(ref slashIndex, "mx_qh_asSlash_index", 0);
            Scribe_Values.Look(ref slashHitCount, "mx_qh_asSlash_hitCount", 0);
            stage = (SlashStage)stageValue;
        }

        public void RestoreAfterLoad(Pawn caster)
        {
            if (caster == null)
            {
                return;
            }

            if (stage is SlashStage.TakeoffDelay
                or SlashStage.Ascending
                or SlashStage.Hover
                or SlashStage.Descending)
            {
                takeoffCell = caster.Position;
            }

        }

        public bool TryApplyDrawPos(ref Vector3 drawPos)
        {
            if (!Active || stage is SlashStage.TakeoffDelay or SlashStage.ImpactDelay or SlashStage.ImpactSlashes)
            {
                return false;
            }

            float progress = stageEndTick > stageStartTick
                ? Mathf.Clamp01((AscentSlashActionUtility.CurrentTick - stageStartTick) / (float)(stageEndTick - stageStartTick))
                : 1f;

            if (stage == SlashStage.Descending)
            {
                float easedProgress = Mathf.Pow(progress, props.descentAccelerationPower);
                float height = 1f - easedProgress;
                drawPos = Vector3.Lerp(
                        takeoffCell.ToVector3ShiftedWithAltitude(AltitudeLayer.Pawn),
                        landingCell.ToVector3ShiftedWithAltitude(AltitudeLayer.Pawn),
                        easedProgress)
                    + Altitudes.AltIncVect * (props.secondStageMaxAltitudeLayers * height)
                    + Vector3.forward * (props.secondStageMaxForwardOffset * height);
                return true;
            }

            float altitude = stage == SlashStage.Hover
                ? 1f
                : 1f - Mathf.Pow(1f - progress, props.ascentDecelerationPower);
            drawPos += Altitudes.AltIncVect * (props.secondStageMaxAltitudeLayers * altitude)
                + Vector3.forward * (props.secondStageMaxForwardOffset * altitude);
            return true;
        }

        private void TickTakeoffDelay(Pawn caster, Map map, int now)
        {
            if (now < stageEndTick)
            {
                return;
            }

            AscentSlashActionUtility.BreakRoofAt(map, takeoffCell, allowThickRoof: false);
            PlayTakeoffVisuals(map, caster.Position);
            BeginStage(SlashStage.Ascending, props.ascentTicks);
        }

        private void TickAscent(Pawn caster, int now)
        {
            if (now >= stageEndTick)
            {
                BeginStage(SlashStage.Hover, props.hoverTicks);
            }
        }

        private void TickHover(Pawn caster, Map map, int now)
        {
            if (now < stageEndTick)
            {
                return;
            }

            ResolveDestination(caster, map);
            if (!impactCell.IsValid || !impactCell.InBounds(map))
            {
                impactCell = firstImpactCell.InBounds(map) ? firstImpactCell : caster.Position;
            }

            landingCell = AscentSlashActionUtility.FindNearestLandingCell(map, impactCell, caster, caster.Position);
            AscentSlashActionUtility.BreakRoofAt(map, landingCell, allowThickRoof: true);
            BeginStage(SlashStage.Descending, props.descentTicks);
            props.dropSound?.PlayOneShot(new TargetInfo(landingCell, map));
            MX_QHGraphicsUtility.Fleck(map, impactCell, props.impactFleck, 1.1f);
        }

        private void TickDescent(Pawn caster, Map map, int now)
        {
            if (now < stageEndTick)
            {
                return;
            }

            IntVec3 resolvedLanding = AscentSlashActionUtility.FindNearestLandingCell(map, landingCell, caster, caster.Position);
            if (resolvedLanding.IsValid && resolvedLanding.InBounds(map) && resolvedLanding != caster.Position)
            {
                landingCell = resolvedLanding;
                caster.Position = resolvedLanding;
                caster.Notify_Teleported(endCurrentJob: false);
            }

            BeginStage(SlashStage.ImpactDelay, Mathf.Max(1, props.impactDelayTicks));
        }

        private void TickImpactDelay(Pawn caster, int now)
        {
            if (now >= stageEndTick)
            {
                ResolveImpact(caster, impactCell, directionCell);
            }
        }

        private void ResolveImpact(Pawn caster, IntVec3 center, IntVec3 facingCell)
        {
            Map map = caster?.MapHeld;
            if (caster == null || map == null || !center.IsValid || !facingCell.IsValid)
            {
                Complete(caster);
                return;
            }

            caster.rotationTracker?.FaceCell(facingCell);
            PlayVisuals(map, center, center, props.exitEffecter, props.exitFleck, 1.15f);
            MX_QHGraphicsUtility.Fx(map, center, props.impactEffecter, 1f);
            MX_QHGraphicsUtility.Fleck(map, center, props.impactFleck, Mathf.Max(0.8f, props.secondImpactRadius * 0.18f));
            props.castSound?.PlayOneShot(new TargetInfo(center, map));

            HediffComp_SwordPressure pressure = MX_QH_HediffUtility.EnsureSwordPressure(caster);
            float consumedPressure = pressure != null && pressure.CompletedPoints >= 1 ? pressure.ConsumeAll() : 0f;
            float specialFactor = MX_QHSkillUtility.GetSpecialAbilityEffectFactor(caster);
            if (consumedPressure >= 1f)
            {
                float damage = props.damageAmount * specialFactor
                    * (1f + consumedPressure * Mathf.Max(0f, props.empoweredDamagePerPressurePoint));
                BeginEmpoweredSlashes(caster, map, center, damage);
                return;
            }

            Thing target = trackedTarget;
            if (target == null || target.Destroyed || !target.Spawned || target.MapHeld != map)
            {
                target = FindNearestTarget(caster, map, center, 1.5f, requireLineOfSight: true);
            }

            int hitCount = 0;
            if (target != null)
            {
                float damage = props.damageAmount * specialFactor * (target is Building ? props.buildingDamageMultiplier : 1f);
                for (int i = 0; i < Mathf.Max(1, props.normalSlashCount); i++)
                {
                    props.slashSound?.PlayOneShot(new TargetInfo(target.Position, map));
                    QingheSwordCombatUtility.ApplySlash(caster, target, damage, props.armorPenetration, empowered: false);
                    hitCount++;
                }
            }

            if (hitCount > 0)
            {
                pressure?.StartRecovery(props.postHitRecoveryPoints, props.postHitRecoveryTicks);
            }
            Complete(caster);
        }

        private void BeginEmpoweredSlashes(Pawn caster, Map map, IntVec3 center, float damage)
        {
            if (caster == null || map == null || !center.IsValid)
            {
                Complete(caster);
                return;
            }

            slashIndex = 0;
            slashHitCount = 0;
            slashDamage = damage;
            slashBaseAngle = Rand.Range(0f, 360f / SlashTotalCount);
            BeginStage(
                SlashStage.ImpactSlashes,
                Mathf.Max(1, (SlashTotalCount - 1) * Mathf.Max(1, props.empoweredSlashIntervalTicks) + 1));
            ResolveNextSlash(caster, map);
        }

        private void TickImpactSlashes(Pawn caster, Map map, int now)
        {
            if (now >= NextSlashTick)
            {
                ResolveNextSlash(caster, map);
            }
        }

        private void ResolveNextSlash(Pawn caster, Map map)
        {
            if (caster == null || map == null || !impactCell.IsValid || slashIndex >= SlashTotalCount)
            {
                Complete(caster);
                return;
            }

            Thing target = ResolveSlashTarget(caster, map);
            if (target != null)
            {
                trackedTarget = target;
                props.slashSound?.PlayOneShot(new TargetInfo(target.Position, map));
                PlaySlashFleck(map, target);
                slashHitCount += QingheSwordCombatUtility.ApplyRadius(
                    caster,
                    target.Position,
                    Mathf.Max(0f, props.empoweredSlashRadius),
                    slashDamage,
                    props.armorPenetration,
                    empowered: true);
            }

            slashIndex++;
            if (slashIndex >= SlashTotalCount)
            {
                if (slashHitCount > 0)
                {
                    MX_QH_HediffUtility.EnsureSwordPressure(caster)?.StartRecovery(props.postHitRecoveryPoints, props.postHitRecoveryTicks);
                }
                Complete(caster);
                return;
            }

        }

        private Thing ResolveSlashTarget(Pawn caster, Map map)
        {
            float radius = Mathf.Max(0f, props.secondImpactRadius);
            return IsValidTarget(caster, map, trackedTarget, radius)
                ? trackedTarget
                : FindNearestTarget(caster, map, impactCell, radius, requireLineOfSight: false);
        }

        private static Thing FindNearestTarget(
            Pawn caster,
            Map map,
            IntVec3 centerCell,
            float radius,
            bool requireLineOfSight)
        {
            Thing nearest = null;
            float nearestDistanceSquared = float.MaxValue;
            Vector3 center = centerCell.ToVector3Shifted();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(centerCell, Mathf.Max(0f, radius), true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                foreach (Thing candidate in cell.GetThingList(map))
                {
                    if (!IsValidTarget(caster, map, candidate, radius, centerCell)
                        || requireLineOfSight && !GenSight.LineOfSight(centerCell, candidate.Position, map, true))
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

        private bool IsValidTarget(Pawn caster, Map map, Thing target, float radius)
        {
            return IsValidTarget(caster, map, target, radius, impactCell);
        }

        private static bool IsValidTarget(Pawn caster, Map map, Thing target, float radius, IntVec3 center)
        {
            return AscentSlashActionUtility.CanHit(caster, target)
                && target.MapHeld == map
                && target.Position.DistanceToSquared(center) <= radius * radius;
        }

        private void ResolveDestination(Pawn caster, Map map)
        {
            IntVec3 start = takeoffCell.IsValid && takeoffCell.InBounds(map) ? takeoffCell : caster.Position;
            IntVec3 desired = start;
            Vector3 trackingDirection = AscentSlashActionUtility.ComputeForward(firstImpactCell, directionCell);

            if (trackedTarget != null && !trackedTarget.Destroyed && trackedTarget.Spawned && trackedTarget.MapHeld == map)
            {
                IntVec3 currentTargetCell = trackedTarget.Position;
                Vector3 moved = (currentTargetCell - firstImpactCell).ToVector3().Yto0();
                if (moved.sqrMagnitude > 0.001f)
                {
                    trackingDirection = moved.normalized;
                }

                desired = firstImpactCell.DistanceTo(currentTargetCell) <= props.secondStageTrackingRange
                    ? start + moved.ToIntVec3()
                    : start + (trackingDirection * props.secondStageLimitedFollowDistance).ToIntVec3();
            }

            impactCell = AscentSlashActionUtility.ClampToMap(desired, map);
            AscentSlashActionUtility.BreakRoofAt(map, impactCell, allowThickRoof: true);
            if (trackingDirection.sqrMagnitude < 0.001f)
            {
                trackingDirection = AscentSlashActionUtility.ComputeForward(takeoffCell, firstImpactCell);
            }

            directionCell = impactCell + trackingDirection.ToIntVec3();
            if (directionCell == impactCell)
            {
                directionCell = AscentSlashActionUtility.ComputeDirectionCell(takeoffCell, impactCell);
            }
        }

        private void PlaySlashFleck(Map map, Thing target)
        {
            if (map == null || props.empoweredSlashFleck == null || target == null)
            {
                return;
            }

            float angleStep = 360f / SlashTotalCount;
            FleckCreationData data = FleckMaker.GetDataStatic(
                target.DrawPos,
                map,
                props.empoweredSlashFleck,
                Mathf.Max(0.01f, props.empoweredSlashVisualScale));
            data.rotation = slashBaseAngle + angleStep * slashIndex
                + Rand.Range(-Mathf.Max(0f, props.empoweredSlashVisualAngleJitter), Mathf.Max(0f, props.empoweredSlashVisualAngleJitter));
            data.rotationRate = 0f;
            map.flecks.CreateFleck(data);
        }

        private void BeginStage(SlashStage nextStage, int durationTicks)
        {
            stage = nextStage;
            stageStartTick = AscentSlashActionUtility.CurrentTick;
            stageEndTick = stageStartTick + Mathf.Max(1, durationTicks);
        }

        private void Complete(Pawn caster)
        {
            AscentSlashActionUtility.RemoveInvulnerability(caster);
            stage = SlashStage.None;
            trackedTarget = null;
            stageStartTick = -1;
            stageEndTick = -1;
            ResetSlashState();
        }

        private void ResetSlashState()
        {
            slashIndex = 0;
            slashHitCount = 0;
            slashDamage = 0f;
            slashBaseAngle = 0f;
        }

        private void PlayTakeoffVisuals(Map map, IntVec3 origin)
        {
            PlaySizedFleck(map, origin, props.takeoffGroundFleck, props.takeoffGroundFleckSize, props.takeoffGroundFleckOffset);
            PlaySizedFleck(map, origin, props.ascentTrailFleck, props.ascentTrailFleckSize, props.ascentTrailFleckOffset);
            MX_QHGraphicsUtility.Fx(map, origin, props.entryEffecter, 1f);
            MX_QHGraphicsUtility.Fleck(map, origin, props.entryFleck, 1f);
        }

        private static void PlayVisuals(Map map, IntVec3 source, IntVec3 cell, string effecter, string fleck, float scale)
        {
            MX_QHGraphicsUtility.Fx(map, cell, effecter, scale);
            MX_QHGraphicsUtility.Fleck(map, cell, fleck, scale);
            if (source.IsValid && source.InBounds(map) && source != cell)
            {
                GenDraw.DrawLineBetween(source.ToVector3Shifted(), cell.ToVector3Shifted());
            }
        }

        private static void PlaySizedFleck(Map map, IntVec3 origin, string defName, Vector2 size, Vector2 offset)
        {
            if (map == null || !origin.IsValid || defName.NullOrEmpty())
            {
                return;
            }

            FleckDef fleckDef = DefDatabase<FleckDef>.GetNamedSilentFail(defName);
            GraphicData graphicData = fleckDef?.graphicData;
            if (graphicData == null)
            {
                return;
            }

            FleckCreationData data = FleckMaker.GetDataStatic(
                origin.ToVector3Shifted() + new Vector3(offset.x, 0f, offset.y),
                map,
                fleckDef);
            Vector2 baseSize = graphicData.drawSize;
            data.exactScale = new Vector3(
                Mathf.Max(0.01f, size.x) / Mathf.Max(0.01f, baseSize.x),
                1f,
                Mathf.Max(0.01f, size.y) / Mathf.Max(0.01f, baseSize.y));
            map.flecks.CreateFleck(data);
        }
    }
}

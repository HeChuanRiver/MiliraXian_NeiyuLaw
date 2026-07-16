using System.Collections.Generic;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Things.Weapons;
using MiliraXian.Characters.QingHe.Vfx;
using MiliraXian.Characters.Vfx;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Abilities
{
    public class CompProperties_AbilityAscentSlash : CompProperties_AbilityEffect
    {
        public float range = 22f;
        public float dashSpeedCellsPerSecond = 60f;
        public int dashDurationMinTicks = 8;
        public float dashCollisionRadius = 1.35f;
        public float dashImpactRadius = 1.8f;
        public float dashDamageAmount = 20f;
        public int ascentTicks = 16;
        public int hoverTicks = 12;
        public int descentTicks = 12;
        public int takeoffDelayTicks = 8;
        public float ascentDecelerationPower = 2.2f;
        public float descentAccelerationPower = 2.4f;
        public float secondStageTrackingRange = 8f;
        public float secondStageLimitedFollowDistance = 2.5f;
        public float secondStageMaxForwardOffset = 12f;
        public float secondStageMaxAltitudeLayers = 48f;
        public float secondImpactRadius = 2f;
        public float damageAmount = 32f;
        public float armorPenetration = 0.35f;
        public float buildingDamageMultiplier = 2f;
        public float empoweredDamagePerPressurePoint = 0.25f;
        public int empoweredSlashCount = 7;
        public int empoweredSlashIntervalTicks = 6;
        public float empoweredSlashRadius = 1f;
        public FleckDef empoweredSlashFleck;
        public float empoweredSlashVisualScale = 3.2f;
        public float empoweredSlashVisualAngleJitter = 12f;
        public int normalSlashCount = 3;
        public float postHitRecoveryPoints = 1f;
        public int postHitRecoveryTicks = 300;
        public int impactDelayTicks = 30;

        public string disabledReason = "MX_QH_AscentSlashNotLearned";
        public string invalidLandingMessage = "MX_QH_AscentSlashInvalidLanding";

        public string entryEffecter;
        public string exitEffecter;
        public string impactEffecter = "ImpactSmallDustCloud";
        public string takeoffGroundFleck = "MXNL_Skyfall_FlyBegin_G";
        public Vector2 takeoffGroundFleckSize = new(9.6f, 3.8f);
        public Vector2 takeoffGroundFleckOffset = Vector2.zero;
        public string entryFleck;
        public string exitFleck;
        public string impactFleck = "ExplosionFlash";
        public string ascentTrailFleck = "MXNL_Skyfall_FlyBegin_F";
        public Vector2 ascentTrailFleckSize = new(2.2f, 20.2f);
        public Vector2 ascentTrailFleckOffset = new(0f, 8f);
        public SoundDef castSound;
        public SoundDef dropSound;
        public SoundDef slashSound;

        public CompProperties_AbilityAscentSlash()
        {
            compClass = typeof(CompAbilityEffect_AscentSlash);
        }
    }

    public class CompAbilityEffect_AscentSlash : CompAbilityEffect
    {
        private enum AscentSlashStage
        {
            None,
            Dash,
            TakeoffDelay,
            Ascending,
            Hover,
            Descending,
            ImpactDelay,
            ImpactSlashes
        }

        private static readonly Color ConePreviewColor = new(1f, 0.45f, 0.65f, 0.55f);
        private AscentSlashStage stage;
        private IntVec3 originCell = IntVec3.Invalid;
        private IntVec3 dashEndCell = IntVec3.Invalid;
        private IntVec3 lastSafeDashCell = IntVec3.Invalid;
        private IntVec3 firstImpactCell = IntVec3.Invalid;
        private IntVec3 secondStageTakeoffCell = IntVec3.Invalid;
        private IntVec3 secondImpactCell = IntVec3.Invalid;
        private IntVec3 secondLandingCell = IntVec3.Invalid;
        private IntVec3 secondDirectionCell = IntVec3.Invalid;
        private Vector3 dashStartPos;
        private Vector3 dashEndPos;
        private Vector3 previousDashPos;
        private Thing trackedTarget;
        private int stageStartTick = -1;
        private int stageEndTick = -1;
        private IntVec3 empoweredSlashCenter = IntVec3.Invalid;
        private int empoweredSlashTotalCount;
        private int empoweredSlashIndex;
        private int empoweredSlashNextTick = -1;
        private int empoweredSlashHitCount;
        private float empoweredSlashDamage;
        private float empoweredSlashBaseAngle;

        public new CompProperties_AbilityAscentSlash Props => (CompProperties_AbilityAscentSlash)props;

        public override bool GizmoDisabled(out string reason)
        {
            if (!HasLearnedJueying(parent?.pawn))
            {
                reason = Props.disabledReason.Translate();
                return true;
            }

            if (stage != AscentSlashStage.None)
            {
                reason = "MX_QH_AscentSlashInProgress".Translate();
                return true;
            }

            return base.GizmoDisabled(out reason);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
            {
                return false;
            }

            Pawn caster = parent?.pawn;
            if (!HasLearnedJueying(caster))
            {
                if (throwMessages)
                {
                    Messages.Message(Props.disabledReason.Translate(), caster, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            if (stage != AscentSlashStage.None)
            {
                return false;
            }

            return ValidateAim(caster, target, throwMessages);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            int stageValue = (int)stage;
            Scribe_Values.Look(ref stageValue, "mx_qh_ascentSlash_stage", 0);
            Scribe_Values.Look(ref originCell, "mx_qh_ascentSlash_originCell", IntVec3.Invalid);
            Scribe_Values.Look(ref dashEndCell, "mx_qh_ascentSlash_dashEndCell", IntVec3.Invalid);
            Scribe_Values.Look(ref lastSafeDashCell, "mx_qh_ascentSlash_lastSafeDashCell", IntVec3.Invalid);
            Scribe_Values.Look(ref firstImpactCell, "mx_qh_ascentSlash_firstImpactCell", IntVec3.Invalid);
            Scribe_Values.Look(ref secondStageTakeoffCell, "mx_qh_ascentSlash_secondStageTakeoffCell", IntVec3.Invalid);
            Scribe_Values.Look(ref secondImpactCell, "mx_qh_ascentSlash_secondImpactCell", IntVec3.Invalid);
            Scribe_Values.Look(ref secondLandingCell, "mx_qh_ascentSlash_secondLandingCell", IntVec3.Invalid);
            Scribe_Values.Look(ref secondDirectionCell, "mx_qh_ascentSlash_secondDirectionCell", IntVec3.Invalid);
            Scribe_Values.Look(ref dashStartPos, "mx_qh_ascentSlash_dashStartPos", Vector3.zero);
            Scribe_Values.Look(ref dashEndPos, "mx_qh_ascentSlash_dashEndPos", Vector3.zero);
            Scribe_Values.Look(ref previousDashPos, "mx_qh_ascentSlash_previousDashPos", Vector3.zero);
            Scribe_References.Look(ref trackedTarget, "mx_qh_ascentSlash_trackedTarget");
            Scribe_Values.Look(ref stageStartTick, "mx_qh_ascentSlash_stageStartTick", -1);
            Scribe_Values.Look(ref stageEndTick, "mx_qh_ascentSlash_stageEndTick", -1);
            Scribe_Values.Look(ref empoweredSlashCenter, "mx_qh_ascentSlash_empoweredSlashCenter", IntVec3.Invalid);
            Scribe_Values.Look(ref empoweredSlashTotalCount, "mx_qh_ascentSlash_empoweredSlashTotalCount", 0);
            Scribe_Values.Look(ref empoweredSlashIndex, "mx_qh_ascentSlash_empoweredSlashIndex", 0);
            Scribe_Values.Look(ref empoweredSlashNextTick, "mx_qh_ascentSlash_empoweredSlashNextTick", -1);
            Scribe_Values.Look(ref empoweredSlashHitCount, "mx_qh_ascentSlash_empoweredSlashHitCount", 0);
            Scribe_Values.Look(ref empoweredSlashDamage, "mx_qh_ascentSlash_empoweredSlashDamage", 0f);
            Scribe_Values.Look(ref empoweredSlashBaseAngle, "mx_qh_ascentSlash_empoweredSlashBaseAngle", 0f);
            stage = (AscentSlashStage)stageValue;
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                RestoreVisualState(parent?.pawn);
            }
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            if (caster == null || !caster.Spawned || caster.MapHeld == null)
            {
                return;
            }

            if (!ValidateAim(caster, target, true))
            {
                return;
            }

            Map map = caster.MapHeld;
            originCell = caster.Position;
            dashStartPos = caster.DrawPos;
            dashEndCell = ComputeDashEndCell(originCell, target.Cell, map, Props.range);
            dashEndPos = dashEndCell.ToVector3Shifted();
            previousDashPos = dashStartPos;
            lastSafeDashCell = originCell;
            firstImpactCell = IntVec3.Invalid;
            secondStageTakeoffCell = IntVec3.Invalid;
            secondImpactCell = IntVec3.Invalid;
            secondLandingCell = IntVec3.Invalid;
            secondDirectionCell = IntVec3.Invalid;
            trackedTarget = null;

            AddAscentSlashInvulnerability(caster);
            caster.pather?.StopDead();
            caster.rotationTracker?.FaceCell(dashEndCell);
            int dashTicks = ResolveDashDurationTicks(dashStartPos, dashEndPos);
            caster.stances?.stunner?.StunFor(dashTicks + 2, caster, addBattleLog: false, showMote: false);
            BeginStage(AscentSlashStage.Dash, dashTicks);
            AscentSlashVisualTracker.BeginDash(caster, stageStartTick, stageEndTick, dashStartPos, dashEndPos);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (stage == AscentSlashStage.None)
            {
                return;
            }

            Pawn caster = parent?.pawn;
            Map map = caster?.MapHeld;
            if (caster == null || caster.Destroyed || !caster.Spawned || map == null)
            {
                ClearSequence(caster);
                return;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : stageEndTick;
            switch (stage)
            {
                case AscentSlashStage.Dash:
                    TickDash(caster, map, now);
                    break;
                case AscentSlashStage.TakeoffDelay:
                    TickTakeoffDelay(caster, map, now);
                    break;
                case AscentSlashStage.Ascending:
                    TickAscent(caster, now);
                    break;
                case AscentSlashStage.Hover:
                    TickHover(caster, map, now);
                    break;
                case AscentSlashStage.Descending:
                    TickDescent(caster, map, now);
                    break;
                case AscentSlashStage.ImpactDelay:
                    TickImpactDelay(caster, now);
                    break;
                case AscentSlashStage.ImpactSlashes:
                    TickImpactSlashes(caster, map, now);
                    break;
            }
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            DrawLandingPreview(target);
        }

        public void DrawLandingPreview(LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || !caster.Spawned)
            {
                return;
            }

            string reason;
            if (!CanAim(caster, target, out reason))
            {
                return;
            }

            IntVec3 endCell = ComputeDashEndCell(caster.Position, target.Cell, caster.MapHeld, Props.range);
            GenDraw.DrawLineBetween(caster.Position.ToVector3Shifted(), endCell.ToVector3Shifted());
            GenDraw.DrawRadiusRing(endCell, Props.dashImpactRadius, ConePreviewColor);
        }

        private bool ValidateAim(Pawn caster, LocalTargetInfo target, bool showMessages)
        {
            string reason;
            if (!CanAim(caster, target, out reason))
            {
                return Reject(reason, caster, target, showMessages);
            }

            return true;
        }

        private bool CanAim(Pawn caster, LocalTargetInfo target, out string reason)
        {
            Map map = caster?.MapHeld;
            if (caster == null || map == null || !target.IsValid || !target.Cell.InBounds(map))
            {
                reason = Props.invalidLandingMessage.Translate();
                return false;
            }

            if (Props.range > 0f && caster.Position.DistanceTo(target.Cell) > Props.range)
            {
                reason = "AbilityOutOfRange".Translate();
                return false;
            }

            if (caster.Position == target.Cell)
            {
                reason = Props.invalidLandingMessage.Translate();
                return false;
            }

            reason = null;
            return true;
        }

        private bool Reject(string message, Pawn caster, LocalTargetInfo target, bool showMessages)
        {
            if (showMessages)
            {
                LookTargets lookTargets = caster != null && target.IsValid && caster.MapHeld != null
                    ? new LookTargets(caster, target.ToTargetInfo(caster.MapHeld))
                    : null;
                Messages.Message(message, lookTargets, MessageTypeDefOf.RejectInput, historical: false);
            }

            return false;
        }

        private static IntVec3 ComputeDirectionCell(IntVec3 origin, IntVec3 landing)
        {
            IntVec3 offset = landing - origin;
            offset.y = 0;
            if (offset.x == 0 && offset.z == 0)
            {
                return landing + IntVec3.North;
            }

            return landing + offset;
        }

        private static bool HasLearnedJueying(Pawn pawn)
        {
            return MX_QH_HediffUtility.EnsureFlowerResonance(pawn)?.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Jueying) == true;
        }

        private void ResolveSecondImpact(Pawn caster, IntVec3 landing, IntVec3 directionCell)
        {
            Map map = caster?.MapHeld;
            if (caster == null || map == null || !landing.IsValid || !directionCell.IsValid)
            {
                return;
            }

            caster.rotationTracker?.FaceCell(directionCell);
            PlayVisuals(map, landing, landing, Props.exitEffecter, Props.exitFleck, 1.15f);
            MX_QHGraphicsUtility.Fx(map, landing, Props.impactEffecter, 1f);
            MX_QHGraphicsUtility.Fleck(map, landing, Props.impactFleck, Mathf.Max(0.8f, Props.secondImpactRadius * 0.18f));
            Props.castSound?.PlayOneShot(new TargetInfo(landing, map));

            HediffComp_SwordPressure pressure = MX_QH_HediffUtility.EnsureSwordPressure(caster);
            float consumedPressure = pressure != null && pressure.CompletedPoints >= 1 ? pressure.ConsumeAll() : 0f;
            float specialFactor = MX_QHSkillUtility.GetSpecialAbilityEffectFactor(caster);
            int hitCount = 0;
            if (consumedPressure >= 1f)
            {
                float damage = Props.damageAmount * specialFactor * (1f + consumedPressure * Mathf.Max(0f, Props.empoweredDamagePerPressurePoint));
                BeginEmpoweredImpactSlashes(caster, map, landing, Mathf.Max(1, Props.empoweredSlashCount), damage);
                return;
            }
            else
            {
                Thing target = trackedTarget;
                if (target == null || target.Destroyed || !target.Spawned || target.MapHeld != map)
                {
                    target = FirstDashTargetAt(caster, map, landing, 1.5f);
                }

                if (target != null)
                {
                    float damage = Props.damageAmount * specialFactor * (target is Building ? Props.buildingDamageMultiplier : 1f);
                    for (int i = 0; i < Mathf.Max(1, Props.normalSlashCount); i++)
                    {
                        Props.slashSound?.PlayOneShot(new TargetInfo(target.Position, map));
                        QingheSwordCombatUtility.ApplySlash(caster, target, damage, Props.armorPenetration, empowered: false);
                        hitCount++;
                    }
                }
            }

            if (hitCount > 0)
            {
                pressure?.StartRecovery(Props.postHitRecoveryPoints, Props.postHitRecoveryTicks);
            }
            ClearSequence(caster);
        }

        private void BeginEmpoweredImpactSlashes(Pawn caster, Map map, IntVec3 landing, int slashCount, float damage)
        {
            if (caster == null || map == null || !landing.IsValid || slashCount <= 0)
            {
                ClearSequence(caster);
                return;
            }

            empoweredSlashCenter = landing;
            empoweredSlashTotalCount = slashCount;
            empoweredSlashIndex = 0;
            empoweredSlashHitCount = 0;
            empoweredSlashDamage = damage;
            empoweredSlashBaseAngle = Rand.Range(0f, 360f / slashCount);
            BeginStage(AscentSlashStage.ImpactSlashes, Mathf.Max(1, (slashCount - 1) * Mathf.Max(1, Props.empoweredSlashIntervalTicks) + 1));
            empoweredSlashNextTick = Find.TickManager != null ? Find.TickManager.TicksGame : stageStartTick;
            ResolveNextEmpoweredImpactSlash(caster, map);
        }

        private void TickDash(Pawn caster, Map map, int now)
        {
            int predictedTick = Mathf.Min(now + 1, stageEndTick);
            float progress = StageProgress(predictedTick);
            float easedProgress = 1f - Mathf.Pow(1f - progress, 2.6f);
            Vector3 predictedPos = Vector3.Lerp(dashStartPos, dashEndPos, easedProgress);
            predictedPos.y = dashStartPos.y;

            if (TryResolveDashCollision(caster, map, previousDashPos, predictedPos, out Thing hitThing, out bool triggersSecondStage, out IntVec3 impactCell))
            {
                trackedTarget = triggersSecondStage ? hitThing : null;
                firstImpactCell = impactCell;
                FinishDash(caster, map, lastSafeDashCell, impactCell, hitThing, triggersSecondStage);
                return;
            }

            previousDashPos = predictedPos;
            TryAddDashAfterimage(caster, map, predictedPos, now);

            if (predictedTick >= stageEndTick)
            {
                IntVec3 landingCell = FindNearestLandingCell(map, dashEndCell, caster, lastSafeDashCell);
                FinishDash(caster, map, landingCell, dashEndCell, null, triggersSecondStage: false);
            }
        }

        private void TickAscent(Pawn caster, int now)
        {
            if (now >= stageEndTick)
            {
                BeginStage(AscentSlashStage.Hover, Props.hoverTicks);
                BeginHoverVisual(caster);
            }
        }

        private void TickTakeoffDelay(Pawn caster, Map map, int now)
        {
            if (now < stageEndTick)
            {
                return;
            }

            BreakRoofAt(map, secondStageTakeoffCell, allowThickRoof: false);
            PlayTakeoffVisuals(map, caster.Position);
            BeginStage(AscentSlashStage.Ascending, Props.ascentTicks);
            BeginAscentVisual(caster);
        }

        private void TickHover(Pawn caster, Map map, int now)
        {
            if (now < stageEndTick)
            {
                return;
            }

            ResolveSecondStageDestination(caster, map);
            if (!secondImpactCell.IsValid || !secondImpactCell.InBounds(map))
            {
                secondImpactCell = firstImpactCell.InBounds(map) ? firstImpactCell : caster.Position;
            }

            secondLandingCell = FindNearestLandingCell(map, secondImpactCell, caster, caster.Position);
            BreakRoofAt(map, secondLandingCell, allowThickRoof: true);
            if (secondLandingCell.IsValid && secondLandingCell.InBounds(map) && secondLandingCell != caster.Position)
            {
                caster.Position = secondLandingCell;
                caster.Notify_Teleported(endCurrentJob: false);
            }

            BeginStage(AscentSlashStage.Descending, Props.descentTicks);
            Props.dropSound?.PlayOneShot(new TargetInfo(caster.Position, map));
            BeginDescentVisual(caster);
            MX_QHGraphicsUtility.Fleck(map, secondImpactCell, Props.impactFleck, 1.1f);
        }

        private void TickDescent(Pawn caster, Map map, int now)
        {
            if (now < stageEndTick)
            {
                return;
            }

            AscentSlashVisualTracker.Clear(caster);
            BeginStage(AscentSlashStage.ImpactDelay, Mathf.Max(1, Props.impactDelayTicks));
        }

        private void TickImpactDelay(Pawn caster, int now)
        {
            if (now < stageEndTick)
            {
                return;
            }

            ResolveSecondImpact(caster, secondImpactCell, secondDirectionCell);
        }

        private void TickImpactSlashes(Pawn caster, Map map, int now)
        {
            if (now >= empoweredSlashNextTick)
            {
                ResolveNextEmpoweredImpactSlash(caster, map);
            }
        }

        private void ResolveNextEmpoweredImpactSlash(Pawn caster, Map map)
        {
            if (caster == null || map == null || !empoweredSlashCenter.IsValid || empoweredSlashIndex >= empoweredSlashTotalCount)
            {
                ClearSequence(caster);
                return;
            }

            Thing target = ResolveEmpoweredSlashTarget(caster, map);
            if (target != null)
            {
                trackedTarget = target;
                Props.slashSound?.PlayOneShot(new TargetInfo(target.Position, map));
                PlayEmpoweredSlashFleck(map, target);
                empoweredSlashHitCount += QingheSwordCombatUtility.ApplyRadius(
                    caster,
                    target.Position,
                    Mathf.Max(0f, Props.empoweredSlashRadius),
                    empoweredSlashDamage,
                    Props.armorPenetration,
                    empowered: true);
            }
            empoweredSlashIndex++;
            if (empoweredSlashIndex >= empoweredSlashTotalCount)
            {
                if (empoweredSlashHitCount > 0)
                {
                    MX_QH_HediffUtility.EnsureSwordPressure(caster)?.StartRecovery(Props.postHitRecoveryPoints, Props.postHitRecoveryTicks);
                }
                ClearSequence(caster);
                return;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : empoweredSlashNextTick;
            empoweredSlashNextTick = now + Mathf.Max(1, Props.empoweredSlashIntervalTicks);
        }

        private void PlayEmpoweredSlashFleck(Map map, Thing target)
        {
            FleckDef fleckDef = Props.empoweredSlashFleck;
            if (map == null || fleckDef == null || target == null)
            {
                return;
            }

            float angleStep = 360f / empoweredSlashTotalCount;
            float jitter = Mathf.Max(0f, Props.empoweredSlashVisualAngleJitter);
            FleckCreationData data = FleckMaker.GetDataStatic(
                target.DrawPos,
                map,
                fleckDef,
                Mathf.Max(0.01f, Props.empoweredSlashVisualScale));
            data.rotation = empoweredSlashBaseAngle + angleStep * empoweredSlashIndex + Rand.Range(-jitter, jitter);
            data.rotationRate = 0f;
            map.flecks.CreateFleck(data);
        }

        private Thing ResolveEmpoweredSlashTarget(Pawn caster, Map map)
        {
            float radius = Mathf.Max(0f, Props.secondImpactRadius);
            if (IsValidEmpoweredSlashTarget(caster, map, trackedTarget, radius))
            {
                return trackedTarget;
            }

            Thing nearest = null;
            float nearestDistanceSquared = float.MaxValue;
            Vector3 center = empoweredSlashCenter.ToVector3Shifted();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(empoweredSlashCenter, radius, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing candidate = things[i];
                    if (!IsValidEmpoweredSlashTarget(caster, map, candidate, radius))
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

        private bool IsValidEmpoweredSlashTarget(Pawn caster, Map map, Thing target, float radius)
        {
            return target != null
                && target != caster
                && !target.Destroyed
                && target.Spawned
                && target.MapHeld == map
                && GenHostility.HostileTo(caster, target)
                && target.Position.DistanceToSquared(empoweredSlashCenter) <= radius * radius;
        }

        private bool TryResolveDashCollision(Pawn caster, Map map, Vector3 from, Vector3 to, out Thing hitThing, out bool triggersSecondStage, out IntVec3 impactCell)
        {
            hitThing = null;
            triggersSecondStage = false;
            impactCell = IntVec3.Invalid;
            IntVec3 fromCell = from.ToIntVec3();
            IntVec3 toCell = to.ToIntVec3();

            foreach (IntVec3 cell in GenSight.BresenhamCellsBetween(fromCell, toCell))
            {
                if (cell == originCell || cell == fromCell && fromCell == previousDashPos.ToIntVec3())
                {
                    continue;
                }

                if (!cell.InBounds(map))
                {
                    impactCell = lastSafeDashCell.IsValid ? lastSafeDashCell : caster.Position;
                    return true;
                }

                Thing hostile = FirstDashTargetAt(caster, map, cell, Props.dashCollisionRadius);
                if (hostile != null)
                {
                    hitThing = hostile;
                    triggersSecondStage = true;
                    impactCell = hostile.Position;
                    return true;
                }

                if (!IsDashPassable(caster, map, cell))
                {
                    hitThing = FirstBlockingThingAt(map, cell);
                    impactCell = cell;
                    return true;
                }

                lastSafeDashCell = cell;
            }

            return false;
        }

        private static Thing FirstDashTargetAt(Pawn caster, Map map, IntVec3 cell, float collisionRadius)
        {
            float radius = Mathf.Max(0f, collisionRadius);
            foreach (IntVec3 candidateCell in GenRadial.RadialCellsAround(cell, radius, true))
            {
                if (!candidateCell.InBounds(map))
                {
                    continue;
                }

                List<Thing> things = candidateCell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Pawn pawn = things[i] as Pawn;
                    if (pawn != null && pawn != caster && !pawn.Dead && pawn.Spawned && GenHostility.HostileTo(caster, pawn))
                    {
                        return pawn;
                    }
                }

                for (int i = 0; i < things.Count; i++)
                {
                    Building building = things[i] as Building;
                    if (building != null && building.Spawned && building.HostileTo(caster))
                    {
                        return building;
                    }
                }
            }

            return null;
        }

        private static Thing FirstBlockingThingAt(Map map, IntVec3 cell)
        {
            Building edifice = cell.GetEdifice(map);
            if (edifice != null)
            {
                return edifice;
            }

            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Thing thing = things[i];
                if (thing != null && thing.def.passability == Traversability.Impassable)
                {
                    return thing;
                }
            }

            return null;
        }

        private static bool IsDashPassable(Pawn caster, Map map, IntVec3 cell)
        {
            if (!cell.WalkableBy(map, caster) || cell.Impassable(map))
            {
                return false;
            }

            Building_Door door = cell.GetEdifice(map) as Building_Door;
            return door == null || door.Open;
        }

        private void FinishDash(Pawn caster, Map map, IntVec3 desiredLandingCell, IntVec3 impactCell, Thing directHitThing, bool triggersSecondStage)
        {
            AscentSlashVisualTracker.Clear(caster);
            IntVec3 landingCell = FindNearestLandingCell(map, desiredLandingCell, caster, caster.Position);
            if (landingCell.IsValid && landingCell.InBounds(map) && landingCell != caster.Position)
            {
                caster.Position = landingCell;
                caster.Notify_Teleported(endCurrentJob: false);
            }

            ResolveDashImpact(caster, map, impactCell, directHitThing);
            if (!triggersSecondStage)
            {
                ClearSequence(caster);
                return;
            }

            RoofDef impactRoof = impactCell.InBounds(map) ? map.roofGrid.RoofAt(impactCell) : null;
            if (impactRoof?.isThickRoof == true)
            {
                ClearSequence(caster);
                return;
            }

            firstImpactCell = impactCell;
            secondStageTakeoffCell = caster.Position;
            secondDirectionCell = ComputeDirectionCell(caster.Position, impactCell);
            int secondStageTicks = Mathf.Max(1, Props.ascentTicks)
                + Mathf.Max(1, Props.takeoffDelayTicks)
                + Mathf.Max(1, Props.hoverTicks)
                + Mathf.Max(1, Props.descentTicks)
                + Mathf.Max(1, Props.impactDelayTicks)
                + Mathf.Max(0, Props.empoweredSlashCount - 1) * Mathf.Max(1, Props.empoweredSlashIntervalTicks);
            caster.stances?.stunner?.StunFor(secondStageTicks + 5, caster, addBattleLog: false, showMote: false);
            BeginStage(AscentSlashStage.TakeoffDelay, Props.takeoffDelayTicks);
        }

        private void ResolveDashImpact(Pawn caster, Map map, IntVec3 center, Thing directHitThing)
        {
            if (!center.IsValid || !center.InBounds(map))
            {
                center = caster.Position;
            }

            MX_QHGraphicsUtility.Fx(map, center, Props.impactEffecter, 0.8f);
            MX_QHGraphicsUtility.Fleck(map, center, Props.impactFleck, 0.85f);
            Props.castSound?.PlayOneShot(new TargetInfo(center, map));

            float specialFactor = MX_QHSkillUtility.GetSpecialAbilityEffectFactor(caster);
            List<Thing> victims = new();
            HashSet<Thing> unique = new();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, Props.dashImpactRadius, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Thing thing = things[i];
                    if (CanHitWithAscentSlash(caster, thing) && unique.Add(thing))
                    {
                        victims.Add(thing);
                    }
                }
            }

            for (int i = 0; i < victims.Count; i++)
            {
                Thing victim = victims[i];
                float damage = Props.dashDamageAmount * specialFactor;
                if (victim is Building)
                {
                    damage *= Props.buildingDamageMultiplier;
                }
                QingheSwordCombatUtility.ApplySlash(caster, victim, damage, Props.armorPenetration, empowered: false);
            }

            if (directHitThing != null && !directHitThing.Destroyed && directHitThing.Spawned && directHitThing.MapHeld == map && unique.Add(directHitThing))
            {
                float directDamage = Props.dashDamageAmount * specialFactor;
                if (directHitThing is Building)
                {
                    directDamage *= Props.buildingDamageMultiplier;
                }
                QingheSwordCombatUtility.ApplySlash(caster, directHitThing, directDamage, Props.armorPenetration, empowered: false);
            }
        }

        private void ResolveSecondStageDestination(Pawn caster, Map map)
        {
            IntVec3 takeoffCell = secondStageTakeoffCell.IsValid && secondStageTakeoffCell.InBounds(map)
                ? secondStageTakeoffCell
                : caster.Position;
            IntVec3 desired = takeoffCell;
            Vector3 trackingDirection = ComputeForward(firstImpactCell, secondDirectionCell);

            if (trackedTarget != null && !trackedTarget.Destroyed && trackedTarget.Spawned && trackedTarget.MapHeld == map)
            {
                IntVec3 currentTargetCell = trackedTarget.Position;
                Vector3 moved = (currentTargetCell - firstImpactCell).ToVector3();
                moved.y = 0f;
                if (moved.sqrMagnitude > 0.001f)
                {
                    trackingDirection = moved.normalized;
                }

                if (firstImpactCell.DistanceTo(currentTargetCell) <= Props.secondStageTrackingRange)
                {
                    desired = takeoffCell + moved.ToIntVec3();
                }
                else
                {
                    desired = takeoffCell + (trackingDirection * Props.secondStageLimitedFollowDistance).ToIntVec3();
                }
            }

            secondImpactCell = ClampToMap(desired, map);
            BreakRoofAt(map, secondImpactCell, allowThickRoof: true);
            if (trackingDirection.sqrMagnitude < 0.001f)
            {
                trackingDirection = ComputeForward(originCell, firstImpactCell);
            }
            secondDirectionCell = secondImpactCell + trackingDirection.ToIntVec3();
            if (secondDirectionCell == secondImpactCell)
            {
                secondDirectionCell = ComputeDirectionCell(originCell, secondImpactCell);
            }
        }

        private static void BreakRoofAt(Map map, IntVec3 cell, bool allowThickRoof)
        {
            if (map == null || !cell.IsValid || !cell.InBounds(map))
            {
                return;
            }

            RoofDef roof = map.roofGrid.RoofAt(cell);
            if (roof == null || roof.isThickRoof && !allowThickRoof)
            {
                return;
            }

            roof.soundPunchThrough?.PlayOneShot(new TargetInfo(cell, map));
            if (roof.filthLeaving != null)
            {
                FilthMaker.TryMakeFilth(cell, map, roof.filthLeaving);
            }
            map.roofGrid.SetRoof(cell, null);
            FleckMaker.ThrowDustPuff(cell.ToVector3Shifted() + Gen.RandomHorizontalVector(0.6f), map, 2f);
        }

        private IntVec3 FindNearestLandingCell(Map map, IntVec3 desired, Pawn caster, IntVec3 fallback)
        {
            if (map == null)
            {
                return fallback;
            }

            desired = ClampToMap(desired, map);
            if (ValidLandingCell(map, desired, caster))
            {
                return desired;
            }

            int count = GenRadial.NumCellsInRadius(3.9f);
            for (int i = 0; i < count; i++)
            {
                IntVec3 cell = desired + GenRadial.RadialPattern[i];
                if (ValidLandingCell(map, cell, caster))
                {
                    return cell;
                }
            }

            return fallback.IsValid && fallback.InBounds(map) ? fallback : caster.Position;
        }

        private static bool ValidLandingCell(Map map, IntVec3 cell, Pawn caster)
        {
            if (!JumpUtility.ValidJumpTarget(caster, map, cell))
            {
                return false;
            }

            List<Thing> things = cell.GetThingList(map);
            for (int i = 0; i < things.Count; i++)
            {
                Pawn pawn = things[i] as Pawn;
                if (pawn != null && pawn != caster && pawn.Spawned && !pawn.Dead)
                {
                    return false;
                }
            }
            return true;
        }

        private static IntVec3 ComputeDashEndCell(IntVec3 origin, IntVec3 aim, Map map, float range)
        {
            Vector3 direction = (aim - origin).ToVector3();
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
            {
                return origin;
            }

            direction.Normalize();
            Vector3 desired = origin.ToVector3Shifted() + direction * Mathf.Max(1f, range);
            return ClampToMap(desired.ToIntVec3(), map);
        }

        private static IntVec3 ClampToMap(IntVec3 cell, Map map)
        {
            if (map == null)
            {
                return cell;
            }

            return new IntVec3(
                Mathf.Clamp(cell.x, 0, map.Size.x - 1),
                0,
                Mathf.Clamp(cell.z, 0, map.Size.z - 1));
        }

        private int ResolveDashDurationTicks(Vector3 start, Vector3 end)
        {
            float speed = Mathf.Max(1f, Props.dashSpeedCellsPerSecond);
            int travelTicks = Mathf.CeilToInt((end - start).Yto0().magnitude / speed * 60f);
            return Mathf.Max(Props.dashDurationMinTicks, travelTicks);
        }

        private void BeginStage(AscentSlashStage nextStage, int durationTicks)
        {
            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            stage = nextStage;
            stageStartTick = now;
            stageEndTick = now + Mathf.Max(1, durationTicks);
        }

        private float StageProgress(int now)
        {
            if (stageEndTick <= stageStartTick)
            {
                return 1f;
            }
            return Mathf.Clamp01((now - stageStartTick) / (float)(stageEndTick - stageStartTick));
        }

        private void BeginAscentVisual(Pawn caster)
        {
            AscentSlashVisualTracker.BeginAscent(
                caster,
                stageStartTick,
                stageEndTick,
                Props.secondStageMaxAltitudeLayers,
                Props.secondStageMaxForwardOffset,
                Props.ascentDecelerationPower);
        }

        private void BeginHoverVisual(Pawn caster)
        {
            AscentSlashVisualTracker.BeginHover(
                caster,
                stageStartTick,
                stageEndTick,
                Props.secondStageMaxAltitudeLayers,
                Props.secondStageMaxForwardOffset);
        }

        private void BeginDescentVisual(Pawn caster)
        {
            AscentSlashVisualTracker.BeginDescent(
                caster,
                stageStartTick,
                stageEndTick,
                Props.secondStageMaxAltitudeLayers,
                Props.secondStageMaxForwardOffset,
                Props.descentAccelerationPower);
        }

        private void RestoreVisualState(Pawn caster)
        {
            AscentSlashVisualTracker.Clear(caster);
            switch (stage)
            {
                case AscentSlashStage.Dash:
                    AscentSlashVisualTracker.BeginDash(caster, stageStartTick, stageEndTick, dashStartPos, dashEndPos);
                    break;
                case AscentSlashStage.Ascending:
                    BeginAscentVisual(caster);
                    break;
                case AscentSlashStage.Hover:
                    BeginHoverVisual(caster);
                    break;
                case AscentSlashStage.Descending:
                    BeginDescentVisual(caster);
                    break;
            }
        }

        private static void TryAddDashAfterimage(Pawn caster, Map map, Vector3 drawPos, int now)
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

        private void ClearSequence(Pawn caster)
        {
            AscentSlashVisualTracker.Clear(caster);
            RemoveAscentSlashInvulnerability(caster);
            stage = AscentSlashStage.None;
            trackedTarget = null;
            stageStartTick = -1;
            stageEndTick = -1;
            empoweredSlashCenter = IntVec3.Invalid;
            empoweredSlashTotalCount = 0;
            empoweredSlashIndex = 0;
            empoweredSlashNextTick = -1;
            empoweredSlashHitCount = 0;
            empoweredSlashDamage = 0f;
            empoweredSlashBaseAngle = 0f;
        }

        private static void AddAscentSlashInvulnerability(Pawn caster)
        {
            if (caster?.health == null || MX_QHDefOf.MX_QH_AscentSlashInvulnerable == null)
            {
                return;
            }

            Hediff hediff = caster.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_AscentSlashInvulnerable) ?? caster.health.AddHediff(MX_QHDefOf.MX_QH_AscentSlashInvulnerable);
            hediff?.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
        }

        private static void RemoveAscentSlashInvulnerability(Pawn caster)
        {
            if (caster?.health == null || MX_QHDefOf.MX_QH_AscentSlashInvulnerable == null)
            {
                return;
            }

            Hediff hediff = caster.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_AscentSlashInvulnerable);
            if (hediff != null)
            {
                caster.health.RemoveHediff(hediff);
            }
        }

        private static bool CanHitWithAscentSlash(Pawn caster, Thing thing)
        {
            if (thing == null || thing == caster || thing.Destroyed || !thing.Spawned)
            {
                return false;
            }

            Pawn pawn = thing as Pawn;
            if (pawn != null)
            {
                return !pawn.Dead && GenHostility.HostileTo(caster, pawn);
            }

            Building building = thing as Building;
            return building != null && thing.HostileTo(caster);
        }

        private static Vector3 ComputeForward(IntVec3 source, IntVec3 target)
        {
            Vector3 forward = (target - source).ToVector3();
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
            {
                return Vector3.zero;
            }

            forward.Normalize();
            return forward;
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

        private void PlayTakeoffVisuals(Map map, IntVec3 origin)
        {
            PlaySizedFleck(map, origin, Props.takeoffGroundFleck, Props.takeoffGroundFleckSize, Props.takeoffGroundFleckOffset);
            PlaySizedFleck(map, origin, Props.ascentTrailFleck, Props.ascentTrailFleckSize, Props.ascentTrailFleckOffset);
            MX_QHGraphicsUtility.Fx(map, origin, Props.entryEffecter, 1f);
            MX_QHGraphicsUtility.Fleck(map, origin, Props.entryFleck, 1f);
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

            Vector3 position = origin.ToVector3Shifted() + new Vector3(offset.x, 0f, offset.y);
            FleckCreationData data = FleckMaker.GetDataStatic(position, map, fleckDef);
            Vector2 baseSize = graphicData.drawSize;
            data.exactScale = new Vector3(
                Mathf.Max(0.01f, size.x) / Mathf.Max(0.01f, baseSize.x),
                1f,
                Mathf.Max(0.01f, size.y) / Mathf.Max(0.01f, baseSize.y));
            map.flecks.CreateFleck(data);
        }
    }

}


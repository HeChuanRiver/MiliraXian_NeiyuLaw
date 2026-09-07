using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Abilities;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Things.Weapons;
using MiliraXian.Characters.QingHe.Vfx;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Jobs
{
    public class JobDriver_IllusoryReflectionStance : JobDriver
    {
        private bool stanceActive;
        private bool stanceSoundPlayed;
        private bool counterQueued;
        private IntVec3 stanceDirectionCell = IntVec3.Invalid;
        private int stanceFacingInt = Rot4.South.AsInt;

        public override bool PlayerInterruptable => false;

        public Rot4 StanceFacing => new(stanceFacingInt);

        public bool StanceActive => stanceActive;

        public int StanceElapsedTicks => Mathf.Max(0, (Find.TickManager?.TicksGame ?? job.startTick) - job.startTick);

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            stanceActive = true;
            stanceSoundPlayed = false;
            counterQueued = false;
            stanceDirectionCell = job.GetTarget(TargetIndex.A).Cell;
            if (!stanceDirectionCell.IsValid || stanceDirectionCell == pawn.Position)
            {
                stanceDirectionCell = pawn.Position + pawn.Rotation.FacingCell;
            }
            pawn.rotationTracker?.FaceCell(stanceDirectionCell);
            stanceFacingInt = pawn.Rotation.AsInt;
            pawn.Rotation = StanceFacing;
        }

        public bool TryHandleDamage(ref DamageInfo dinfo, ref bool absorbed)
        {
            if (!stanceActive || dinfo.Amount <= 0f)
            {
                return false;
            }

            QueueCounter();
            dinfo.SetAmount(0f);
            absorbed = true;
            return true;
        }

        public bool TryHandleAttackAttempt(Thing instigator)
        {
            if (!stanceActive || !IsHostileAttackSource(instigator))
            {
                return false;
            }

            QueueCounter();
            return true;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref stanceActive, "mx_qh_illusoryReflection_stanceActive", false);
            Scribe_Values.Look(ref stanceSoundPlayed, "mx_qh_illusoryReflection_stanceSoundPlayed", false);
            Scribe_Values.Look(ref counterQueued, "mx_qh_illusoryReflection_counterQueued", false);
            Scribe_Values.Look(ref stanceDirectionCell, "mx_qh_illusoryReflection_stanceDirectionCell", IntVec3.Invalid);
            Scribe_Values.Look(ref stanceFacingInt, "mx_qh_illusoryReflection_stanceFacing", Rot4.South.AsInt);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            Toil stance = ToilMaker.MakeToil("IllusoryReflectionStance");
            stance.tickAction = delegate
            {
                if (!stanceSoundPlayed)
                {
                    ResolveProps()?.stanceSound?.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                    stanceSoundPlayed = true;
                }
                pawn.Rotation = StanceFacing;
                if (counterQueued)
                {
                    ResolveCounter();
                    ReadyForNextToil();
                }
            };
            stance.AddFinishAction(delegate { stanceActive = false; });
            stance.defaultCompleteMode = ToilCompleteMode.Delay;
            stance.defaultDuration = ResolveStanceDuration();
            yield return stance;
        }

        private int ResolveStanceDuration()
        {
            return ResolveProps()?.durationTicks ?? 300;
        }

        private CompProperties_AbilityIllusoryReflection ResolveProps()
        {
            return job.ability?.CompOfType<CompAbilityEffect_IllusoryReflection>()?.Props;
        }

        private void QueueCounter()
        {
            if (!counterQueued)
            {
                counterQueued = true;
            }
        }

        private bool IsHostileAttackSource(Thing instigator)
        {
            return instigator != null
                && instigator != pawn
                && instigator.Spawned
                && instigator.MapHeld == pawn.MapHeld
                && GenHostility.HostileTo(pawn, instigator);
        }

        private void ResolveCounter()
        {
            Map map = pawn.MapHeld;
            if (map == null)
            {
                return;
            }

            IntVec3 directionCell = stanceDirectionCell.IsValid && stanceDirectionCell != pawn.Position
                ? stanceDirectionCell
                : pawn.Position + StanceFacing.FacingCell;
            pawn.Rotation = StanceFacing;
            CompProperties_AbilityIllusoryReflection props = ResolveProps();
            props?.slashSound?.PlayOneShot(new TargetInfo(pawn.Position, map));
            float specialFactor = MX_QHSkillUtility.GetSpecialAbilityEffectFactor(pawn);
            PlayEmpoweredCounterSlash(directionCell, props);
            int hitCount = QingheSwordCombatUtility.ApplyCone(
                pawn,
                pawn.Position,
                directionCell,
                props?.coneRadius ?? 5.5f,
                props?.coneAngleDegrees ?? 90f,
                (props?.empoweredDamage ?? 48f) * specialFactor,
                props?.armorPenetration ?? 0.45f,
                empowered: true);
            if (hitCount > 0)
            {
                MX_QH_HediffUtility.EnsureSwordPressure(pawn)?.AddPoints(1f);
            }
        }

        private void PlayEmpoweredCounterSlash(IntVec3 directionCell, CompProperties_AbilityIllusoryReflection props)
        {
            Map map = pawn.MapHeld;
            if (map == null || props == null)
            {
                return;
            }

            Vector3 forward = (directionCell - pawn.Position).ToVector3().Yto0();
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = pawn.Rotation.FacingCell.ToVector3();
            }
            forward.Normalize();

            map.GetComponent<MapComponent_QingheMirrorSlashVisuals>()?.AddSlash(
                pawn.DrawPos,
                forward,
                props.coneRadius,
                props.mirrorSlashTexPath,
                props.mirrorSlashRevealSeconds,
                props.mirrorSlashHoldSeconds,
                props.mirrorSlashFadeSeconds,
                props.mirrorSlashDrawSizeFactor,
                props.mirrorSlashForwardOffsetFactor,
                props.mirrorSlashAngleOffsetDegrees,
                props.mirrorSlashHeadWidth,
                props.mirrorSlashHeadIntensity,
                props.mirrorSlashAdditiveIntensity,
                props.mirrorSlashDistortionStrength,
                props.mirrorSlashDistortionScale,
                props.mirrorSlashDistortionScrollX,
                props.mirrorSlashDistortionScrollY,
                props.mirrorSlashDistortionOpacity,
                directionCell.x < pawn.Position.x);
        }

    }
}

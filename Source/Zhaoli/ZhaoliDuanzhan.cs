using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MiliraXian.Characters.Neiyu;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Zhaoli
{
    internal enum DuanzhanSequenceStage
    {
        None,
        Charging,
        Slash
    }

    internal struct DuanzhanVisualState
    {
        public int weaponThingId;
        public int sequenceStartTick;
        public int firstScaleDelayTicks;
        public int secondScaleDelayTicks;
        public int thirdScaleDelayTicks;
        public int postMaxHoldTicks;
        public int firstSlashEndTick;
        public int reversePauseTicks;
        public int secondSlashEndTick;
        public int postReversePauseTicks;
        public int horizontalMoveTicks;
        public int horizontalHoldTicks;
        public int thrustChargeTicks;
        public int thrustChargeHoldTicks;
        public int thrustTicks;
        public int horizontalMoveEndTick;
        public int horizontalHoldEndTick;
        public int thrustChargeEndTick;
        public int thrustChargeHoldEndTick;
        public int sequenceEndTick;
        public float baseAimAngle;
        public float firstWeaponScale;
        public float secondWeaponScale;
        public float thirdWeaponScale;
        public float reverseSlashScaleMultiplier;
        public float slashStartAngleOffset;
        public float slashEndAngleOffset;
        public float thrustChargeDistanceCells;
        public float thrustDistanceCells;
    }

    internal static class DuanzhanVisualTracker
    {
        private static readonly Dictionary<int, DuanzhanVisualState> States = new Dictionary<int, DuanzhanVisualState>();

        public static void SetState(
            Pawn pawn,
            ThingWithComps weapon,
            int sequenceStartTick,
            int firstScaleDelayTicks,
            int secondScaleDelayTicks,
            int thirdScaleDelayTicks,
            int postMaxHoldTicks,
            int firstSlashTicks,
            int reversePauseTicks,
            int secondSlashTicks,
            int postReversePauseTicks,
            int horizontalMoveTicks,
            int horizontalHoldTicks,
            int thrustChargeTicks,
            int thrustChargeHoldTicks,
            int thrustTicks,
            float baseAimAngle,
            float firstWeaponScale,
            float secondWeaponScale,
            float thirdWeaponScale,
            float reverseSlashScaleMultiplier,
            float slashStartAngleOffset,
            float slashEndAngleOffset,
            float thrustChargeDistanceCells,
            float thrustDistanceCells)
        {
            if (pawn == null || weapon == null)
            {
                return;
            }

            int maxScaleHoldTicks = Mathf.Max(0, postMaxHoldTicks);
            int firstSlashStartTick = sequenceStartTick + Mathf.Max(1, thirdScaleDelayTicks) + maxScaleHoldTicks;
            int firstSlashEndTick = firstSlashStartTick + Mathf.Max(1, firstSlashTicks);
            int secondSlashEndTick = firstSlashEndTick + Mathf.Max(0, reversePauseTicks) + Mathf.Max(1, secondSlashTicks);
            int horizontalMoveEndTick = secondSlashEndTick + Mathf.Max(0, postReversePauseTicks) + Mathf.Max(0, horizontalMoveTicks);
            int horizontalHoldEndTick = horizontalMoveEndTick + Mathf.Max(0, horizontalHoldTicks);
            int thrustChargeEndTick = horizontalHoldEndTick + Mathf.Max(0, thrustChargeTicks);
            int thrustChargeHoldEndTick = thrustChargeEndTick + Mathf.Max(0, thrustChargeHoldTicks);
            int sequenceEndTick = thrustChargeHoldEndTick + Mathf.Max(1, thrustTicks);
            States[pawn.thingIDNumber] = new DuanzhanVisualState
            {
                weaponThingId = weapon.thingIDNumber,
                sequenceStartTick = sequenceStartTick,
                firstScaleDelayTicks = Mathf.Max(1, firstScaleDelayTicks),
                secondScaleDelayTicks = Mathf.Max(1, secondScaleDelayTicks),
                thirdScaleDelayTicks = Mathf.Max(1, thirdScaleDelayTicks),
                postMaxHoldTicks = maxScaleHoldTicks,
                firstSlashEndTick = firstSlashEndTick,
                reversePauseTicks = Mathf.Max(0, reversePauseTicks),
                secondSlashEndTick = secondSlashEndTick,
                postReversePauseTicks = Mathf.Max(0, postReversePauseTicks),
                horizontalMoveTicks = Mathf.Max(0, horizontalMoveTicks),
                horizontalHoldTicks = Mathf.Max(0, horizontalHoldTicks),
                thrustChargeTicks = Mathf.Max(0, thrustChargeTicks),
                thrustChargeHoldTicks = Mathf.Max(0, thrustChargeHoldTicks),
                thrustTicks = Mathf.Max(1, thrustTicks),
                horizontalMoveEndTick = horizontalMoveEndTick,
                horizontalHoldEndTick = horizontalHoldEndTick,
                thrustChargeEndTick = thrustChargeEndTick,
                thrustChargeHoldEndTick = thrustChargeHoldEndTick,
                sequenceEndTick = sequenceEndTick,
                baseAimAngle = baseAimAngle,
                firstWeaponScale = firstWeaponScale,
                secondWeaponScale = secondWeaponScale,
                thirdWeaponScale = thirdWeaponScale,
                reverseSlashScaleMultiplier = reverseSlashScaleMultiplier,
                slashStartAngleOffset = slashStartAngleOffset,
                slashEndAngleOffset = slashEndAngleOffset,
                thrustChargeDistanceCells = thrustChargeDistanceCells,
                thrustDistanceCells = thrustDistanceCells
            };
        }

        public static void Clear(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            States.Remove(pawn.thingIDNumber);
        }

        public static bool TryGetWeaponVisual(Thing eq, out float weaponScale, out float drawAimAngle, out Vector3 drawOffset, out bool drawAfterimages)
        {
            weaponScale = 1f;
            drawAimAngle = 0f;
            drawOffset = Vector3.zero;
            drawAfterimages = false;

            Pawn_EquipmentTracker equipmentTracker = eq?.ParentHolder as Pawn_EquipmentTracker;
            Pawn pawn = equipmentTracker?.pawn;
            if (pawn == null || !States.TryGetValue(pawn.thingIDNumber, out DuanzhanVisualState state))
            {
                return false;
            }

            if (state.weaponThingId < 0 || eq.thingIDNumber != state.weaponThingId)
            {
                return false;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : state.sequenceEndTick;
            int firstScaleTick = state.sequenceStartTick + state.firstScaleDelayTicks;
            int secondScaleTick = state.sequenceStartTick + state.secondScaleDelayTicks;
            int thirdScaleTick = state.sequenceStartTick + state.thirdScaleDelayTicks;
            int firstSlashStartTick = thirdScaleTick + state.postMaxHoldTicks;
            int firstSlashEndTick = state.firstSlashEndTick;
            int firstPauseEndTick = firstSlashEndTick + state.reversePauseTicks;
            int secondSlashStartTick = firstPauseEndTick;
            int secondSlashEndTick = state.secondSlashEndTick;
            int secondPauseEndTick = secondSlashEndTick + state.postReversePauseTicks;
            int horizontalMoveEndTick = state.horizontalMoveEndTick;
            int horizontalHoldEndTick = state.horizontalHoldEndTick;
            int thrustChargeEndTick = state.thrustChargeEndTick;
            int thrustChargeHoldEndTick = state.thrustChargeHoldEndTick;
            int thrustStartTick = thrustChargeHoldEndTick;
            int thrustEndTick = state.sequenceEndTick;
            float reverseSlashScale = state.thirdWeaponScale * Mathf.Max(1f, state.reverseSlashScaleMultiplier);
            Vector3 forward = ForwardFromAimAngle(state.baseAimAngle);

            if (now < firstScaleTick)
            {
                return false;
            }

            if (now < secondScaleTick)
            {
                weaponScale = state.firstWeaponScale;
                drawAimAngle = state.baseAimAngle + state.slashStartAngleOffset;
                return true;
            }

            if (now < thirdScaleTick)
            {
                weaponScale = state.secondWeaponScale;
                drawAimAngle = state.baseAimAngle + state.slashStartAngleOffset;
                return true;
            }

            if (now < firstSlashStartTick)
            {
                weaponScale = state.thirdWeaponScale;
                drawAimAngle = state.baseAimAngle + state.slashStartAngleOffset;
                return true;
            }

            if (now < firstSlashEndTick)
            {
                weaponScale = state.thirdWeaponScale;
                float progress = Mathf.Clamp01((now - firstSlashStartTick) / (float)Mathf.Max(1, firstSlashEndTick - firstSlashStartTick));
                drawAimAngle = state.baseAimAngle + Mathf.Lerp(state.slashStartAngleOffset, state.slashEndAngleOffset, progress);
                drawAfterimages = true;
                return true;
            }

            if (now < firstPauseEndTick)
            {
                weaponScale = state.thirdWeaponScale;
                drawAimAngle = state.baseAimAngle + state.slashEndAngleOffset;
                return true;
            }

            if (now < secondSlashEndTick)
            {
                float progress = Mathf.Clamp01((now - secondSlashStartTick) / (float)Mathf.Max(1, secondSlashEndTick - secondSlashStartTick));
                weaponScale = Mathf.Lerp(state.thirdWeaponScale, reverseSlashScale, progress);
                drawAimAngle = state.baseAimAngle + Mathf.Lerp(state.slashEndAngleOffset, state.slashStartAngleOffset, progress);
                drawAfterimages = true;
                return true;
            }

            if (now < secondPauseEndTick)
            {
                weaponScale = reverseSlashScale;
                drawAimAngle = state.baseAimAngle + state.slashStartAngleOffset;
                return true;
            }

            if (now < horizontalMoveEndTick)
            {
                float progress = Mathf.Clamp01((now - secondPauseEndTick) / (float)Mathf.Max(1, horizontalMoveEndTick - secondPauseEndTick));
                weaponScale = reverseSlashScale;
                drawAimAngle = state.baseAimAngle + Mathf.Lerp(state.slashStartAngleOffset, 0f, progress);
                return true;
            }

            if (now < horizontalHoldEndTick)
            {
                weaponScale = reverseSlashScale;
                drawAimAngle = state.baseAimAngle;
                return true;
            }

            if (now < thrustChargeEndTick)
            {
                float progress = Mathf.Clamp01((now - horizontalHoldEndTick) / (float)Mathf.Max(1, thrustChargeEndTick - horizontalHoldEndTick));
                weaponScale = reverseSlashScale;
                drawAimAngle = state.baseAimAngle;
                drawOffset = -forward * (state.thrustChargeDistanceCells * progress);
                return true;
            }

            if (now < thrustChargeHoldEndTick)
            {
                weaponScale = reverseSlashScale;
                drawAimAngle = state.baseAimAngle;
                drawOffset = -forward * state.thrustChargeDistanceCells;
                return true;
            }

            if (now < thrustEndTick)
            {
                float progress = Mathf.Clamp01((now - thrustStartTick) / (float)Mathf.Max(1, thrustEndTick - thrustStartTick));
                weaponScale = reverseSlashScale;
                drawAimAngle = state.baseAimAngle;
                drawOffset = -forward * state.thrustChargeDistanceCells + forward * ((state.thrustChargeDistanceCells + state.thrustDistanceCells) * progress);
                return true;
            }

            return false;
        }

        private static Vector3 ForwardFromAimAngle(float aimAngle)
        {
            float radians = aimAngle * 0.0174532924f;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }
    }

    public class CompProperties_AbilityDuanzhan : CompProperties_AbilityEffect
    {
        public float impactRadius = 3f;
        public float damageAmount = 500f;
        public float armorPenetration = 999f;
        public int backswingTicks = 180;
        public float minghuoFlameDamageFactor = 0.35f;
        public int firstScaleDelayTicks = 60;
        public int secondScaleDelayTicks = 180;
        public int thirdScaleDelayTicks = 300;
        public int postMaxHoldTicks = 60;
        public int slashTicks = 54;
        public float firstSlashSpeedMultiplier = 2f;
        public int reversePauseTicks = 24;
        public float secondSlashSpeedMultiplier = 3f;
        public int postReversePauseTicks = 18;
        public int horizontalMoveTicks = 12;
        public int horizontalHoldTicks = 12;
        public int thrustChargeTicks = 18;
        public int thrustChargeHoldTicks = 18;
        public int thrustTicks = 8;
        public float firstWeaponScale = 1.2f;
        public float secondWeaponScale = 2f;
        public float thirdWeaponScale = 3.5f;
        public float reverseSlashScaleMultiplier = 1.5f;
        public float slashStartAngleOffset = -90f;
        public float slashEndAngleOffset = 90f;
        public float thrustChargeDistanceCells = 1.6f;
        public float thrustDistanceCells = 10f;

        public CompProperties_AbilityDuanzhan()
        {
            compClass = typeof(CompAbilityEffect_Duanzhan);
        }
    }

    public class CompAbilityEffect_Duanzhan : CompAbilityEffect
    {
        private new CompProperties_AbilityDuanzhan Props => (CompProperties_AbilityDuanzhan)props;

        private DuanzhanSequenceStage stage;
        private IntVec3 originCell;
        private IntVec3 targetCell;
        private int sequenceStartTick;
        private int slashStartTick;
        private int slashEndTick;
        private int sequenceEndTick;
        private bool damageApplied;
        private bool firstSlashFxPlayed;
        private bool secondSlashFxPlayed;
        private bool thrustChargeFxPlayed;
        private bool thrustLaunchFxPlayed;
        private bool thrustImpactFxPlayed;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            Map map = caster?.MapHeld;
            if (caster == null || map == null || !target.IsValid)
            {
                return false;
            }

            if (!target.Cell.InBounds(map))
            {
                if (throwMessages)
                {
                    Messages.Message("断斩的目标位置无效。", MessageTypeDefOf.RejectInput, false);
                }

                return false;
            }

            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            Map map = caster?.MapHeld;
            if (caster == null || map == null || !target.IsValid)
            {
                return;
            }

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : 0;
            originCell = caster.Position;
            targetCell = target.Cell;
            sequenceStartTick = now;
            int firstSlashTicks = GetSlashTicks(Props.firstSlashSpeedMultiplier);
            int secondSlashTicks = GetSlashTicks(Props.secondSlashSpeedMultiplier);
            slashStartTick = now + Mathf.Max(1, Props.thirdScaleDelayTicks) + Mathf.Max(0, Props.postMaxHoldTicks);
            slashEndTick = slashStartTick + firstSlashTicks;
            sequenceEndTick = slashEndTick
                + Mathf.Max(0, Props.reversePauseTicks)
                + secondSlashTicks
                + Mathf.Max(0, Props.postReversePauseTicks)
                + Mathf.Max(0, Props.horizontalMoveTicks)
                + Mathf.Max(0, Props.horizontalHoldTicks)
                + Mathf.Max(0, Props.thrustChargeTicks)
                + Mathf.Max(0, Props.thrustChargeHoldTicks)
                + Mathf.Max(1, Props.thrustTicks);
            damageApplied = false;
            firstSlashFxPlayed = false;
            secondSlashFxPlayed = false;
            thrustChargeFxPlayed = false;
            thrustLaunchFxPlayed = false;
            thrustImpactFxPlayed = false;
            stage = DuanzhanSequenceStage.Charging;

            int totalLockTicks = Mathf.Max(1,
                Props.thirdScaleDelayTicks
                + Props.postMaxHoldTicks
                + firstSlashTicks
                + Props.reversePauseTicks
                + secondSlashTicks
                + Props.postReversePauseTicks
                + Props.horizontalMoveTicks
                + Props.horizontalHoldTicks
                + Props.thrustChargeTicks
                + Props.thrustChargeHoldTicks
                + Props.thrustTicks
                + Props.backswingTicks);
            if (caster.stances?.stunner != null)
            {
                caster.stances.stunner.StunFor(totalLockTicks, caster, addBattleLog: false, showMote: false);
            }

            caster.rotationTracker?.FaceCell(targetCell);
            RefreshVisualState(caster);
            FleckMaker.Static(originCell, map, FleckDefOf.ExplosionFlash);
            FleckMaker.Static(originCell, map, FleckDefOf.FeedbackMelee, 1.6f);
        }

        public override void CompTick()
        {
            base.CompTick();

            if (stage == DuanzhanSequenceStage.None)
            {
                return;
            }

            Pawn caster = parent?.pawn;
            Map map = caster?.MapHeld;
            if (caster == null || map == null)
            {
                ClearSequence(caster);
                return;
            }

            caster.rotationTracker?.FaceCell(targetCell);
            RefreshVisualState(caster);

            int now = Find.TickManager != null ? Find.TickManager.TicksGame : sequenceEndTick;
            int secondSlashStartTick = slashEndTick + Mathf.Max(0, Props.reversePauseTicks);
            int secondSlashEndTick = secondSlashStartTick + GetSlashTicks(Props.secondSlashSpeedMultiplier);
            int horizontalMoveStartTick = secondSlashEndTick + Mathf.Max(0, Props.postReversePauseTicks);
            int horizontalMoveEndTick = horizontalMoveStartTick + Mathf.Max(0, Props.horizontalMoveTicks);
            int horizontalHoldEndTick = horizontalMoveEndTick + Mathf.Max(0, Props.horizontalHoldTicks);
            int thrustChargeStartTick = horizontalHoldEndTick;
            int thrustChargeEndTick = thrustChargeStartTick + Mathf.Max(0, Props.thrustChargeTicks);
            int thrustStartTick = thrustChargeEndTick + Mathf.Max(0, Props.thrustChargeHoldTicks);
            if (stage == DuanzhanSequenceStage.Charging && now >= slashStartTick)
            {
                stage = DuanzhanSequenceStage.Slash;
            }

            if (!firstSlashFxPlayed && now >= slashStartTick)
            {
                PlaySlashStartEffects(map, originCell, 1.2f);
                firstSlashFxPlayed = true;
            }

            if (!secondSlashFxPlayed && now >= secondSlashStartTick)
            {
                PlaySlashStartEffects(map, originCell, 1f);
                secondSlashFxPlayed = true;
            }

            if (!thrustChargeFxPlayed && now >= thrustChargeStartTick)
            {
                PlayThrustChargeEffects(map, originCell);
                thrustChargeFxPlayed = true;
            }

            if (!thrustLaunchFxPlayed && now >= thrustStartTick)
            {
                PlayThrustLaunchEffects(map, originCell);
                thrustLaunchFxPlayed = true;
            }

            if (stage == DuanzhanSequenceStage.Slash && !damageApplied)
            {
                int triggerTick = slashStartTick + Mathf.Max(1, (slashEndTick - slashStartTick) / 2);
                if (now >= triggerTick)
                {
                    DoDuanzhan(caster, map, originCell, ComputeForward(originCell, targetCell));
                    damageApplied = true;
                    FleckMaker.Static(originCell, map, FleckDefOf.ExplosionFlash);
                }
            }

            if (!thrustImpactFxPlayed && now >= sequenceEndTick)
            {
                PlayThrustImpactEffects(map, ComputeForward(originCell, targetCell));
                thrustImpactFxPlayed = true;
            }

            if (now >= sequenceEndTick)
            {
                if (!damageApplied)
                {
                    DoDuanzhan(caster, map, originCell, ComputeForward(originCell, targetCell));
                }

                ClearSequence(caster);
            }
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            int stageInt = (int)stage;
            Scribe_Values.Look(ref stageInt, "mxnl_zhaoli_duanzhan_stage", 0);
            Scribe_Values.Look(ref originCell, "mxnl_zhaoli_duanzhan_originCell");
            Scribe_Values.Look(ref targetCell, "mxnl_zhaoli_duanzhan_targetCell");
            Scribe_Values.Look(ref sequenceStartTick, "mxnl_zhaoli_duanzhan_sequenceStartTick", -1);
            Scribe_Values.Look(ref slashStartTick, "mxnl_zhaoli_duanzhan_slashStartTick", -1);
            Scribe_Values.Look(ref slashEndTick, "mxnl_zhaoli_duanzhan_slashEndTick", -1);
            Scribe_Values.Look(ref sequenceEndTick, "mxnl_zhaoli_duanzhan_sequenceEndTick", -1);
            Scribe_Values.Look(ref damageApplied, "mxnl_zhaoli_duanzhan_damageApplied", false);
            Scribe_Values.Look(ref firstSlashFxPlayed, "mxnl_zhaoli_duanzhan_firstSlashFxPlayed", false);
            Scribe_Values.Look(ref secondSlashFxPlayed, "mxnl_zhaoli_duanzhan_secondSlashFxPlayed", false);
            Scribe_Values.Look(ref thrustChargeFxPlayed, "mxnl_zhaoli_duanzhan_thrustChargeFxPlayed", false);
            Scribe_Values.Look(ref thrustLaunchFxPlayed, "mxnl_zhaoli_duanzhan_thrustLaunchFxPlayed", false);
            Scribe_Values.Look(ref thrustImpactFxPlayed, "mxnl_zhaoli_duanzhan_thrustImpactFxPlayed", false);
            stage = (DuanzhanSequenceStage)stageInt;
        }

        private int GetSlashTicks(float speedMultiplier)
        {
            return Mathf.Max(1, Mathf.RoundToInt(Props.slashTicks / Mathf.Max(0.01f, speedMultiplier)));
        }

        private void RefreshVisualState(Pawn caster)
        {
            ThingWithComps weapon = caster?.equipment?.Primary;
            if (caster == null || weapon == null || stage == DuanzhanSequenceStage.None)
            {
                DuanzhanVisualTracker.Clear(caster);
                return;
            }

            DuanzhanVisualTracker.SetState(
                caster,
                weapon,
                sequenceStartTick,
                Props.firstScaleDelayTicks,
                Props.secondScaleDelayTicks,
                Props.thirdScaleDelayTicks,
                Props.postMaxHoldTicks,
                GetSlashTicks(Props.firstSlashSpeedMultiplier),
                Props.reversePauseTicks,
                GetSlashTicks(Props.secondSlashSpeedMultiplier),
                Props.postReversePauseTicks,
                Props.horizontalMoveTicks,
                Props.horizontalHoldTicks,
                Props.thrustChargeTicks,
                Props.thrustChargeHoldTicks,
                Props.thrustTicks,
                ComputeAimAngle(originCell, targetCell),
                Props.firstWeaponScale,
                Props.secondWeaponScale,
                Props.thirdWeaponScale,
                Props.reverseSlashScaleMultiplier,
                Props.slashStartAngleOffset,
                Props.slashEndAngleOffset,
                Props.thrustChargeDistanceCells,
                Props.thrustDistanceCells);
        }

        private void ClearSequence(Pawn caster)
        {
            stage = DuanzhanSequenceStage.None;
            damageApplied = false;
            firstSlashFxPlayed = false;
            secondSlashFxPlayed = false;
            thrustChargeFxPlayed = false;
            thrustLaunchFxPlayed = false;
            thrustImpactFxPlayed = false;
            DuanzhanVisualTracker.Clear(caster);
        }

        private static Vector3 ComputeForward(IntVec3 sourceCell, IntVec3 targetCell)
        {
            Vector3 forward = (targetCell - sourceCell).ToVector3();
            if (forward.sqrMagnitude < 0.001f)
            {
                forward = Vector3.forward;
            }

            forward.y = 0f;
            forward.Normalize();
            return forward;
        }

        private static float ComputeAimAngle(IntVec3 sourceCell, IntVec3 targetCell)
        {
            Vector3 forward = ComputeForward(sourceCell, targetCell);
            return Mathf.Atan2(forward.x, forward.z) * 57.29578f;
        }

        private void PlaySlashStartEffects(Map map, IntVec3 cell, float flashScale)
        {
            if (map == null || !cell.IsValid)
            {
                return;
            }

            FleckMaker.Static(cell, map, FleckDefOf.PsycastSkipFlashEntry, flashScale);
            FleckMaker.Static(cell, map, FleckDefOf.ShotFlash, flashScale);
        }

        private void PlayThrustChargeEffects(Map map, IntVec3 cell)
        {
            if (map == null || !cell.IsValid)
            {
                return;
            }

            FleckMaker.Static(cell, map, FleckDefOf.PsycastSkipInnerExit, 1.15f);
            FleckMaker.Static(cell, map, FleckDefOf.MicroSparksFast, 1.05f);
        }

        private void PlayThrustLaunchEffects(Map map, IntVec3 cell)
        {
            if (map == null || !cell.IsValid)
            {
                return;
            }

            FleckMaker.Static(cell, map, FleckDefOf.FeedbackShoot, 1.2f);
            FleckMaker.Static(cell, map, FleckDefOf.ShotFlash, 1.25f);
        }

        private void PlayThrustImpactEffects(Map map, Vector3 forward)
        {
            if (map == null || !originCell.IsValid)
            {
                return;
            }

            Vector3 tipPosition = originCell.ToVector3Shifted() + forward * Props.thrustDistanceCells;
            FleckMaker.Static(tipPosition.ToIntVec3(), map, FleckDefOf.PsycastSkipFlashEntry, 1.35f);
            FleckMaker.Static(tipPosition.ToIntVec3(), map, FleckDefOf.ExplosionFlash, 1.35f);
            FleckMaker.Static(tipPosition.ToIntVec3(), map, FleckDefOf.ShotFlash, 1.1f);
        }

        private void DoDuanzhan(Pawn caster, Map map, IntVec3 center, Vector3 forward)
        {
            ThingWithComps weapon = caster.equipment?.Primary;
            HediffComp_ZhaoliMinghuo minghuoComp = GetActiveMinghuoComp(caster, weapon);
            HashSet<Pawn> targets = NeiyuFlowerSwordSkillUtility.CollectPawnsInRadius(map, center, Props.impactRadius);

            foreach (Pawn pawn in targets)
            {
                if (pawn == null || pawn == caster || pawn.Dead || !pawn.Spawned)
                {
                    continue;
                }

                if (!NeiyuFlowerSwordSkillUtility.IsHostile(caster, pawn))
                {
                    continue;
                }

                if (!IsInsideFrontHalfCircle(center, pawn.Position, forward, Props.impactRadius))
                {
                    continue;
                }

                ApplyPrimaryDamage(caster, pawn, weapon);
                ApplyMinghuoDamage(caster, pawn, weapon, minghuoComp);
                FleckMaker.Static(pawn.Position, map, FleckDefOf.FeedbackMelee, 1.3f);
                FleckMaker.Static(pawn.Position, map, FleckDefOf.ExplosionFlash);

                if (caster.Spawned && pawn.Spawned)
                {
                    caster.Drawer.Notify_MeleeAttackOn(pawn);
                }
            }
        }

        private static bool IsInsideFrontHalfCircle(IntVec3 center, IntVec3 targetCell, Vector3 forward, float radius)
        {
            Vector3 offset = (targetCell - center).ToVector3();
            offset.y = 0f;
            if (offset.sqrMagnitude > radius * radius)
            {
                return false;
            }

            if (offset.sqrMagnitude < 0.01f)
            {
                return true;
            }

            offset.Normalize();
            return Vector3.Dot(forward, offset) >= 0f;
        }

        private void ApplyPrimaryDamage(Pawn caster, Pawn target, ThingWithComps weapon)
        {
            IEnumerable<BodyPartRecord> parts = target.health?.hediffSet?.GetNotMissingParts();
            BodyPartRecord torso = parts != null ? parts.FirstOrDefault(part => part.def == BodyPartDefOf.Torso) : null;
            DamageInfo damageInfo = new DamageInfo(DamageDefOf.Cut, Props.damageAmount, Props.armorPenetration, -1f, caster, torso, weapon?.def);
            damageInfo.SetIgnoreArmor(true);
            damageInfo.SetIgnoreInstantKillProtection(true);
            damageInfo.SetBodyRegion(BodyPartHeight.Middle, BodyPartDepth.Outside);
            target.TakeDamage(damageInfo);
        }

        private void ApplyMinghuoDamage(Pawn caster, Pawn target, ThingWithComps weapon, HediffComp_ZhaoliMinghuo minghuoComp)
        {
            if (minghuoComp == null)
            {
                return;
            }

            IEnumerable<BodyPartRecord> parts = target.health?.hediffSet?.GetNotMissingParts();
            BodyPartRecord torso = parts != null ? parts.FirstOrDefault(part => part.def == BodyPartDefOf.Torso) : null;
            float flameDamage = Mathf.Max(1f, Props.damageAmount * minghuoComp.PropsMinghuo.fireDamageFactor * Props.minghuoFlameDamageFactor);
            DamageInfo damageInfo = new DamageInfo(DamageDefOf.Flame, flameDamage, 0f, -1f, caster, torso, weapon?.def);
            damageInfo.SetIgnoreArmor(true);
            damageInfo.SetBodyRegion(BodyPartHeight.Middle, BodyPartDepth.Outside);
            target.TakeDamage(damageInfo);
            if (target.Spawned)
            {
                FleckMaker.Static(target.Position, target.Map, FleckDefOf.FireGlow, 1.15f);
            }
        }

        private static HediffComp_ZhaoliMinghuo GetActiveMinghuoComp(Pawn caster, ThingWithComps weapon)
        {
            if (caster?.health?.hediffSet == null || weapon == null)
            {
                return null;
            }

            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(ZhaoliMinghuoUtility.MinghuoHediffDefName);
            if (hediffDef == null)
            {
                return null;
            }

            Hediff hediff = caster.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            HediffComp_ZhaoliMinghuo comp = hediff?.TryGetComp<HediffComp_ZhaoliMinghuo>();
            return comp != null && comp.IsActiveFor(caster, weapon) ? comp : null;
        }
    }

    [HarmonyPatch(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAiming))]
    internal static class Patch_ZhaoliDuanzhan_DrawEquipmentAiming
    {
        private const string SlashTexturePath = "MiliraXianNeiyu/Items/MX_Neiyu_Form_Weapon_OnHand";
        private const float SlashTextureScaleFactor = 3f;
        private static readonly float[] AfterimageAlphas = { 0.45f, 0.28f, 0.16f };
        private static readonly float[] AfterimageAngleOffsets = { 10f, 22f, 36f };
        private static readonly Color[] AfterimageColors =
        {
            new Color(1f, 0.93f, 0.82f, 1f),
            new Color(0.98f, 0.48f, 0.32f, 1f),
            new Color(0.84f, 0.14f, 0.18f, 1f)
        };
        private static Material slashMaterial;
        private static Material[] afterimageMaterials;
        private static Vector2 slashBaseDrawSize = Vector2.one;
        private static bool triedLoadSlashMaterial;

        [HarmonyPrefix]
        private static bool Prefix(Thing eq, Vector3 drawLoc, float aimAngle)
        {
            if (eq == null || !DuanzhanVisualTracker.TryGetWeaponVisual(eq, out float weaponScale, out float drawAimAngle, out Vector3 drawOffset, out bool drawAfterimages))
            {
                return true;
            }

            return !DrawScaledEquipment(drawLoc + drawOffset, drawAimAngle, weaponScale * SlashTextureScaleFactor, drawAfterimages);
        }

        private static Material GetSlashMaterial()
        {
            if (!triedLoadSlashMaterial)
            {
                triedLoadSlashMaterial = true;
                Texture2D texture = ContentFinder<Texture2D>.Get(SlashTexturePath, reportFailure: false);
                if (texture != null)
                {
                    slashMaterial = MaterialPool.MatFrom(texture, ShaderDatabase.Cutout, Color.white);
                    afterimageMaterials = new Material[AfterimageColors.Length];
                    for (int i = 0; i < AfterimageColors.Length; i++)
                    {
                        afterimageMaterials[i] = MaterialPool.MatFrom(texture, ShaderDatabase.Cutout, AfterimageColors[i]);
                    }

                    slashBaseDrawSize = new Vector2(Mathf.Max(0.01f, texture.width / (float)Mathf.Max(1, texture.height)), 1f);
                }
            }

            return slashMaterial;
        }

        private static bool DrawScaledEquipment(Vector3 drawLoc, float aimAngle, float scaleMultiplier, bool drawAfterimages)
        {
            Material material = GetSlashMaterial();
            if (material == null)
            {
                return false;
            }

            if (drawAfterimages)
            {
                for (int i = AfterimageAlphas.Length - 1; i >= 0; i--)
                {
                    Material baseTrailMaterial = afterimageMaterials != null && i < afterimageMaterials.Length && afterimageMaterials[i] != null
                        ? afterimageMaterials[i]
                        : material;
                    Material afterimageMaterial = FadedMaterialPool.FadedVersionOf(baseTrailMaterial, AfterimageAlphas[i]);
                    float afterimageScale = scaleMultiplier * (1f + 0.05f * (i + 1));
                    float afterimageAngle = aimAngle - AfterimageAngleOffsets[i];
                    Vector3 sweepBackOffset = new Vector3(0f, 0.01f * (i + 1), 0f) - AimOffset(afterimageAngle) * (0.08f * (i + 1));
                    Vector3 afterimageDrawLoc = drawLoc + sweepBackOffset;
                    DrawSlashMesh(afterimageDrawLoc, afterimageAngle, afterimageScale, afterimageMaterial);
                }
            }

            DrawSlashMesh(drawLoc, aimAngle, scaleMultiplier, material);
            return true;
        }

        private static void DrawSlashMesh(Vector3 drawLoc, float aimAngle, float scaleMultiplier, Material material)
        {
            float rotation = aimAngle % 360f;
            Vector2 drawSize = slashBaseDrawSize * Mathf.Max(0.01f, scaleMultiplier);
            Quaternion quaternion = Quaternion.AngleAxis(rotation, Vector3.up);
            Vector3 pivotToCenter = quaternion * new Vector3(0f, 0f, drawSize.y * 0.5f);
            Vector3 meshCenter = drawLoc + pivotToCenter;
            Matrix4x4 matrix = Matrix4x4.TRS(meshCenter, quaternion, new Vector3(drawSize.x, 0f, drawSize.y));
            Mesh mesh = MeshPool.plane10;
            Graphics.DrawMesh(mesh, matrix, material, 0);
        }

        private static Vector3 AimOffset(float aimAngle)
        {
            float radians = aimAngle * 0.0174532924f;
            return new Vector3(Mathf.Sin(radians), 0f, Mathf.Cos(radians));
        }
    }
}

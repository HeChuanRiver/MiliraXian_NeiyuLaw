using System;
using System.Collections.Generic;
using System.Linq;
using MiliraXian.Characters.Common.Combat;
using MiliraXian.Characters.Common.Vfx.BladeBody;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Neiyu;

public sealed partial class JobDriver_NeiyuPostureExecution
{
    private enum MultiStage { Thrust, Spin, Air, Recovery }

    private const float MultiRadius = 4f;
    private const float MultiReleaseHeight = .78f;
    private const float MultiPursuitArc = 1.35f;
    private const int MultiPinAt = 8;
    private const int MultiHoldTicks = 4;
    private const int MultiReleaseAt = MultiPinAt + 60;
    private const int MultiSwingAt = MultiReleaseAt + 10;
    private const int MultiFirstCutAt = MultiSwingAt + 4;
    private const int MultiCutInterval = 6;
    private static readonly float[] MultiCutAngles = [0f, 62f, -52f, 101f, -91f, 39f, -28f, 136f, -132f, 82f, -72f, 16f];

    private MultiStage multiStage;
    private float pinReach, previousPinReach;
    private bool multiCombo, multiPursuit;
    private List<Vector3> multiCutContacts;

    private int MultiLastCut => multiCombo ? MultiCutAngles.Length - 1 : 0;
    private int MultiHitAt => MultiCutAt(MultiLastCut);
    private int MultiLandAt => MultiHitAt + (multiCombo ? 15 : 10);
    private int MultiFollowThroughAt => MultiHitAt + 5;
    private int MultiEndAt => MultiHitAt + 18;
    private static int MultiCutAt(int cut) => MultiFirstCutAt + cut * MultiCutInterval;
    private float MultiCutYaw(int cut) => throwYaw + MultiCutAngles[cut];
    private float MultiHandYaw(int cut) => throwYaw + MultiCutAngles[cut] * .26f;
    private float MultiCutPitch(int cut) => cut == 0 || cut == MultiLastCut ? 30f : cut % 2 == 0 ? 19f : 43f;

    private void BeginMulti()
    {
        multiCombo = true;
        multiPursuit = true;
        multiCutContacts = new List<Vector3>(MultiCutAngles.Length);
        sceneStart = startedAt;
        BeginSceneMotion();
        previousPinReach = pinReach = Vector3.Distance(actorDraw, targetGround);
        CombatPosture.Get(Victim)?.HoldThrough(pawn, sceneStart + MultiEndAt + 1);
    }

    private void ExposeMulti()
    {
        if (!IsMulti) return;
        Scribe_Values.Look(ref multiStage, "multiExecutionStage");
        Scribe_Values.Look(ref pinReach, "multiExecutionPinReach");
        Scribe_Values.Look(ref previousPinReach, "multiExecutionPreviousPinReach");
        // The existing air and recovery stages retain their original deadlines in older saves.
        Scribe_Values.Look(ref multiCombo, "multiExecutionCombo");
        Scribe_Values.Look(ref multiPursuit, "multiExecutionPursuit");
        if (multiPursuit)
        {
            Scribe_Values.Look(ref approachCell, "multiExecutionPursuitCell", IntVec3.Invalid);
            Scribe_Values.Look(ref curvedApproach, "multiExecutionPursuitCurved");
        }
        Scribe_Collections.Look(ref multiCutContacts, "multiExecutionCutContacts", LookMode.Value);
        if (Scribe.mode == LoadSaveMode.PostLoadInit) multiCutContacts ??= new List<Vector3>(MultiLastCut + 1);
    }

    private void TickMulti()
    {
        Pawn target = Victim;
        int tick = SceneTick;
        bool ownsTarget = ValidSceneTarget(target);
        if (pawn.Position != actorCell || (!strikeCommitted &&
            (!ownsTarget || (multiStage == MultiStage.Thrust && !NeiyuSwordCombat.InExecutionReach(pawn, target)))))
        {
            EndJobWith(JobCondition.Incompletable);
            return;
        }

        if (pawn.pather.Moving) pawn.pather.StopDead();
        previousActorDraw = actorDraw;
        previousTargetDraw = targetDraw;
        previousPinReach = pinReach;
        if (tick >= MultiPinAt && multiStage == MultiStage.Thrust)
        {
            multiStage = MultiStage.Spin;
            target.Rotation = Rot4.FromAngleFlat(initialYaw + 90f);
            NeiyuSwordCombatVfx.SingleSound(pawn, targetDraw, Single.firstCutSound ?? NeiyuSwordCombat.Config.strikeSound);
        }
        if (multiStage == MultiStage.Spin)
        {
            if (!MovePinnedTarget(target, Mathf.Min(tick, MultiReleaseAt)))
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            if (tick >= MultiReleaseAt)
            {
                multiStage = MultiStage.Air;
                // A short tangent carries the upward flick away from Neiyu. The landing
                // search clips that motion against doors, walls and occupied cells.
                BeginTargetMotion(target, Forward(initialYaw + 67f), multiPursuit ? 2.8f : 1.2f, MultiLandAt, .20f);
                if (multiPursuit) ChooseMultiPursuit(target);
                SetMultiAim(approachCell.IsValid && multiPursuit
                    ? approachCell.ToVector3Shifted().Yto0() : actorDraw);
                NeiyuSwordCombatVfx.SingleAfterimage(pawn, actorDraw - Forward(initialYaw) * .20f,
                    Rot4.FromAngleFlat(MultiYaw(MultiReleaseAt - 8)), NeiyuSwordCombat.Now, 12, this);
            }
        }
        if (ownsTarget && (multiStage is MultiStage.Air or MultiStage.Recovery))
        {
            MoveTargetAlongMotion(target, tick, MultiReleaseAt, MultiReleaseHeight, 1.48f,
                verticalThrow: true, groundTravelTicks: multiPursuit ? 24 : 0);
            if (!strikeCommitted) aimPoint = targetDraw;
            if (multiPursuit && multiStage == MultiStage.Air && tick <= MultiSwingAt)
                MoveMultiPursuit(target, tick);
        }

        pawn.Rotation = Rot4.FromAngleFlat(tick <= MultiReleaseAt ? MultiYaw(tick)
            : Mathf.LerpAngle(initialYaw, throwYaw, ProgressBetween(tick, MultiReleaseAt, MultiSwingAt)));
        while (multiStage == MultiStage.Air && !strikeCommitted && multiCutContacts.Count <= MultiLastCut
            && tick >= MultiCutAt(multiCutContacts.Count))
        {
            int cut = multiCutContacts.Count;
            multiCutContacts.Add(aimPoint);
            if (cut == MultiLastCut)
            {
                multiStage = MultiStage.Recovery;
                if (!CommitStrike(target)) return;
            }
            else if (cut % 3 == 0)
            {
                NeiyuSwordCombatVfx.SingleSound(pawn, aimPoint, Single.swordLaunchSound);
            }
        }
        if (tick >= MultiEndAt) EndJobWith(JobCondition.Succeeded);
    }

    private float MultiYaw(float tick) => initialYaw
        + 360f * Mathf.SmoothStep(0f, 1f, ProgressBetween(tick, MultiPinAt + MultiHoldTicks, MultiReleaseAt));

    private static float MultiLift(float tick) =>
        Mathf.SmoothStep(0f, MultiReleaseHeight, ProgressBetween(tick, MultiReleaseAt - 12, MultiReleaseAt));

    private float MultiBladeScale(float tick) => multiPursuit
        ? 1f + .85f * (ProgressBetween(tick, MultiPinAt, MultiReleaseAt)
            - ProgressBetween(tick, MultiReleaseAt, MultiSwingAt))
        : 1f;

    private bool MovePinnedTarget(Pawn target, int tick)
    {
        // The blade makes contact in the original cell and holds before pulling the victim off it.
        if (tick <= MultiPinAt + MultiHoldTicks) return true;
        float initialReach = Vector3.Distance(origin.ToVector3Shifted().Yto0(), hitCell.ToVector3Shifted().Yto0());
        float preferred = Mathf.Lerp(initialReach, Mathf.Clamp(initialReach, .92f, 1.52f),
            ProgressBetween(tick, MultiPinAt + MultiHoldTicks, MultiPinAt + 19));
        float reach = Mathf.Min(preferred * MultiBladeScale(tick), pinReach + .16f);
        float yaw = MultiYaw(tick);
        Vector3 radial = Forward(yaw);
        Vector3 next = actorDraw + radial * reach;
        while (!CanTravel(target, actorDraw, next, pawn) || !CanTravel(target, targetGround, next, pawn))
        {
            if (reach <= 0f) return false;
            reach = Mathf.Max(0f, reach - .20f);
            next = actorDraw + radial * reach;
        }

        pinReach = reach;
        targetGround = next;
        targetDraw = next + Vector3.forward * MultiLift(tick);
        MovePawn(target, ref targetCell, next.ToIntVec3());
        target.Rotation = Rot4.FromAngleFlat(yaw + 90f);
        return true;
    }

    private void ChooseMultiPursuit(Pawn target)
    {
        approachCell = IntVec3.Invalid;
        Vector3 landing = landingCell.ToVector3Shifted().Yto0();
        Vector3 axis = (landing - actorDraw).normalized;
        if (axis.sqrMagnitude < .001f) axis = Forward(initialYaw + 67f);
        float best = .10f;
        foreach (IntVec3 offset in GenAdj.AdjacentCells)
        {
            IntVec3 cell = landingCell + offset;
            float alignment = Vector3.Dot(offset.ToVector3().normalized, axis);
            if (alignment <= best || !CanOccupy(pawn, cell)) continue;
            bool curved = ClearMultiPursuitArc(target, cell);
            if (!curved && !CanTravel(pawn, actorDraw, cell.ToVector3Shifted().Yto0(), target)) continue;
            approachCell = cell;
            curvedApproach = curved;
            best = alignment;
        }
    }

    private bool ClearMultiPursuitArc(Pawn target, IntVec3 cell)
    {
        Vector3 previous = actorDraw;
        for (int step = 1; step <= 12; step++)
        {
            Vector3 next = ApproachAtProgress(step / 12f, cell, true, MultiPursuitArc);
            if (!CanTravel(pawn, previous, next, target)) return false;
            previous = next;
        }
        return true;
    }

    private void MoveMultiPursuit(Pawn target, int tick)
    {
        if (!approachCell.IsValid) return;
        Vector3 next = ApproachAtProgress(ProgressBetween(tick, MultiReleaseAt, MultiSwingAt),
            approachCell, curvedApproach, MultiPursuitArc);
        if (!CanTravel(pawn, actorDraw, next, target) ||
            (tick == MultiSwingAt && !CanOccupy(pawn, approachCell)))
        {
            // Keep the last legal position if the path becomes blocked during the dash.
            approachCell = IntVec3.Invalid;
            SetMultiAim(actorDraw);
            return;
        }
        actorDraw = next;
        MovePawn(pawn, ref actorCell, next.ToIntVec3());
        if (tick == MultiSwingAt) SetMultiAim(actorDraw);
    }

    private void SetMultiAim(Vector3 from)
    {
        Vector3 aim = landingCell.ToVector3Shifted().Yto0() - from;
        throwYaw = aim.sqrMagnitude > .001f ? aim.AngleFlat() : initialYaw;
    }

    private Vector3 MultiTargetAtFrame(float tick)
    {
        if (tick >= MultiPinAt && tick <= MultiReleaseAt)
        {
            float reach = Mathf.Lerp(previousPinReach, pinReach, FrameLerp);
            return origin.ToVector3Shifted().Yto0() + Forward(MultiYaw(tick)) * reach + Vector3.forward * MultiLift(tick);
        }
        return Vector3.Lerp(previousTargetDraw, targetDraw, FrameLerp);
    }

    private Vector3 MultiCutContact(int cut, float tick)
    {
        if (multiCutContacts != null && cut < multiCutContacts.Count) return multiCutContacts[cut];
        return strikeCommitted ? aimPoint : MultiTargetAtFrame(tick);
    }

    private BladePose MultiBladeAt(BladeGraphicExtension blade, float tick)
    {
        Vector3 body = Vector3.Lerp(previousActorDraw, actorDraw, FrameLerp);
        float yaw, pitch;
        Vector3 tip;
        if (tick < MultiPinAt)
        {
            float thrust = ProgressBetween(tick, 0, MultiPinAt);
            yaw = initialYaw;
            pitch = Mathf.Lerp(56f, 6f, thrust);
            Vector3 ready = body + Forward(yaw) * .65f + Vector3.forward * .28f;
            tip = Vector3.Lerp(ready, hitCell.ToVector3Shifted().Yto0(), thrust * thrust);
        }
        else if (tick <= MultiReleaseAt)
        {
            yaw = MultiYaw(tick);
            float reach = Mathf.Lerp(previousPinReach, pinReach, FrameLerp);
            pitch = MultiPinnedPitch(tick, reach / MultiBladeScale(tick));
            tip = MultiTargetAtFrame(tick);
        }
        else
        {
            float finalYaw = MultiHandYaw(MultiLastCut);
            Vector3 cocked = body + Forward(throwYaw - 72f) * .85f + Vector3.forward * .46f;
            Vector3 follow = body + Forward(finalYaw + 67f) * 1.58f + Vector3.forward * .28f;
            if (tick < MultiSwingAt)
            {
                float charge = Mathf.SmoothStep(0f, 1f, ProgressBetween(tick, MultiReleaseAt, MultiSwingAt));
                Vector3 released = origin.ToVector3Shifted().Yto0() + Forward(initialYaw) * pinReach + Vector3.forward * MultiReleaseHeight;
                yaw = Mathf.LerpAngle(initialYaw, throwYaw - 72f, charge);
                pitch = Mathf.Lerp(MultiPinnedPitch(MultiReleaseAt, pinReach / MultiBladeScale(MultiReleaseAt)), 48f, charge);
                tip = Vector3.Lerp(released, cocked, charge);
            }
            else if (tick <= MultiFirstCutAt)
            {
                float cut = ProgressBetween(tick, MultiSwingAt, MultiFirstCutAt);
                cut *= cut;
                yaw = Mathf.Lerp(throwYaw - 72f, throwYaw, cut);
                pitch = Mathf.Lerp(48f, 30f, cut);
                tip = Vector3.Lerp(cocked, MultiCutContact(0, tick), cut);
            }
            else if (tick < MultiHitAt)
            {
                int cut = (int)((tick - MultiFirstCutAt) / MultiCutInterval);
                float phase = ProgressBetween(tick, MultiCutAt(cut), MultiCutAt(cut + 1));
                float lift = Mathf.Sin(phase * Mathf.PI);
                float turn = cut % 2 == 0 ? lift : -lift;
                yaw = Mathf.LerpAngle(MultiHandYaw(cut), MultiHandYaw(cut + 1), phase);
                pitch = Mathf.Lerp(MultiCutPitch(cut), MultiCutPitch(cut + 1), phase) + 12f * lift;
                tip = Vector3.Lerp(MultiCutContact(cut, tick), MultiCutContact(cut + 1, tick), phase)
                    + Forward(yaw + 90f) * (.92f * turn);
                yaw += 31f * turn;
            }
            else if (tick < MultiFollowThroughAt)
            {
                float cut = ProgressBetween(tick, MultiHitAt, MultiFollowThroughAt);
                yaw = Mathf.Lerp(finalYaw, finalYaw + 67f, cut);
                pitch = Mathf.Lerp(30f, 3f, cut);
                tip = Vector3.Lerp(aimPoint, follow, cut);
            }
            else
            {
                float settle = Mathf.SmoothStep(0f, 1f, ProgressBetween(tick, MultiFollowThroughAt, MultiEndAt));
                yaw = Mathf.Lerp(finalYaw + 67f, finalYaw + 28f, settle);
                pitch = Mathf.Lerp(3f, 49f, settle);
                tip = Vector3.Lerp(follow, body + Forward(finalYaw + 28f) * .65f + Vector3.forward * .25f, settle);
            }
        }

        float tilt = pitch * Mathf.Deg2Rad;
        Vector3 direction = Forward(yaw) * Mathf.Cos(tilt) + Vector3.up * Mathf.Sin(tilt);
        BladePose pose = new()
        {
            dir = direction,
            edge = Quaternion.AngleAxis(18f, direction) * Forward(yaw + 90f),
            heightToScreen = .68f,
            lengthScale = MultiBladeScale(tick),
            reveal = NeiyuSwordCombatVfx.Enabled && NeiyuSwordCombatVfx.Intensity > 0f
                ? ProgressBetween(tick, MultiReleaseAt, MultiSwingAt)
                    * (1f - ProgressBetween(tick, MultiHitAt, MultiFollowThroughAt))
                : 0f
        };
        // Solve the grip from the same projected tip that carries the pawn.
        BladeRenderer.GetGripAndTip(blade, pose, body, out _, out Vector3 neutralTip);
        pose.grip = (tip - neutralTip).Yto0();
        // After release the wide slash travels to the victim, while the actual hand stays in reach.
        if (multiCombo && tick > MultiReleaseAt)
            pose.grip = Vector3.Lerp(pose.grip, Vector3.ClampMagnitude(pose.grip, 1.15f),
                ProgressBetween(tick, MultiReleaseAt, MultiReleaseAt + 4));
        return pose;
    }

    private static float MultiPinnedPitch(float tick, float reach) =>
        Mathf.Max(Mathf.Lerp(6f, 65f, MultiLift(tick) / MultiReleaseHeight),
            Mathf.Lerp(76f, 6f, Mathf.Clamp01(reach / 1.4f)));

    private void SubmitMultiSlash(Pawn victim)
    {
        Pawn caster = pawn;
        Map map = executionMap;
        List<Pawn> targets = new();
        foreach (Pawn other in map.mapPawns.AllPawnsSpawned)
            if (other != victim && other.Position.DistanceToSquared(actorCell) <= MultiRadius * MultiRadius
                && SceneEnemy(other))
                targets.Add(other);

        Verb blade = executionWeapon.TryGetComp<CompEquippable>()?.AllVerbs.FirstOrDefault(
            verb => verb.verbProps.IsMeleeAttack && verb.verbProps.meleeDamageDef == DamageDefOf.Cut);
        float amount = blade?.verbProps.AdjustedMeleeDamageAmount(blade, caster) ?? 0f;
        NeiyuSwordCombatVfx.SingleSound(caster, aimPoint, Single.firstCutSound ?? NeiyuSwordCombat.Config.strikeSound);
        if (!float.IsFinite(amount) || amount <= 0f) return;

        DamageInfo hit = new(DamageDefOf.Cut, amount, 0f, -1f, caster, null, executionWeapon.def);
        hit.SetTool(blade.tool);
        hit.SetIgnoreArmor(true);
        // The batch is sampled once and submitted before the original target's death callback.
        foreach (Pawn other in targets)
        {
            if (other is not { Spawned: true, Dead: false, Destroyed: false } || other.Map != map) continue;
            try { other.TakeDamage(hit); }
            catch (Exception exception)
            {
                Log.ErrorOnce($"[霓羽弹刀] 终斩范围伤害回调失败：{exception}", 197632110);
            }
        }
    }
}

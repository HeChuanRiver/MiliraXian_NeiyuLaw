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
    private const int MultiPinAt = 8;
    private const int MultiHoldTicks = 4;
    private const int MultiReleaseAt = MultiPinAt + 60;
    private const int MultiSwingAt = MultiReleaseAt + 10;
    private const int MultiHitAt = MultiSwingAt + 4;
    private const int MultiLandAt = MultiReleaseAt + 24;
    private const int MultiFollowThroughAt = MultiHitAt + 5;
    private const int MultiEndAt = MultiHitAt + 18;

    private MultiStage multiStage;
    private float pinReach, previousPinReach;

    private void BeginMulti()
    {
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
                BeginTargetMotion(target, Forward(initialYaw + 67f), 1.2f, MultiLandAt, .20f);
                Vector3 aim = landingCell.ToVector3Shifted().Yto0() - actorDraw;
                throwYaw = aim.sqrMagnitude > .001f ? aim.AngleFlat() : initialYaw;
                NeiyuSwordCombatVfx.SingleAfterimage(pawn, actorDraw - Forward(initialYaw) * .20f,
                    Rot4.FromAngleFlat(MultiYaw(MultiReleaseAt - 8)), NeiyuSwordCombat.Now, 12, this);
            }
        }
        if (ownsTarget && (multiStage is MultiStage.Air or MultiStage.Recovery))
        {
            MoveTargetAlongMotion(target, tick, MultiReleaseAt, MultiReleaseHeight, 1.48f, verticalThrow: true);
            if (!strikeCommitted) aimPoint = targetDraw;
        }

        pawn.Rotation = Rot4.FromAngleFlat(tick <= MultiReleaseAt ? MultiYaw(tick)
            : Mathf.LerpAngle(initialYaw, throwYaw, ProgressBetween(tick, MultiReleaseAt, MultiSwingAt)));
        if (tick >= MultiHitAt && !strikeCommitted)
        {
            multiStage = MultiStage.Recovery;
            if (!CommitStrike(target)) return;
        }
        if (tick >= MultiEndAt) EndJobWith(JobCondition.Succeeded);
    }

    private float MultiYaw(float tick) => initialYaw
        + 360f * Mathf.SmoothStep(0f, 1f, ProgressBetween(tick, MultiPinAt + MultiHoldTicks, MultiReleaseAt));

    private static float MultiLift(float tick) =>
        Mathf.SmoothStep(0f, MultiReleaseHeight, ProgressBetween(tick, MultiReleaseAt - 12, MultiReleaseAt));

    private bool MovePinnedTarget(Pawn target, int tick)
    {
        // The blade makes contact in the original cell and holds before pulling the victim off it.
        if (tick <= MultiPinAt + MultiHoldTicks) return true;
        float initialReach = Vector3.Distance(origin.ToVector3Shifted().Yto0(), hitCell.ToVector3Shifted().Yto0());
        float preferred = Mathf.Lerp(initialReach, Mathf.Clamp(initialReach, .92f, 1.52f),
            ProgressBetween(tick, MultiPinAt + MultiHoldTicks, MultiPinAt + 19));
        float reach = Mathf.Min(preferred, pinReach + .16f);
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

    private Vector3 MultiTargetAtFrame(float tick)
    {
        if (tick >= MultiPinAt && tick <= MultiReleaseAt)
        {
            float reach = Mathf.Lerp(previousPinReach, pinReach, FrameLerp);
            return actorDraw + Forward(MultiYaw(tick)) * reach + Vector3.forward * MultiLift(tick);
        }
        return Vector3.Lerp(previousTargetDraw, targetDraw, FrameLerp);
    }

    private BladePose MultiBladeAt(BladeGraphicExtension blade, float tick)
    {
        float yaw, pitch;
        Vector3 tip;
        if (tick < MultiPinAt)
        {
            float thrust = ProgressBetween(tick, 0, MultiPinAt);
            yaw = initialYaw;
            pitch = Mathf.Lerp(56f, 6f, thrust);
            Vector3 ready = actorDraw + Forward(yaw) * .65f + Vector3.forward * .28f;
            tip = Vector3.Lerp(ready, hitCell.ToVector3Shifted().Yto0(), thrust * thrust);
        }
        else if (tick <= MultiReleaseAt)
        {
            yaw = MultiYaw(tick);
            float reach = Mathf.Lerp(previousPinReach, pinReach, FrameLerp);
            pitch = MultiPinnedPitch(tick, reach);
            tip = MultiTargetAtFrame(tick);
        }
        else
        {
            Vector3 cocked = actorDraw + Forward(throwYaw - 72f) * .85f + Vector3.forward * .46f;
            Vector3 follow = actorDraw + Forward(throwYaw + 67f) * 1.58f + Vector3.forward * .28f;
            if (tick < MultiSwingAt)
            {
                float charge = Mathf.SmoothStep(0f, 1f, ProgressBetween(tick, MultiReleaseAt, MultiSwingAt));
                Vector3 released = actorDraw + Forward(initialYaw) * pinReach + Vector3.forward * MultiReleaseHeight;
                yaw = Mathf.LerpAngle(initialYaw, throwYaw - 72f, charge);
                pitch = Mathf.Lerp(MultiPinnedPitch(MultiReleaseAt, pinReach), 48f, charge);
                tip = Vector3.Lerp(released, cocked, charge);
            }
            else if (tick <= MultiHitAt)
            {
                float cut = ProgressBetween(tick, MultiSwingAt, MultiHitAt);
                cut *= cut;
                yaw = Mathf.Lerp(throwYaw - 72f, throwYaw, cut);
                pitch = Mathf.Lerp(48f, 30f, cut);
                tip = Vector3.Lerp(cocked, strikeCommitted ? aimPoint : MultiTargetAtFrame(tick), cut);
            }
            else if (tick < MultiFollowThroughAt)
            {
                float cut = ProgressBetween(tick, MultiHitAt, MultiFollowThroughAt);
                yaw = Mathf.Lerp(throwYaw, throwYaw + 67f, cut);
                pitch = Mathf.Lerp(30f, 3f, cut);
                tip = Vector3.Lerp(aimPoint, follow, cut);
            }
            else
            {
                float settle = Mathf.SmoothStep(0f, 1f, ProgressBetween(tick, MultiFollowThroughAt, MultiEndAt));
                yaw = Mathf.Lerp(throwYaw + 67f, throwYaw + 28f, settle);
                pitch = Mathf.Lerp(3f, 49f, settle);
                tip = Vector3.Lerp(follow, actorDraw + Forward(throwYaw + 28f) * .65f + Vector3.forward * .25f, settle);
            }
        }

        float tilt = pitch * Mathf.Deg2Rad;
        Vector3 direction = Forward(yaw) * Mathf.Cos(tilt) + Vector3.up * Mathf.Sin(tilt);
        BladePose pose = new()
        {
            dir = direction,
            edge = Quaternion.AngleAxis(18f, direction) * Forward(yaw + 90f),
            heightToScreen = .68f,
            reveal = NeiyuSwordCombatVfx.Enabled && NeiyuSwordCombatVfx.Intensity > 0f
                ? ProgressBetween(tick, MultiReleaseAt, MultiSwingAt)
                    * (1f - ProgressBetween(tick, MultiHitAt, MultiFollowThroughAt))
                : 0f
        };
        // Solve the grip from the same projected tip that carries the pawn.
        BladeRenderer.GetGripAndTip(blade, pose, actorDraw, out _, out Vector3 neutralTip);
        pose.grip = (tip - neutralTip).Yto0();
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

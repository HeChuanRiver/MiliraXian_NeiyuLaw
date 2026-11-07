using MiliraXian.Characters.Common.Combat;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Common.Vfx.BladeBody;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Neiyu;

public sealed class NeiyuSingleExecutionProps
{
    public float enemyRadius = 8f, slashWidth = 18f, knockbackDistance = 3.5f, throwDistance = 25f, spiritSwordScale = 1.45f;
    public int lockTicks = 150;
    public int readyTicks = 20, enchantTicks = 30, flashTicks = 3, swingTicks = 8, hitStopTicks = 9;
    public int knockbackTicks = 24, approachTicks = 24, spinLeadTicks = 12, spinTicksPerTurn = 30;
    public int flightTicks = 30, glyphDelayTicks = 8, swordCount = 5, swordStaggerTicks = 20, swordFlightTicks = 36, finishTicks = 24;
    public int waveTicks = 38, glyphHoldTicks = 9, glyphFadeTicks = 15, pierceTicks = 17, afterimageTicks = 20;
    public SoundDef pierceSound;
    public BladeKeyframes drawBlade, aimBlade;

    internal bool Valid => float.IsFinite(enemyRadius) && enemyRadius > 0f
        && slashWidth is >= 5f and <= 18f && knockbackDistance is >= 1f and <= 6f && throwDistance is >= 2f and <= 25f
        && spiritSwordScale is >= .5f and <= 2.5f && lockTicks >= 0
        && readyTicks > 0 && enchantTicks > 0 && flashTicks > 0 && swingTicks >= 4 && hitStopTicks >= 0
        && knockbackTicks > 0 && approachTicks > 0 && spinLeadTicks > 0 && spinTicksPerTurn >= 8
        && flightTicks is > 1 and <= 30 && glyphDelayTicks > 0
        && swordCount is > 0 and <= 9 && swordStaggerTicks > 0 && swordFlightTicks > 0 && finishTicks > 0
        && waveTicks > 0 && glyphHoldTicks >= 0 && glyphFadeTicks > 0 && pierceTicks > 0 && afterimageTicks > 0
        && drawBlade?.keys.Count >= 2 && aimBlade?.keys.Count >= 2;
}

public sealed partial class JobDriver_NeiyuPostureExecution
{
    private enum SingleStage { Lock, Draw, FirstCut, Approach, Grab, Flight, Volley, Recovery, Knockback }

    private SingleStage singleStage;
    private int sceneStart, spinTurns, impacts, touchdown;
    private int hitStopTicks, knockbackTicks, approachTicks, spinLeadTicks, spinTicksPerTurn;
    private int flightTicks, glyphDelayTicks, swordCount, swordStaggerTicks, swordFlightTicks, finishTicks;
    private int volleyStart = -1;
    private IntVec3 actorCell, targetCell, approachCell, landingCell;
    private IntVec3 grabStartCell = IntVec3.Invalid;
    private Vector3 actorDraw, targetDraw, previousActorDraw, previousTargetDraw;
    private Vector3 targetGround, motionOrigin, aimPoint;
    private float initialYaw, throwYaw, throwDistance;
    private bool curvedApproach, afterimageBound;

    private NeiyuSingleExecutionProps Single => NeiyuSwordCombat.Config.single;
    private int SceneTick => NeiyuSwordCombat.Now - sceneStart;
    private float SceneFrame => VisualClock.TicksSince(sceneStart);
    private int FlashAt => Single.readyTicks + Single.enchantTicks;
    private int SwingAt => FlashAt + Single.flashTicks;
    private int FirstCutAt => SwingAt + Mathf.RoundToInt(Single.swingTicks * .625f);
    private int KnockbackAt => SwingAt + Single.swingTicks + hitStopTicks;
    private int ApproachAt => KnockbackAt + knockbackTicks;
    private int GrabAt => ApproachAt + approachTicks;
    private int SpinAt => GrabAt + spinLeadTicks;
    private int ReleaseAt => SpinAt + spinTurns * spinTicksPerTurn;
    private int AimAt => ReleaseAt + glyphDelayTicks;
    private bool VolleyAfterLanding => swordFlightTicks > 0;
    private int VolleyAt => VolleyAfterLanding
        ? (volleyStart >= 0 ? volleyStart : AimAt + flightTicks)
        : AimAt;
    private int SwordLaunchAt(int index) => VolleyAt + index * swordStaggerTicks;
    private int SwordImpactAt(int index) => SwordLaunchAt(index)
        + (VolleyAfterLanding ? swordFlightTicks : flightTicks - glyphDelayTicks);
    private int FinalImpactAt => SwordImpactAt(swordCount - 1);
    private int EndAt => FinalImpactAt + finishTicks;
    private IntVec3 GrabCell => grabStartCell.IsValid ? grabStartCell : hitCell;

    private Branch ChooseBranch(Pawn target)
    {
        if (Single?.Valid != true) return Branch.Compatibility;
        float radiusSquared = Single.enemyRadius * Single.enemyRadius;
        // The original target has already passed the execution eligibility check.
        foreach (Pawn other in executionMap.mapPawns.AllPawnsSpawned)
        {
            if (other == target || other.Downed || !NeiyuSwordCombat.HostilePair(pawn, other)
                || other.Position.DistanceToSquared(hitCell) > radiusSquared) continue;
            if (other.Awake() && !other.Deathresting && !other.InContainerEnclosed && !CombatPosture.ControlsActions(other)
                && other.health.capacities.CanBeAwake && !other.WorkTagIsDisabled(WorkTags.Violent))
                return Branch.Compatibility;
        }
        return Branch.Single;
    }

    private void BeginSingle()
    {
        HediffComp_CombatPosture posture = CombatPosture.Get(Victim);
        sceneStart = (posture?.BrokenAt is >= 0 ? posture.BrokenAt : startedAt) + Single.lockTicks;
        spinTurns = Rand.RangeInclusive(1, 3);
        hitStopTicks = Single.hitStopTicks;
        knockbackTicks = Single.knockbackTicks;
        approachTicks = Single.approachTicks;
        spinLeadTicks = Single.spinLeadTicks;
        spinTicksPerTurn = Single.spinTicksPerTurn;
        flightTicks = Single.flightTicks;
        glyphDelayTicks = Single.glyphDelayTicks;
        swordCount = Single.swordCount;
        swordStaggerTicks = Single.swordStaggerTicks;
        swordFlightTicks = Single.swordFlightTicks;
        finishTicks = Single.finishTicks;
        throwDistance = Single.throwDistance;
        posture?.HoldThrough(pawn, sceneStart + EndAt + 1);
        actorCell = origin;
        targetCell = hitCell;
        previousActorDraw = actorDraw = origin.ToVector3Shifted().Yto0();
        previousTargetDraw = targetDraw = targetGround = aimPoint = hitCell.ToVector3Shifted().Yto0();
        Vector3 direction = targetGround - actorDraw;
        initialYaw = direction.sqrMagnitude > .001f ? direction.AngleFlat() : pawn.Rotation.AsAngle;
        pawn.pather.StopDead();
    }

    private void ExposeSingle()
    {
        if (!IsSingle) return;
        Scribe_Values.Look(ref singleStage, "singleExecutionStage");
        Scribe_Values.Look(ref sceneStart, "singleExecutionSceneStart");
        // Older saves already hold a victim through their original deadlines.
        Scribe_Values.Look(ref hitStopTicks, "singleExecutionHitStopTicks", 5);
        Scribe_Values.Look(ref knockbackTicks, "singleExecutionKnockbackTicks", 0);
        Scribe_Values.Look(ref approachTicks, "singleExecutionApproachTicks", 12);
        Scribe_Values.Look(ref spinLeadTicks, "singleExecutionSpinLeadTicks", 8);
        Scribe_Values.Look(ref spinTicksPerTurn, "singleExecutionSpinTicksPerTurn", 20);
        Scribe_Values.Look(ref flightTicks, "singleExecutionFlightTicks", 24);
        Scribe_Values.Look(ref glyphDelayTicks, "singleExecutionGlyphDelayTicks", 7);
        Scribe_Values.Look(ref swordCount, "singleExecutionSwordCount", 5);
        Scribe_Values.Look(ref swordStaggerTicks, "singleExecutionSwordStaggerTicks", 2);
        Scribe_Values.Look(ref swordFlightTicks, "singleExecutionSwordFlightTicks");
        Scribe_Values.Look(ref volleyStart, "singleExecutionVolleyStart", -1);
        Scribe_Values.Look(ref finishTicks, "singleExecutionFinishTicks", 14);
        Scribe_Values.Look(ref throwDistance, "singleExecutionThrowDistance", 6f);
        Scribe_Values.Look(ref spinTurns, "singleExecutionTurns");
        Scribe_Values.Look(ref impacts, "singleExecutionImpacts");
        Scribe_Values.Look(ref touchdown, "singleExecutionTouchdown");
        Scribe_Values.Look(ref actorCell, "singleExecutionActorCell");
        Scribe_Values.Look(ref targetCell, "singleExecutionTargetCell");
        Scribe_Values.Look(ref approachCell, "singleExecutionApproachCell");
        Scribe_Values.Look(ref grabStartCell, "singleExecutionGrabCell", IntVec3.Invalid);
        Scribe_Values.Look(ref landingCell, "singleExecutionLanding");
        Scribe_Values.Look(ref actorDraw, "singleExecutionActorDraw");
        Scribe_Values.Look(ref targetDraw, "singleExecutionTargetDraw");
        Scribe_Values.Look(ref previousActorDraw, "singleExecutionPreviousActorDraw");
        Scribe_Values.Look(ref previousTargetDraw, "singleExecutionPreviousTargetDraw");
        Scribe_Values.Look(ref targetGround, "singleExecutionTargetGround");
        Scribe_Values.Look(ref motionOrigin, "singleExecutionThrowOrigin");
        Scribe_Values.Look(ref aimPoint, "singleExecutionAimPoint");
        Scribe_Values.Look(ref initialYaw, "singleExecutionInitialYaw");
        Scribe_Values.Look(ref throwYaw, "singleExecutionThrowYaw");
        Scribe_Values.Look(ref curvedApproach, "singleExecutionCurvedApproach");
        if (Scribe.mode == LoadSaveMode.PostLoadInit
            && (singleStage is SingleStage.Flight or SingleStage.Volley or SingleStage.Recovery))
            executionMap?.GetComponent<MapComponent_NeiyuSwordVfx>()?.TrackVolley(this);
    }

    private void TickSingle()
    {
        Pawn target = Victim;
        int tick = SceneTick;
        bool beforeDisplacement = singleStage is SingleStage.Lock or SingleStage.Draw or SingleStage.FirstCut;
        if (pawn.Position != actorCell || (!strikeCommitted
            && (!NeiyuSwordCombat.ExecutionTarget(target) || !NeiyuSwordCombat.HostilePair(pawn, target)
                || target.Position != targetCell || CombatPosture.Get(target)?.Owns(pawn) != true
                || (beforeDisplacement && !NeiyuSwordCombat.InExecutionReach(pawn, target)))))
        {
            EndJobWith(JobCondition.Incompletable);
            return;
        }
        if (pawn.pather.Moving) pawn.pather.StopDead();
        if (tick < 0) return;

        previousActorDraw = actorDraw;
        previousTargetDraw = targetDraw;
        if (singleStage == SingleStage.Lock)
        {
            singleStage = SingleStage.Draw;
            NeiyuSwordCombatVfx.SingleSound(pawn, actorDraw, NeiyuSwordCombat.Config.prepareSound);
        }
        if (tick >= FirstCutAt && singleStage == SingleStage.Draw)
        {
            singleStage = SingleStage.FirstCut;
            NeiyuSwordCombatVfx.SingleSound(pawn, targetDraw, NeiyuSwordCombat.Config.strikeSound);
        }
        if (tick >= KnockbackAt && singleStage == SingleStage.FirstCut && knockbackTicks > 0)
        {
            singleStage = SingleStage.Knockback;
            BeginTargetMotion(target, Forward(initialYaw), Single.knockbackDistance, ApproachAt, .65f);
        }
        if (singleStage == SingleStage.Knockback)
            MoveTargetAlongMotion(target, tick, KnockbackAt, 0f, .19f);
        if (tick >= ApproachAt && (singleStage is SingleStage.FirstCut or SingleStage.Knockback))
        {
            grabStartCell = targetCell;
            if (!ChooseApproach())
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            singleStage = SingleStage.Approach;
        }
        if (!afterimageBound && tick >= ApproachAt && tick < ApproachAt + Single.afterimageTicks)
        {
            afterimageBound = true;
            NeiyuSwordCombatVfx.SingleAfterimage(pawn, origin.ToVector3Shifted(), Rot4.FromAngleFlat(initialYaw - 18f),
                sceneStart + ApproachAt, Single.afterimageTicks, this);
        }
        if (singleStage == SingleStage.Approach)
        {
            Vector3 next = ApproachAtProgress(ProgressBetween(tick, ApproachAt, GrabAt), approachCell, curvedApproach);
            if (!CanTravel(pawn, actorDraw, next))
            {
                EndJobWith(JobCondition.Incompletable);
                return;
            }
            actorDraw = next;
            MovePawn(pawn, ref actorCell, actorDraw.ToIntVec3());
            if (tick >= GrabAt)
            {
                if (!actorCell.AdjacentTo8WayOrInside(targetCell) || !NeiyuSwordCombat.InExecutionReach(pawn, target))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }
                singleStage = SingleStage.Grab;
            }
        }

        if (singleStage == SingleStage.Grab)
        {
            MoveHeldTarget(target, Mathf.Min(tick, ReleaseAt));
            if (tick >= ReleaseAt)
            {
                singleStage = SingleStage.Flight;
                BeginThrow(target);
            }
        }
        if (singleStage == SingleStage.Flight || (!VolleyAfterLanding && singleStage == SingleStage.Volley))
        {
            MoveTargetAlongMotion(target, tick, ReleaseAt, .23f, .55f);
            aimPoint = targetDraw;
            if (VolleyAfterLanding && tick >= touchdown)
            {
                volleyStart = touchdown + glyphDelayTicks;
                singleStage = SingleStage.Volley;
            }
            else if (!VolleyAfterLanding && tick >= VolleyAt)
                singleStage = SingleStage.Volley;
        }
        else if (singleStage == SingleStage.FirstCut)
        {
            float recoil = Mathf.Sin(ProgressBetween(tick, FirstCutAt, KnockbackAt) * Mathf.PI);
            targetDraw = targetGround + Forward(initialYaw) * (.16f * recoil);
        }
        else if (singleStage is SingleStage.Draw or SingleStage.Approach)
        {
            targetDraw = targetGround;
        }

        pawn.Rotation = Rot4.FromAngleFlat(BodyYaw(tick, actorDraw, aimPoint));
        while (impacts < swordCount && tick >= SwordImpactAt(impacts))
        {
            impacts++;
            if (impacts == swordCount && !strikeCommitted)
            {
                // Set the terminal frame before invoking callbacks which can save the game.
                singleStage = SingleStage.Recovery;
                if (!CommitStrike(target)) return;
            }
        }
        if (tick >= EndAt) EndJobWith(JobCondition.Succeeded);
    }

    private static float ProgressBetween(float tick, int start, int end) =>
        Mathf.Clamp01((tick - start) / Mathf.Max(1, end - start));

    private static Vector3 Forward(float degrees) => Quaternion.AngleAxis(degrees, Vector3.up) * Vector3.forward;

    private float GrabYaw
    {
        get
        {
            Vector3 reach = (GrabCell - approachCell).ToVector3();
            return reach.sqrMagnitude > .001f ? reach.AngleFlat() : initialYaw;
        }
    }

    private float SpinYaw(float tick)
    {
        float p = ProgressBetween(tick, SpinAt, ReleaseAt);
        // Accelerate continuously into the release, with the hand travelling a full number of turns.
        return GrabYaw + 360f * spinTurns * p * p;
    }

    private float BodyYaw(float tick, Vector3 actor, Vector3 aim)
    {
        if (tick < ApproachAt)
            return initialYaw + Mathf.Lerp(48f, -18f, ProgressBetween(tick, SwingAt, FirstCutAt));
        if (tick < GrabAt)
            return Mathf.LerpAngle(initialYaw - 18f, GrabYaw - 65f, ProgressBetween(tick, ApproachAt, GrabAt));
        if (tick < ReleaseAt) return SpinYaw(tick) - 65f;
        float releaseYaw = GrabYaw - 65f;
        Vector3 direction = aim - actor;
        float aimYaw = direction.sqrMagnitude > .001f ? direction.AngleFlat() : throwYaw;
        return Mathf.LerpAngle(releaseYaw, aimYaw, Mathf.SmoothStep(0f, 1f,
            ProgressBetween(tick, ReleaseAt, AimAt)));
    }

    private Vector3 ApproachAtProgress(float progress, IntVec3 destination, bool curved)
    {
        float p = Mathf.SmoothStep(0f, 1f, progress);
        Vector3 start = origin.ToVector3Shifted().Yto0(), end = destination.ToVector3Shifted().Yto0();
        Vector3 offset = curved ? Vector3.Cross(Vector3.up, end - start).normalized * (.42f * Mathf.Sin(p * Mathf.PI)) : Vector3.zero;
        return Vector3.Lerp(start, end, p) + offset;
    }

    private bool ChooseApproach()
    {
        approachCell = IntVec3.Invalid;
        float bestScore = float.NegativeInfinity;
        Vector3 from = origin.ToVector3Shifted().Yto0();
        foreach (IntVec3 offset in GenAdj.AdjacentCells)
        {
            IntVec3 cell = GrabCell + offset;
            Vector3 to = cell.ToVector3Shifted().Yto0();
            if (!CanOccupy(pawn, cell) || !CanTravel(pawn, from, to)) continue;
            int openSides = 0;
            foreach (IntVec3 side in GenAdj.AdjacentCells)
                if (CanOccupy(Victim, cell + side, pawn)) openSides++;
            float score = openSides * .45f - (to - from).magnitude + (cell == origin ? -.65f : 0f);
            if (score <= bestScore) continue;
            approachCell = cell;
            bestScore = score;
        }
        if (!approachCell.IsValid) return false;
        Vector3 last = from;
        curvedApproach = approachCell != origin;
        for (int i = 1; curvedApproach && i <= 12; i++)
        {
            Vector3 next = ApproachAtProgress(i / 12f, approachCell, true);
            curvedApproach = CanTravel(pawn, last, next);
            last = next;
        }
        return true;
    }

    private void MoveHeldTarget(Pawn target, int tick)
    {
        Vector3 gripSide = Forward(SpinYaw(tick));
        float reach = Mathf.Lerp((GrabCell.ToVector3Shifted().Yto0() - actorDraw).magnitude, .84f,
            ProgressBetween(tick, GrabAt, SpinAt));
        reach += .16f * ProgressBetween(tick, ReleaseAt - spinTicksPerTurn / 4, ReleaseAt);
        Vector3 next = actorDraw + gripSide * reach;
        if (!CanTravel(target, targetGround, next, pawn))
        {
            // In a tight space keep the grip close while turning through the available area.
            next = actorDraw + gripSide * .36f;
            if (!CanTravel(target, targetGround, next, pawn)) next = targetGround;
        }
        targetGround = next;
        MovePawn(target, ref targetCell, next.ToIntVec3());
        targetDraw = next + Vector3.forward * (.23f * ProgressBetween(tick, GrabAt, SpinAt));
    }

    private void BeginThrow(Pawn target)
    {
        throwYaw = SpinYaw(ReleaseAt) + 90f;
        Vector3 tangent = Forward(throwYaw);
        if (VolleyAfterLanding)
        {
            motionOrigin = targetGround;
            touchdown = ReleaseAt + flightTicks;
            Vector3 end = motionOrigin + tangent * throwDistance;
            // Quantize the endpoint without putting its cell centre beyond the range.
            landingCell = end.ToIntVec3();
            if ((landingCell.ToVector3Shifted().Yto0() - motionOrigin).sqrMagnitude > throwDistance * throwDistance)
                landingCell = (end - tangent * .72f).ToIntVec3();
        }
        else
            PlanSavedThrow(target, tangent);
        executionMap.GetComponent<MapComponent_NeiyuSwordVfx>()?.TrackVolley(this);
    }

    private void PlanSavedThrow(Pawn target, Vector3 tangent)
    {
        BeginTargetMotion(target, tangent, throwDistance, ReleaseAt + flightTicks, 0f);
        if (landingCell == actorCell)
        {
            // A short release is still possible if the tangent points into a wall.
            foreach (IntVec3 offset in GenAdj.AdjacentCells)
            {
                IntVec3 cell = actorCell + offset;
                if (!CanTravel(target, motionOrigin, cell.ToVector3Shifted().Yto0())) continue;
                landingCell = cell;
                break;
            }
        }
        Vector3 throwVector = landingCell.ToVector3Shifted().Yto0() - motionOrigin;
        if (landingCell != targetCell && throwVector.sqrMagnitude > .01f) throwYaw = throwVector.AngleFlat();
    }

    private void BeginTargetMotion(Pawn target, Vector3 direction, float range, int endTick, float minAlignment)
    {
        motionOrigin = targetGround;
        landingCell = targetCell;
        touchdown = endTick;
        float bestScore = float.NegativeInfinity;
        int searchRadius = Mathf.CeilToInt(range);
        float rangeSquared = range * range;
        // Snap to real cells only after checking the complete segment, including closed doors and corners.
        for (int z = -searchRadius; z <= searchRadius; z++)
        for (int x = -searchRadius; x <= searchRadius; x++)
        {
            IntVec3 cell = targetCell + new IntVec3(x, 0, z);
            if (cell == targetCell) continue;
            Vector3 destination = cell.ToVector3Shifted().Yto0();
            Vector3 delta = destination - motionOrigin;
            float distanceSquared = delta.sqrMagnitude;
            if (distanceSquared > rangeSquared || distanceSquared < .001f) continue;
            float distance = Mathf.Sqrt(distanceSquared);
            float alignment = Vector3.Dot(delta / distance, direction);
            float score = alignment * range + Mathf.Min(distance, range - .5f);
            if (alignment < minAlignment || score <= bestScore
                || !CanOccupy(target, cell) || !CanTravel(target, motionOrigin, destination)) continue;
            landingCell = cell;
            bestScore = score;
        }
    }

    private void MoveTargetAlongMotion(Pawn target, int tick, int startTick, float startHeight, float arcHeight)
    {
        Vector3 end = landingCell.ToVector3Shifted().Yto0();
        if (tick > touchdown && targetGround == end) return;
        float p = ProgressBetween(tick, startTick, touchdown);
        Vector3 next = Vector3.Lerp(motionOrigin, end, 1f - (1f - p) * (1f - p));
        if (!TryTravel(target, targetGround, next, out IntVec3 lastClear, targetCell == actorCell ? pawn : null))
        {
            landingCell = lastClear;
            touchdown = tick;
            next = landingCell.ToVector3Shifted().Yto0();
            p = 1f;
        }
        targetGround = next;
        MovePawn(target, ref targetCell, targetGround.ToIntVec3());
        float height = Mathf.Lerp(startHeight, 0f, p) + arcHeight * Mathf.Sin(p * Mathf.PI);
        targetDraw = targetGround + Vector3.forward * height;
    }

    private static void MovePawn(Pawn subject, ref IntVec3 occupied, IntVec3 cell)
    {
        if (cell == occupied) return;
        occupied = cell;
        subject.Position = cell;
        subject.Notify_Teleported(endCurrentJob: false);
    }

    private bool CanOccupy(Pawn subject, IntVec3 cell, Pawn sharingWith = null)
    {
        if (!cell.InBounds(executionMap) || !cell.Standable(executionMap)
            || cell.GetEdifice(executionMap) is Building_Door { Open: false }) return false;
        foreach (Thing thing in cell.GetThingList(executionMap))
            if (thing is Pawn other && other != subject && other != sharingWith) return false;
        return true;
    }

    private bool CanTravel(Pawn subject, Vector3 from, Vector3 to, Pawn sharingWith = null) =>
        TryTravel(subject, from, to, out _, sharingWith);

    private bool TryTravel(Pawn subject, Vector3 from, Vector3 to, out IntVec3 lastClear, Pawn sharingWith = null)
    {
        lastClear = from.ToIntVec3();
        int samples = Mathf.Max(1, Mathf.CeilToInt((to - from).magnitude * 5f));
        for (int i = 1; i <= samples; i++)
        {
            Vector3 point = Vector3.Lerp(from, to, i / (float)samples);
            // ToIntVec3 truncates negative fractions into the zero cell.
            if (point.x < 0f || point.z < 0f) return false;
            IntVec3 cell = point.ToIntVec3();
            if (cell == lastClear) continue;
            if (!CanOccupy(subject, cell, sharingWith)) return false;
            if (lastClear.x != cell.x && lastClear.z != cell.z
                && (!CanOccupy(subject, new IntVec3(lastClear.x, 0, cell.z), sharingWith)
                    || !CanOccupy(subject, new IntVec3(cell.x, 0, lastClear.z), sharingWith))) return false;
            lastClear = cell;
        }
        return true;
    }

    private float FrameLerp => Mathf.Clamp01(VisualClock.TicksSince(NeiyuSwordCombat.Now) + 1f);

    bool IPostureExecutionDriver.DrivesExecutorPosition => IsSingle;

    internal static bool TryGetExecutionDrawPos(Pawn subject, out Vector3 position)
    {
        if (subject.jobs?.curDriver is JobDriver_NeiyuPostureExecution { IsSingle: true } actor
            && subject.Position == actor.actorCell)
        {
            position = Vector3.Lerp(actor.previousActorDraw, actor.actorDraw, actor.FrameLerp);
            return true;
        }
        if (CombatPosture.Get(subject)?.Executioner?.jobs?.curDriver is
                JobDriver_NeiyuPostureExecution { IsSingle: true, strikeCommitted: false } driver
            && driver.Victim == subject && subject.Map == driver.executionMap && subject.Position == driver.targetCell)
        {
            position = Vector3.Lerp(driver.previousTargetDraw, driver.targetDraw, driver.FrameLerp);
            return true;
        }
        position = default;
        return false;
    }

    public bool TryGetBladePose(out BladePose pose)
    {
        if (!IsSingle || singleStage == SingleStage.Lock || pawn.equipment?.Primary != executionWeapon)
        {
            pose = default;
            return false;
        }
        float tick = Mathf.Max(0f, SceneFrame);
        pose = BladeAt(tick, Vector3.Lerp(previousActorDraw, actorDraw, FrameLerp),
            Vector3.Lerp(previousTargetDraw, aimPoint, FrameLerp));
        return true;
    }

    private BladePose BladeAt(float tick, Vector3 actor, Vector3 aim)
    {
        float yaw = initialYaw;
        BladeKeyframes keys = Single.drawBlade;
        float time;
        if (tick < SwingAt) time = .2f * ProgressBetween(tick, 0, Single.readyTicks);
        else if (tick < FirstCutAt) time = Mathf.Lerp(.2f, .7f, ProgressBetween(tick, SwingAt, FirstCutAt));
        else if (tick < FirstCutAt + hitStopTicks) time = .7f;
        else if (tick < ApproachAt) time = Mathf.Lerp(.7f, 1f, ProgressBetween(tick, FirstCutAt + hitStopTicks, KnockbackAt));
        else
        {
            keys = Single.aimBlade;
            time = .45f * ProgressBetween(tick, ApproachAt, GrabAt);
            yaw = Mathf.LerpAngle(initialYaw, GrabYaw - 47f, ProgressBetween(tick, ApproachAt, GrabAt));
            if (tick >= GrabAt)
            {
                yaw = BodyYaw(tick, actor, aim) + 18f;
                if (tick >= ReleaseAt)
                {
                    float drawAim = Mathf.SmoothStep(0f, 1f, ProgressBetween(tick, ReleaseAt, AimAt));
                    yaw -= 18f * drawAim;
                    time = Mathf.Lerp(.45f, 1f, drawAim);
                }
            }
        }
        keys.Evaluate(time, out Vector3 grip, out Vector3 direction, out Vector3 edge);
        Quaternion frame = Quaternion.AngleAxis(yaw, Vector3.up);
        return new BladePose
        {
            grip = frame * grip,
            dir = frame * direction,
            edge = frame * edge,
            heightToScreen = .68f,
            reveal = NeiyuSwordCombatVfx.Enabled && NeiyuSwordCombatVfx.Intensity > 0f
                ? ProgressBetween(tick, Single.readyTicks, FlashAt) * (1f - ProgressBetween(tick, FirstCutAt, KnockbackAt))
                : 0f
        };
    }
}

using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Neiyu;

public enum NeiyuExecutionPhase { Normal, PostureBroken, ExecutionStart, ExecutionStrike, ExecutionEnd }

public sealed class JobDriver_NeiyuPostureExecution : JobDriver
{
    private int startedAt = -1, duration, dashTick, strikeTick, recoverTick;
    private bool prepared, dashed, strikeCommitted, endedVisual;
    private bool visualsRebound;
    private Map executionMap;
    private IntVec3 origin = IntVec3.Invalid, hitCell = IntVec3.Invalid;

    public Pawn Victim => job?.targetA.Pawn;
    public int Elapsed => Mathf.Max(0, NeiyuSwordCombat.Now - startedAt);
    public float Progress => Mathf.Clamp01(Elapsed / (float)Mathf.Max(1, duration));
    public float FacingAngle => (hitCell.ToVector3Shifted() - pawn.DrawPos).AngleFlat();
    public NeiyuExecutionPhase Phase => strikeCommitted
        ? Elapsed >= recoverTick ? NeiyuExecutionPhase.ExecutionEnd : NeiyuExecutionPhase.ExecutionStrike
        : NeiyuExecutionPhase.ExecutionStart;
    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

    public override void Notify_Starting()
    {
        base.Notify_Starting();
        NeiyuSwordCombatDef c = NeiyuSwordCombat.Config;
        startedAt = NeiyuSwordCombat.Now;
        duration = Mathf.Max(12, c.executionTicks);
        dashTick = Mathf.Clamp(Mathf.RoundToInt(duration * c.dashFraction), 1, duration - 3);
        strikeTick = Mathf.Clamp(Mathf.RoundToInt(duration * c.strikeFraction), dashTick + 1, duration - 2);
        recoverTick = Mathf.Clamp(Mathf.RoundToInt(duration * c.recoverFraction), strikeTick + 1, duration - 1);
        executionMap = pawn.Map;
        origin = pawn.Position;
        hitCell = Victim?.Position ?? origin;
    }

    public override void ExposeData()
    {
        // Base reconstructs toils during PostLoadInit; load deadlines first.
        Scribe_Values.Look(ref startedAt, "swordExecutionStart", -1);
        Scribe_Values.Look(ref duration, "swordExecutionDuration");
        Scribe_Values.Look(ref dashTick, "swordExecutionDash");
        Scribe_Values.Look(ref strikeTick, "swordExecutionStrike");
        Scribe_Values.Look(ref recoverTick, "swordExecutionRecover");
        Scribe_Values.Look(ref prepared, "swordExecutionPrepared");
        Scribe_Values.Look(ref dashed, "swordExecutionDashed");
        Scribe_Values.Look(ref strikeCommitted, "swordExecutionCommitted");
        Scribe_Values.Look(ref endedVisual, "swordExecutionEndedVisual");
        Scribe_Values.Look(ref origin, "swordExecutionOrigin", IntVec3.Invalid);
        Scribe_Values.Look(ref hitCell, "swordExecutionHitCell", IntVec3.Invalid);
        Scribe_References.Look(ref executionMap, "swordExecutionMap");
        base.ExposeData();
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        AddFinishAction(Finish);
        Toil execution = ToilMaker.MakeToil("NeiyuPostureExecution");
        execution.defaultCompleteMode = ToilCompleteMode.Never;
        execution.tickAction = TickExecution;
        yield return execution;
    }

    private bool Valid()
    {
        if (startedAt < 0 || duration < 12 || !origin.IsValid || executionMap == null
            || !NeiyuSwordCombat.CanAct(pawn) || pawn.Map != executionMap || pawn.Position != origin) return false;
        if (strikeCommitted) return true;
        return NeiyuSwordCombat.ExecutionPair(pawn, Victim)
            && (Victim.Downed || Victim.CurJobDef == NeiyuSwordCombat.Config.brokenJob)
            && NeiyuSwordCombat.Posture(Victim)?.Owns(pawn) == true;
    }

    private void TickExecution()
    {
        if (!Valid()) { EndJobWith(JobCondition.Incompletable); return; }
        pawn.pather.StopDead();
        if (!visualsRebound)
        {
            visualsRebound = true;
            if (prepared) NeiyuSwordCombatVfx.Resume(pawn, hitCell, startedAt, dashTick, strikeTick, recoverTick,
                dashed, strikeCommitted, endedVisual);
        }
        if (!strikeCommitted) hitCell = Victim.Position;
        pawn.rotationTracker?.FaceCell(hitCell);
        if (!prepared)
        {
            prepared = true;
            NeiyuSwordCombatVfx.Execution(pawn, hitCell, NeiyuExecutionPhase.ExecutionStart);
        }
        if (!dashed && Elapsed >= dashTick)
        {
            dashed = true;
            NeiyuSwordCombatVfx.Dash(pawn, Victim);
        }
        if (!strikeCommitted && Elapsed >= strikeTick)
        {
            Pawn victim = Victim;
            HediffComp_NeiyuPosture posture = NeiyuSwordCombat.Posture(victim);
            // Saved BEFORE invoking a native/mod death callback, including callbacks
            // that save, resurrect, teleport, throw or decline the death.
            strikeCommitted = true;
            NeiyuSwordCombatVfx.Execution(pawn, hitCell, NeiyuExecutionPhase.ExecutionStrike);
            pawn.Drawer.Notify_MeleeAttackOn(victim);
            try
            {
                if (NeiyuSwordCombat.ExecutionTarget(victim))
                {
                    DamageInfo execution = new(DamageDefOf.ExecutionCut, 1f, 0f, -1f,
                        pawn, null, pawn.equipment.Primary.def);
                    victim.Kill(execution);
                }
            }
            catch (Exception exception)
            {
                Log.ErrorOnce($"[霓羽弹刀] 处决死亡回调中断，已释放目标状态：{exception}", 197632100);
                EndJobWith(JobCondition.Errored);
                return;
            }
            finally { posture?.ReleaseBreak(); }
        }
        if (!endedVisual && Elapsed >= recoverTick)
        {
            endedVisual = true;
            NeiyuSwordCombatVfx.Execution(pawn, hitCell, NeiyuExecutionPhase.ExecutionEnd);
        }
        if (Elapsed >= duration) EndJobWith(JobCondition.Succeeded);
    }

    private void Finish(JobCondition condition)
    {
        HediffComp_NeiyuPosture posture = NeiyuSwordCombat.Posture(Victim);
        if (posture?.Executioner == pawn) posture.ReleaseBreak();
        // StartJob's native resumeCurJobAfterwards owns restoration. Never clear
        // the player's queue or revive a saved reference to a pooled old Job.
    }
}

public sealed class JobDriver_NeiyuPostureBroken : JobDriver
{
    public override bool PlayerInterruptable => false;
    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

    protected override IEnumerable<Toil> MakeNewToils()
    {
        Toil hold = ToilMaker.MakeToil("NeiyuPostureBroken");
        hold.defaultCompleteMode = ToilCompleteMode.Never;
        hold.tickAction = delegate
        {
            if (NeiyuSwordCombat.Posture(pawn)?.Broken != true)
            {
                EndJobWith(JobCondition.Succeeded);
                return;
            }
            pawn.pather.StopDead();
        };
        yield return hold;
    }
}

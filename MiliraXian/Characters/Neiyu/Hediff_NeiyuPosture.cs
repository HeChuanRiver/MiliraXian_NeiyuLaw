using MiliraXian.Characters.Common;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Neiyu;

public sealed class Hediff_NeiyuPosture : HediffWithComps
{
    public override bool ShouldRemove => this.TryGetComp<HediffComp_NeiyuPosture>()?.Expired != false;
}

public sealed class HediffCompProperties_NeiyuPosture : HediffCompProperties_PawnSpecialResource
{
    public HediffCompProperties_NeiyuPosture()
    {
        compClass = typeof(HediffComp_NeiyuPosture);
        hideOnHealthTab = false;
        showGizmo = false;
        generateStats = false; // Health, not a constant stat base, determines the maximum.
        // Only this resource's health-dependent override supplies its drain rate.
        recoveryPerSecond = 0f;
        clampToMax = false;
    }
}

public sealed class HediffComp_NeiyuPosture : HediffComp_PawnSpecialResource
{
    private int lastIncreaseTick = -1, fadeUntilTick, brokenUntilTick, openingUntilTick;
    private int nextUpdateTick, openingGeneration, reservedGeneration;
    private bool broken, openingReserved, holdStarted;
    private Pawn source, executioner;

    private static NeiyuSwordCombatDef C => NeiyuSwordCombat.Config;
    private static int Now => NeiyuSwordCombat.Now;
    private float HealthRatio => NeiyuSwordCombatRules.Health(Pawn.health.summaryHealth.SummaryHealthPercent);
    public override float MaxValue => NeiyuSwordCombatRules.Maximum(C.basePosture, C.minimumPostureFraction, HealthRatio);
    public override float RecoveryPerSecond => broken ? 0f
        : -NeiyuSwordCombatRules.Recovery(C.recoveryPerSecond, C.minimumRecoveryFraction, HealthRatio);
    protected override int RecoveryStartTick => lastIncreaseTick + C.recoveryDelayTicks;
    public bool Broken => broken;
    public bool Locked => executioner != null;
    public Pawn Executioner => executioner;
    public int LastIncreaseTick => lastIncreaseTick;
    public bool HasOpening => openingUntilTick > Now && !NeiyuPowerBalance.PassivesDisabled;
    public float BarAlpha => CurrentValue > 0f || broken ? 1f
        : Mathf.Clamp01((fadeUntilTick - Now) / (float)Mathf.Max(1, C.barFadeTicks));
    public bool Expired => Pawn.Dead || Pawn.Destroyed || (!broken && !Locked && CurrentValue <= 0f
        && Now >= openingUntilTick && Now >= fadeUntilTick);
    public override string CompLabelInBracketsExtra => broken ? "架势崩溃" : $"{CurrentValue:0} / {MaxValue:0}";
    public override string CompTipStringExtra => $"架势：{CurrentValue:0.0} / {MaxValue:0.0}\n恢复：{-RecoveryPerSecond:0.0} / 秒"
        + (HasOpening ? $"\n破绽：下一次直接攻击伤害 ×{C.vulnerabilityMultiplier:0.##}" : "");

    public override void CompExposeData()
    {
        base.CompExposeData();
        Scribe_Values.Look(ref lastIncreaseTick, "lastPostureIncrease", -1);
        Scribe_Values.Look(ref fadeUntilTick, "postureFadeUntil");
        Scribe_Values.Look(ref brokenUntilTick, "postureBrokenUntil");
        Scribe_Values.Look(ref openingUntilTick, "swordOpeningUntil");
        Scribe_Values.Look(ref openingGeneration, "swordOpeningGeneration");
        Scribe_Values.Look(ref broken, "postureBroken");
        Scribe_Values.Look(ref holdStarted, "postureHoldStarted");
        Scribe_References.Look(ref source, "postureSource");
        Scribe_References.Look(ref executioner, "postureExecutioner");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            SetValue(NeiyuSwordCombatRules.Finite(CurrentValue));
            openingReserved = false;
            nextUpdateTick = 0;
        }
    }

    internal void ReceiveDeflection(Pawn defender, bool perfect)
    {
        // Settle before changing the grace-period origin, including several hits
        // inside one settlement interval. There is exactly one saved resource value.
        Settle(force: true);
        if (!broken) source = defender;
        lastIncreaseTick = Now;
        fadeUntilTick = Now + C.barFadeTicks;
        if (perfect)
        {
            openingUntilTick = Now + C.vulnerabilityTicks;
            openingGeneration++;
        }
        if (broken) return;
        AddValue(perfect ? C.perfectPosture : C.normalPosture);
        CheckForBreak();
    }

    private void CheckForBreak()
    {
        if (broken || CurrentValue < MaxValue || CurrentValue <= 0f || !Pawn.Spawned) return;
        broken = true;
        brokenUntilTick = Now + Mathf.Max(C.breakWindowTicks, C.executionTicks + 30);
        holdStarted = false;
        Pawn.pather?.StopDead();
        Pawn.stances?.CancelBusyStanceHard();
        NeiyuSwordCombatVfx.Break(Pawn);
    }

    public override void CompPostTick(ref float severityAdjustment)
    {
        if (Now < nextUpdateTick) return;
        nextUpdateTick = Now + SettleIntervalTicks;
        if (!Pawn.Spawned)
        {
            if (broken || Locked) ReleaseBreak();
            base.CompPostTick(ref severityAdjustment);
            return;
        }
        // Check the reduced health-based maximum before draining any posture.
        CheckForBreak();
        float previous = CurrentValue;
        base.CompPostTick(ref severityAdjustment);
        if (previous > 0f && CurrentValue <= 0f) fadeUntilTick = Now + C.barFadeTicks;
        if (!broken) return;
        if (Now >= brokenUntilTick || !NeiyuSwordCombat.ExecutionPair(source, Pawn))
        {
            ReleaseBreak();
            return;
        }
        if (executioner != null)
        {
            // A saved lock is valid only while the matching saved Job owns it.
            if (executioner.jobs?.curDriver is not JobDriver_NeiyuPostureExecution driver || driver.Victim != Pawn)
                ReleaseBreak();
            return;
        }
        if (holdStarted && !Pawn.Downed && Pawn.CurJobDef != C.brokenJob)
        {
            ReleaseBreak();
            return;
        }
        if (!holdStarted)
        {
            holdStarted = true;
            if (!Pawn.Downed)
                Pawn.jobs.StartJob(JobMaker.MakeJob(C.brokenJob), JobCondition.InterruptForced);
        }
        // Multiple broken enemies wait in their own bounded break window while
        // their source finishes an existing execution. No map search or queue scan.
        if (source.jobs?.curDriver is JobDriver_NeiyuPostureExecution) return;
        if (source.stances?.curStance is Stance_Warmup) { ReleaseBreak(); return; }
        executioner = source;
        Job job = JobMaker.MakeJob(C.executionJob, Pawn);
        source.jobs.StartJob(job, JobCondition.InterruptForced, resumeCurJobAfterwards: true);
        if (source.CurJob != job) ReleaseBreak();
    }

    // HediffWithComps dispatches interval comps separately in 1.7. Sharing the
    // same deadline also makes legacy comp callbacks safe if both are dispatched.
    public override void CompPostTickInterval(ref float severityAdjustment, int delta) =>
        CompPostTick(ref severityAdjustment);

    internal bool Owns(Pawn executor) => broken && executioner == executor && Now < brokenUntilTick;

    internal void ReleaseBreak()
    {
        broken = false;
        executioner = null;
        source = null;
        holdStarted = false;
        brokenUntilTick = 0;
        SetValue(0f);
        fadeUntilTick = Now + C.barFadeTicks;
        // The hold job terminates itself; never stop an unrelated replacement job
        // or clear another system's stunner.
    }

    internal bool TryReserveOpening()
    {
        if (!HasOpening || openingReserved || Pawn.Dead) return false;
        openingReserved = true;
        reservedGeneration = openingGeneration;
        return true;
    }

    internal void CompleteOpening(bool damageDealt)
    {
        if (openingReserved && damageDealt && reservedGeneration == openingGeneration) openingUntilTick = 0;
        openingReserved = false;
    }

    internal void ReleaseOpening() => openingReserved = false;
    public override void CompPostPostRemoved()
    {
        broken = false;
        executioner = source = null;
        openingReserved = false;
    }
}

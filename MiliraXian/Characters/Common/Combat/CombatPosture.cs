using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Common.Combat;

// Shared posture balance. Characters supply how much a block adds and how they
// execute; the meter, its recovery, the break window and openings live here.
public sealed class CombatPostureDef : Def
{
    public HediffDef posture;
    public JobDef brokenJob;
    public float basePosture = 100f, minimumPostureFraction = .45f;
    public int recoveryDelayTicks = 120;
    public float recoveryPerSecond = 4f, minimumRecoveryFraction = .1f;
    public int vulnerabilityTicks = 360;
    public float vulnerabilityMultiplier = 2f;
    public int breakWindowTicks = 180, barFadeTicks = 90;

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors()) yield return error;
        if (posture == null || brokenJob == null)
            yield return "架势 Hediff 与崩溃 Job 引用必须完整。";
        if (!(basePosture >= 1f && minimumPostureFraction > 0f && minimumPostureFraction <= 1f
            && recoveryPerSecond >= 0f && minimumRecoveryFraction >= 0f && minimumRecoveryFraction <= 1f
            && vulnerabilityMultiplier >= 1f))
            yield return "架势、恢复和易伤参数超出有效范围。";
        if (!(recoveryDelayTicks >= 0 && vulnerabilityTicks > 0 && breakWindowTicks > 0 && barFadeTicks > 0))
            yield return "架势时间参数无效。";
    }
}

public sealed class CombatPostureMutantExtension : DefModExtension
{
    public float maximumOffset;
}

// Engine-independent arithmetic shared by the live system and its focused checks.
public static class CombatPostureRules
{
    public static float Finite(float value, float fallback = 0f) => float.IsFinite(value) ? value : fallback;
    public static float Health(float health) => Math.Clamp(Finite(health, 1f), 0f, 1f);
    public static float Maximum(float basis, float minimumFraction, float health) =>
        Math.Max(1f, Finite(basis, 100f)) *
        (Math.Clamp(Finite(minimumFraction, .45f), .01f, 1f)
        + (1f - Math.Clamp(Finite(minimumFraction, .45f), .01f, 1f)) * Health(health));
    public static float Recovery(float rate, float minimumFraction, float health) =>
        Math.Max(0f, Finite(rate, 4f)) *
        (Math.Clamp(Finite(minimumFraction, .1f), 0f, 1f)
        + (1f - Math.Clamp(Finite(minimumFraction, .1f), 0f, 1f)) * Health(health));
}

[StaticConstructorOnStartup]
public static class CombatPosture
{
    public static readonly CombatPostureDef Config = DefDatabase<CombatPostureDef>.GetNamed("MX_CombatPosture");
    internal static int Now => Find.TickManager?.TicksGame ?? 0;

    public static HediffComp_CombatPosture Get(Pawn pawn) =>
        pawn?.health?.hediffSet.GetFirstHediffOfDef(Config.posture)?.TryGetComp<HediffComp_CombatPosture>();

    public static HediffComp_CombatPosture Ensure(Pawn pawn) =>
        Get(pawn) ?? PawnSpecialResourceUtility.EnsureSpecialResourceComp(pawn, Config.posture) as HediffComp_CombatPosture;

    private static readonly List<Predicate<Pawn>> Immunities = new();

    /// <summary>
    /// A character state in which the pawn takes no posture at all (an invulnerable skill).
    /// Character code registers these at startup; this file does not depend on any character.
    /// </summary>
    public static void RegisterImmunity(Predicate<Pawn> immune)
    {
        if (immune != null && !Immunities.Contains(immune)) Immunities.Add(immune);
    }

    internal static bool Immune(Pawn pawn)
    {
        for (int i = 0; i < Immunities.Count; i++)
            if (Immunities[i](pawn)) return true;
        return false;
    }

    internal static bool ControlsActions(Pawn pawn) => pawn is { Spawned: true, Dead: false }
        && pawn.health is { isBeingKilled: false } && Get(pawn)?.Broken == true;

    // A melee attack order on a broken victim: refused while another pawn's execution of it runs or
    // is ordered, otherwise it may become the orderer's execution.
    internal static bool TakeAttackOrder(Pawn pawn, Job job)
    {
        if (job?.def != JobDefOf.AttackMelee || job.targetA.Thing is not Pawn victim) return true;
        HediffComp_CombatPosture posture = Get(victim);
        return posture?.Broken != true || posture.TakeOrder(pawn);
    }

    internal static bool BlocksAutonomousJobs(Pawn pawn) => ControlsActions(pawn)
        || pawn?.jobs?.curDriver is IPostureExecutionDriver { DrivesExecutorPosition: true };
}

public sealed class Hediff_CombatPosture : HediffWithComps
{
    public override bool ShouldRemove => this.TryGetComp<HediffComp_CombatPosture>()?.Expired != false;
}

public sealed class HediffCompProperties_CombatPosture : HediffCompProperties_PawnSpecialResource
{
    public HediffCompProperties_CombatPosture()
    {
        compClass = typeof(HediffComp_CombatPosture);
        hideOnHealthTab = false;
        showGizmo = false;
        generateStats = false; // Health, not a constant stat base, determines the maximum.
        // Only this resource's health-dependent override supplies its drain rate.
        recoveryPerSecond = 0f;
        clampToMax = false;
    }
}

public sealed class HediffComp_CombatPosture : HediffComp_PawnSpecialResource
{
    private int lastIncreaseTick = -1, fadeUntilTick, brokenUntilTick, openingUntilTick;
    private int brokenAt = -1;
    private IntVec3 breakCell = IntVec3.Invalid;
    private int nextUpdateTick, openingGeneration, reservedGeneration;
    private bool broken, openingReserved, holdStarted, autoDeclined;
    private int untilBeforeExecution = -1;
    private Pawn source, executioner, orderedBy;

    private static CombatPostureDef C => CombatPosture.Config;
    private static int Now => CombatPosture.Now;
    private float HealthRatio => CombatPostureRules.Health(Pawn.health.summaryHealth.SummaryHealthPercent);
    public override float MaxValue => CombatPostureRules.Maximum(C.basePosture, C.minimumPostureFraction, HealthRatio)
        + (Pawn.mutant?.Def?.GetModExtension<CombatPostureMutantExtension>()?.maximumOffset ?? 0f);
    public override float RecoveryPerSecond => broken ? 0f
        : -CombatPostureRules.Recovery(C.recoveryPerSecond, C.minimumRecoveryFraction, HealthRatio);
    protected override int RecoveryStartTick => lastIncreaseTick + C.recoveryDelayTicks;
    public bool Broken => broken;
    public bool Locked => executioner != null;
    public Pawn Executioner => executioner;
    public int BrokenAt => brokenAt;
    public int LastIncreaseTick => lastIncreaseTick;
    public bool HasOpening => openingUntilTick > Now && !CombatBlockers.OpeningsSuppressed;
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
        Scribe_Values.Look(ref brokenAt, "postureBrokenAt", -1);
        Scribe_Values.Look(ref breakCell, "postureBreakCell", IntVec3.Invalid);
        Scribe_Values.Look(ref openingUntilTick, "swordOpeningUntil");
        Scribe_Values.Look(ref openingGeneration, "swordOpeningGeneration");
        Scribe_Values.Look(ref broken, "postureBroken");
        Scribe_Values.Look(ref holdStarted, "postureHoldStarted");
        Scribe_References.Look(ref source, "postureSource");
        Scribe_References.Look(ref executioner, "postureExecutioner");
        Scribe_References.Look(ref orderedBy, "postureOrderedBy");
        Scribe_Values.Look(ref autoDeclined, "postureAutoDeclined");
        Scribe_Values.Look(ref untilBeforeExecution, "postureUntilBeforeExecution", -1);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            SetValue(CombatPostureRules.Finite(CurrentValue));
            openingReserved = false;
            nextUpdateTick = 0;
        }
    }

    // A blocked melee hit: add posture to the attacker; opening leaves a one-hit vulnerability.
    public void ReceiveBlock(Pawn defender, float amount, bool opening)
    {
        if (CombatPosture.Immune(Pawn)) return;
        // Settle before changing the grace-period origin, including several hits
        // inside one settlement interval. There is exactly one saved resource value.
        Settle(force: true);
        if (!broken) source = defender;
        lastIncreaseTick = Now;
        fadeUntilTick = Now + C.barFadeTicks;
        if (opening)
        {
            openingUntilTick = Now + C.vulnerabilityTicks;
            openingGeneration++;
        }
        if (broken) return;
        AddValue(amount);
        CheckForBreak();
    }

    private void CheckForBreak()
    {
        if (broken || CurrentValue < MaxValue || CurrentValue <= 0f || !Pawn.Spawned) return;
        CombatBlocker owner = CombatBlockers.ExecutionerFor(source);
        broken = true;
        brokenAt = Now;
        breakCell = Pawn.Position;
        brokenUntilTick = Now + CombatBlockers.BreakWindowFor(owner, C.breakWindowTicks);
        holdStarted = false;
        Pawn.pather?.StopDead();
        Pawn.stances?.CancelBusyStanceHard();
        // The hold starts on the next tick, not inside the hit that broke it (that may be the
        // victim's own swing); until then no new job or cast can start (CombatPostureControl).
        nextUpdateTick = Now + 1;
        owner?.NotifyPostureBroken(source, Pawn);
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
        if (Now >= brokenUntilTick)
        {
            ReleaseBreak();
            return;
        }
        // The first settle after the break, outside the hit that broke it.
        if (!holdStarted) CombatBlockers.NotifyTargetBroken(Pawn);
        // A claimed execution owns the victim from here on (it may move it, as Neiyu's single
        // execution throws it); its own job validates the reach. A saved lock is valid only while
        // the matching saved Job owns it.
        if (executioner != null)
        {
            if (executioner.jobs?.curDriver is not IPostureExecutionDriver driver || driver.Victim != Pawn)
                AbandonExecution(executioner);
            return;
        }
        // Moved by something else before any execution claimed it (knocked back, teleported).
        if (breakCell.IsValid && Pawn.Position != breakCell)
        {
            ReleaseBreak();
            return;
        }
        // An attack order on the victim is carried out as the orderer's execution once it can start.
        if (orderedBy != null)
        {
            if (AttacksVictim(orderedBy))
            {
                EnsureHold();
                CombatBlocker ordered = CombatBlockers.ExecutionerFor(orderedBy);
                if (ordered.CanExecute(orderedBy, Pawn)) StartExecution(orderedBy, ordered);
                return;
            }
            orderedBy = null;
        }
        CombatBlocker owner = CombatBlockers.ExecutionerFor(source);
        if (owner == null || !owner.Executes)
        {
            // Broken by someone who does not execute: hold still for the window, then recover.
            HoldWithoutExecution();
            return;
        }
        ExecutionMode mode = owner.ModeFor(source);
        if (mode != ExecutionMode.Auto || autoDeclined)
        {
            HoldWithoutExecution();
            return;
        }
        if (!owner.CanExecute(source, Pawn))
        {
            if (owner.WaitForExecutor) HoldWithoutExecution();
            else ReleaseBreak();
            return;
        }
        EnsureHold();
        StartExecution(source, owner);
    }

    private bool AttacksVictim(Pawn pawn) => pawn is { Spawned: true, Dead: false }
        && pawn.CurJob is { } job && job.def == JobDefOf.AttackMelee && job.targetA.Thing == Pawn;

    private void StartExecution(Pawn executor, CombatBlocker owner)
    {
        // Multiple broken enemies wait in their own bounded break window while
        // their executor finishes an existing execution. No map search or queue scan.
        if (executor.jobs?.curDriver is IPostureExecutionDriver) return;
        // An executor that waits decides itself (in CanExecute) which of its own stances it may leave.
        if (executor.stances?.curStance is Stance_Warmup && !owner.WaitForExecutor) { ReleaseBreak(); return; }
        Job job = owner.MakeExecutionJob(executor, Pawn);
        if (job == null) { ReleaseBreak(); return; }
        executioner = executor;
        orderedBy = null;
        untilBeforeExecution = brokenUntilTick;
        // An executor that waited may start late: the break lasts through its execution.
        if (owner.WaitForExecutor) brokenUntilTick = Mathf.Max(brokenUntilTick, Now + owner.MinimumBreakWindowTicks);
        // Not resumed afterwards: a suspended job goes to the front of the job queue, and an AI
        // pawn whose think tree issues a fresh attack job never takes it back, so each execution
        // left one more stale job queued. After the execution the AI or the draft picks anew.
        executor.jobs.StartJob(job, JobCondition.InterruptForced, resumeCurJobAfterwards: false);
        if (executor.CurJob != job) ReleaseBreak();
    }

    /// <summary>
    /// The player's melee attack order on this broken pawn. Refused while an execution of it runs or
    /// another pawn's order on it stands; an executor that takes orders executes it instead of cutting.
    /// </summary>
    internal bool TakeOrder(Pawn pawn)
    {
        if (executioner != null || (orderedBy != null && orderedBy != pawn)) return false;
        CombatBlocker b = CombatBlockers.ExecutionerFor(pawn);
        if (b is { Executes: true, AcceptsOrders: true } && b.ModeFor(pawn) != ExecutionMode.None) orderedBy = pawn;
        return true;
    }

    /// <summary>
    /// The execution ended before its blow. An executor that keeps the break leaves it on its own
    /// time and no longer takes it by itself; an attack order can still execute it.
    /// </summary>
    public void AbandonExecution(Pawn executor)
    {
        if (executioner != executor) return;
        if (CombatBlockers.ExecutionerFor(executor)?.KeepsBreakOnAbandon != true)
        {
            ReleaseBreak();
            return;
        }
        executioner = null;
        autoDeclined = true;
        if (untilBeforeExecution >= 0) brokenUntilTick = untilBeforeExecution;
        untilBeforeExecution = -1;
    }

    private void HoldWithoutExecution() => EnsureHold();

    /// <summary>
    /// The victim holds still in the broken job for the whole window. Other jobs, casts, paths and
    /// orders cannot start meanwhile (CombatPostureControl); if the hold job still ends (a forced
    /// end, a load), it starts again instead of the break being dropped.
    /// </summary>
    private void EnsureHold()
    {
        holdStarted = true;
        if (Pawn.Downed || Pawn.CurJobDef == C.brokenJob || Pawn.jobs == null) return;
        Pawn.jobs.StartJob(JobMaker.MakeJob(C.brokenJob), JobCondition.InterruptForced);
    }

    // Posture from something other than a block on the attacker (for example a
    // defender's own guard taking the hit). No opening.
    public void AddPressure(Pawn source, float amount) => ReceiveBlock(source, amount, opening: false);

    // HediffWithComps dispatches interval comps separately in 1.7. Sharing the
    // same deadline also makes legacy comp callbacks safe if both are dispatched.
    public override void CompPostTickInterval(ref float severityAdjustment, int delta) =>
        CompPostTick(ref severityAdjustment);

    public bool Owns(Pawn executor) => broken && executioner == executor && Now < brokenUntilTick;

    /// <summary>The pawn's next move on this broken pawn is an execution, not an ordinary attack.</summary>
    public bool ExecutesNext(Pawn pawn)
    {
        if (!broken || pawn == null) return false;
        if (executioner == pawn || orderedBy == pawn) return true;
        CombatBlocker b = CombatBlockers.ExecutionerFor(pawn);
        return source == pawn && executioner == null && !autoDeclined && b is { Executes: true } && b.ModeFor(pawn) == ExecutionMode.Auto;
    }

    internal void HoldThrough(Pawn executor, int untilTick)
    {
        if (Owns(executor)) brokenUntilTick = Mathf.Max(brokenUntilTick, untilTick);
    }

    // The break of a move that cannot execute lasts past its end, so the execution can still follow.
    public void HoldBreakPast(Pawn executor, int endTick)
    {
        if (!broken || executioner != null || source != executor) return;
        int window = CombatBlockers.BreakWindowFor(CombatBlockers.ExecutionerFor(source), C.breakWindowTicks);
        brokenUntilTick = Mathf.Max(brokenUntilTick, endTick + window);
    }

    /// <param name="executed">The execution blow landed: the meter empties. Otherwise the breaker's
    /// blocker decides how much posture stays (<see cref="CombatBlocker.UnexecutedPostureKept"/>).</param>
    public void ReleaseBreak(bool executed = false)
    {
        float kept = 0f;
        if (broken && !executed && Pawn is { Dead: false })
            kept = Mathf.Clamp01(CombatPostureRules.Finite(CombatBlockers.ExecutionerFor(source)?.UnexecutedPostureKept ?? 0f));
        broken = false;
        executioner = null;
        source = null;
        orderedBy = null;
        autoDeclined = false;
        untilBeforeExecution = -1;
        holdStarted = false;
        brokenUntilTick = 0;
        brokenAt = -1;
        breakCell = IntVec3.Invalid;
        // Kept below the maximum so the release itself never breaks it again.
        SetValue(kept > 0f ? Mathf.Min(MaxValue * kept, MaxValue - .01f) : 0f);
        // The kept posture waits out the usual recovery delay before it drains.
        if (kept > 0f) lastIncreaseTick = Now;
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
        executioner = source = orderedBy = null;
        openingReserved = false;
    }
}

// The victim holds still for the break window; execution jobs check this job def.
public sealed class JobDriver_CombatPostureBroken : JobDriver
{
    public override bool PlayerInterruptable => false;
    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

    protected override IEnumerable<Toil> MakeNewToils()
    {
        Toil hold = ToilMaker.MakeToil("CombatPostureBroken");
        hold.defaultCompleteMode = ToilCompleteMode.Never;
        hold.tickAction = delegate
        {
            if (CombatPosture.Get(pawn)?.Broken != true)
            {
                EndJobWith(JobCondition.Succeeded);
                return;
            }
            pawn.pather.StopDead();
        };
        yield return hold;
    }
}

[HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
internal static class Patch_CombatPostureOpening
{
    private static void Prefix(Thing __instance, ref DamageInfo dinfo, out HediffComp_CombatPosture __state)
    {
        __state = null;
        if (__instance is not Pawn pawn || !float.IsFinite(dinfo.Amount) || dinfo.Amount <= 0f
            || dinfo.Instigator is not Pawn || dinfo.Def?.harmsHealth != true
            || dinfo.Def.Worker is not DamageWorker_AddInjury || !dinfo.Def.ExternalViolenceFor(pawn)
            || dinfo.Def.isExplosive || (dinfo.Tool == null && dinfo.Weapon?.IsWeapon != true
                && dinfo.Weapon?.projectile == null)) return;
        HediffComp_CombatPosture posture = CombatPosture.Get(pawn);
        if (posture?.TryReserveOpening() != true) return;
        __state = posture;
        dinfo.SetAmount(dinfo.Amount * CombatPosture.Config.vulnerabilityMultiplier);
    }

    private static void Postfix(HediffComp_CombatPosture __state, DamageWorker.DamageResult __result) =>
        __state?.CompleteOpening(__result?.totalDamageDealt > 0f);

    private static void Finalizer(HediffComp_CombatPosture __state) => __state?.ReleaseOpening();
}

[HarmonyPatch(typeof(Pawn), nameof(Pawn.DrawGUIOverlay))]
internal static class Patch_CombatPostureBar
{
    private static readonly Color Red = new(1f, .19f, .12f);

    private static void Postfix(Pawn __instance)
    {
        if (Event.current.type != EventType.Repaint || !__instance.Spawned || __instance.Dead
            || __instance.IsHiddenFromPlayer() || __instance.Position.Fogged(__instance.Map)
            || Find.ScreenshotModeHandler.Active) return;
        HediffComp_CombatPosture state = CombatPosture.Get(__instance);
        if (state == null || state.BarAlpha <= 0f) return;
        float ratio = state.ValuePercent, alpha = state.BarAlpha;
        float width = Mathf.Clamp(Find.CameraDriver.CellSizePixels / Prefs.UIScale * .92f, 16f, 48f);
        Vector2 pos = GenMapUI.LabelDrawPosFor(__instance, .55f);
        Rect rect = new(pos.x - width * .5f, pos.y - 15f, width, 4f);
        Color color = ratio >= .8f ? Red
            : ratio >= .5f ? new Color(1f, .38f, .1f) : new Color(.65f, .29f, .08f);
        if (ratio >= .8f) alpha *= .8f + .2f * Mathf.Sin(CombatPosture.Now * .3f);
        color.a = alpha;
        Widgets.DrawBoxSolid(rect.ExpandedBy(1f), new Color(.06f, .025f, .025f, alpha * .85f));
        if (state.Broken) color = new Color(1f, .88f, .56f, alpha);
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.y, rect.width * (state.Broken ? 1f : ratio), rect.height), color);
        if (ratio >= .9f)
            Widgets.DrawBoxSolid(new Rect(rect.xMax - 1f, rect.y - 1f, 2f, 6f), new Color(1f, .85f, .4f, alpha));
        if (!Find.Selector.IsSelected(__instance)) return;
        GameFont font = Text.Font;
        TextAnchor anchor = Text.Anchor;
        Color previous = GUI.color;
        Text.Font = GameFont.Tiny; Text.Anchor = TextAnchor.LowerCenter; GUI.color = Color.white;
        Widgets.Label(new Rect(pos.x - 60f, rect.y - 20f, 120f, 19f), state.Broken ? "架势崩溃" : $"架势 {state.CurrentValue:0}/{state.MaxValue:0}");
        GUI.color = previous; Text.Font = font; Text.Anchor = anchor;
    }
}

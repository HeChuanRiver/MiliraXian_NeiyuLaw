using System.Collections.Generic;
using MiliraXian.Characters.Common.Combat;
using MiliraXian.Characters.Common.Vfx.BladeBody;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Neiyu;

// Neiyu's block and execution values live in MX_NeiyuSwordCombat. The posture
// meter itself (maximum, recovery, break window, openings) is the shared
// MX_CombatPosture. Visual preferences use the existing mod settings.
public sealed class NeiyuSwordCombatDef : Def
{
    public ThingDef sword;
    public JobDef executionJob;
    public float successChance = .8f, perfectChance = .45f;
    public bool reflectProjectiles = true;
    public float projectileReflectChance = .6f, projectilePerfectChance = .15f;
    public float projectileReturnSpreadDegrees = 4f;
    public float normalPosture = 25f, perfectPosture = 45f;
    public int perfectStunTicks = 45;
    public int executionTicks = 84;
    public float dashFraction = .18f, strikeFraction = .40f, recoverFraction = .61f;
    public float executionRange = 2f, visualIntensity = 1f;
    public int maxVisuals = 96, sparksPerDeflection = 12;
    public SoundDef normalSound, perfectSound, breakSound, prepareSound, strikeSound, endSound;
    public NeiyuSingleExecutionProps single = new();

    public override IEnumerable<string> ConfigErrors()
    {
        foreach (string error in base.ConfigErrors()) yield return error;
        if (sword == null || executionJob == null)
            yield return "剑与处决 Job 引用必须完整。";
        if (!(perfectChance >= 0f && perfectChance <= successChance && successChance <= 1f))
            yield return "弹刀概率必须满足 0 ≤ 完美概率 ≤ 总概率 ≤ 1。";
        if (!(projectilePerfectChance >= 0f && projectilePerfectChance <= projectileReflectChance
            && projectileReflectChance <= 1f))
            yield return "弹丸反弹概率必须满足 0 ≤ 完美概率 ≤ 总概率 ≤ 1。";
        if (!(projectileReturnSpreadDegrees >= 0f && projectileReturnSpreadDegrees <= 30f))
            yield return "弹丸弹回方向的偏转角必须在 0–30 度之间。";
        if (!(normalPosture > 0f && perfectPosture > 0f))
            yield return "弹刀架势参数超出有效范围。";
        if (!(executionTicks >= 12 && dashFraction > 0f && dashFraction < strikeFraction
            && strikeFraction < recoverFraction && recoverFraction < 1f
            && executionRange >= 1.42f && perfectStunTicks >= 0
            && maxVisuals > 0 && sparksPerDeflection >= 0 && visualIntensity >= 0f))
            yield return "处决时间、阶段、距离或特效参数无效。";
        if (single == null || !single.Valid)
            yield return "单体处决的时间、距离或光剑参数无效。";
    }
}

internal static class NeiyuSwordCombatRules
{
    internal static float Finite(float value, float fallback = 0f) => CombatPostureRules.Finite(value, fallback);

    // One roll: 0 = failed, 1 = normal, 2 = perfect. Perfect is an absolute
    // probability, bounded by total success rather than rolled a second time.
    internal static int Deflection(float roll, float success, float perfect)
    {
        success = System.Math.Clamp(Finite(success), 0f, 1f);
        perfect = System.Math.Clamp(Finite(perfect), 0f, success);
        return roll < perfect ? 2 : roll < success ? 1 : 0;
    }
}

// Neiyu's sword form in the shared block framework: melee deflection with
// posture, projectile reflection and posture-break execution. Behaviour is
// unchanged from the Neiyu-only interception it replaces.
internal sealed class NeiyuSwordBlocker : CombatBlocker
{
    public override bool Covers(Pawn pawn) => NeiyuEquipmentUtility.IsNeiyu(pawn);
    public override bool TryBlockMelee(Verb_MeleeAttack verb, Pawn defender) => NeiyuSwordCombat.TryDeflect(verb, defender);
    public override bool TryBlockProjectile(Projectile projectile, Pawn defender) => NeiyuProjectileReflection.TryReflect(projectile, defender);
    public override bool CanExecute(Pawn executor, Pawn victim) => NeiyuSwordCombat.ExecutionPair(executor, victim);
    public override Job MakeExecutionJob(Pawn executor, Pawn victim) => JobMaker.MakeJob(NeiyuSwordCombat.Config.executionJob, victim);
    public override int MinimumBreakWindowTicks => NeiyuSwordCombat.Config.executionTicks + 30;
    public override void NotifyPostureBroken(Pawn source, Pawn victim) => NeiyuSwordCombatVfx.Break(victim);
    public override bool SuppressOpenings => NeiyuPowerBalance.PassivesDisabled;
}

[StaticConstructorOnStartup]
internal static class NeiyuSwordCombat
{
    internal static readonly NeiyuSwordCombatDef Config = DefDatabase<NeiyuSwordCombatDef>.GetNamed("MX_NeiyuSwordCombat");
    internal static int Now => Find.TickManager?.TicksGame ?? 0;

    static NeiyuSwordCombat() => CombatBlockers.Register(new NeiyuSwordBlocker());
    internal static bool SwordEquipped(Pawn pawn) => NeiyuEquipmentUtility.IsNeiyu(pawn)
        && NeiyuFlowerSwordSkillUtility.HasRequiredWeapon(pawn, Config.sword);

    internal static bool CanAct(Pawn pawn) => pawn is { Spawned: true, Dead: false, Destroyed: false, Downed: false }
        && !NeiyuPowerBalance.PassivesDisabled && !pawn.InMentalState && !pawn.Deathresting
        && !pawn.InContainerEnclosed && pawn.Awake() && pawn.health?.capacities.CanBeAwake == true
        && !CombatPosture.ControlsActions(pawn)
        && pawn.GetPosture() == PawnPosture.Standing && !pawn.IsBurning()
        && pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
        && !pawn.WorkTagIsDisabled(WorkTags.Violent) && pawn.stances?.stunner?.Stunned != true
        && SwordEquipped(pawn);

    internal static HediffComp_CombatPosture Posture(Pawn pawn) => CombatPosture.Get(pawn);

    internal static bool HostilePair(Pawn defender, Pawn attacker) => attacker is { Spawned: true, Dead: false, Destroyed: false }
        && defender != attacker && defender.Map == attacker.Map && GenHostility.HostileTo(attacker, defender);

    internal static bool TryDeflect(Verb_MeleeAttack verb, Pawn defender)
    {
        Pawn attacker = verb.CasterPawn;
        // The melee verb is the attack boundary. Mechanoid blades, natural tools,
        // fists and implanted weapons need not have an EquipmentSource.
        if (!CanAct(defender) || !HostilePair(defender, attacker)
            || !verb.verbProps.IsMeleeAttack
            || !DebugSettings.enableDamage) return false;
        int outcome = NeiyuSwordCombatRules.Deflection(Rand.Value, Config.successChance, Config.perfectChance);
        if (outcome == 0) return false;
        HediffComp_CombatPosture posture = CombatPosture.Ensure(attacker);
        if (posture == null) return false;
        bool perfect = outcome == 2;
        posture.ReceiveBlock(defender, perfect ? Config.perfectPosture : Config.normalPosture, opening: perfect);
        if (perfect && Config.perfectStunTicks > 0)
            attacker.stances?.stunner?.StunFor(Config.perfectStunTicks, defender);
        NeiyuSwordCombatVfx.Deflect(defender, attacker, perfect);
        return true;
    }

    internal static bool ExecutionTarget(Pawn target)
    {
        if (target is not { Spawned: true, Dead: false, Destroyed: false } || target.health == null
            || target.health.isBeingKilled || target.IsQuestLodger()) return false;
        // Native deathless and hediff protection must not be bypassed by direct Kill.
        if (ModsConfig.BiotechActive && target.genes?.HasActiveGene(GeneDefOf.Deathless) == true) return false;
        foreach (Hediff hediff in target.health.hediffSet.hediffs)
            if (hediff.def.preventsDeath) return false;
        return true;
    }

    internal static bool ExecutionPair(Pawn executor, Pawn target) => CanAct(executor) && ExecutionTarget(target)
        && HostilePair(executor, target) && InExecutionReach(executor, target);

    internal static bool InExecutionReach(Pawn executor, Pawn target) =>
        executor.Map == target.Map
        && executor.Position.DistanceToSquared(target.Position) <= Config.executionRange * Config.executionRange
        && GenSight.LineOfSight(executor.Position, target.Position, executor.Map);

    // Execution is a committed finishing event, never an ordinary melee hit or
    // damage packet. Neither hit/dodge rolls nor damage-avoidance shields run here.
    internal static void Execute(Pawn executor, Pawn victim)
    {
        if (!CanAct(executor) || !ExecutionTarget(victim) || executor.Map != victim.Map) return;
        DamageInfo execution = new(DamageDefOf.ExecutionCut, 1f, 0f, -1f,
            executor, null, executor.equipment.Primary.def);
        // The one exception: a life-like charge (Qinghe's Flowing Form) takes the execution instead.
        if (ExecutionGuard.TryNegate(victim, execution)) return;
        victim.Kill(execution);
    }
}

public enum NeiyuExecutionPhase { Normal, PostureBroken, ExecutionStart, ExecutionStrike, ExecutionEnd }

public sealed partial class JobDriver_NeiyuPostureExecution : JobDriver, IPostureExecutionDriver, IBladePoseSource
{
    private enum Branch { Compatibility, Single, Invalid }
    private Branch branch;
    private int startedAt = -1, duration, dashTick, strikeTick, recoverTick;
    private bool prepared, dashed, strikeCommitted, endedVisual;
    private bool visualsRebound;
    private Map executionMap;
    private ThingWithComps executionWeapon;
    private IntVec3 origin = IntVec3.Invalid, hitCell = IntVec3.Invalid;

    public Pawn Victim => job?.targetA.Pawn;
    internal bool IsSingle => branch == Branch.Single;
    public int Elapsed => Mathf.Max(0, NeiyuSwordCombat.Now - startedAt);
    public float Progress => Mathf.Clamp01(Elapsed / (float)Mathf.Max(1, duration));
    public float FacingAngle => (hitCell.ToVector3Shifted() - pawn.DrawPos).AngleFlat();
    public NeiyuExecutionPhase Phase => strikeCommitted
        ? Elapsed >= recoverTick ? NeiyuExecutionPhase.ExecutionEnd : NeiyuExecutionPhase.ExecutionStrike
        : NeiyuExecutionPhase.ExecutionStart;
    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

    public override string GetReport()
    {
        if (IsSingle && SceneTick < 0)
            return $"正在锁定{Victim?.LabelShortCap}，剩余 {(-SceneTick).TicksToSeconds():0.0} 秒。";
        return base.GetReport();
    }

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
        executionWeapon = pawn.equipment?.Primary;
        if (!NeiyuSwordCombat.ExecutionPair(pawn, Victim))
        {
            branch = Branch.Invalid;
            return;
        }
        branch = ChooseBranch(Victim);
        if (IsSingle) BeginSingle();
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
        Scribe_References.Look(ref executionWeapon, "swordExecutionWeapon");
        Scribe_Values.Look(ref branch, "swordExecutionBranch");
        ExposeSingle();
        base.ExposeData();
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        AddFinishAction(Finish);
        Toil execution = ToilMaker.MakeToil("NeiyuPostureExecution");
        execution.defaultCompleteMode = ToilCompleteMode.Never;
        // The single timeline owns its deadline and validates the pawn itself.
        // A lingering combat stance must allow the first Toil to start.
        execution.atomicWithPrevious = IsSingle;
        execution.handlingFacing = IsSingle;
        execution.tickAction = TickExecution;
        yield return execution;
    }

    private bool ValidExecutor() => startedAt >= 0 && executionMap != null && pawn.Map == executionMap
        && (executionWeapon == null ? !IsSingle : pawn.equipment?.Primary == executionWeapon)
        && NeiyuSwordCombat.CanAct(pawn);

    private void TickExecution()
    {
        if (branch == Branch.Invalid || !ValidExecutor())
        {
            EndJobWith(JobCondition.Incompletable);
            return;
        }
        if (IsSingle) TickSingle();
        else TickCompatibility();
    }

    private bool ValidCompatibility()
    {
        if (duration < 12 || !origin.IsValid || pawn.Position != origin) return false;
        if (strikeCommitted) return true;
        return NeiyuSwordCombat.ExecutionPair(pawn, Victim)
            && (Victim.Downed || Victim.CurJobDef == CombatPosture.Config.brokenJob)
            && NeiyuSwordCombat.Posture(Victim)?.Owns(pawn) == true;
    }

    private void TickCompatibility()
    {
        if (!ValidCompatibility()) { EndJobWith(JobCondition.Incompletable); return; }
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
            if (!CommitStrike(Victim)) return;
        }
        if (!endedVisual && Elapsed >= recoverTick)
        {
            endedVisual = true;
            NeiyuSwordCombatVfx.Execution(pawn, hitCell, NeiyuExecutionPhase.ExecutionEnd);
        }
        if (Elapsed >= duration) EndJobWith(JobCondition.Succeeded);
    }

    private bool CommitStrike(Pawn victim)
    {
        // Write the event before death callbacks, including callbacks that save,
        // resurrect, move the victim, or replace this job.
        strikeCommitted = true;
        int jobId = job.loadID;
        try
        {
            if (IsSingle)
                NeiyuSwordCombatVfx.SingleSound(pawn, aimPoint, Single.pierceSound ?? NeiyuSwordCombat.Config.endSound);
            else
            {
                NeiyuSwordCombatVfx.Execution(pawn, hitCell, NeiyuExecutionPhase.ExecutionStrike);
                pawn.Drawer.Notify_MeleeAttackOn(victim);
            }
            if (pawn.jobs.curDriver != this || job.loadID != jobId) return false;
            NeiyuSwordCombat.Execute(pawn, victim);
        }
        catch (System.Exception exception)
        {
            Log.ErrorOnce($"[霓羽弹刀] 处决死亡回调中断，已释放目标状态：{exception}", 197632100);
            if (pawn.jobs.curDriver == this) EndJobWith(JobCondition.Errored);
            return false;
        }
        finally { ReleaseVictim(victim); }
        return pawn.jobs.curDriver == this && job.loadID == jobId;
    }

    private void ReleaseVictim(Pawn victim)
    {
        HediffComp_CombatPosture posture = NeiyuSwordCombat.Posture(victim);
        if (posture?.Executioner == pawn) posture.ReleaseBreak();
    }

    private void Finish(JobCondition condition)
    {
        ReleaseVictim(Victim);
        if (IsSingle) FinishSingle();
        // The execution does not resume the interrupted job (see CombatPosture): the AI or the
        // draft picks anew. Never clear the player's queue or revive a saved reference to a
        // pooled old Job.
    }
}

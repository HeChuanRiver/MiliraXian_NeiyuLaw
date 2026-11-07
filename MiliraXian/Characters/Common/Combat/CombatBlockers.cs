using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Common.Combat;

// A character's part in the shared block / posture / execution rules. The shared
// interception points ask the registered blockers instead of naming characters.
// Several blockers may cover the same pawn (an ordinary block and a stronger
// skill block); they are asked from the highest priority down, first success wins.
public abstract class CombatBlocker
{
    public virtual int Priority => 0;

    // Cheap identity gate, no rolls: whether this blocker can ever act for the pawn.
    public abstract bool Covers(Pawn pawn);

    // True: asked before the attack's hit roll (every swing that comes out is
    // checked, even one that would miss). False: asked where vanilla rolls dodge.
    public virtual bool BeforeHit => false;

    // Decide and apply one block. Called at most once per melee invocation and target.
    public virtual bool TryBlockMelee(Verb_MeleeAttack verb, Pawn defender) => false;

    // A melee hit on a covered pawn that no blocker stopped, before its damage.
    // Once per melee invocation and target.
    public virtual void NotifyMeleeHit(Verb_MeleeAttack verb, Pawn defender) { }

    // The same unblocked hit after its damage (null result: nothing was applied).
    public virtual void NotifyMeleeHitResult(Verb_MeleeAttack verb, Pawn defender, DamageWorker.DamageResult result) { }

    // False: posture this pawn breaks is never executed; the victim just holds
    // still for the break window and recovers.
    public virtual bool Executes => true;

    // Decide and apply one projectile block; true consumes the impact.
    public virtual bool TryBlockProjectile(Projectile projectile, Pawn defender) => false;

    // Execution of a posture-broken victim by the pawn who broke it.
    public virtual bool CanExecute(Pawn executor, Pawn victim) => false;
    public virtual Job MakeExecutionJob(Pawn executor, Pawn victim) => null;
    // Extra break window an execution needs beyond the posture's own. Also the
    // least time left on the break once the execution job takes the victim.
    public virtual int MinimumBreakWindowTicks => 0;
    // True: while CanExecute is false (the executor is busy or out of reach) the
    // victim holds still for the rest of the break window and the executor may
    // still come. False: the break ends at once (Neiyu's behaviour).
    public virtual bool WaitForExecutor => false;
    // Share of its maximum posture a victim this pawn broke keeps when the break ends without
    // an execution blow (no time to execute it); it then drains as usual after the recovery delay.
    // 0: the meter empties, as after an execution.
    public virtual float UnexecutedPostureKept => 0f;
    public virtual void NotifyPostureBroken(Pawn source, Pawn victim) { }

    // While true, existing posture openings give no bonus damage.
    public virtual bool SuppressOpenings => false;
}

// A running execution job; the posture lock is valid only while it owns the victim.
public interface IPostureExecutionDriver
{
    Pawn Victim { get; }
    bool DrivesExecutorPosition => false;
}

// Which blockers a melee block decision asks: all, only the ones that decide
// before the hit roll, or only the ones that decide at the dodge roll.
public enum MeleeBlockStage { Any, BeforeHit, AfterHit }

public static class CombatBlockers
{
    private static readonly List<CombatBlocker> Blockers = new();

    public static void Register(CombatBlocker blocker)
    {
        if (blocker == null || Blockers.Contains(blocker)) return;
        int i = 0;
        while (i < Blockers.Count && Blockers[i].Priority >= blocker.Priority) i++;
        Blockers.Insert(i, blocker);
    }

    public static bool Covers(Pawn pawn)
    {
        if (pawn == null) return false;
        for (int i = 0; i < Blockers.Count; i++)
            if (Blockers[i].Covers(pawn)) return true;
        return false;
    }

    public static bool TryBlockMelee(Verb_MeleeAttack verb, Pawn defender, MeleeBlockStage stage = MeleeBlockStage.Any)
    {
        for (int i = 0; i < Blockers.Count; i++)
        {
            CombatBlocker b = Blockers[i];
            if (stage == MeleeBlockStage.BeforeHit && !b.BeforeHit || stage == MeleeBlockStage.AfterHit && b.BeforeHit) continue;
            if (b.Covers(defender) && b.TryBlockMelee(verb, defender)) return true;
        }
        return false;
    }

    public static void NotifyMeleeHit(Verb_MeleeAttack verb, Pawn defender)
    {
        if (defender == null) return;
        for (int i = 0; i < Blockers.Count; i++)
            if (Blockers[i].Covers(defender)) Blockers[i].NotifyMeleeHit(verb, defender);
    }

    public static void NotifyMeleeHitResult(Verb_MeleeAttack verb, Pawn defender, DamageWorker.DamageResult result)
    {
        if (defender == null) return;
        for (int i = 0; i < Blockers.Count; i++)
            if (Blockers[i].Covers(defender)) Blockers[i].NotifyMeleeHitResult(verb, defender, result);
    }

    public static bool TryBlockProjectile(Projectile projectile, Pawn defender)
    {
        for (int i = 0; i < Blockers.Count; i++)
            if (Blockers[i].Covers(defender) && Blockers[i].TryBlockProjectile(projectile, defender)) return true;
        return false;
    }

    // The blocker that owns executions by this pawn, if any.
    public static CombatBlocker ExecutionerFor(Pawn executor)
    {
        if (executor == null) return null;
        for (int i = 0; i < Blockers.Count; i++)
            if (Blockers[i].Covers(executor)) return Blockers[i];
        return null;
    }

    public static bool CanExecute(Pawn executor, Pawn victim) =>
        ExecutionerFor(executor)?.CanExecute(executor, victim) == true;

    public static bool OpeningsSuppressed
    {
        get
        {
            for (int i = 0; i < Blockers.Count; i++)
                if (Blockers[i].SuppressOpenings) return true;
            return false;
        }
    }
}

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

    // Decide and apply one block. Called at most once per melee invocation and target.
    public virtual bool TryBlockMelee(Verb_MeleeAttack verb, Pawn defender) => false;

    // Decide and apply one projectile block; true consumes the impact.
    public virtual bool TryBlockProjectile(Projectile projectile, Pawn defender) => false;

    // Execution of a posture-broken victim by the pawn who broke it.
    public virtual bool CanExecute(Pawn executor, Pawn victim) => false;
    public virtual Job MakeExecutionJob(Pawn executor, Pawn victim) => null;
    // Extra break window an execution needs beyond the posture's own.
    public virtual int MinimumBreakWindowTicks => 0;
    public virtual void NotifyPostureBroken(Pawn source, Pawn victim) { }

    // While true, existing posture openings give no bonus damage.
    public virtual bool SuppressOpenings => false;
}

// A running execution job; the posture lock is valid only while it owns the victim.
public interface IPostureExecutionDriver
{
    Pawn Victim { get; }
}

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

    public static bool TryBlockMelee(Verb_MeleeAttack verb, Pawn defender)
    {
        for (int i = 0; i < Blockers.Count; i++)
            if (Blockers[i].Covers(defender) && Blockers[i].TryBlockMelee(verb, defender)) return true;
        return false;
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

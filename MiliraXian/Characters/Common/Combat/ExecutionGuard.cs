using System;
using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters.Common.Combat;

/// <summary>
/// The only things allowed to stop an execution. Executions skip every shield, guard and damage
/// reduction (<see cref="DirectDamage"/>, or a plain kill), but a guard registered here is asked
/// first: one that spends a life-like charge (Qinghe's Flowing Form, one bar of a future phantom
/// boss) negates the whole execution. Character code registers its guards at startup; this file
/// does not depend on any character.
/// </summary>
public static class ExecutionGuard
{
    /// <summary>Returns true when it negated the execution of the victim (and spent what that costs).</summary>
    public delegate bool Guard(Pawn victim, DamageInfo dinfo);

    private static readonly List<Guard> Guards = new();

    public static void Register(Guard guard)
    {
        if (guard != null && !Guards.Contains(guard))
        {
            Guards.Add(guard);
        }
    }

    /// <summary>Asks every guard in order; true as soon as one negates the execution.</summary>
    public static bool TryNegate(Pawn victim, DamageInfo dinfo)
    {
        if (victim == null || victim.Dead)
        {
            return false;
        }
        for (int i = 0; i < Guards.Count; i++)
        {
            try
            {
                if (Guards[i](victim, dinfo))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Log.ErrorOnce("[MiliraXian] Execution guard failed: " + ex, 0x51c7a7);
            }
        }
        return false;
    }
}

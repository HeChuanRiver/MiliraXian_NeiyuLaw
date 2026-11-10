using HarmonyLib;
using MiliraXian.Characters.QingHe.Verbs;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    /// <summary>How the Flower Bell answers a melee threat (RangedRework.md 2.9, owner 11-11; a Gizmo toggle).</summary>
    public enum FlowerBellThreatMode
    {
        /// <summary>Keep charging where she stands; the melee hits are taken.</summary>
        Hold,
        /// <summary>Step away from the attacker, then attack again (the charge decays meanwhile).</summary>
        Retreat,
    }

    /// <summary>
    /// Vanilla answers a melee hit with a one-attack melee job (<see cref="JobGiver_ReactToCloseMeleeThreat"/>),
    /// high in the think tree, for drafted pawns too: its start cancels the charge. With the bell:
    /// at tier 1 or more she releases at once (the notes land on the attacker at close range);
    /// below it she holds (keeps charging) or steps away, by the weapon's toggle.
    /// </summary>
    [HarmonyPatch(typeof(JobGiver_ReactToCloseMeleeThreat), "TryGiveJob")]
    internal static class Patch_FlowerBellMeleeThreat
    {
        /// <summary>Cells to step away; vanilla melee pawns are about as fast, so a short hop and a new attack.</summary>
        private const float RetreatDistance = 4f;

        private static bool Prefix(Pawn pawn, ref Job __result)
        {
            Pawn threat = pawn?.mindState?.meleeThreat;
            CompFlowerBellCharge comp = pawn?.equipment?.Primary?.GetComp<CompFlowerBellCharge>();
            if (threat == null || comp == null || !pawn.mindState.MeleeThreatStillThreat)
            {
                return true;
            }
            if (comp.Charging)
            {
                if (comp.Tier >= 1)
                {
                    // Release now, at the current target (the notes home; the attacker is close by).
                    comp.releaseRequested = true;
                    __result = null;
                    return false;
                }
                if (comp.ThreatMode == FlowerBellThreatMode.Hold)
                {
                    __result = null;
                    return false;
                }
            }
            else if (comp.ThreatMode == FlowerBellThreatMode.Hold)
            {
                // Not charging (cooldown, idle): vanilla's answer, a melee blow, is fine.
                return true;
            }
            // Retreat: away from the attacker; the attack job comes back from the think tree after.
            if (pawn.Downed || !pawn.Spawned || pawn.stances.stunner.Stunned)
            {
                return true;
            }
            if (!RCellFinder.TryFindDirectFleeDestination(threat.Position, RetreatDistance, pawn, out IntVec3 dest) || dest == pawn.Position)
            {
                // Nowhere to go: hold (the charge goes on) rather than a melee blow.
                __result = null;
                return !comp.Charging;
            }
            Job job = JobMaker.MakeJob(JobDefOf.Goto, dest);
            job.locomotionUrgency = LocomotionUrgency.Sprint;
            job.expiryInterval = 120;
            job.checkOverrideOnExpire = true;
            // A drafted pawn would otherwise stand at the new cell; let it shoot again from there.
            job.reactingToMeleeThreat = true;
            __result = job;
            return false;
        }
    }
}

using HarmonyLib;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    /// <summary>How the notes of a volley pick their targets (a Gizmo toggle).</summary>
    public enum FlowerBellAimMode
    {
        /// <summary>Every note homes on the selected target.</summary>
        Focus,
        /// <summary>The first note on the selected target, the others on random hostiles around it.</summary>
        Scatter,
    }

    /// <summary>
    /// The Flower Bell never uses fists. All melee verb attempts are blocked.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_FlowerBellMeleeThreat
    {
        /// <summary>
        /// With the bell in hand she never strikes anyone in melee. This is the single point every
        /// melee blow goes through (the AI's reaction job, an attack order, terrain verbs): refusing
        /// here leaves the calling <see cref="JobDriver_AttackMelee"/> without a verb, and since its
        /// attack toil never completes on its own, the job is ended at once so she does not stand there.
        /// </summary>
        [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryMeleeAttack))]
        [HarmonyPrefix]
        private static bool Prefix_NoMeleeAttack(Pawn_MeleeVerbs __instance, ref bool __result)
        {
            Pawn pawn = __instance.Pawn;
            if (pawn?.equipment?.Primary?.GetComp<CompFlowerBellCharge>() == null)
            {
                return true;
            }
            __result = false;
            if (pawn.jobs?.curDriver is JobDriver_AttackMelee)
            {
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
            return false;
        }

        /// <summary>No melee verb is offered at all while the bell is held.</summary>
        [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryGetMeleeVerb))]
        [HarmonyPrefix]
        private static bool Prefix_NoMeleeVerb(Pawn_MeleeVerbs __instance, ref Verb __result)
        {
            if (__instance.Pawn?.equipment?.Primary?.GetComp<CompFlowerBellCharge>() == null)
            {
                return true;
            }
            __result = null;
            return false;
        }

        /// <summary>
        /// The bell may be rung at any distance. Vanilla refuses an adjacent shot at a standing hostile
        /// (VerbUtility.AllowAdjacentShot returns true only for a downed one), which pushes the ranged
        /// minimum range up to 1.421 cells: a target in her face then cannot be hit at all, so a full
        /// charge is wasted. Allow it for the bell's wielder, nobody else.
        /// </summary>
        [HarmonyPatch(typeof(VerbUtility), nameof(VerbUtility.AllowAdjacentShot))]
        [HarmonyPrefix]
        private static bool Prefix_AllowAdjacentShot(Thing caster, ref bool __result)
        {
            if (caster is not Pawn pawn || pawn.equipment?.Primary?.GetComp<CompFlowerBellCharge>() == null)
            {
                return true;
            }
            __result = true;
            return false;
        }
    }
}

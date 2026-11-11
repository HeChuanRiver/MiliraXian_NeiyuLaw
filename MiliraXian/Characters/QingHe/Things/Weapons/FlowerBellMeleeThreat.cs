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
    /// The Flower Bell never uses fists: every melee blow with it in hand becomes a ring. A target in
    /// her face is exactly what the bell is worst at in vanilla (a ranged pawn refuses to shoot an
    /// adjacent enemy), so the two refusals cancel: she rings whatever comes close.
    /// </summary>
    [HarmonyPatch]
    internal static class Patch_FlowerBellMeleeThreat
    {
        private static CompFlowerBellCharge BellOf(Pawn pawn) =>
            pawn?.equipment?.Primary?.GetComp<CompFlowerBellCharge>();

        /// <summary>
        /// One melee threat answered with the bell: standing still for the charge would just take the
        /// blows, and running away drops the charge, so she rings the attacker where she stands.
        /// </summary>
        [HarmonyPatch(typeof(JobGiver_ReactToCloseMeleeThreat), "TryGiveJob")]
        [HarmonyPrefix]
        private static bool Prefix_RingInsteadOfPunch(Pawn pawn, ref Job __result)
        {
            if (BellOf(pawn) == null)
            {
                return true;
            }
            Pawn threat = pawn.mindState?.meleeThreat;
            if (threat == null || !pawn.mindState.MeleeThreatStillThreat || pawn.Downed
                || pawn.stances?.stunner?.Stunned == true || pawn.WorkTagIsDisabled(WorkTags.Violent)
                || PawnUtility.PlayerForcedJobNowOrSoon(pawn))
            {
                return true;
            }
            if (pawn.playerSettings != null && pawn.playerSettings.UsesConfigurableHostilityResponse
                && pawn.playerSettings.hostilityResponse != HostilityResponseMode.Attack)
            {
                return true;
            }
            __result = RingJob(threat);
            return false;
        }

        /// <summary>The bell's answer to an adjacent enemy: stand and ring it until the charge lets go.</summary>
        private static Job RingJob(Pawn threat)
        {
            Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, threat);
            job.maxNumStaticAttacks = 1;
            job.expiryInterval = 900;
            job.checkOverrideOnExpire = true;
            job.endIfCantShootTargetFromCurPos = true;
            return job;
        }

        /// <summary>
        /// With the bell in hand she never strikes anyone in melee. This is the single point every
        /// melee blow goes through (the AI's reaction job, an attack order, terrain verbs, and the
        /// adjacent-enemy scan of <see cref="JobDriver_Wait.CheckForAutoAttack"/>).
        /// </summary>
        [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryMeleeAttack))]
        [HarmonyPrefix]
        private static bool Prefix_NoMeleeAttack(Pawn_MeleeVerbs __instance, Thing target, ref bool __result)
        {
            Pawn pawn = __instance.Pawn;
            if (BellOf(pawn) == null)
            {
                return true;
            }
            __result = false;
            if (pawn.jobs?.curDriver is JobDriver_AttackMelee)
            {
                // A melee blow has no replacement inside its own toil: end the job, the think tree
                // hands her the ring instead. A cast started here would be cancelled by that job.
                pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
                return false;
            }
            // The auto-attack scan of a waiting pawn looks at the adjacent eight cells before it
            // looks any further, and returns once it finds someone: a target in her face would
            // otherwise be dropped unshot. Ring it, as vanilla's ranged branch does further down.
            if (target != null && target.Spawned && !pawn.stances.FullBodyBusy
                && GenHostility.HostileTo(target, pawn))
            {
                pawn.TryStartAttack(target);
            }
            return false;
        }

        /// <summary>No melee verb is offered at all while the bell is held.</summary>
        [HarmonyPatch(typeof(Pawn_MeleeVerbs), nameof(Pawn_MeleeVerbs.TryGetMeleeVerb))]
        [HarmonyPrefix]
        private static bool Prefix_NoMeleeVerb(Pawn_MeleeVerbs __instance, ref Verb __result)
        {
            if (BellOf(__instance.Pawn) == null)
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
            if (caster is not Pawn pawn || BellOf(pawn) == null)
            {
                return true;
            }
            __result = true;
            return false;
        }
    }
}

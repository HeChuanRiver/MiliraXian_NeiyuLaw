using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MiliraXian.Characters.Mingyuan
{
    public class JobGiver_MingyuanAttackFlame : JobGiver_AIFightEnemies
    {
        protected override Thing FindAttackTarget(Pawn pawn)
        {
            Thing target = pawn.mindState.duty?.focus.Thing;
            if (target is not Thing_MingyuanQuestRebirthFlame || target.Destroyed
                || !target.Spawned || target.Map != pawn.Map || !pawn.HostileTo(target))
            {
                return null;
            }

            Verb verb = pawn.TryGetAttackVerb(target, allowManualCastWeapons: true);
            return verb != null && (verb.CanHitTarget(target)
                || pawn.CanReach(target, PathEndMode.Touch, Danger.Deadly)) ? target : null;
        }

        protected override Job TryGiveJob(Pawn pawn)
        {
            // Reuse vanilla weapon selection, melee jobs and ranged firing positions.
            Job job = base.TryGiveJob(pawn);
            if (job?.def != JobDefOf.Wait_Combat)
            {
                return job;
            }

            // Wait_Combat searches for a different target when firing. AttackStatic
            // keeps the objective selected above, including for ordinary ranged mechs.
            Job attack = JobMaker.MakeJob(JobDefOf.AttackStatic, pawn.mindState.enemyTarget);
            attack.expiryInterval = job.expiryInterval;
            attack.checkOverrideOnExpire = true;
            attack.endIfCantShootTargetFromCurPos = true;
            return attack;
        }
    }

    public class LordJob_MingyuanAssaultFlame : LordJob
    {
        private Thing target;

        public LordJob_MingyuanAssaultFlame() { }

        public LordJob_MingyuanAssaultFlame(Thing target)
        {
            this.target = target;
        }

        public override StateGraph CreateGraph()
        {
            // Keep the vanilla assault lifecycle and leave when the objective is gone.
            var graph = new StateGraph();
            var assault = new LordToil_MingyuanAssaultFlame(target);
            var exit = new LordToil_ExitMapAndDefendSelf { useAvoidGrid = true };
            graph.AddToil(assault);
            graph.AddToil(exit);
            var transition = new Transition(assault, exit);
            transition.AddTrigger(new Trigger_ThingsDamageTaken(new List<Thing> { target }, 1f));
            graph.AddTransition(transition);
            return graph;
        }

        public override void ExposeData()
        {
            Scribe_References.Look(ref target, "target");
        }
    }

    public class LordToil_MingyuanAssaultFlame : LordToil_AssaultThings
    {
        private readonly Thing target;

        public LordToil_MingyuanAssaultFlame(Thing target) : base(new[] { target })
        {
            this.target = target;
        }

        public override void UpdateAllDuties()
        {
            if (target == null || target.Destroyed || !target.Spawned) return;

            // The inherited toil updates duties every 300 ticks and on arrival.
            foreach (Pawn pawn in lord.ownedPawns)
            {
                PawnDuty duty = pawn.mindState.duty;
                if (duty?.def != MX_MingyuanDefOf.MX_Mingyuan_AssaultRebirthFlame
                    || duty.focus.Thing != target)
                {
                    pawn.mindState.duty = new PawnDuty(MX_MingyuanDefOf.MX_Mingyuan_AssaultRebirthFlame, target);
                }
            }
        }
    }
}

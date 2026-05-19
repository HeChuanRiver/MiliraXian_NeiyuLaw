using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Zhaoli
{
    public class JobDriver_MX_ZhaoliCastAbility : JobDriver_CastVerbOnce
    {
        public override bool PlayerInterruptable => false;

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOnDespawnedOrNull(TargetIndex.A);
            this.FailOn(() => job?.ability == null);
            this.FailOn(() => !job.ability.CanCast && !job.ability.Casting);

            AddFinishAction(delegate
            {
                if (job?.ability != null && job.def.abilityCasting && job.ability.HasCooldown)
                {
                    job.ability.StartCooldown(job.ability.def.cooldownTicksRange.RandomInRange);
                }
            });

            Toil stopMoving = ToilMaker.MakeToil("ZhaoliCastAbility_StopMoving");
            stopMoving.initAction = delegate
            {
                pawn.pather.StopDead();
            };
            stopMoving.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return stopMoving;

            Toil cast = Toils_Combat.CastVerb(TargetIndex.A, TargetIndex.B, canHitNonTargetPawns: false);
            if (job.ability != null && job.ability.def.showCastingProgressBar && job.verbToUse != null)
            {
                cast.WithProgressBar(TargetIndex.A, () => job.verbToUse.WarmupProgress);
            }

            yield return cast;
        }

        public override void Notify_Starting()
        {
            base.Notify_Starting();
            job.ability?.Notify_StartedCasting();
        }

        public override string GetReport()
        {
            if (job.ability == null || job.ability.def.targetRequired)
            {
                return base.GetReport();
            }

            string report = "UsingVerbNoTarget".Translate(job.verbToUse.ReportLabel);
            if (job.ability.def.showCastingProgressBar)
            {
                report += " " + "DurationLeft".Translate(job.verbToUse.WarmupTicksLeft.ToStringSecondsFromTicks()) + ".";
            }

            return report;
        }
    }
}

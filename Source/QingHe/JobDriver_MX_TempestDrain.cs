using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe
{
    public class JobDriver_MX_TempestDrain : JobDriver_CastAbility
    {
        private Thing_TempestDrainField spawnedField;

        private CompProperties_AbilityTempestDrain Props
        {
            get
            {
                var comp = job?.ability?.CompOfType<CompAbilityEffect_TempestDrain>();
                return comp?.Props;
            }
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            foreach (var toil in base.MakeNewToils())
            {
                yield return toil;
            }

            AddFinishAction(delegate { CleanUp(); });

            Toil t = ToilMaker.MakeToil();
            t.initAction = delegate
            {
                pawn.pather.StopDead();
                var p = Props;
                if (p == null || p.fieldDef == null)
                {
                    Log.Error("TempestDrain: Cannot find CompAbilityEffect_TempestDrain or Field Def");
                    EndJobWith(JobCondition.Errored);
                    return;
                }

                IntVec3 target = TargetA.Cell;
                if (!target.InBounds(pawn.Map))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                spawnedField = (Thing_TempestDrainField)GenSpawn.Spawn(p.fieldDef, target, pawn.Map);
                var fieldComp = spawnedField.TryGetComp<CompTempestDrainField>();
                if (fieldComp == null)
                {
                    Log.Error("TempestDrain: Cannot find Spawned Field");
                    EndJobWith(JobCondition.Errored);
                    return;
                }

                fieldComp.Init(pawn);
            };
            t.tickAction = delegate
            {
                if (pawn.Downed || pawn.Dead)
                {
                    return;
                }

                var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_Tempest);
                if (hediff != null)
                {
                    var comp = hediff.TryGetComp<HediffComp_Tempest>();
                    comp?.AddValue(-0.1f);
                }
            };
            t.tickIntervalAction = delegate { pawn.rotationTracker.FaceCell(TargetA.Cell); };
            t.defaultCompleteMode = ToilCompleteMode.Delay;
            t.defaultDuration = Props != null ? System.Math.Max(1, Props.channelDurationTicks) : 999999;
            t.handlingFacing = true;
            t.AddFailCondition(() => PawnSpecialResourceUtility.GetCurrentResource(pawn, MX_QHDefOf.MX_QH_Tempest) <= (Props?.minResourceToMaintain ?? 10.0f));
            yield return t;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref spawnedField, "spawnedField", false);
        }

        private void CleanUp()
        {
            if (spawnedField != null && !spawnedField.Destroyed)
            {
                spawnedField.Destroy();
            }

            spawnedField = null;
        }
    }
}

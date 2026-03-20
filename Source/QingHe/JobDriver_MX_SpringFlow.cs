using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe
{
    public class JobDriver_MX_SpringFlow : JobDriver_CastAbility
    {
        private Thing_SpringFlowField spawnedField;
        private const int lastingTicks = 900;
        
        protected override IEnumerable<Toil> MakeNewToils()
        {
            foreach (var toil in base.MakeNewToils())
            {
                yield return toil;
            }
            // Add cleanup action
            AddFinishAction(delegate
            {
                CleanUp();
            });

            Toil t = ToilMaker.MakeToil();
            t.initAction = delegate
            {
                pawn.pather.StopDead();
                var comp = job.ability.CompOfType<CompAbilityEffect_SpringFlow>();
                if (comp == null || comp.Props.fieldDef == null)
                {
                    Log.Error("SpringFlow: Cannot find CompAbilityEffect_SpringFlow or Field Def");
                    EndJobWith(JobCondition.Errored);
                    return;
                }

                IntVec3 target = TargetA.Cell;
                if (!target.InBounds(pawn.Map))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                //Log.Message("Spawning field");
                spawnedField = (Thing_SpringFlowField)GenSpawn.Spawn(comp.Props.fieldDef, target, pawn.Map);
                var fieldComp = spawnedField.TryGetComp<CompSpringFlowField>();
                if (fieldComp == null)
                {
                    Log.Error("SpringFlow: Cannot find Spawned Field");
                    EndJobWith(JobCondition.Errored);
                    return;
                }
                fieldComp.Init(pawn);
            };
            t.tickAction = delegate
            {
                if (pawn.Downed || pawn.Dead) return;
                var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_Tempest);
                if (hediff != null)
                {
                    var comp = hediff.TryGetComp<HediffComp_Tempest>();
                    comp?.AddValue(0.04f);
                }
            };
            t.tickIntervalAction = delegate
            {
                pawn.rotationTracker.FaceCell(TargetA.Cell);
            };
            t.defaultCompleteMode = ToilCompleteMode.Delay;
            t.defaultDuration = lastingTicks;
            t.handlingFacing = true;
            t.AddFailCondition(() =>
            {
                // TODO: verify fail condition
                return false;
            });
            yield return t;
        }
        
        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref spawnedField, "spawnedField", false);
        }

        private void CleanUp()
        {
            //Log.Message("Clearing field");
            if (spawnedField != null && !spawnedField.Destroyed)
            {
                spawnedField.Destroy();
            }
            spawnedField = null;
        }
    }
}
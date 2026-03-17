using System;
using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe
{
    public class JobDriver_MX_SpringFlow : JobDriver_CastAbility
    {
        private Thing_SpringFlowField spawnedField;
        private const int lastingTicks = 1800;
        
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
                CompAbilityEffect_SpringFlow comp = job.ability.CompOfType<CompAbilityEffect_SpringFlow>();
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

                Log.Message("Spawning field"); // TODO: remove debug msg
                spawnedField = (Thing_SpringFlowField)GenSpawn.Spawn(comp.Props.fieldDef, target, pawn.Map);
                CompSpringFlowField fieldComp = spawnedField.TryGetComp<CompSpringFlowField>();
                if (fieldComp == null)
                {
                    Log.Error("SpringFlow: Cannot find Spawned Field");
                    EndJobWith(JobCondition.Errored);
                    return;
                }
                fieldComp.Init(pawn);
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
            Log.Message("Clearing field"); // TODO: remove debug msg
            if (spawnedField != null && !spawnedField.Destroyed)
            {
                spawnedField.Destroy();
            }
            spawnedField = null;
        }
    }
}
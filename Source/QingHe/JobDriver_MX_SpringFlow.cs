using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe
{
    public class JobDriver_MX_SpringFlow : JobDriver_CastAbility
    {
        private Thing_SpringFlowField spawnedField;

        private CompProperties_AbilitySpringFlow Props
        {
            get
            {
                var comp = job?.ability?.CompOfType<CompAbilityEffect_SpringFlow>();
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

            var t = ToilMaker.MakeToil();
            t.initAction = delegate
            {
                pawn.pather.StopDead();
                var p = Props;
                if (p == null || p.fieldDef == null)
                {
                    Log.Error("SpringFlow: Cannot find CompAbilityEffect_SpringFlow or Field Def");
                    EndJobWith(JobCondition.Errored);
                    return;
                }

                var target = TargetA.Cell;
                if (!target.InBounds(pawn.Map))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                spawnedField = (Thing_SpringFlowField)GenSpawn.Spawn(p.fieldDef, target, pawn.Map);
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
                if (pawn.Downed || pawn.Dead)
                {
                    return;
                }

                TempestUtility.AddTempest(pawn, 0.04f);
            };
            t.tickIntervalAction = delegate { pawn.rotationTracker.FaceCell(TargetA.Cell); };
            t.defaultCompleteMode = ToilCompleteMode.Delay;
            t.defaultDuration = System.Math.Max(1, Props?.fieldDurationTicks ?? 1);
            t.handlingFacing = true;
            t.AddFailCondition(() => false);
            t.WithProgressBarToilDelay(TargetIndex.None, false, -0.5f);
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
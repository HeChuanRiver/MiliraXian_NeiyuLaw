using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe
{
    public class JobDriver_MX_YangChun : JobDriver_CastAbility
    {
        private Thing_YangChunField spawnedField;

        private CompProperties_AbilityYangChun Props
        {
            get
            {
                var ability = job?.ability;
                if (ability?.def?.comps == null)
                {
                    return null;
                }

                foreach (var t in ability.def.comps)
                {
                    if (t is CompProperties_AbilityYangChun p)
                    {
                        return p;
                    }
                }

                return null;
            }
        }

        public override string GetReport()
        {
            return "Casting YangChun";
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            foreach (var toil in base.MakeNewToils())
            {
                yield return toil;
            }

            AddFinishAction(delegate
            {
                CleanUp();
            });

            var channel = ToilMaker.MakeToil("QHEleganceYangChun_Channel");
            channel.initAction = delegate
            {
                var p = Props;
                if (p == null)
                {
                    EndJobWith(JobCondition.Errored);
                    return;
                }

                if (!MX_QHUtility.HasRequiredWeapon(pawn, p.requiredWeapon) || p.fieldDef == null || pawn.Map == null || !pawn.Spawned)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                pawn.pather.StopDead();

                var thing = GenSpawn.Spawn(p.fieldDef, pawn.Position, pawn.Map);
                spawnedField = thing as Thing_YangChunField;
                if (spawnedField == null)
                {
                    thing.Destroy();
                    Log.Error("YangChun: spawned field is not Thing_YangChunField.");
                    EndJobWith(JobCondition.Errored);
                    return;
                }

                if (!(spawnedField.TryGetComp<CompYangChunField>() is CompYangChunField fieldComp))
                {
                    spawnedField.Destroy();
                    Log.Error("YangChun: Cannot find CompYangChunField on spawned field.");
                    EndJobWith(JobCondition.Errored);
                    return;
                }

                fieldComp.Init(pawn);
                fieldComp.SpawnFx();
            };
            channel.defaultCompleteMode = ToilCompleteMode.Delay;
            channel.defaultDuration = Props != null ? Mathf.Max(1, Props.fieldDurationTicks) : 1;
            channel.handlingFacing = true;
            channel.tickIntervalAction = delegate
            {
                pawn.rotationTracker.FaceCell(pawn.Position);
            };
            channel.AddFailCondition(delegate
            {
                var p = Props;
                return p == null || !MX_QHUtility.HasRequiredWeapon(pawn, p.requiredWeapon);
            });
            yield return channel;
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
                if (spawnedField.TryGetComp<CompYangChunField>() is CompYangChunField fieldComp)
                {
                    fieldComp.EndFx();
                }

                spawnedField.Destroy();
            }

            spawnedField = null;
        }
    }
}
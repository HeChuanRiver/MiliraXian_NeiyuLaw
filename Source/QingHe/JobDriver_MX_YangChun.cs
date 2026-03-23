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
                Ability ability = job != null ? job.ability : null;
                if (ability == null || ability.def == null || ability.def.comps == null)
                {
                    return null;
                }

                for (int i = 0; i < ability.def.comps.Count; i++)
                {
                    CompProperties_AbilityYangChun p = ability.def.comps[i] as CompProperties_AbilityYangChun;
                    if (p != null)
                    {
                        return p;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// Job report text.
        /// </summary>
        public override string GetReport()
        {
            return "施放阳春";
        }

        /// <summary>
        /// Spawn field when channel starts and clean it on job finish.
        /// </summary>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            foreach (Toil toil in base.MakeNewToils())
            {
                yield return toil;
            }

            AddFinishAction(delegate
            {
                CleanUp();
            });

            Toil channel = ToilMaker.MakeToil("QHEleganceYangChun_Channel");
            channel.initAction = delegate
            {
                CompProperties_AbilityYangChun p = Props;
                if (p == null)
                {
                    EndJobWith(JobCondition.Errored);
                    return;
                }

                if (!MX_QHUtility.HasRequiredWeapon(pawn, p.requiredWeapon))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                if (p.fieldDef == null || pawn.Map == null || !pawn.Spawned)
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                pawn.pather.StopDead();

                Thing thing = GenSpawn.Spawn(p.fieldDef, pawn.Position, pawn.Map);
                spawnedField = thing as Thing_YangChunField;
                if (spawnedField == null)
                {
                    thing.Destroy();
                    Log.Error("YangChun: spawned field is not Thing_YangChunField.");
                    EndJobWith(JobCondition.Errored);
                    return;
                }

                CompYangChunField fieldComp = spawnedField.TryGetComp<CompYangChunField>();
                if (fieldComp == null)
                {
                    spawnedField.Destroy();
                    Log.Error("YangChun: Cannot find CompYangChunField on spawned field.");
                    EndJobWith(JobCondition.Errored);
                    return;
                }

                fieldComp.Init(pawn);
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
                CompProperties_AbilityYangChun p = Props;
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
                spawnedField.Destroy();
            }

            spawnedField = null;
        }
    }
}

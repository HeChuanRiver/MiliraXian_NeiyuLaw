using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters
{
    public abstract class JobDriver_MX_ChannelAbility : JobDriver_CastAbility
    {
        protected Thing spawnedField;

        protected abstract ThingDef FieldDef { get; }
        protected abstract int DurationTicks { get; }

        protected virtual IntVec3 SpawnPosition => TargetA.Cell;
        protected virtual string ChannelReport => null;
        protected virtual bool ShowProgressBar => false;

        protected virtual bool ValidateSpawn(out JobCondition failCondition)
        {
            failCondition = JobCondition.Ongoing;
            return true;
        }

        protected virtual void OnFieldSpawned(Thing field)
        {
        }

        protected virtual void OnChannelTick()
        {
        }

        protected virtual bool CheckFailCondition()
        {
            return false;
        }

        protected virtual void OnCleanup()
        {
        }

        public override string GetReport()
        {
            return ChannelReport ?? base.GetReport();
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            foreach (var toil in base.MakeNewToils())
            {
                yield return toil;
            }

            AddFinishAction(delegate { CleanupField(); });

            var channel = ToilMaker.MakeToil(GetType().Name + "_Channel");

            channel.initAction = delegate
            {
                pawn.pather.StopDead();

                if (FieldDef == null)
                {
                    Log.Error($"{GetType().Name}: FieldDef is null");
                    EndJobWith(JobCondition.Errored);
                    return;
                }

                if (!ValidateSpawn(out var failCondition))
                {
                    EndJobWith(failCondition);
                    return;
                }

                var pos = SpawnPosition;
                if (!pos.IsValid || !pos.InBounds(pawn.Map))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                spawnedField = GenSpawn.Spawn(FieldDef, pos, pawn.Map);
                if (spawnedField == null)
                {
                    Log.Error($"{GetType().Name}: Failed to spawn field");
                    EndJobWith(JobCondition.Errored);
                    return;
                }

                OnFieldSpawned(spawnedField);
            };

            channel.tickAction = delegate
            {
                if (!pawn.Downed && !pawn.Dead)
                {
                    OnChannelTick();
                }
            };

            channel.tickIntervalAction = delegate
            {
                pawn.rotationTracker.FaceCell(SpawnPosition);
            };

            channel.defaultCompleteMode = ToilCompleteMode.Delay;
            channel.defaultDuration = Mathf.Max(1, DurationTicks);
            channel.handlingFacing = true;
            channel.AddFailCondition(CheckFailCondition);

            if (ShowProgressBar)
            {
                channel.WithProgressBarToilDelay(TargetIndex.None, false, -0.5f);
            }

            yield return channel;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref spawnedField, "spawnedField", false);
        }

        private void CleanupField()
        {
            OnCleanup();

            if (spawnedField != null && !spawnedField.Destroyed)
            {
                spawnedField.Destroy();
            }

            spawnedField = null;
        }
    }
}

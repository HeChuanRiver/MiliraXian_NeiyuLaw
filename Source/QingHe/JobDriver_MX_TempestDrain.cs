using System.Collections.Generic;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe
{
    public class JobDriver_MX_TempestDrain : JobDriver_MX_ChannelAbility
    {
        private CompProperties_AbilityTempestDrain Props =>
            job?.ability?.CompOfType<CompAbilityEffect_TempestDrain>()?.Props;

        protected override ThingDef FieldDef => Props?.fieldDef;
        protected override int DurationTicks => Props?.channelDurationTicks ?? 999999;

        protected override bool ValidateSpawn(out JobCondition failCondition)
        {
            if (Props?.fieldDef == null)
            {
                Log.Error("TempestDrain: Cannot find CompAbilityEffect_TempestDrain or Field Def");
                failCondition = JobCondition.Errored;
                return false;
            }

            var target = TargetA.Cell;
            if (!target.InBounds(pawn.Map))
            {
                failCondition = JobCondition.Incompletable;
                return false;
            }

            failCondition = JobCondition.Ongoing;
            return true;
        }

        protected override void OnFieldSpawned(Thing field)
        {
            field.TryGetComp<CompTempestDrainField>()?.Init(pawn);
        }

        protected override void OnChannelTick()
        {
            job.ability?.CompOfType<CompAbilityEffect_ChannelResource>()?.Tick(pawn);
        }

        protected override bool CheckFailCondition()
        {
            return job.ability?.CompOfType<CompAbilityEffect_ChannelResource>()?.CheckFailCondition(pawn) ?? false;
        }
    }
}

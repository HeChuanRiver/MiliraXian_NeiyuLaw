using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe
{
    public class JobDriver_MX_SpringFlow : JobDriver_MX_ChannelAbility
    {
        private CompProperties_AbilitySpringFlow Props =>
            job?.ability?.CompOfType<CompAbilityEffect_SpringFlow>()?.Props;

        protected override ThingDef FieldDef => Props?.fieldDef;
        protected override int DurationTicks => Props?.fieldDurationTicks ?? 1;
        protected override bool ShowProgressBar => true;

        protected override bool ValidateSpawn(out JobCondition failCondition)
        {
            if (Props?.fieldDef == null)
            {
                Log.Error("SpringFlow: Cannot find CompAbilityEffect_SpringFlow or Field Def");
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
            field.TryGetComp<CompSpringFlowField>()?.Init(pawn);
        }

        protected override void OnChannelTick()
        {
            job.ability?.CompOfType<CompAbilityEffect_ChannelResource>()?.Tick(pawn);
        }
    }
}

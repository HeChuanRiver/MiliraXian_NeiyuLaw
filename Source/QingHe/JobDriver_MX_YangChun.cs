using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe
{
    public class JobDriver_MX_YangChun : JobDriver_MX_ChannelAbility
    {
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

        protected override ThingDef FieldDef => Props?.fieldDef;
        protected override int DurationTicks => Props?.fieldDurationTicks ?? 1;
        protected override IntVec3 SpawnPosition => pawn.Position;
        protected override string ChannelReport => "MX_QH_ReportYangChun".Translate().ToString();

        protected override bool ValidateSpawn(out JobCondition failCondition)
        {
            if (Props == null)
            {
                failCondition = JobCondition.Errored;
                return false;
            }

            if (!MX_QHUtility.HasRequiredWeapon(pawn, Props.requiredWeapon)
                || Props.fieldDef == null
                || pawn.Map == null
                || !pawn.Spawned)
            {
                failCondition = JobCondition.Incompletable;
                return false;
            }

            failCondition = JobCondition.Ongoing;
            return true;
        }

        protected override void OnFieldSpawned(Thing field)
        {
            var comp = field.TryGetComp<CompYangChunField>();
            comp?.Init(pawn);
            comp?.SpawnFx();
        }

        protected override bool CheckFailCondition()
        {
            var p = Props;
            return p == null || !MX_QHUtility.HasRequiredWeapon(pawn, p.requiredWeapon);
        }

        protected override void OnCleanup()
        {
            spawnedField?.TryGetComp<CompYangChunField>()?.EndFx();
        }
    }
}

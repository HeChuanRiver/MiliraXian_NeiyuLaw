using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe
{
    public class JobDriver_MX_DuanHun : JobDriver_CastAbility
    {
        private CompProperties_AbilityDuanHun Props
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
                    CompProperties_AbilityDuanHun p = ability.def.comps[i] as CompProperties_AbilityDuanHun;
                    if (p != null)
                    {
                        return p;
                    }
                }

                return null;
            }
        }

        /// <summary>
        /// 返回该施法 Job 在检查面板中的行为描述。
        /// </summary>
        public override string GetReport()
        {
            return "正在引导断魂音律";
        }

        /// <summary>
        /// 复用原版施法流程，并在同一 Job 末尾追加断魂结算。
        /// </summary>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            foreach (Toil toil in base.MakeNewToils())
            {
                yield return toil;
            }

            CompProperties_AbilityDuanHun props = Props;
            int delay = props != null ? props.postCastDelayTicks : 0;
            if (delay > 0)
            {
                Toil wait = ToilMaker.MakeToil("QHEleganceDuanHun_PostCastDelay");
                wait.defaultCompleteMode = ToilCompleteMode.Delay;
                wait.defaultDuration = delay;
                yield return wait;
            }

            Toil pulse = ToilMaker.MakeToil("QHEleganceDuanHun_ResolvePulse");
            pulse.initAction = delegate()
            {
                CompProperties_AbilityDuanHun p = Props;
                if (p == null)
                {
                    return;
                }

                if (!MX_QHUtility.HasRequiredWeapon(pawn, p.requiredWeapon))
                {
                    return;
                }

                IntVec3 center = (job != null && job.targetA.IsValid) ? job.targetA.Cell : pawn.Position;
                MX_QHUtility.ExecuteDuanHunPulseByProps(pawn, p, center);
            };
            pulse.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return pulse;
        }
    }
}

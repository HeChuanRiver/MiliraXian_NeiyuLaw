using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe
{
    public class JobDriver_CastAbility_HengZhi : JobDriver_CastAbility
    {
        private CompProperties_AbilityHengZhi Props
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
                    CompProperties_AbilityHengZhi p = ability.def.comps[i] as CompProperties_AbilityHengZhi;
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
            return "正在引导横指冲击";
        }

        /// <summary>
        /// 复用原版施法流程，并在同一 Job 末尾追加横指结算。
        /// </summary>
        protected override IEnumerable<Toil> MakeNewToils()
        {
            foreach (Toil toil in base.MakeNewToils())
            {
                yield return toil;
            }

            CompProperties_AbilityHengZhi props = Props;
            int delay = props != null ? props.postCastDelayTicks : 0;
            if (delay > 0)
            {
                Toil wait = ToilMaker.MakeToil("QHEleganceHengZhi_PostCastDelay");
                wait.defaultCompleteMode = ToilCompleteMode.Delay;
                wait.defaultDuration = delay;
                yield return wait;
            }

            Toil pulse = ToilMaker.MakeToil("QHEleganceHengZhi_ResolvePulse");
            pulse.initAction = delegate()
            {
                CompProperties_AbilityHengZhi p = Props;
                if (p == null)
                {
                    return;
                }

                if (!MX_QHUtility.HasRequiredWeapon(pawn, p.requiredWeapon))
                {
                    return;
                }

                MX_QHUtility.ExecuteHengZhiPulseByProps(pawn, p);
            };
            pulse.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return pulse;
        }
    }
}

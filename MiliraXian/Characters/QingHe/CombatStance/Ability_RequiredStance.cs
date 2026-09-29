using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.CombatStance
{
    public class AbilityDef_RequiredStance : AbilityDef
    {
        public CombatStanceDef requiredStance;
    }

    public class Ability_RequiredStance : Ability
    {
        public Ability_RequiredStance()
        {
        }

        public Ability_RequiredStance(Pawn pawn, AbilityDef def)
            : base(pawn, def)
        {
        }

        private CombatStanceDef RequiredStance => ((AbilityDef_RequiredStance)def).requiredStance;

        private bool StanceMet
        {
            get
            {
                CombatStanceDef required = RequiredStance;
                return required == null
                    || MX_QH_HediffUtility.GetCombatStance(pawn)?.CurrentStance == required;
            }
        }

        public override AcceptanceReport CanCast => StanceMet && base.CanCast;

        public override bool GizmoDisabled(out string reason)
        {
            if (!StanceMet)
            {
                reason = "MX_QH_RequiresStance".Translate(RequiredStance?.label ?? "");
                return true;
            }

            return base.GizmoDisabled(out reason);
        }

        public override IEnumerable<Command> GetGizmos()
        {
            if (StanceMet)
            {
                foreach (Command command in base.GetGizmos())
                {
                    yield return command;
                }
            }
        }
    }
}

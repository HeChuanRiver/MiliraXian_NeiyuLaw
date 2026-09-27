using MiliraXian.Characters.QingHe.Things.Weapons;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities
{
    public class CompProperties_AbilityQingheWeaponMode : CompProperties_AbilityEffect
    {
        public bool requireSword;

        public CompProperties_AbilityQingheWeaponMode()
        {
            compClass = typeof(CompAbilityEffect_QingheWeaponMode);
        }
    }

    public class CompAbilityEffect_QingheWeaponMode : CompAbilityEffect
    {
        public new CompProperties_AbilityQingheWeaponMode Props => (CompProperties_AbilityQingheWeaponMode)props;

        private bool HasRequiredMode => Props.requireSword
            ? QingheSwordCombatUtility.IsSwordMode(parent?.pawn)
            : QingheSwordCombatUtility.IsBellMode(parent?.pawn);

        public override bool CanCast => HasRequiredMode;

        public override bool ShouldHideGizmo => !HasRequiredMode;

        public override bool GizmoDisabled(out string reason)
        {
            if (!HasRequiredMode)
            {
                reason = Props.requireSword
                    ? "MX_QH_RequiresSwordMode".Translate().ToString()
                    : "MX_QH_RequiresBellMode".Translate().ToString();
                return true;
            }

            return base.GizmoDisabled(out reason);
        }
    }
}

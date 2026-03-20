
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_QingheWeaponAbilityGrant : CompProperties
    {
        public AbilityDef abilityDef;

        public CompProperties_QingheWeaponAbilityGrant()
        {
            compClass = typeof(Comp_QingheWeaponAbilityGrant);
        }
    }

    public class Comp_QingheWeaponAbilityGrant : ThingComp
    {
        private CompProperties_QingheWeaponAbilityGrant Props => props as CompProperties_QingheWeaponAbilityGrant;

        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            TryGrantAbility(pawn);
        }

        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            TryRemoveAbility(pawn);
        }

        private void TryGrantAbility(Pawn pawn)
        {
            AbilityDef abilityDef = Props?.abilityDef;
            if (abilityDef == null || pawn?.abilities == null)
            {
                return;
            }

            if (!MX_QHUtility.IsQinghe(pawn))
            {
                return;
            }

            ThingWithComps primary = pawn.equipment?.Primary;
            if (primary != parent)
            {
                return;
            }

            if (pawn.abilities.GetAbility(abilityDef, includeTemporary: false) == null)
            {
                pawn.abilities.GainAbility(abilityDef);
            }
        }

        private void TryRemoveAbility(Pawn pawn)
        {
            AbilityDef abilityDef = Props?.abilityDef;
            if (abilityDef == null || pawn?.abilities == null)
            {
                return;
            }

            pawn.abilities.RemoveAbility(abilityDef);
        }
    }
}


using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_WeaponAbility : CompProperties
    {
        public AbilityDef abilityDef;

        public CompProperties_WeaponAbility()
        {
            compClass = typeof(CompWeaponAbility);
        }
    }

    public class CompWeaponAbility : ThingComp
    {
        private CompProperties_WeaponAbility Props => props as CompProperties_WeaponAbility;

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

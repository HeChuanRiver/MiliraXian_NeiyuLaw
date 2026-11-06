using System.Collections.Generic;
using System.Linq;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Common
{
    public sealed class CompProperties_WeaponDropProtection : CompProperties
    {
        public CompProperties_WeaponDropProtection() => compClass = typeof(CompWeaponDropProtection);

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
                yield return error;

            if (!parentDef.IsWeapon || parentDef.equipmentType != EquipmentType.Primary
                || parentDef.stackLimit != 1
                || !parentDef.comps.Any(comp => typeof(CompEquippable).IsAssignableFrom(comp.compClass)))
                yield return "Weapon drop protection requires a non-stackable primary weapon with CompEquippable.";
        }
    }

    public sealed class CompWeaponDropProtection : ThingComp
    {
        private Pawn boundOwner;

        internal Pawn BoundOwner
        {
            get
            {
                // Older saves have no binding. Only a real equipment holder proves prior use.
                if (boundOwner == null && parent.ParentHolder is Pawn_EquipmentTracker equipment)
                    BindIfEquipped(equipment.pawn);
                return boundOwner;
            }
        }

        internal bool Transferring { get; set; }

        public override void PostExposeData()
        {
            if (Scribe.mode == LoadSaveMode.Saving)
                _ = BoundOwner;

            Scribe_References.Look(ref boundOwner, "mx_weaponBoundOwner");
        }

        public override void Notify_Equipped(Pawn pawn) => BindIfEquipped(pawn);

        internal void InheritFormOwner(ThingWithComps source, Pawn pawn)
        {
            if (source.TryGetComp<CompWeaponDropProtection>()?.BoundOwner == pawn)
                boundOwner = pawn;
        }

        private void BindIfEquipped(Pawn pawn)
        {
            if (pawn?.equipment is { } equipment && parent.ParentHolder == equipment && equipment.Contains(parent)
                && parent.TryGetComp<CompPawnKindRestrictedWeapon>()?.CanEquip(pawn) != false)
                boundOwner = pawn;
        }
    }
}

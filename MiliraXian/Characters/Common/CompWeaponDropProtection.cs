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

        internal Pawn RecordedOwner => boundOwner;

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
            if (Scribe.mode == LoadSaveMode.PostLoadInit && Prefs.DevMode)
                WeaponDropDiagnostics.Write("Comp.Loaded", parent, "");
        }

        public override void Notify_Equipped(Pawn pawn) => BindIfEquipped(pawn);

        public override void Notify_Unequipped(Pawn pawn)
        {
            if (Prefs.DevMode)
                WeaponDropDiagnostics.Write("Comp.Unequipped", parent, "", pawn, includeStack: true);
        }

        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            if (Prefs.DevMode)
                WeaponDropDiagnostics.Write("Comp.Spawned", parent,
                    $"loaded={respawningAfterLoad} cell={parent.Position} map={parent.Map?.uniqueID}",
                    includeStack: !respawningAfterLoad);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            if (Prefs.DevMode)
                WeaponDropDiagnostics.Write("Comp.Destroyed", parent, $"mode={mode} previousMap={previousMap?.uniqueID}",
                    includeStack: true);
        }

        internal void InheritFormOwner(ThingWithComps source, Pawn pawn)
        {
            if (source.TryGetComp<CompWeaponDropProtection>()?.BoundOwner == pawn)
                boundOwner = pawn;

            if (Prefs.DevMode)
                WeaponDropDiagnostics.Write("Comp.InheritFormOwner", parent,
                    $"previousForm={WeaponDropDiagnostics.ThingId(source)} inherited={boundOwner == pawn}", pawn);
        }

        private void BindIfEquipped(Pawn pawn)
        {
            Pawn previous = boundOwner;
            bool held = pawn?.equipment is { } equipment && parent.ParentHolder == equipment && equipment.Contains(parent);
            bool accepted = held && parent.TryGetComp<CompPawnKindRestrictedWeapon>()?.CanEquip(pawn) != false;
            if (accepted)
                boundOwner = pawn;

            if (Prefs.DevMode)
                WeaponDropDiagnostics.Write("Comp.Bind", parent,
                    $"previousOwner={WeaponDropDiagnostics.ThingId(previous)} held={held} accepted={accepted}", pawn);
        }
    }
}

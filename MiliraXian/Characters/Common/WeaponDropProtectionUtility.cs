using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common
{
    internal static class WeaponDropProtectionUtility
    {
        internal static bool IsStoredWeapon(Thing weapon, Pawn pawn, bool allowUnbound = false)
        {
            if (GetProtection(weapon) is not { } protection || pawn?.inventory is not { } inventory
                || pawn.Discarded || weapon.ParentHolder != inventory || inventory.innerContainer?.Contains(weapon) != true
                || !weapon.HasComp<CompEquippable>())
                return false;

            Pawn owner = protection.BoundOwner;
            return owner == pawn || (allowUnbound && owner == null);
        }

        internal static bool ShouldCancelGroundDrop(Thing thing, bool trace)
        {
            if (thing is not ThingWithComps weapon || GetProtection(weapon) is not { } protection)
            {
                if (trace)
                    WeaponDropDiagnostics.Write("Protection.Drop.pass", thing,
                        $"reason=ineligible-weapon equipmentType={thing.def.equipmentType} stack={thing.stackCount}/{thing.def.stackLimit}");
                return false;
            }

            // Keep weapons in inventory when DropAndForbidEverything drops equipment and then inventory.
            if (weapon.ParentHolder is Pawn_InventoryTracker inventory)
            {
                bool stored = IsStoredWeapon(weapon, inventory.pawn);
                if (trace)
                    WeaponDropDiagnostics.Write("Protection.Drop.inventory", weapon,
                        $"protectedInInventory={stored} contains={inventory.innerContainer?.Contains(weapon)} "
                        + $"discarded={inventory.pawn.Discarded}", inventory.pawn);
                return stored;
            }

            if (weapon.ParentHolder is not Pawn_EquipmentTracker equipment)
            {
                if (trace)
                    WeaponDropDiagnostics.Write("Protection.Drop.pass", weapon, "reason=source-is-not-pawn-equipment-or-inventory",
                        includeStack: true);
                return false;
            }

            bool stowed = TryRedirectWeaponDrop(equipment, weapon, out bool protectedDrop);
            bool cancel = stowed
                || (protectedDrop && (weapon.ParentHolder != equipment || protection.Transferring || weapon.def.destroyOnDrop));
            if (trace)
                WeaponDropDiagnostics.Write("Protection.Drop.equipment", weapon,
                    $"eligible={protectedDrop} stowed={stowed} cancel={cancel} destroyOnDrop={weapon.def.destroyOnDrop}",
                    equipment.pawn);
            return cancel;
        }

        internal static bool TryRedirectWeaponDrop(Pawn_EquipmentTracker equipment, ThingWithComps weapon, out bool protectedDrop)
        {
            Pawn pawn = equipment.pawn;
            CompWeaponDropProtection protection = GetProtection(weapon);
            bool held = protection != null && weapon.ParentHolder == equipment && equipment.Contains(weapon);
            bool boundToHolder = held && protection.BoundOwner == pawn;
            protectedDrop = boundToHolder && !pawn.Discarded
                && weapon.TryGetComp<CompPawnKindRestrictedWeapon>()?.CanEquip(pawn) != false;
            if (WeaponDropDiagnostics.ShouldTrace(weapon, pawn))
                WeaponDropDiagnostics.Write("Protection.Redirect.check", weapon,
                    $"eligible={protectedDrop} supported={protection != null} inEquipment={held} "
                    + $"boundToHolder={boundToHolder} discarded={pawn.Discarded} "
                    + $"inventory={WeaponDropDiagnostics.HolderId(pawn.inventory)}", pawn);
            if (!protectedDrop || protection.Transferring)
                return false;

            protection.Transferring = true;
            try
            {
                if (TryStow(equipment, weapon, pawn.inventory?.innerContainer))
                {
                    if (pawn.mindState?.droppedWeapon == weapon)
                        pawn.mindState.droppedWeapon = null;
                    return true;
                }

                Log.WarningOnce($"[MiliraXian] Could not stow protected weapon {weapon} for {pawn}; "
                    + "the original drop may proceed if it can preserve the weapon.",
                    Gen.HashCombineInt(weapon.thingIDNumber, 0x4D585744));
                return false;
            }
            finally
            {
                protection.Transferring = false;
            }
        }

        internal static string EquipDisabledReason(Pawn pawn, ThingWithComps weapon, bool allowUnbound = false)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.Downed || pawn.Faction != Faction.OfPlayer
                || pawn.equipment == null || pawn.mindState == null || pawn.InMentalState
                || pawn.stances?.stunner?.Stunned == true || pawn.jobs?.curDriver?.PlayerInterruptable == false
                || pawn.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation) != true)
                return Text("Unavailable", "当前无法装备武器。");

            if (!IsStoredWeapon(weapon, pawn, allowUnbound) || GetProtection(weapon).Transferring)
                return Text("Missing", "武器已无法从背包装备。");

            ThingWithComps primary = pawn.equipment.Primary;
            if (pawn.CurJobDef == JobDefOf.Hunt
                || (primary != null && pawn.stances?.curStance is Stance_Warmup warmup
                    && warmup.verb?.EquipmentSource == primary))
                return Text("Busy", "等当前狩猎或武器蓄力结束后再换装。");

            if (pawn.WorkTagIsDisabled(WorkTags.Violent)
                || (weapon.def.IsRangedWeapon && pawn.WorkTagIsDisabled(WorkTags.Shooting)))
                return "Incapable".Translate().ToString();

            if (pawn.IsQuestLodger() && !EquipmentUtility.QuestLodgerCanEquip(weapon, pawn))
                return "QuestRelated".Translate().ToString();

            if (!EquipmentUtility.CanEquip(weapon, pawn, out string cantReason))
                return cantReason.NullOrEmpty() ? Text("Unavailable", "当前无法装备武器。") : cantReason;

            if (primary != null && !CanStow(pawn.inventory.innerContainer, primary))
                return Text("InventoryFull", "背包无法收纳当前武器。");

            return null;
        }

        internal static void EquipOrReject(Pawn pawn, ThingWithComps weapon, bool allowUnbound = false)
        {
            string reason = EquipDisabledReason(pawn, weapon, allowUnbound);
            bool trace = WeaponDropDiagnostics.ShouldTrace(weapon, pawn);
            if (trace)
                WeaponDropDiagnostics.Write("Protection.Reequip.begin", weapon,
                    $"current={WeaponDropDiagnostics.ThingId(pawn?.equipment?.Primary)} allowUnbound={allowUnbound} "
                    + $"blocked={reason ?? "no"}", pawn);
            bool equipped = reason == null && TryEquip(pawn, weapon, allowUnbound);
            if (trace)
                WeaponDropDiagnostics.Write("Protection.Reequip.end", weapon,
                    $"equipped={equipped} current={WeaponDropDiagnostics.ThingId(pawn?.equipment?.Primary)}", pawn);
            if (equipped)
                return;

            Messages.Message(reason ?? Text("SwapFailed", "未能完成换装。"), pawn,
                MessageTypeDefOf.RejectInput, historical: false);
        }

        private static bool TryEquip(Pawn pawn, ThingWithComps weapon, bool allowUnbound)
        {
            Pawn_EquipmentTracker equipment = pawn.equipment;
            ThingOwner inventory = pawn.inventory.innerContainer;
            ThingWithComps previous = equipment.Primary;
            CompWeaponDropProtection protection = GetProtection(weapon);
            CompWeaponDropProtection previousProtection = GetProtection(previous);
            if (previousProtection?.Transferring == true)
                return false;

            protection.Transferring = true;
            if (previousProtection != null)
                previousProtection.Transferring = true;

            try
            {
                if (previous != null && !TryStow(equipment, previous, inventory))
                    return false;

                return pawn.equipment == equipment && pawn.inventory?.innerContainer == inventory
                    && IsStoredWeapon(weapon, pawn, allowUnbound) && TryEquipFromInventory(equipment, inventory, weapon);
            }
            finally
            {
                try
                {
                    if (equipment.Primary == null && previous != null && inventory.Contains(previous))
                    {
                        if (WeaponDropDiagnostics.ShouldTrace(previous, pawn))
                            WeaponDropDiagnostics.Write("Protection.Reequip.restorePrevious", previous,
                                $"failedTarget={WeaponDropDiagnostics.ThingId(weapon)}", pawn);
                        TryEquipFromInventory(equipment, inventory, previous);
                    }
                }
                finally
                {
                    protection.Transferring = false;
                    if (previousProtection != null)
                        previousProtection.Transferring = false;
                }
            }
        }

        private static bool TryEquipFromInventory(Pawn_EquipmentTracker equipment, ThingOwner inventory, ThingWithComps weapon)
        {
            bool trace = WeaponDropDiagnostics.ShouldTrace(weapon, equipment.pawn);
            try
            {
                if (equipment.Primary == null && inventory.Remove(weapon) && IsLoose(weapon))
                {
                    if (trace)
                        WeaponDropDiagnostics.Write("Protection.EquipFromInventory.add", weapon, "", equipment.pawn);
                    equipment.AddEquipment(weapon);
                }
            }
            catch (Exception exception)
            {
                Log.Error($"[MiliraXian] Equipping {weapon} on {equipment.pawn} raised an exception: {exception}");
            }

            RestoreIfLoose(weapon, inventory, equipment.pawn);
            bool equipped = equipment.Primary == weapon;
            if (trace)
                WeaponDropDiagnostics.Write("Protection.EquipFromInventory.end", weapon, $"equipped={equipped}", equipment.pawn);
            return equipped;
        }

        private static bool TryStow(Pawn_EquipmentTracker equipment, ThingWithComps weapon, ThingOwner inventory)
        {
            bool trace = WeaponDropDiagnostics.ShouldTrace(weapon, equipment.pawn);
            int capacity = StowCapacity(inventory, weapon);
            if (trace)
                WeaponDropDiagnostics.Write("Protection.Stow.check", weapon,
                    $"hasInventory={inventory != null} acceptable={capacity} required={weapon.stackCount}", equipment.pawn);
            if (inventory == null || capacity < weapon.stackCount)
                return false;

            ThingOwner source = equipment.GetDirectlyHeldThings();
            try
            {
                bool transferred = source.TryTransferToContainer(weapon, inventory, canMergeWithExistingStacks: false);
                if (trace)
                    WeaponDropDiagnostics.Write("Protection.Stow.transfer", weapon, $"apiResult={transferred}", equipment.pawn);
            }
            catch (Exception exception)
            {
                Log.Error($"[MiliraXian] Stowing {weapon} for {equipment.pawn} raised an exception: {exception}");
            }

            RestoreIfLoose(weapon, equipment.Primary == null ? source : inventory, equipment.pawn);
            bool stowed = inventory.Contains(weapon);
            if (trace)
                WeaponDropDiagnostics.Write("Protection.Stow.end", weapon, $"inInventory={stowed}", equipment.pawn);
            return stowed;
        }

        private static bool CanStow(ThingOwner inventory, Thing weapon) =>
            inventory != null && StowCapacity(inventory, weapon) >= weapon.stackCount;

        private static int StowCapacity(ThingOwner inventory, Thing weapon) =>
            inventory?.GetCountCanAccept(weapon, canMergeWithExistingStacks: false) ?? 0;

        private static bool IsLoose(Thing thing) => !thing.Destroyed && !thing.Spawned && thing.ParentHolder == null;

        internal static void RestoreIfLoose(Thing weapon, ThingOwner container, Pawn pawn)
        {
            if (!IsLoose(weapon))
                return;

            bool trace = WeaponDropDiagnostics.ShouldTrace(weapon, pawn);
            TryRestoreToContainer(weapon, container, pawn, trace);
            ThingOwner inventory = pawn.inventory?.innerContainer;
            if (IsLoose(weapon) && inventory != container)
                TryRestoreToContainer(weapon, inventory, pawn, trace);

            if (IsLoose(weapon))
            {
                // A container callback can veto both the original operation and its rollback.
                bool placed = pawn.MapHeld is { } map && pawn.PositionHeld.InBounds(map)
                    && GenPlace.TryPlaceThing(weapon, pawn.PositionHeld, map, ThingPlaceMode.Near);
                if (trace)
                    WeaponDropDiagnostics.Write("Protection.Restore.ground", weapon, $"placed={placed}", pawn);
                Log.Error($"[MiliraXian] Could not restore {weapon} to {pawn}'s container; "
                    + (placed ? "the original instance was placed nearby." : "no safe map placement was available."));
            }
        }

        private static void TryRestoreToContainer(Thing weapon, ThingOwner container, Pawn pawn, bool trace)
        {
            if (container == null || (container.Owner is Pawn_EquipmentTracker equipment
                && weapon.def.equipmentType == EquipmentType.Primary && equipment.Primary != null))
            {
                if (trace)
                    WeaponDropDiagnostics.Write("Protection.Restore.skip", weapon,
                        $"destination={WeaponDropDiagnostics.HolderId(container?.Owner)}", pawn);
                return;
            }

            try
            {
                bool restored = container.TryAdd(weapon, canMergeWithExistingStacks: false);
                if (trace)
                    WeaponDropDiagnostics.Write("Protection.Restore.container", weapon,
                        $"destination={WeaponDropDiagnostics.HolderId(container.Owner)} result={restored}", pawn);
            }
            catch (Exception exception)
            {
                Log.Error($"[MiliraXian] Restoring {weapon} to {container.Owner} raised an exception: {exception}");
            }
        }

        private static CompWeaponDropProtection GetProtection(Thing weapon) =>
            weapon is { Destroyed: false, stackCount: 1 } && weapon.def.IsWeapon
                && weapon.def.equipmentType == EquipmentType.Primary && weapon.def.stackLimit == 1
                ? weapon.TryGetComp<CompWeaponDropProtection>()
                : null;

        internal static string Text(string suffix, string source)
        {
            string key = "MX_WeaponDropProtection_" + suffix;
            return key.CanTranslate() ? key.Translate().ToString() : source;
        }

        internal static Texture2D Icon(Thing weapon) =>
            weapon.def.uiIcon != null && weapon.def.uiIcon != BaseContent.BadTex ? weapon.def.uiIcon : TexCommand.Attack;
    }
}

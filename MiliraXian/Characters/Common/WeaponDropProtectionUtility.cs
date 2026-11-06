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

        internal static bool ShouldCancelGroundDrop(Thing thing)
        {
            if (thing is not ThingWithComps weapon || GetProtection(weapon) is not { } protection)
                return false;

            // Keep weapons in inventory when DropAndForbidEverything drops equipment and then inventory.
            if (weapon.ParentHolder is Pawn_InventoryTracker inventory)
                return IsStoredWeapon(weapon, inventory.pawn);

            if (weapon.ParentHolder is not Pawn_EquipmentTracker equipment)
                return false;

            return TryRedirectWeaponDrop(equipment, weapon, out bool protectedDrop)
                || (protectedDrop && (weapon.ParentHolder != equipment || protection.Transferring || weapon.def.destroyOnDrop));
        }

        internal static bool TryRedirectWeaponDrop(Pawn_EquipmentTracker equipment, ThingWithComps weapon, out bool protectedDrop)
        {
            Pawn pawn = equipment.pawn;
            CompWeaponDropProtection protection = GetProtection(weapon);
            protectedDrop = protection != null && weapon.ParentHolder == equipment && equipment.Contains(weapon)
                && protection.BoundOwner == pawn && !pawn.Discarded
                && weapon.TryGetComp<CompPawnKindRestrictedWeapon>()?.CanEquip(pawn) != false;
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
            if (reason == null && TryEquip(pawn, weapon, allowUnbound))
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
                        TryEquipFromInventory(equipment, inventory, previous);
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
            try
            {
                if (equipment.Primary == null && inventory.Remove(weapon) && IsLoose(weapon))
                    equipment.AddEquipment(weapon);
            }
            catch (Exception exception)
            {
                Log.Error($"[MiliraXian] Equipping {weapon} on {equipment.pawn} raised an exception: {exception}");
            }

            RestoreIfLoose(weapon, inventory, equipment.pawn);
            return equipment.Primary == weapon;
        }

        private static bool TryStow(Pawn_EquipmentTracker equipment, ThingWithComps weapon, ThingOwner inventory)
        {
            if (!CanStow(inventory, weapon))
                return false;

            ThingOwner source = equipment.GetDirectlyHeldThings();
            try
            {
                source.TryTransferToContainer(weapon, inventory, canMergeWithExistingStacks: false);
            }
            catch (Exception exception)
            {
                Log.Error($"[MiliraXian] Stowing {weapon} for {equipment.pawn} raised an exception: {exception}");
            }

            RestoreIfLoose(weapon, equipment.Primary == null ? source : inventory, equipment.pawn);
            return inventory.Contains(weapon);
        }

        private static bool CanStow(ThingOwner inventory, Thing weapon) =>
            inventory != null && inventory.GetCountCanAccept(weapon, canMergeWithExistingStacks: false) >= weapon.stackCount;

        private static bool IsLoose(Thing thing) => !thing.Destroyed && !thing.Spawned && thing.ParentHolder == null;

        internal static void RestoreIfLoose(Thing weapon, ThingOwner container, Pawn pawn)
        {
            if (!IsLoose(weapon))
                return;

            TryRestoreToContainer(weapon, container);
            ThingOwner inventory = pawn.inventory?.innerContainer;
            if (IsLoose(weapon) && inventory != container)
                TryRestoreToContainer(weapon, inventory);

            if (IsLoose(weapon))
            {
                // A container callback can veto both the original operation and its rollback.
                bool placed = pawn.MapHeld is { } map && pawn.PositionHeld.InBounds(map)
                    && GenPlace.TryPlaceThing(weapon, pawn.PositionHeld, map, ThingPlaceMode.Near);
                Log.Error($"[MiliraXian] Could not restore {weapon} to {pawn}'s container; "
                    + (placed ? "the original instance was placed nearby." : "no safe map placement was available."));
            }
        }

        private static void TryRestoreToContainer(Thing weapon, ThingOwner container)
        {
            if (container == null || (container.Owner is Pawn_EquipmentTracker equipment
                && weapon.def.equipmentType == EquipmentType.Primary && equipment.Primary != null))
                return;

            try
            {
                container.TryAdd(weapon, canMergeWithExistingStacks: false);
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

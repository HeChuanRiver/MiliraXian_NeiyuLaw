using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common
{
    [HarmonyPatch(typeof(GenDrop), nameof(GenDrop.TryDropSpawn))]
    internal static class Patch_GenDrop_ProtectedWeapons
    {
        private static bool Prefix(Thing thing, ref Thing resultingThing, ref bool __result)
        {
            if (!WeaponDropProtectionUtility.ShouldCancelGroundDrop(thing))
                return true;

            resultingThing = null;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.MakeRoomFor),
        new[] { typeof(ThingWithComps), typeof(ThingWithComps) },
        new[] { ArgumentType.Normal, ArgumentType.Out })]
    internal static class Patch_MakeRoomFor_ProtectedWeapons
    {
        private static bool Prefix(Pawn_EquipmentTracker __instance, ThingWithComps eq, ref ThingWithComps dropped)
        {
            if (eq.def.equipmentType != EquipmentType.Primary || __instance.Primary is not { } primary
                || !WeaponDropProtectionUtility.TryRedirectWeaponDrop(__instance, primary, out _))
                return true;

            dropped = null;
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.FirstUnloadableThing), MethodType.Getter)]
    internal static class Patch_FirstUnloadableThing_ProtectedWeapons
    {
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            // Skip the item inside the vanilla loop, retaining its drug, stock and food policy for all other items.
            var moveNext = AccessTools.Method(typeof(List<Thing>.Enumerator), nameof(List<Thing>.Enumerator.MoveNext));
            var moveNextUnloadable = AccessTools.Method(typeof(Patch_FirstUnloadableThing_ProtectedWeapons), nameof(MoveNextUnloadable));
            bool replaced = false;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(moveNext))
                {
                    instruction.operand = moveNextUnloadable;
                    replaced = true;
                }
                yield return instruction;
            }

            if (!replaced)
                Log.Warning("[MiliraXian] Could not find the inventory unload loop for weapon protection.");
        }

        private static bool MoveNextUnloadable(ref List<Thing>.Enumerator items)
        {
            while (items.MoveNext())
            {
                Thing item = items.Current;
                if (item.ParentHolder is not Pawn_InventoryTracker inventory
                    || !WeaponDropProtectionUtility.IsStoredWeapon(item, inventory.pawn))
                    return true;
            }
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    internal static class Patch_Pawn_GetGizmos_ProtectedWeapons
    {
        private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo gizmo in __result)
                yield return gizmo;

            Pawn pawn = __instance;
            if (pawn.Faction != Faction.OfPlayer || pawn.inventory?.innerContainer is not { } inventory)
                yield break;

            ThingWithComps first = null;
            bool multiple = false;
            foreach (Thing item in inventory)
            {
                if (item is not ThingWithComps weapon || !WeaponDropProtectionUtility.IsStoredWeapon(weapon, pawn))
                    continue;
                if (first != null)
                {
                    multiple = true;
                    break;
                }
                first = weapon;
            }

            if (first == null)
                yield break;

            var command = new Command_Action
            {
                defaultLabel = WeaponDropProtectionUtility.Text("Equip", "装备武器"),
                defaultDesc = WeaponDropProtectionUtility.Text("EquipDesc", "从背包取出武器装备到主手。当前武器会收进背包。"),
                icon = WeaponDropProtectionUtility.Icon(first),
                groupable = false,
                action = multiple ? () => OpenWeaponMenu(pawn) : () => WeaponDropProtectionUtility.EquipOrReject(pawn, first)
            };

            if (WeaponDropProtectionUtility.EquipDisabledReason(pawn, first) is { } firstReason)
            {
                // Another weapon in the menu may be usable even if the first is restricted.
                if (!multiple || !HasUsableWeapon(pawn, inventory))
                    command.Disable(firstReason);
            }

            yield return command;
        }

        private static bool HasUsableWeapon(Pawn pawn, ThingOwner inventory)
        {
            foreach (Thing item in inventory)
            {
                if (item is ThingWithComps weapon && WeaponDropProtectionUtility.IsStoredWeapon(weapon, pawn)
                    && WeaponDropProtectionUtility.EquipDisabledReason(pawn, weapon) == null)
                    return true;
            }
            return false;
        }

        private static void OpenWeaponMenu(Pawn pawn)
        {
            if (pawn.inventory?.innerContainer is not { } inventory)
                return;

            List<FloatMenuOption> options = new();
            foreach (Thing item in inventory)
            {
                if (item is not ThingWithComps weapon || !WeaponDropProtectionUtility.IsStoredWeapon(weapon, pawn))
                    continue;

                string reason = WeaponDropProtectionUtility.EquipDisabledReason(pawn, weapon);
                string label = $"{weapon.LabelCap} · {WeaponDropProtectionUtility.Text("Stored", "背包")}";
                if (reason != null)
                    label += $" ({reason})";
                options.Add(new FloatMenuOption(label,
                    reason == null ? () => WeaponDropProtectionUtility.EquipOrReject(pawn, weapon) : null,
                    WeaponDropProtectionUtility.Icon(weapon), Color.white));
            }

            if (options.Count > 0)
                Find.WindowStack.Add(new FloatMenu(options));
        }
    }
}

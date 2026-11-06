using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Common
{
    internal static class WeaponDropDiagnostics
    {
        internal const string Tag = "[MXWeaponDropDebug]";

        internal static bool ShouldTrace(Thing weapon, Pawn pawn = null)
        {
            if (!Prefs.DevMode || weapon == null)
                return false;

            pawn ??= HolderPawn(weapon.ParentHolder);
            // Include player weapons without the comp so a missing XML mount is visible.
            return weapon.HasComp<CompWeaponDropProtection>()
                || (pawn?.Faction?.IsPlayer == true && weapon.def.IsWeapon);
        }

        internal static void Write(string stage, Thing weapon, string detail, Pawn pawn = null, bool includeStack = false)
        {
            IThingHolder holder = weapon.ParentHolder;
            var protection = weapon.TryGetComp<CompWeaponDropProtection>();
            pawn ??= HolderPawn(holder);
            string stack = includeStack ? "\n" + new StackTrace(1, false) : "";
            Log.Message($"{Tag} {stage} weapon={ThingId(weapon)} pawn={ThingId(pawn)} "
                + $"holder={HolderId(holder)} comp={protection != null} owner={ThingId(protection?.RecordedOwner)} "
                + $"transferring={protection?.Transferring == true} spawned={weapon.Spawned} destroyed={weapon.Destroyed} "
                + $"{detail}{stack}");
        }

        internal static string ThingId(Thing thing) => thing == null ? "none" : $"{thing.def.defName}#{thing.thingIDNumber}";

        internal static string HolderId(IThingHolder holder) =>
            holder == null ? "none" : $"{holder.GetType().Name}/{ThingId(HolderPawn(holder) ?? holder as Thing)}";

        internal static Pawn HolderPawn(IThingHolder holder) => holder switch
        {
            Pawn_EquipmentTracker equipment => equipment.pawn,
            Pawn_InventoryTracker inventory => inventory.pawn,
            Pawn_CarryTracker carry => carry.pawn,
            Pawn pawn => pawn,
            _ => null
        };

        internal static string HealthState(Pawn pawn) =>
            $"manipulation={pawn.health?.capacities?.GetLevel(PawnCapacityDefOf.Manipulation):0.###} "
            + $"canManipulate={pawn.health?.capacities?.CapableOf(PawnCapacityDefOf.Manipulation)} "
            + $"downed={pawn.Downed} dead={pawn.Dead} destroyGearOnDrop={pawn.kindDef?.destroyGearOnDrop} "
            + $"enclosed={pawn.InContainerEnclosed} map={pawn.MapHeld?.uniqueID}";
    }

    // These observers record the game's decisions before the item reaches the protection prefix.
    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.CheckForStateChange))]
    internal static class Patch_WeaponDropTrace_Health
    {
        private static void Prefix(Pawn ___pawn, DamageInfo? dinfo, Hediff hediff, out ThingWithComps __state)
        {
            __state = null;
            ThingWithComps weapon = ___pawn.equipment?.Primary;
            if (!WeaponDropDiagnostics.ShouldTrace(weapon, ___pawn)
                || (!dinfo.HasValue && hediff?.Part == null
                    && ___pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)))
                return;

            __state = weapon;
            WeaponDropDiagnostics.Write("Health.Check.begin", weapon,
                $"hediff={hediff?.def?.defName} part={hediff?.Part?.def?.defName ?? dinfo?.HitPart?.def?.defName} "
                + $"damage={dinfo?.Def?.defName} {WeaponDropDiagnostics.HealthState(___pawn)}", ___pawn);
        }

        private static void Postfix(Pawn ___pawn, ThingWithComps __state)
        {
            if (__state != null)
                WeaponDropDiagnostics.Write("Health.Check.end", __state,
                    $"primary={WeaponDropDiagnostics.ThingId(___pawn.equipment?.Primary)} "
                    + WeaponDropDiagnostics.HealthState(___pawn), ___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DropAndForbidEverything))]
    internal static class Patch_WeaponDropTrace_BulkDrop
    {
        private static void Prefix(Pawn __instance, bool keepInventoryAndEquipmentIfInBed, bool rememberPrimary,
            out ThingWithComps __state)
        {
            ThingWithComps weapon = __instance.equipment?.Primary;
            __state = WeaponDropDiagnostics.ShouldTrace(weapon, __instance) ? weapon : null;
            if (__state != null)
                WeaponDropDiagnostics.Write("Pawn.DropEverything.begin", weapon,
                    $"keepIfInBed={keepInventoryAndEquipmentIfInBed} inBed={__instance.InBed()} "
                    + $"rememberPrimary={rememberPrimary} {WeaponDropDiagnostics.HealthState(__instance)}", __instance);
        }

        private static void Postfix(Pawn __instance, ThingWithComps __state)
        {
            if (__state != null)
                WeaponDropDiagnostics.Write("Pawn.DropEverything.end", __state,
                    $"remembered={WeaponDropDiagnostics.ThingId(__instance.mindState?.droppedWeapon)}", __instance);
        }
    }

    [HarmonyPatch(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.TryDropEquipment))]
    internal static class Patch_WeaponDropTrace_EquipmentDrop
    {
        private static void Prefix(Pawn_EquipmentTracker __instance, ThingWithComps eq, IntVec3 pos, bool forbid, out bool __state)
        {
            __state = WeaponDropDiagnostics.ShouldTrace(eq, __instance.pawn);
            if (__state)
                WeaponDropDiagnostics.Write("Equipment.TryDrop.begin", eq,
                    $"cell={pos} forbid={forbid} {WeaponDropDiagnostics.HealthState(__instance.pawn)}", __instance.pawn);
        }

        private static void Postfix(Pawn_EquipmentTracker __instance, ThingWithComps eq, ref ThingWithComps resultingEq,
            bool __result, bool __state)
        {
            if (__state)
                WeaponDropDiagnostics.Write("Equipment.TryDrop.end", eq,
                    $"result={__result} ground={WeaponDropDiagnostics.ThingId(resultingEq)}", __instance.pawn);
        }
    }

    [HarmonyPatch]
    internal static class Patch_WeaponDropTrace_ContainerDrop
    {
        private static IEnumerable<MethodBase> TargetMethods()
        {
            Type byRefThing = typeof(Thing).MakeByRefType();
            yield return AccessTools.Method(typeof(ThingOwner), nameof(ThingOwner.TryDrop),
                new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(ThingPlaceMode), typeof(int),
                    byRefThing, typeof(Action<Thing, int>), typeof(Predicate<IntVec3>) });
            yield return AccessTools.Method(typeof(ThingOwner), nameof(ThingOwner.TryDrop),
                new[] { typeof(Thing), typeof(IntVec3), typeof(Map), typeof(ThingPlaceMode),
                    byRefThing, typeof(Action<Thing, int>), typeof(Predicate<IntVec3>), typeof(bool) });
        }

        private static void Prefix(ThingOwner __instance, Thing thing, IntVec3 dropLoc, Map map, out bool __state)
        {
            __state = false;
            if (!Prefs.DevMode)
                return;

            Pawn pawn = WeaponDropDiagnostics.HolderPawn(__instance.Owner);
            __state = WeaponDropDiagnostics.ShouldTrace(thing, pawn);
            if (__state)
                WeaponDropDiagnostics.Write("Container.TryDrop.begin", thing,
                    $"source={WeaponDropDiagnostics.HolderId(__instance.Owner)} contains={__instance.Contains(thing)} "
                    + $"cell={dropLoc} map={map?.uniqueID}", pawn);
        }

        private static void Postfix(ThingOwner __instance, Thing thing, bool __result, bool __state)
        {
            if (__state)
                WeaponDropDiagnostics.Write("Container.TryDrop.end", thing, $"result={__result}",
                    WeaponDropDiagnostics.HolderPawn(__instance.Owner));
        }
    }

    [HarmonyPatch(typeof(ThingOwner), nameof(ThingOwner.TryTransferToContainer),
        new[] { typeof(Thing), typeof(ThingOwner), typeof(int), typeof(Thing), typeof(bool) },
        new[] { ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Normal, ArgumentType.Out, ArgumentType.Normal })]
    internal static class Patch_WeaponDropTrace_ContainerTransfer
    {
        private static void Prefix(ThingOwner __instance, Thing item, ThingOwner otherContainer, int count,
            bool canMergeWithExistingStacks, out bool __state)
        {
            __state = false;
            if (!Prefs.DevMode)
                return;

            Pawn pawn = WeaponDropDiagnostics.HolderPawn(__instance.Owner);
            __state = WeaponDropDiagnostics.ShouldTrace(item, pawn ?? WeaponDropDiagnostics.HolderPawn(otherContainer?.Owner));
            if (__state)
                WeaponDropDiagnostics.Write("Container.TryTransfer.begin", item,
                    $"source={WeaponDropDiagnostics.HolderId(__instance.Owner)} "
                    + $"destination={WeaponDropDiagnostics.HolderId(otherContainer?.Owner)} count={count} merge={canMergeWithExistingStacks}",
                    pawn);
        }

        private static void Postfix(ThingOwner __instance, Thing item, ref Thing resultingTransferredItem, int __result, bool __state)
        {
            if (__state)
                WeaponDropDiagnostics.Write("Container.TryTransfer.end", item,
                    $"moved={__result} resultItem={WeaponDropDiagnostics.ThingId(resultingTransferredItem)}",
                    WeaponDropDiagnostics.HolderPawn(__instance.Owner));
        }
    }
}

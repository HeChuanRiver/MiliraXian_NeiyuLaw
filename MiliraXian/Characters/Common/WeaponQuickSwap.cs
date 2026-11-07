using System;
using System.Collections.Generic;
using HarmonyLib;
using MiliraXian.Characters.Common.Combat;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Common
{
    /// <summary>
    /// Quick swap between two real weapons: the equipped one goes into the inventory and the partner
    /// stored there comes to hand, through the drop-protection swap (both stay the same things, with
    /// their style and comp state). Unlike the form switch nothing is made or destroyed.
    /// </summary>
    public sealed class CompProperties_WeaponQuickSwap : CompProperties
    {
        /// <summary>Weapons this one swaps to, in order of preference, when one of them is in the inventory.</summary>
        public List<ThingDef> partnerDefs = new();
        public int cooldownTicks;

        public CompProperties_WeaponQuickSwap() => compClass = typeof(CompWeaponQuickSwap);

        public override IEnumerable<string> ConfigErrors(ThingDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
                yield return error;
            if (partnerDefs.NullOrEmpty())
                yield return "Weapon quick swap needs at least one partner weapon.";
            if (!parentDef.HasComp(typeof(CompWeaponDropProtection)))
                yield return "Weapon quick swap uses the weapon drop protection swap; add CompProperties_WeaponDropProtection.";
        }
    }

    public sealed class CompWeaponQuickSwap : ThingComp
    {
        private int lastSwapTick = -999999;

        public CompProperties_WeaponQuickSwap Props => (CompProperties_WeaponQuickSwap)props;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref lastSwapTick, "mx_lastQuickSwapTick", -999999);
        }

        internal int CooldownTicksLeft => Props.cooldownTicks - (Find.TickManager.TicksGame - lastSwapTick);

        internal void NotifySwapped() => lastSwapTick = Find.TickManager.TicksGame;
    }

    public static class WeaponQuickSwap
    {
        private static readonly List<Func<Pawn, string>> Blockers = new();

        /// <summary>
        /// A character's own reasons not to swap now (a cut in progress, a guard): the function returns
        /// the reason, or null to allow. Register once, from a static constructor.
        /// </summary>
        public static void RegisterBlocker(Func<Pawn, string> blocker)
        {
            if (blocker != null && !Blockers.Contains(blocker))
                Blockers.Add(blocker);
        }

        /// <summary>The quick swap button shows for this pawn (the generic equip button would repeat it).</summary>
        internal static bool Covers(Pawn pawn) => TryGetPartner(pawn, out _, out _);

        /// <summary>The partner of the equipped weapon stored in the inventory, if any.</summary>
        internal static bool TryGetPartner(Pawn pawn, out CompWeaponQuickSwap comp, out ThingWithComps partner)
        {
            partner = null;
            comp = pawn?.equipment?.Primary?.TryGetComp<CompWeaponQuickSwap>();
            if (comp == null || pawn.inventory?.innerContainer is not { } inventory)
                return false;

            foreach (ThingDef def in comp.Props.partnerDefs)
            {
                foreach (Thing item in inventory)
                {
                    // Unbound too: a weapon generated into the inventory was never held yet.
                    if (item is ThingWithComps weapon && weapon.def == def
                        && WeaponDropProtectionUtility.IsStoredWeapon(weapon, pawn, allowUnbound: true))
                    {
                        partner = weapon;
                        return true;
                    }
                }
            }
            return false;
        }

        internal static string DisabledReason(Pawn pawn, CompWeaponQuickSwap comp, ThingWithComps partner)
        {
            if (WeaponDropProtectionUtility.EquipDisabledReason(pawn, partner, allowUnbound: true) is { } reason)
                return reason;
            if (pawn.jobs?.curDriver is IPostureExecutionDriver)
                return Text("Executing");
            foreach (Func<Pawn, string> blocker in Blockers)
            {
                if (blocker(pawn) is { } own)
                    return own;
            }
            int left = comp.CooldownTicksLeft;
            if (comp.Props.cooldownTicks > 0 && left > 0)
                return Text("Cooldown", (left / 60f).ToString("F1"));
            return null;
        }

        internal static Command_Action MakeCommand(Pawn pawn)
        {
            if (!TryGetPartner(pawn, out CompWeaponQuickSwap comp, out ThingWithComps partner))
                return null;

            ThingWithComps current = pawn.equipment.Primary;
            Command_Action command = new()
            {
                defaultLabel = Text("Label", partner.LabelShortCap),
                defaultDesc = Text("Desc", current.LabelShortCap, partner.LabelShortCap),
                icon = WeaponDropProtectionUtility.Icon(partner),
                groupable = false,
                action = () => Swap(pawn),
            };
            if (DisabledReason(pawn, comp, partner) is { } reason)
                command.Disable(reason);
            return command;
        }

        private static void Swap(Pawn pawn)
        {
            if (!TryGetPartner(pawn, out CompWeaponQuickSwap comp, out ThingWithComps partner))
                return;
            if (DisabledReason(pawn, comp, partner) is { } reason)
            {
                Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            WeaponDropProtectionUtility.EquipOrReject(pawn, partner, allowUnbound: true);
            if (pawn.equipment?.Primary == partner)
            {
                // Both ends: the cooldown holds whichever weapon is in hand.
                comp.NotifySwapped();
                partner.TryGetComp<CompWeaponQuickSwap>()?.NotifySwapped();
            }
        }

        private static string Text(string suffix, params NamedArgument[] args) =>
            ("MX_WeaponQuickSwap_" + suffix).Translate(args).ToString();
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.GetGizmos))]
    internal static class Patch_Pawn_GetGizmos_WeaponQuickSwap
    {
        private static IEnumerable<Gizmo> Postfix(IEnumerable<Gizmo> __result, Pawn __instance)
        {
            foreach (Gizmo gizmo in __result)
                yield return gizmo;

            if (__instance.Faction != Faction.OfPlayer || __instance.Dead)
                yield break;
            if (WeaponQuickSwap.MakeCommand(__instance) is { } command)
                yield return command;
        }
    }
}

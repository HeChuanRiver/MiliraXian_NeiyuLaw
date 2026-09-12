using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    public class CompProperties_PawnKindRestrictedWeapon : CompProperties
    {
        public List<PawnKindDef> allowedPawnKinds = new();
        public string cannotEquipKey = "MX_WeaponPawnKindRestricted";

        public CompProperties_PawnKindRestrictedWeapon()
        {
            compClass = typeof(CompPawnKindRestrictedWeapon);
        }
    }

    public class CompPawnKindRestrictedWeapon : ThingComp
    {
        public CompProperties_PawnKindRestrictedWeapon Props => (CompProperties_PawnKindRestrictedWeapon)props;

        public bool CanEquip(Pawn pawn)
        {
            return Props.allowedPawnKinds.Contains(pawn.kindDef);
        }

        // Follow AL's equipped notification so direct equipment transfers also enforce the restriction.
        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            if (CanEquip(pawn))
            {
                return;
            }

            bool removed = pawn.Spawned
                ? pawn.equipment.TryDropEquipment(parent, out _, pawn.Position, forbid: false)
                : pawn.equipment.TryTransferEquipmentToContainer(parent, pawn.inventory.innerContainer);
            if (!removed)
            {
                Log.Error($"Could not unequip pawn-kind-restricted weapon {parent} from {pawn}.");
                return;
            }

            Messages.Message("MX_WeaponPawnKindRejected".Translate(pawn.LabelShort, parent.Label),
                pawn, MessageTypeDefOf.RejectInput);
        }
    }

    [StaticConstructorOnStartup]
    internal static class Patch_PawnKindRestrictedWeapon_CanEquip
    {
        static Patch_PawnKindRestrictedWeapon_CanEquip()
        {
            var harmony = new Harmony("MiliraXian.Characters.PawnKindRestrictedWeapon");
            harmony.Patch(
                AccessTools.Method(typeof(EquipmentUtility), nameof(EquipmentUtility.CanEquip),
                    new[] { typeof(Thing), typeof(Pawn), typeof(string).MakeByRefType(), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(Patch_PawnKindRestrictedWeapon_CanEquip), nameof(Postfix)));
        }

        // Follow HAR's CanEquip postfix and preserve refusals from the game and other mods.
        private static void Postfix(Thing thing, Pawn pawn, ref string cantReason, ref bool __result)
        {
            if (!__result)
            {
                return;
            }

            CompPawnKindRestrictedWeapon restriction = thing.TryGetComp<CompPawnKindRestrictedWeapon>();
            if (restriction != null && !restriction.CanEquip(pawn))
            {
                __result = false;
                cantReason = restriction.Props.cannotEquipKey.Translate();
            }
        }
    }
}

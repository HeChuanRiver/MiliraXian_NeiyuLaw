using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class MX_QHCharacterUtility
    {
        private static readonly HashSet<int> PendingLoadoutStabilizationPawnIds = new();

        public static bool IsQinghe(Pawn pawn)
        {
            return pawn?.kindDef == MX_QHDefOf.MiliraXian_Qinghe;
        }

        public static bool HasRequiredWeapon(Pawn pawn, ThingDef requiredWeapon)
        {
            if (pawn?.equipment?.Primary == null)
            {
                return false;
            }

            if (requiredWeapon == null)
            {
                return true;
            }

            return pawn.equipment.Primary.def == requiredWeapon;
        }

        public static string TranslateIfKey(string text)
        {
            if (text.NullOrEmpty())
            {
                return text;
            }

            return Translator.CanTranslate(text) ? text.Translate().ToString() : text;
        }

        public static void EnsureDefaultLoadout(Pawn pawn)
        {
            EnsureDefaultWeapon(pawn);
            EnsureDefaultClothing(pawn);
            EnsureDefaultHeaddress(pawn);
        }

        public static void MarkForLoadoutStabilization(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            PendingLoadoutStabilizationPawnIds.Add(pawn.thingIDNumber);
        }

        public static bool ShouldFinalizeLoadout(Pawn pawn)
        {
            return pawn != null && PendingLoadoutStabilizationPawnIds.Contains(pawn.thingIDNumber);
        }

        public static void ClearLoadoutStabilization(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            PendingLoadoutStabilizationPawnIds.Remove(pawn.thingIDNumber);
        }

        private static void EnsureDefaultWeapon(Pawn pawn)
        {
            if (!IsQinghe(pawn) || pawn.equipment == null || pawn.equipment.Primary != null)
            {
                return;
            }

            ThingDef weaponDef = MX_QHDefOf.MX_QH_Weapon_FlowerBell;
            if (weaponDef == null)
            {
                Log.Error("[MiliraXian.Characters.QingHe] Missing Qinghe default weapon ThingDef.");
                return;
            }

            ThingWithComps weapon = ThingMaker.MakeThing(weaponDef) as ThingWithComps;
            if (weapon == null)
            {
                Log.Error("[MiliraXian.Characters.QingHe] Qinghe default weapon is not ThingWithComps.");
                return;
            }

            PawnGenerator.PostProcessGeneratedGear(weapon, pawn);
            CompEquippable compEquippable = weapon.TryGetComp<CompEquippable>();
            if (compEquippable != null)
            {
                if (pawn.kindDef.weaponStyleDef != null)
                {
                    compEquippable.parent.StyleDef = pawn.kindDef.weaponStyleDef;
                }
                else if (pawn.Ideo != null)
                {
                    compEquippable.parent.StyleDef = pawn.Ideo.GetStyleFor(weapon.def);
                }
            }

            pawn.equipment.AddEquipment(weapon);
        }

        private static void EnsureDefaultClothing(Pawn pawn)
        {
            EnsureDefaultApparel(pawn, MX_QHDefOf.MX_QingheNormal, "Default clothing");
        }

        private static void EnsureDefaultHeaddress(Pawn pawn)
        {
            EnsureDefaultApparel(pawn, MX_QHDefOf.MX_QingheHeaddress, "Default headdress");
        }

        private static void EnsureDefaultApparel(Pawn pawn, ThingDef apparelDef, string debugLabel)
        {
            if (!IsQinghe(pawn) || pawn.apparel == null)
            {
                return;
            }

            Apparel existing = FindWornApparel(pawn, apparelDef);
            if (existing != null)
            {
                EnsureForcedApparel(pawn, existing);
                return;
            }

            if (apparelDef == null)
            {
                Log.Error("[MiliraXian.Characters.QingHe] Missing Qinghe " + debugLabel + " ThingDef.");
                return;
            }

            Apparel apparel = ThingMaker.MakeThing(apparelDef) as Apparel;
            if (apparel == null)
            {
                Log.Error("[MiliraXian.Characters.QingHe] " + debugLabel + " is not Apparel.");
                return;
            }

            PawnGenerator.PostProcessGeneratedGear(apparel, pawn);
            pawn.apparel.Wear(apparel, dropReplacedApparel: true);
            EnsureForcedApparel(pawn, apparel);
        }

        private static void EnsureForcedApparel(Pawn pawn, Apparel apparel)
        {
            if (pawn?.apparel == null || apparel == null)
            {
                return;
            }

            if (pawn.apparel.IsLocked(apparel))
            {
                pawn.apparel.Unlock(apparel);
            }

            if (pawn.outfits?.forcedHandler != null)
            {
                pawn.outfits.forcedHandler.SetForced(apparel, forced: true);
            }
        }

        private static Apparel FindWornApparel(Pawn pawn, ThingDef apparelDef)
        {
            if (pawn?.apparel == null || apparelDef == null)
            {
                return null;
            }

            for (int index = 0; index < pawn.apparel.WornApparel.Count; index++)
            {
                Apparel apparel = pawn.apparel.WornApparel[index];
                if (apparel?.def == apparelDef)
                {
                    return apparel;
                }
            }

            return null;
        }
    }
}

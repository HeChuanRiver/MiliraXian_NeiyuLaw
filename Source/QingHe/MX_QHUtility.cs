using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class MX_QHUtility
    {
        public const string PawnKindDef_Qinghe = "MiliraXian_Qinghe";
        private const string DefaultWeaponDefName = "MX_QH_Weapon_FlowerBell";
        private const string DefaultClothingDefName = "MX_QingheNormal";
        private const string DefaultHeaddressDefName = "MX_QingheHeaddress";

        private static readonly HashSet<int> PendingLoadoutStabilizationPawnIds = new HashSet<int>();

        public static bool IsQinghe(Pawn pawn)
        {
            return pawn?.kindDef.defName == PawnKindDef_Qinghe;
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

            ThingDef weaponDef = DefDatabase<ThingDef>.GetNamedSilentFail(DefaultWeaponDefName);
            if (weaponDef == null)
            {
                Log.Error("[MiliraXian.Characters.QingHe] Missing ThingDef: " + DefaultWeaponDefName);
                return;
            }

            ThingWithComps weapon = ThingMaker.MakeThing(weaponDef) as ThingWithComps;
            if (weapon == null)
            {
                Log.Error("[MiliraXian.Characters.QingHe] Default weapon is not ThingWithComps: " + DefaultWeaponDefName);
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
            EnsureDefaultApparel(pawn, DefaultClothingDefName, "Default clothing");
        }

        private static void EnsureDefaultHeaddress(Pawn pawn)
        {
            EnsureDefaultApparel(pawn, DefaultHeaddressDefName, "Default headdress");
        }

        private static void EnsureDefaultApparel(Pawn pawn, string defName, string debugLabel)
        {
            if (!IsQinghe(pawn) || pawn.apparel == null)
            {
                return;
            }

            Apparel existing = FindWornApparel(pawn, defName);
            if (existing != null)
            {
                EnsureForcedApparel(pawn, existing);
                return;
            }

            ThingDef apparelDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (apparelDef == null)
            {
                Log.Error("[MiliraXian.Characters.QingHe] Missing ThingDef: " + defName);
                return;
            }

            Apparel apparel = ThingMaker.MakeThing(apparelDef) as Apparel;
            if (apparel == null)
            {
                Log.Error("[MiliraXian.Characters.QingHe] " + debugLabel + " is not Apparel: " + defName);
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

        private static Apparel FindWornApparel(Pawn pawn, string defName)
        {
            if (pawn?.apparel == null)
            {
                return null;
            }

            for (int index = 0; index < pawn.apparel.WornApparel.Count; index++)
            {
                Apparel apparel = pawn.apparel.WornApparel[index];
                if (apparel?.def?.defName == defName)
                {
                    return apparel;
                }
            }

            return null;
        }
    }
}

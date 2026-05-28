using System.Collections.Generic;
using RimWorld;
using UnityEngine;
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

        public static void TryApplyOrRefreshHediff(Pawn pawn, HediffDef hediffDef, float severity, int durationTicks)
        {
            if (pawn?.health?.hediffSet == null || hediffDef == null)
            {
                return;
            }

            var hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(hediffDef, pawn);
                pawn.health.AddHediff(hediff);
            }

            hediff.Severity = Mathf.Max(hediff.Severity, severity);
            var disappears = hediff.TryGetComp<HediffComp_Disappears>();
            if (disappears != null && durationTicks > 0)
            {
                disappears.SetDuration(durationTicks);
            }
        }

        public static void TryKnockback(Pawn pawn, IntVec3 center, float distance)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead || !pawn.Spawned || pawn.MapHeld == null)
            {
                return;
            }

            var map = pawn.MapHeld;
            var start = pawn.Position;
            var direction = (start - center).ToVector3();
            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = new Vector3(Rand.Range(-1f, 1f), 0f, Rand.Range(-1f, 1f));
            }
            direction.Normalize();

            var best = start;
            var steps = Mathf.Max(1, Mathf.RoundToInt(distance));
            for (var i = 1; i <= steps; i++)
            {
                var next = start + (direction * i).ToIntVec3();
                if (!ValidKnockbackCell(map, next, pawn))
                {
                    break;
                }
                best = next;
            }

            if (best == start)
            {
                return;
            }

            pawn.Position = best;
            pawn.pather?.StopDead();
            pawn.jobs?.StopAll(false, true);
        }

        public static void ApplyBleed(Pawn pawn, float bleedDamage)
        {
            if (pawn?.health?.hediffSet == null || bleedDamage <= 0f)
            {
                return;
            }

            var part = pawn.health.hediffSet.GetRandomNotMissingPart(DamageDefOf.Cut, BodyPartHeight.Undefined, BodyPartDepth.Outside);
            if (part == null)
            {
                return;
            }

            var injury = HediffMaker.MakeHediff(HediffDefOf.Cut, pawn, part) as Hediff_Injury;
            if (injury == null)
            {
                return;
            }

            injury.Severity = Mathf.Max(0.1f, bleedDamage);
            pawn.health.AddHediff(injury, part);
        }

        public static void HealInjuries(Pawn pawn, float totalHeal)
        {
            if (pawn?.health?.hediffSet == null || totalHeal <= 0f)
            {
                return;
            }

            var remain = totalHeal;
            var hediffs = pawn.health.hediffSet.hediffs;
            for (var i = hediffs.Count - 1; i >= 0 && remain > 0f; i--)
            {
                var injury = hediffs[i] as Hediff_Injury;
                if (injury == null || injury.Severity <= 0f)
                {
                    continue;
                }

                var heal = Mathf.Min(remain, injury.Severity);
                injury.Heal(heal);
                remain -= heal;
            }
        }

        private static bool ValidKnockbackCell(Map map, IntVec3 cell, Pawn movingPawn)
        {
            if (!cell.IsValid || !cell.InBounds(map))
            {
                return false;
            }

            if (!cell.Walkable(map) || cell.Impassable(map) || cell.Fogged(map))
            {
                return false;
            }

            if (cell.GetEdifice(map) is Building_Door door && !door.Open)
            {
                return false;
            }

            var things = cell.GetThingList(map);
            for (var i = 0; i < things.Count; i++)
            {
                var other = things[i] as Pawn;
                if (other != null && other != movingPawn && other.Spawned && !other.Dead)
                {
                    return false;
                }
            }

            return true;
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

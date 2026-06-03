using System;
using System.Text;
using HarmonyLib;
using MiliraXian.Characters.Neiyu;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan
{
    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_Mingyuan_PreApplyDamage
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (__instance == null || __instance.Dead)
            {
                return;
            }

            if (MingyuanTimeLockUtility.IsEternalBurning(__instance))
            {
                absorbed = true;
                return;
            }

            if (MingyuanTimeLockUtility.IsLocked(__instance))
            {
                dinfo.SetIgnoreArmor(true);
                dinfo.SetApplyAllDamage(true);
            }

            if (!MingyuanUtility.IsMingyuan(__instance))
            {
                return;
            }

            HediffComp_MingyuanBurningBody body = (__instance.health?.hediffSet?.GetFirstHediffOfDef(MingyuanUtility.BurningBodyDef) as HediffWithComps)?.GetComp<HediffComp_MingyuanBurningBody>();
            if (body != null && body.Invulnerable)
            {
                absorbed = true;
                return;
            }

            HediffComp_MingyuanProtectiveFlameShield shield = (__instance.health?.hediffSet?.GetFirstHediffOfDef(MingyuanUtility.ShieldDef) as HediffWithComps)?.GetComp<HediffComp_MingyuanProtectiveFlameShield>();
            if (MingyuanUtility.IsHeatOrExplosionDamage(dinfo.Def))
            {
                absorbed = true;
                shield?.AddEnergy(dinfo.Amount * (body?.PropsBody.heatShieldEnergyFactor ?? 0.25f));
                MingyuanUtility.RestorePawnToBestCondition(__instance, true);
                return;
            }

            if (absorbed)
            {
                return;
            }

            shield?.TryAbsorb(ref dinfo, ref absorbed);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    public static class Patch_Mingyuan_OnHitLifeBurn
    {
        [HarmonyPostfix]
        public static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
        {
            if (MingyuanUtility.SuppressOnHitLifeBurn || __result == null || __result.totalDamageDealt <= 0f)
            {
                return;
            }

            Pawn instigator = dinfo.Instigator as Pawn;
            Pawn target = __instance as Pawn;
            if (!MingyuanUtility.IsMingyuan(instigator)
                || target == null
                || target == instigator
                || target.Dead
                || !target.HostileTo(instigator))
            {
                return;
            }

            float step = MingyuanUtility.GetLifeBurnBonusStep(instigator);
            bool ranged = dinfo.Def?.defName == "Arrow";
            float layers = ranged ? 2f + step * 2f : 10f + step * 10f;
            MingyuanUtility.AddLifeBurn(target, instigator, layers);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.DoTick))]
    public static class Patch_Mingyuan_TimeLockedThingTick
    {
        [HarmonyPrefix]
        public static bool Prefix(Thing __instance)
        {
            return !MingyuanTimeLockUtility.IsLocked(__instance);
        }
    }

    [HarmonyPatch(typeof(ThingSelectionUtility), nameof(ThingSelectionUtility.SelectableByMapClick))]
    public static class Patch_Mingyuan_EternalBurningSelection
    {
        [HarmonyPostfix]
        public static void Postfix(Thing t, ref bool __result)
        {
            if (__result && t is Pawn pawn && MingyuanTimeLockUtility.IsEternalBurning(pawn))
            {
                __result = false;
            }
        }
    }

    [HarmonyPatch(typeof(BodyPartDef), nameof(BodyPartDef.GetMaxHealth))]
    public static class Patch_Mingyuan_BodyPartHealth
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, ref float __result)
        {
            if (MingyuanUtility.IsMingyuan(pawn) && MingyuanUtility.HasHediff(pawn, MingyuanUtility.BurningBodyDef))
            {
                __result *= 100f;
            }
        }
    }

    [HarmonyPatch(typeof(StatExtension), nameof(StatExtension.GetStatValue))]
    public static class Patch_Mingyuan_StatEffects
    {
        [HarmonyPostfix]
        public static void Postfix(Thing thing, StatDef stat, bool applyPostProcess, int cacheStaleAfterTicks, ref float __result)
        {
            Pawn pawn = thing as Pawn;
            if (pawn == null || stat == null)
            {
                return;
            }

            float lifeBurn = MingyuanUtility.GetLifeBurnLayers(pawn);
            if (lifeBurn > 0f)
            {
                float per100 = lifeBurn / 100f;
                if (stat == StatDefOf.MoveSpeed || stat == StatDefOf.ShootingAccuracyPawn || stat == StatDefOf.WorkSpeedGlobal || stat == StatDefOf.RangedWeapon_DamageMultiplier)
                {
                    __result *= Mathf.Max(0.05f, 1f - per100 * 0.01f);
                }
                else if (stat == StatDefOf.IncomingDamageFactor || stat == StatDefOf.MeleeCooldownFactor || stat == StatDefOf.RangedCooldownFactor)
                {
                    __result *= 1f + per100 * 0.01f;
                }
            }

            float selfBurn = MingyuanUtility.GetSelfBurnLayers(pawn);
            if (selfBurn <= 0f)
            {
                return;
            }

            if (stat == StatDefOf.MoveSpeed)
            {
                __result *= 1f + selfBurn * 0.005f;
            }
            else if (stat == StatDefOf.WorkSpeedGlobal || stat == StatDefOf.RangedWeapon_DamageMultiplier)
            {
                __result *= 1f + selfBurn * 0.01f;
            }
            else if (stat == StatDefOf.MeleeCooldownFactor || stat == StatDefOf.RangedCooldownFactor)
            {
                __result /= 1f + selfBurn * 0.01f;
            }
        }
    }

    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedMeleeDamageAmount), new Type[] { typeof(Tool), typeof(Pawn), typeof(Thing), typeof(HediffComp_VerbGiver) })]
    public static class Patch_Mingyuan_MeleeDamage
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn attacker, ref float __result)
        {
            float selfBurn = MingyuanUtility.GetSelfBurnLayers(attacker);
            if (selfBurn > 0f)
            {
                __result *= 1f + selfBurn * 0.01f;
            }
        }
    }

    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedCooldown), new Type[] { typeof(Verb), typeof(Pawn) })]
    public static class Patch_Mingyuan_AttackCooldown
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn attacker, ref float __result)
        {
            float selfBurn = MingyuanUtility.GetSelfBurnLayers(attacker);
            if (selfBurn > 0f)
            {
                __result /= 1f + selfBurn * 0.01f;
            }
        }
    }

    [HarmonyPatch(typeof(ProjectileProperties), nameof(ProjectileProperties.GetDamageAmount), new Type[] { typeof(Thing), typeof(StringBuilder) })]
    public static class Patch_Mingyuan_RangedDamage
    {
        [HarmonyPostfix]
        public static void Postfix(Thing weapon, ref int __result)
        {
            Pawn ownerPawn = MXNeiyuShieldUtility.TryGetEquipmentOwnerPawn(weapon);
            float selfBurn = MingyuanUtility.GetSelfBurnLayers(ownerPawn);
            if (selfBurn > 0f)
            {
                __result = Mathf.Max(1, Mathf.RoundToInt(__result * (1f + selfBurn * 0.01f)));
            }
        }
    }
}

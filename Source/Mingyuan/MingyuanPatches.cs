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
        private const float MeleeLifeBurnLayers = 420f;
        private const float RangedLifeBurnLayers = 140f;

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
            bool ranged = dinfo.Weapon?.IsRangedWeapon == true;
            float baseLayers = ranged ? RangedLifeBurnLayers : MeleeLifeBurnLayers;
            float layers = baseLayers * (1f + step);
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

    internal static class MingyuanLifeBurnPatchUtility
    {
        public static float PenaltyFactor(float lifeBurn)
        {
            if (lifeBurn <= 0f)
            {
                return 1f;
            }

            return Mathf.Max(0.05f, 1f - (lifeBurn / 100f) * 0.01f);
        }

        public static HediffComp_MingyuanLifeBurn GetLifeBurnComp(Pawn pawn)
        {
            return (pawn?.health?.hediffSet?.GetFirstHediffOfDef(MingyuanUtility.LifeBurnDef) as HediffWithComps)?.GetComp<HediffComp_MingyuanLifeBurn>();
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
                if (stat == StatDefOf.MoveSpeed
                    || stat == StatDefOf.MeleeHitChance
                    || stat == StatDefOf.ShootingAccuracyPawn
                    || stat == StatDefOf.WorkSpeedGlobal
                    || stat == StatDefOf.RangedWeapon_DamageMultiplier)
                {
                    __result *= MingyuanLifeBurnPatchUtility.PenaltyFactor(lifeBurn);
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
            float lifeBurn = MingyuanUtility.GetLifeBurnLayers(attacker);
            if (lifeBurn > 0f)
            {
                __result *= MingyuanLifeBurnPatchUtility.PenaltyFactor(lifeBurn);
            }

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
            float lifeBurn = MingyuanUtility.GetLifeBurnLayers(ownerPawn);
            if (lifeBurn > 0f)
            {
                __result = Mathf.Max(1, Mathf.RoundToInt(__result * MingyuanLifeBurnPatchUtility.PenaltyFactor(lifeBurn)));
            }

            float selfBurn = MingyuanUtility.GetSelfBurnLayers(ownerPawn);
            if (selfBurn > 0f)
            {
                __result = Mathf.Max(1, Mathf.RoundToInt(__result * (1f + selfBurn * 0.01f)));
            }
        }
    }

    [StaticConstructorOnStartup]
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
    public static class Patch_Mingyuan_LifeBurnProgressBar
    {
        private static readonly AccessTools.FieldRef<PawnRenderer, Pawn> PawnRef = AccessTools.FieldRefAccess<PawnRenderer, Pawn>("pawn");
        private static readonly Material BarFilledMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(1f, 0.56f, 0.22f, 0.92f));
        private static readonly Material BarCriticalMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(1f, 0.78f, 0.28f, 0.98f));
        private static readonly Material BarEmptyMat = SolidColorMaterials.SimpleSolidColorMaterial(new Color(0.16f, 0.04f, 0.025f, 0.82f));

        [HarmonyPostfix]
        public static void Postfix(PawnRenderer __instance, Vector3 drawLoc, Rot4? rotOverride = null, bool neverAimWeapon = false)
        {
            if (__instance == null)
            {
                return;
            }

            Pawn pawn = PawnRef(__instance);
            if (pawn == null || pawn.Dead || !pawn.Spawned)
            {
                return;
            }

            HediffComp_MingyuanLifeBurn comp = MingyuanLifeBurnPatchUtility.GetLifeBurnComp(pawn);
            if (comp == null)
            {
                return;
            }

            float layers = comp.CurrentLayers;
            float threshold = comp.ExecuteThreshold;
            if (layers <= 0f || threshold <= 0f)
            {
                return;
            }

            float rawProgress = Mathf.Clamp01(layers / threshold);
            float displayProgress = Mathf.Max(rawProgress, 0.035f);
            Vector3 center = new Vector3(drawLoc.x, AltitudeLayer.MetaOverlays.AltitudeFor(), drawLoc.z - 0.58f);
            GenDraw.DrawFillableBar(new GenDraw.FillableBarRequest
            {
                center = center,
                size = new Vector2(0.95f, 0.09f),
                fillPercent = displayProgress,
                filledMat = rawProgress >= 0.9f ? BarCriticalMat : BarFilledMat,
                unfilledMat = BarEmptyMat,
                margin = 0.012f,
                rotation = Rot4.North
            });
        }
    }
}

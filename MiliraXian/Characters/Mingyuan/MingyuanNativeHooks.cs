using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MiliraXian.Characters.Neiyu;
using RimWorld;
using UnityEngine;
using Verse;
using MiliraXian.Characters.Common;
using MiliraXian.Characters.Common.Combat;

namespace MiliraXian.Characters.Mingyuan
{
    internal static class MingyuanDeveloperDamageContext
    {
        [ThreadStatic]
        private static int activeDepth;

        public static bool Active => Prefs.DevMode && activeDepth > 0;

        public static void Enter()
        {
            activeDepth++;
        }

        public static void Exit()
        {
            if (activeDepth > 0)
            {
                activeDepth--;
            }
        }
    }

    [HarmonyPatch]
    internal static class Patch_MingyuanDeveloperDamageCommands
    {
        private static readonly string[] GeneralDamageMethods =
        {
            "Take10Damage",
            "Take300Damage",
            "Take5000Damage"
        };

        private static readonly string[] PawnDamageMethods =
        {
            "DamageUntilDown",
            "DamageLegs",
            "DamageUntilIncapableOfManipulation",
            "DamageToDeath",
            "CarriedDamageToDeath",
            "Do10DamageUntilDead",
            "DamageHeldPawnToDeath"
        };

        private static IEnumerable<MethodBase> TargetMethods()
        {
            HashSet<MethodBase> methods = new();
            AddNamedMethods(methods, "Verse.DebugToolsGeneral", GeneralDamageMethods);
            AddNamedMethods(methods, "Verse.DebugToolsPawns", PawnDamageMethods);
            AddHealthDamageCallbacks(methods);

            foreach (MethodBase method in methods)
            {
                yield return method;
            }
        }

        private static void AddNamedMethods(HashSet<MethodBase> methods, string typeName, string[] methodNames)
        {
            Type type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                return;
            }

            MethodInfo[] declaredMethods = type.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            for (int index = 0; index < declaredMethods.Length; index++)
            {
                MethodInfo method = declaredMethods[index];
                for (int nameIndex = 0; nameIndex < methodNames.Length; nameIndex++)
                {
                    if (method.Name == methodNames[nameIndex])
                    {
                        methods.Add(method);
                        break;
                    }
                }
            }
        }

        private static void AddHealthDamageCallbacks(HashSet<MethodBase> methods)
        {
            Type healthTools = AccessTools.TypeByName("Verse.DebugTools_Health");
            if (healthTools == null)
            {
                return;
            }

            Type[] nestedTypes = healthTools.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
            for (int typeIndex = 0; typeIndex < nestedTypes.Length; typeIndex++)
            {
                MethodInfo[] callbacks = nestedTypes[typeIndex].GetMethods(
                    BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                for (int methodIndex = 0; methodIndex < callbacks.Length; methodIndex++)
                {
                    MethodInfo callback = callbacks[methodIndex];
                    if (callback.Name.IndexOf("Options_ApplyDamage", StringComparison.Ordinal) >= 0
                        || callback.Name.IndexOf("Options_Damage_BodyParts", StringComparison.Ordinal) >= 0)
                    {
                        methods.Add(callback);
                    }
                }
            }
        }

        [HarmonyPrefix]
        private static void Prefix()
        {
            MingyuanDeveloperDamageContext.Enter();
        }

        [HarmonyFinalizer]
        private static Exception Finalizer(Exception __exception)
        {
            MingyuanDeveloperDamageContext.Exit();
            return __exception;
        }
    }

    internal static class MingyuanStatUtility
    {
        public static float LifeBurnCombatChange(float layers) =>
            MingyuanPowerBalance.Sealed ? 0f : Mathf.Clamp(layers / 200f * 0.05f, 0f, 0.60f);
    }

    public class CompProperties_MingyuanDamageResponder : CompProperties
    {
        public CompProperties_MingyuanDamageResponder()
        {
            compClass = typeof(CompMingyuanDamageResponder);
        }
    }

    public class CompMingyuanDamageResponder : ThingComp
    {
        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null)
            {
                return;
            }

            if (MingyuanDeveloperDamageContext.Active)
            {
                return;
            }

            HediffComp_MingyuanBurningBody body =
                (pawn.health.hediffSet.GetFirstHediffOfDef(MingyuanUtility.BurningBodyDef) as HediffWithComps)
                ?.GetComp<HediffComp_MingyuanBurningBody>();
            if (body == null)
            {
                return;
            }

            if (MingyuanPowerBalance.Sealed) return;

            if (dinfo.Amount <= 0f) return;
            bool heat = dinfo.Def.armorCategory?.armorRatingStat == StatDefOf.ArmorRating_Heat
                || typeof(DamageWorker_Flame).IsAssignableFrom(dinfo.Def.workerClass);
            bool explosion = dinfo.Def.isExplosive;
            if (heat || explosion)
            {
                // Thermal hits take priority over blast damage when a Def marks both.
                (pawn.health.hediffSet.GetFirstHediffOfDef(MingyuanUtility.ShieldDef) as HediffWithComps)
                    ?.GetComp<HediffComp_MingyuanProtectiveFlameShield>()?.RefillFromAttack(explosion: !heat);
            }

            bool melee = !explosion && !dinfo.Def.isRanged && dinfo.Instigator is Pawn
                && (dinfo.Tool != null || dinfo.WeaponBodyPartGroup != null || dinfo.WeaponLinkedHediff != null
                    || dinfo.Weapon?.IsMeleeWeapon == true);
            absorbed = heat || body.Invulnerable || (melee && MingyuanUtility.IsOverburning(pawn));
            if (!absorbed && explosion) dinfo.SetAmount(dinfo.Amount * 0.30f);
        }
    }

    [StaticConstructorOnStartup]
    internal sealed class MingyuanOverburnBlocker : CombatBlocker
    {
        static MingyuanOverburnBlocker() => CombatBlockers.Register(new MingyuanOverburnBlocker());

        public override bool Covers(Pawn pawn) => MingyuanUtility.IsMingyuan(pawn)
            && !MingyuanDeveloperDamageContext.Active && MingyuanUtility.IsOverburning(pawn);

        public override bool TryBlockMelee(Verb_MeleeAttack verb, Pawn defender) => true;
    }

    internal static class MingyuanSurvival
    {
        public const float DeathBloodLoss = 0.95f;

        internal static bool AppliesTo(Pawn pawn) =>
            !MingyuanPowerBalance.Sealed && !MingyuanDeveloperDamageContext.Active
            && MingyuanUtility.IsMingyuan(pawn) && pawn.health?.hediffSet != null;

        internal static bool CanDie(Pawn pawn)
        {
            if (BloodLoss(pawn) >= DeathBloodLoss) return true;
            HediffSet set = pawn.health.hediffSet;
            List<BodyPartRecord> parts = pawn.RaceProps.body.AllParts;
            float maximum = 0f;
            foreach (BodyPartRecord part in parts) maximum += part.def.GetMaxHealth(pawn);
            if (maximum <= 0f) return false;

            float remaining = maximum * 0.05f;
            foreach (BodyPartRecord part in parts)
            {
                remaining -= set.GetPartHealth(part);
                if (remaining < 0f) return false;
            }
            return true;
        }

        internal static float BloodLoss(Pawn pawn) =>
            pawn.health.hediffSet.GetFirstHediffOfDef(HediffDefOf.BloodLoss)?.Severity ?? 0f;
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDead))]
    internal static class Patch_MingyuanShouldBeDead
    {
        private static bool Prefix(Pawn ___pawn, ref bool __result)
        {
            if (___pawn.Dead || !MingyuanSurvival.AppliesTo(___pawn)) return true;
            __result = MingyuanSurvival.CanDie(___pawn);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), nameof(Pawn_HealthTracker.ShouldBeDowned))]
    internal static class Patch_MingyuanShouldBeDowned
    {
        private static bool Prefix(Pawn ___pawn, ref bool __result)
        {
            if (!MingyuanSurvival.AppliesTo(___pawn)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(Hediff), nameof(Hediff.Severity), MethodType.Setter)]
    internal static class Patch_MingyuanBloodLossThreshold
    {
        private static void Prefix(Hediff __instance, float __0, out bool __state) =>
            __state = __instance.def is { } def && def == HediffDefOf.BloodLoss
                && __instance.Severity < MingyuanSurvival.DeathBloodLoss
                && __0 >= MingyuanSurvival.DeathBloodLoss && __0 < def.lethalSeverity;

        private static void Postfix(Hediff __instance, bool __state)
        {
            if (!__state || __instance.Severity < MingyuanSurvival.DeathBloodLoss) return;
            Pawn pawn = __instance.pawn;
            if (pawn != null && !pawn.Dead && MingyuanSurvival.AppliesTo(pawn)
                && pawn.health.hediffSet.hediffs.Contains(__instance))
                pawn.health.Notify_HediffChanged(__instance);
        }
    }

    public class Hediff_MingyuanBurningBody : HediffWithComps
    {
        public override void Notify_PawnDamagedThing(Thing thing, DamageInfo dinfo, DamageWorker.DamageResult result)
        {
            base.Notify_PawnDamagedThing(thing, dinfo, result);
            if (MingyuanPowerBalance.Sealed) return;
            if (MingyuanUtility.SuppressOnHitLifeBurn || result == null || result.totalDamageDealt <= 0f)
            {
                return;
            }

            Pawn target = thing as Pawn;
            if (target == null || target == pawn || target.Dead || !target.HostileTo(pawn))
            {
                return;
            }

            HediffComp_MingyuanBurningBody body = GetComp<HediffComp_MingyuanBurningBody>();
            if (body == null)
            {
                return;
            }

            bool ranged = dinfo.Weapon?.IsRangedWeapon == true;
            float bonusSteps = MingyuanUtility.GetLifeBurnBonusStep(pawn);
            float baseLayers = ranged ? body.PropsBody.rangedLifeBurnLayers : body.PropsBody.meleeLifeBurnLayers;
            float bonusPer100 = ranged ? body.PropsBody.rangedSelfBurnBonusPer100 : body.PropsBody.meleeSelfBurnBonusPer100;
            MingyuanUtility.AddLifeBurn(target, pawn, baseLayers + bonusSteps * bonusPer100, scaleWithOverburn: true);
        }
    }

    public class StatPart_MingyuanRangedDamage : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            val *= FactorFor(req);
        }

        public override string ExplanationPart(StatRequest req)
        {
            float factor = FactorFor(req);
            if (Mathf.Approximately(factor, 1f))
            {
                return null;
            }

            return "MX_Mingyuan_RangedDamage_StatPart".Translate(factor.ToStringPercent()).ToString();
        }

        private static float FactorFor(StatRequest req)
        {
            Pawn owner = MXNeiyuShieldUtility.TryGetEquipmentOwnerPawn(req.Thing);
            if (owner == null)
            {
                return 1f;
            }

            float factor = 1f - MingyuanStatUtility.LifeBurnCombatChange(MingyuanUtility.GetLifeBurnLayers(owner));
            factor *= MingyuanUtility.GetSelfBurnRangedWeaponDamageFactor(owner);
            factor *= MingyuanUtility.GetOverburnDamageFactor(owner);
            return factor;
        }
    }
}

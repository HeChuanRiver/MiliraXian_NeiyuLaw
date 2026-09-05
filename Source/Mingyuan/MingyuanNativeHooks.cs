using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using MiliraXian.Characters.Neiyu;
using RimWorld;
using UnityEngine;
using Verse;

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
        public static float LifeBurnPenaltyFactor(float lifeBurn)
        {
            if (!MingyuanPowerBalance.IsOriginal) return MingyuanPowerBalance.Sealed ? 1f : Mathf.Max(.9f, 1f - lifeBurn * .001f);
            if (lifeBurn <= 0f)
            {
                return 1f;
            }

            return Mathf.Max(0.05f, 1f - lifeBurn * 0.0001f);
        }
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

            if (!MingyuanPowerBalance.IsOriginal)
            {
                if (MingyuanPowerBalance.IsBalanced && MingyuanUtility.IsHeatOrExplosionDamage(dinfo.Def))
                    dinfo.SetAmount(dinfo.Amount * (dinfo.Def.isExplosive ? .7f : .5f));
                return;
            }

            if (body.Invulnerable)
            {
                absorbed = true;
                return;
            }

            if (MingyuanUtility.IsHeatOrExplosionDamage(dinfo.Def))
            {
                absorbed = true;
                MingyuanUtility.RestorePawnToBestCondition(pawn, true);
            }
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

            float factor = MingyuanStatUtility.LifeBurnPenaltyFactor(MingyuanUtility.GetLifeBurnLayers(owner));
            factor *= MingyuanUtility.GetSelfBurnRangedWeaponDamageFactor(owner);
            factor *= MingyuanUtility.GetOverburnDamageFactor(owner);
            return factor;
        }
    }
}

using HarmonyLib;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Stats;
using MiliraXian.Characters.QingHe.Things;
using RimWorld;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    [StaticConstructorOnStartup]
    public static class MX_QHPatches
    {
        private static readonly Harmony patcher = new Harmony("MiliraXian.Characters.QingHe");

        static MX_QHPatches()
        {
            RegisterFlowerBellCorrosionArmorStatParts();
            RegisterFlowerBellCooldownStatPart();

            patcher.Patch(AccessTools.Method(typeof(StartingPawnUtility), nameof(StartingPawnUtility.NewGeneratedStartingPawn)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_StartingPawnUtility_NewGeneratedStartingPawn_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) }),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_PawnGenerator_GeneratePawn_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.SpawnSetup)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Pawn_SpawnSetup_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.PreApplyDamage)),
                prefix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Pawn_PreApplyDamage_Prefix))
                {
                    priority = Priority.First
                },
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Pawn_PreApplyDamage_Postfix))
                {
                    priority = Priority.Last
                });

            patcher.Patch(AccessTools.Method(typeof(InspirationWorker), nameof(InspirationWorker.CommonalityFor)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_InspirationWorker_CommonalityFor_Postfix)));

            patcher.Patch(
                AccessTools.Method(typeof(Projectile), "CheckForFreeInterceptBetween", new[] { typeof(Vector3), typeof(Vector3) }),
                prefix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Projectile_CheckForFreeInterceptBetween_Prefix)));
        }

        public static void Patch_StartingPawnUtility_NewGeneratedStartingPawn_Postfix(Pawn __result)
        {
            if (!MX_QHUtility.IsQinghe(__result))
            {
                return;
            }

            MX_QHUtility.MarkForLoadoutStabilization(__result);
            MX_QHUtility.EnsureDefaultLoadout(__result);
        }

        public static void Patch_PawnGenerator_GeneratePawn_Postfix(ref Pawn __result)
        {
            if (!MX_QHUtility.IsQinghe(__result))
            {
                return;
            }

            MX_QHUtility.EnsureDefaultLoadout(__result);
        }

        public static void Patch_Pawn_SpawnSetup_Postfix(Pawn __instance)
        {
            if (!MX_QHUtility.IsQinghe(__instance))
            {
                return;
            }

            EnsureQingheCoreTraits(__instance);
            FlowerCourtUtility.EnsureFlowerCourtSystems(__instance);
            if (MX_QHUtility.ShouldFinalizeLoadout(__instance))
            {
                MX_QHUtility.EnsureDefaultLoadout(__instance);
                MX_QHUtility.ClearLoadoutStabilization(__instance);
            }
        }

        public static bool Patch_Pawn_PreApplyDamage_Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (__instance?.health?.hediffSet == null)
            {
                return true;
            }

            if (dinfo.Amount <= 0f)
            {
                return true;
            }

            if (!HasLongBreathDamageImmunity(__instance))
            {
                return true;
            }

            dinfo.SetAmount(0f);
            absorbed = true;
            return false;
        }

        public static void Patch_Pawn_PreApplyDamage_Postfix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (__instance?.health?.hediffSet == null)
            {
                return;
            }

            Hediff longBreath = __instance.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_LongBreath);
            HediffComp_LongBreathWard longBreathComp = longBreath?.TryGetComp<HediffComp_LongBreathWard>();
            if (longBreathComp == null)
            {
                return;
            }

            // Lotus shield is processed by pawn ThingComp.PostPreApplyDamage.
            // LongBreath only checks when damage still reaches the body.
            if (absorbed)
            {
                return;
            }

            longBreathComp.NotifyDamageNotAbsorbed(ref dinfo);

            if (!longBreathComp.CanTrigger(ref dinfo))
            {
                return;
            }

            longBreathComp.Trigger(ref dinfo, ref absorbed);
        }

        public static void Patch_InspirationWorker_CommonalityFor_Postfix(InspirationWorker __instance, Pawn pawn, ref float __result)
        {
            if (pawn?.story?.traits == null || __instance?.def == null)
            {
                return;
            }

            if (MX_QHDefOf.MX_QH_FlowerWord_JadeHairpin == null
                || !pawn.story.traits.HasTrait(MX_QHDefOf.MX_QH_FlowerWord_JadeHairpin))
            {
                return;
            }

            if (__instance.def == MX_QHDefOf.Frenzy_Work || __instance.def == MX_QHDefOf.Inspired_Creativity)
            {
                __result *= 2f;
            }
        }

        public static bool Patch_Projectile_CheckForFreeInterceptBetween_Prefix(
            Projectile __instance,
            Vector3 lastExactPos,
            Vector3 newExactPos,
            ref bool __result)
        {
            if (__instance?.Map == null || __instance.Destroyed || MX_QHDefOf.MX_QH_FlowerMandate_Wintersweet == null)
            {
                return true;
            }

            var shields = __instance.Map.listerThings.ThingsOfDef(MX_QHDefOf.MX_QH_FlowerMandate_Wintersweet);
            for (int i = 0; i < shields.Count; i++)
            {
                if (shields[i]?.TryGetComp<CompFlowerMandate_WintersweetShield>()?.TryInterceptProjectile(__instance, lastExactPos, newExactPos) == true)
                {
                    ImpactBlockedByShield(__instance);
                    __result = true;
                    return false;
                }
            }

            return true;
        }

        private static bool HasLongBreathDamageImmunity(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || MX_QHDefOf.MX_QH_LongBreathDamageImmunity == null)
            {
                return false;
            }

            return pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_LongBreathDamageImmunity) != null;
        }

        private static void EnsureQingheCoreTraits(Pawn pawn)
        {
            if (pawn?.story?.traits == null)
            {
                return;
            }

            if (MX_QHDefOf.MX_QH_Trait_LongBreath != null
                && !pawn.story.traits.HasTrait(MX_QHDefOf.MX_QH_Trait_LongBreath))
            {
                pawn.story.traits.GainTrait(new Trait(MX_QHDefOf.MX_QH_Trait_LongBreath));
            }

            if (MX_QHDefOf.MX_QH_Trait_WaterFairy != null
                && !pawn.story.traits.HasTrait(MX_QHDefOf.MX_QH_Trait_WaterFairy))
            {
                pawn.story.traits.GainTrait(new Trait(MX_QHDefOf.MX_QH_Trait_WaterFairy));
            }
        }

        private static void RegisterFlowerBellCorrosionArmorStatParts()
        {
            RegisterFlowerBellCorrosionArmorStatPart(StatDefOf.ArmorRating_Sharp);
            RegisterFlowerBellCorrosionArmorStatPart(StatDefOf.ArmorRating_Blunt);
            RegisterFlowerBellCorrosionArmorStatPart(StatDefOf.ArmorRating_Heat);
        }

        private static void RegisterFlowerBellCorrosionArmorStatPart(StatDef stat)
        {
            if (stat == null)
            {
                return;
            }

            if (stat.parts == null)
            {
                stat.parts = new List<StatPart>();
            }

            for (int i = 0; i < stat.parts.Count; i++)
            {
                if (stat.parts[i] is StatPart_FlowerBellCorrosionArmor)
                {
                    return;
                }
            }

            stat.parts.Add(new StatPart_FlowerBellCorrosionArmor
            {
                parentStat = stat
            });
        }

        private static void RegisterFlowerBellCooldownStatPart()
        {
            StatDef stat = StatDefOf.RangedWeapon_Cooldown;
            if (stat == null)
            {
                return;
            }

            if (stat.parts == null)
            {
                stat.parts = new List<StatPart>();
            }

            for (int i = 0; i < stat.parts.Count; i++)
            {
                if (stat.parts[i] is StatPart_FlowerBellCooldown)
                {
                    return;
                }
            }

            stat.parts.Add(new StatPart_FlowerBellCooldown
            {
                parentStat = stat
            });
        }

        private static readonly MethodInfo ProjectileImpactMethod =
            AccessTools.Method(typeof(Projectile), "Impact", new[] { typeof(Thing), typeof(bool) });

        private static void ImpactBlockedByShield(Projectile projectile)
        {
            MethodInfo impactMethod = AccessTools.Method(projectile.GetType(), "Impact", new[] { typeof(Thing), typeof(bool) })
                ?? ProjectileImpactMethod;
            impactMethod?.Invoke(projectile, new object[] { null, true });
        }

    }
}

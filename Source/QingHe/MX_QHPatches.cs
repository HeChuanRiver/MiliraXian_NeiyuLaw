using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    [StaticConstructorOnStartup]
    public static class MX_QHPatches
    {
        private static readonly Harmony patcher = new Harmony("MiliraXian.Characters.QingHe");

        static MX_QHPatches()
        {
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

            patcher.Patch(
                AccessTools.Method(
                    typeof(Verb),
                    nameof(Verb.TryStartCastOn),
                    new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo), typeof(bool), typeof(bool), typeof(bool), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Verb_TryStartCastOn_Postfix)));
        }

        public static void Patch_Pawn_SpawnSetup_Postfix(Pawn __instance)
        {
            if (!MX_QHUtility.IsQinghe(__instance))
            {
                return;
            }

            PawnSpecialResourceUtility.EnsureSpecialResourceComp(__instance, MX_QHDefOf.MX_QH_Tempest);
            PawnSpecialResourceUtility.EnsureSpecialResourceComp(__instance, MX_QHDefOf.MX_QH_Elegance);

            EnsureLongBreathState(__instance);
            EnsureWaterFairyTrait(__instance);
            EnsureSpringRegenState(__instance);
            EnsureLotusShieldBinding(__instance);
            EnsureQingheStatusGizmoState(__instance);
            SyncEleganceAbilityByCurrentWeapon(__instance);
        }

        public static bool Patch_Pawn_PreApplyDamage_Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (__instance?.health?.hediffSet == null)
            {
                return true;
            }

            if (dinfo.Def != null && dinfo.Instigator != __instance)
            {
                EleganceUtility.NotifyCombatEvent(__instance);

                var attacker = dinfo.Instigator as Pawn;
                if (attacker != null && attacker != __instance && attacker.HostileTo(__instance))
                {
                    EleganceUtility.NotifyCombatEvent(attacker);
                }
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

        public static void Patch_Verb_TryStartCastOn_Postfix(
            Verb __instance,
            LocalTargetInfo castTarg,
            bool __result)
        {
            if (!__result || __instance?.verbProps == null)
            {
                return;
            }

            if (__instance is Verb_CastAbility)
            {
                return;
            }

            if (!__instance.verbProps.violent)
            {
                return;
            }

            if (!__instance.verbProps.IsMeleeAttack && !__instance.verbProps.Ranged)
            {
                return;
            }

            Pawn caster = __instance.CasterPawn;
            Thing targetThing = castTarg.HasThing ? castTarg.Thing : null;
            if (caster == null || targetThing == null || !caster.HostileTo(targetThing))
            {
                return;
            }

            EleganceUtility.NotifyCombatEvent(caster);
        }

        private static bool HasLongBreathDamageImmunity(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || MX_QHDefOf.MX_QH_LongBreathDamageImmunity == null)
            {
                return false;
            }

            return pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_LongBreathDamageImmunity) != null;
        }

        private static void EnsureLongBreathState(Pawn pawn)
        {
            if (pawn?.health?.hediffSet != null && MX_QHDefOf.MX_QH_LongBreath != null)
            {
                if (pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_LongBreath) == null)
                {
                    pawn.health.AddHediff(HediffMaker.MakeHediff(MX_QHDefOf.MX_QH_LongBreath, pawn));
                }
            }
        }

        private static void EnsureWaterFairyTrait(Pawn pawn)
        {
            if (pawn?.story?.traits == null || MX_QHDefOf.MX_QH_Trait_WaterFairy == null)
            {
                return;
            }

            Trait oldLongBreathTrait = MX_QHDefOf.MX_QH_Trait_LongBreath != null
                ? pawn.story.traits.GetTrait(MX_QHDefOf.MX_QH_Trait_LongBreath)
                : null;
            if (oldLongBreathTrait != null)
            {
                pawn.story.traits.RemoveTrait(oldLongBreathTrait);
            }

            if (!pawn.story.traits.HasTrait(MX_QHDefOf.MX_QH_Trait_WaterFairy))
            {
                pawn.story.traits.GainTrait(new Trait(MX_QHDefOf.MX_QH_Trait_WaterFairy));
            }
        }

        private static void EnsureSpringRegenState(Pawn pawn)
        {
            if (pawn?.health?.hediffSet != null && MX_QHDefOf.MX_QH_SpringRegen != null)
            {
                if (pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_SpringRegen) == null)
                {
                    pawn.health.AddHediff(HediffMaker.MakeHediff(MX_QHDefOf.MX_QH_SpringRegen, pawn));
                }
            }
        }

        private static void EnsureLotusShieldBinding(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null || MX_QHDefOf.MX_QH_LotusShield == null)
            {
                return;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_LotusShield);
            HediffComp_LotusShield comp = (hediff as HediffWithComps)?.GetComp<HediffComp_LotusShield>();
            comp?.EnsureShieldBound();
        }

        private static void EnsureQingheStatusGizmoState(Pawn pawn)
        {
            if (pawn?.health?.hediffSet != null && MX_QHDefOf.MX_QH_QingheStatusGizmo != null)
            {
                if (pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_QingheStatusGizmo) == null)
                {
                    pawn.health.AddHediff(HediffMaker.MakeHediff(MX_QHDefOf.MX_QH_QingheStatusGizmo, pawn));
                }
            }
        }

        private static void SyncEleganceAbilityByCurrentWeapon(Pawn pawn)
        {
            if (pawn?.abilities == null)
            {
                return;
            }

            RemoveAbility(pawn, "MX_Qinghe_Elegance_HengZhi");
            RemoveAbility(pawn, "MX_Qinghe_Elegance_DuanHun");
            RemoveAbility(pawn, "MX_Qinghe_Elegance_YangChun");

            ThingDef primaryDef = pawn.equipment?.Primary?.def;
            if (primaryDef == null)
            {
                return;
            }

            if (primaryDef.defName == "MX_Qinghe_Form_Pipa")
            {
                EnsureAbility(pawn, "MX_Qinghe_Elegance_HengZhi");
                return;
            }

            if (primaryDef.defName == "MX_Qinghe_Form_Zhudi")
            {
                EnsureAbility(pawn, "MX_Qinghe_Elegance_DuanHun");
                return;
            }

            if (primaryDef.defName == "MX_Qinghe_Form_Qin")
            {
                EnsureAbility(pawn, "MX_Qinghe_Elegance_YangChun");
            }
        }

        private static void EnsureAbility(Pawn pawn, string abilityDefName)
        {
            AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail(abilityDefName);
            if (def == null)
            {
                return;
            }

            if (pawn.abilities.GetAbility(def, includeTemporary: false) == null)
            {
                pawn.abilities.GainAbility(def);
            }
        }

        private static void RemoveAbility(Pawn pawn, string abilityDefName)
        {
            AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail(abilityDefName);
            if (def == null)
            {
                return;
            }

            pawn.abilities.RemoveAbility(def);
        }

    }
}

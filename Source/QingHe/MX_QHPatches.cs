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
        }

        public static void Patch_Pawn_SpawnSetup_Postfix(Pawn __instance)
        {
            if (!MX_QHUtility.IsQinghe(__instance))
            {
                return;
            }

            PawnSpecialResourceUtility.EnsureSpecialResourceComp(__instance, MX_QHDefOf.MX_QH_Tempest);
            PawnSpecialResourceUtility.EnsureSpecialResourceComp(__instance, MX_QHDefOf.MX_QH_Elegance);

            SyncEleganceAbilityByCurrentWeapon(__instance);
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

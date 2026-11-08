using System.Text;
using HarmonyLib;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Neiyu.Cultivation
{
    // PreActivate is the one vanilla cast commitment point. Existing Ability objects, charges and
    // cooldowns survive upgrades/load; only a new cast receives its authored tier's cooldown.
    [HarmonyPatch(typeof(Ability), "PreActivate")]
    internal static class Patch_NeiyuCultivation_CastCooldown
    {
        private static void Postfix(Ability __instance)
        {
            if (NeiyuPowerBalance.AbilitiesDisabled || !NeiyuEquipmentUtility.IsNeiyu(__instance.pawn)) return;
            var values = CultivationService.AbilityValues(__instance.pawn, __instance.def);
            if (values == null || values.cooldownTicks < 0 || !__instance.HasCooldown
                || __instance.def.groupDef != null || __instance.CooldownTicksTotal == values.cooldownTicks) return;
            int charges = __instance.RemainingCharges;
            __instance.StartCooldown(values.cooldownTicks);
            __instance.RemainingCharges = charges;
        }
    }

    // The vanilla ability verb inherits this getter. A scoped hook also covers old saved verbs,
    // without replacing their trackers or adding one Ability subclass per character/condition.
    [HarmonyPatch(typeof(Verb), nameof(Verb.EffectiveRange), MethodType.Getter)]
    internal static class Patch_NeiyuCultivation_AbilityRange
    {
        private static void Postfix(Verb __instance, ref float __result)
        {
            if (__instance is not Verb_CastAbility { ability: { } ability }
                || !NeiyuEquipmentUtility.IsNeiyu(ability.pawn)) return;
            var values = CultivationService.AbilityValues(ability.pawn, ability.def);
            if (values?.range >= 0f) __result = values.range;
        }
    }

    // AbilityDef caches its summary per pawn using the shared verb range. Instance tooltips
    // need the live range and node values instead; the Def and its cache remain untouched.
    [HarmonyPatch(typeof(Ability), nameof(Ability.Tooltip), MethodType.Getter)]
    internal static class Patch_NeiyuCultivation_AbilityTooltip
    {
        private static void Postfix(Ability __instance, ref string __result)
        {
            if (!NeiyuEquipmentUtility.IsNeiyu(__instance.pawn)
                || __instance.CompOfType<CompAbilityEffect_NeiyuCultivationInfo>() == null) return;
            StringBuilder text = new();
            text.AppendLine(__instance.def.LabelCap);
            text.AppendLine();
            if (NeiyuPowerBalance.AbilitiesDisabled)
            {
                text.Append(NeiyuPowerBalance.AbilitiesDisabledReason);
            }
            else
            {
                text.AppendLine(__instance.def.description);
                text.AppendLine();
                text.AppendLine("施法：" + __instance.def.verbProperties.warmupTime.ToString("0.##") + "秒");
                var values = CultivationService.AbilityValues(__instance.pawn, __instance.def);
                if (__instance.verb.EffectiveRange > 0f && values?.range is not >= 0f)
                    text.AppendLine("射程：" + __instance.verb.EffectiveRange.ToString("0.##") + "格");
                foreach (var effect in __instance.EffectComps)
                {
                    string extra = effect.ExtraTooltipPart();
                    if (!extra.NullOrEmpty()) text.AppendLine(extra);
                }
                if (__instance.UsesCharges) text.AppendLine("剩余次数：" + __instance.RemainingCharges);
            }
            __result = text.ToString().TrimEnd();
        }
    }

    public sealed class CompProperties_AbilityNeiyuCultivationInfo : CompProperties_AbilityEffect
    {
        public CompProperties_AbilityNeiyuCultivationInfo() => compClass = typeof(CompAbilityEffect_NeiyuCultivationInfo);
    }
    public sealed class CompAbilityEffect_NeiyuCultivationInfo : CompAbilityEffect
    {
        public override string ExtraTooltipPart() => NeiyuPowerBalance.AbilitiesDisabled
            ? NeiyuPowerBalance.AbilitiesDisabledReason
            : CultivationEffectText.SkillSummary(CultivationService.AbilityValues(parent.pawn, parent.def));
    }
}

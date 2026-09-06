using MiliraXian.Characters.Neiyu;
using RimWorld;
using UnityEngine;
using Verse;
using static MiliraXian.Characters.CharacterPowerProfile;

namespace MiliraXian.Characters.Zhaoli
{
    internal static class ZhaoliPowerBalance
    {
        internal static readonly CharacterPowerProfile Profile = new CharacterPowerProfile();
        public static bool IsOriginal => Profile.Original;
        public static bool IsBalanced => Profile.Balanced;
        public static bool Sealed => Profile.Sealed;
        public static void SetLevel(CharacterPowerLevel level) => Profile.SetLevel(level);

        internal static void Initialize()
        {
            var p = Profile;
            p.LibraryPassives("MiliraXian_Zhaoli");
            p.Weapon("MX_Zhaoli_DuanzhanBlade", 16f, 2.5f);
            p.Armor("MX_ZhaoliNormal");
            p.Armor("MX_ZhaoliHood");
            var clothes = Thing("MX_ZhaoliNormal");
            p.ScaleStat(clothes, "CarryingCapacity", ConservativePowerTuning.Bonus, 0f, true);
            p.ScaleStat(clothes, "MeleeDodgeChance", ConservativePowerTuning.Bonus, 0f, true);
            p.ScaleStat(clothes, "MoveSpeed", ConservativePowerTuning.Bonus, 0f, true);
            p.ScaleStat(Thing("MX_ZhaoliHood"), "MentalBreakThreshold", ConservativePowerTuning.Bonus, 0f, true);

            p.Ability("MX_Zhaoli_Duanzhan", 12000, 5f);
            var slash = AbilityComp<CompProperties_AbilityDuanzhan>("MX_Zhaoli_Duanzhan");
            p.ScaleField(slash, "damageAmount", ConservativePowerTuning.Damage, 0f);
            p.ScaleField(slash, "lineDamageMultiplier", ConservativePowerTuning.Bonus, 1f, 1f);
            p.ScaleField(slash, "armorPenetration", ConservativePowerTuning.Defense, 0f);
            p.KeepField(slash, "impactRadius", 0f);
            p.KeepField(slash, "lineLengthCells", 0f);

            p.Ability("MX_Zhaoli_DeathField", 18000, 24f);
            p.KeepField(AbilityComp<CompProperties_AbilityZhaoliDeathField>("MX_Zhaoli_DeathField"), "radius", 0f);
            var field = HediffComp<HediffCompProperties_ZhaoliDeathField>("MXZL_ZhaoliDeathFieldActive");
            p.KeepField(field, "radius", 0f);
            // Preserve the complete field, sentence and link cycles; only scale damage/cost.
            p.KeepField(field, "fieldDurationTicks", 0);
            p.ScaleField(HediffComp<HediffCompProperties_ZhaoliDeathSentence>("MX_AbnormalDeathSentence"), "cutSeverity", ConservativePowerTuning.Damage, 0f);

            p.Ability("MX_Zhaoli_Guiyi", 15000, 20f);
            p.ScaleField(AbilityComp<CompProperties_AbilityZhaoliGuiyi>("MX_Zhaoli_Guiyi"), "karmaCost", ConservativePowerTuning.Cooldown, 2f);
            p.Ability("MX_Zhaoli_Dingshu", 180000, 15f);
            var revive = AbilityComp<CompProperties_AbilityZhaoliDingshu>("MX_Zhaoli_Dingshu");
            p.ScaleField(revive, "karmaCost", ConservativePowerTuning.Cooldown, 8f);
            p.ScaleField(revive, "channelDurationTicks", ConservativePowerTuning.Cooldown, 5000);
            var links = HediffComp<HediffCompProperties_ZhaoliKarmaLinks>("MXZL_ZhaoliKarma");
            p.KeepField(links, "maxLinks", 0);
            p.KeepField(links, "linkDurationTicks", 300000);

            p.Ability("MX_Zhaoli_Minghuo", 30000, 1.9f);
            p.KeepField(AbilityComp<CompProperties_AbilityZhaoliMinghuo>("MX_Zhaoli_Minghuo"), "durationTicks", 0);
            var flame = HediffComp<HediffCompProperties_ZhaoliMinghuo>("MXZL_ZhaoliMinghuo");
            p.ScaleField(flame, "damageMultiplier", ConservativePowerTuning.Bonus, 1f, 1f);
            p.ScaleField(flame, "armorPenetrationMultiplier", ConservativePowerTuning.Bonus, 1f, 1f);
            p.ScaleField(flame, "hitChanceMultiplier", ConservativePowerTuning.Bonus, 1f, 1f);
            p.ScaleField(flame, "attackSpeedMultiplier", ConservativePowerTuning.Bonus, 1f, 1f);
            p.ScaleField(flame, "rangeOffset", ConservativePowerTuning.Bonus, 0f);
            p.ScaleField(flame, "fireDamageFactor", ConservativePowerTuning.Damage, 0f);

            p.Ability("MX_Zhaoli_Minshen", 18000, 24f);
            var mind = AbilityComp<CompProperties_AbilityZhaoliMinshen>("MX_Zhaoli_Minshen");
            p.KeepField(mind, "areaWidth", 0);
            p.KeepField(mind, "areaHeight", 0);
            p.ScaleField(mind, "dazeChance", ConservativePowerTuning.Bonus, 0f);
            p.KeepField(mind, "mentalStateDurationTicks", 0);
            p.ScaleField(mind, "empDamage", ConservativePowerTuning.Damage, 0f);
            p.ScaleField(Hediff("MXZL_ZhaoliMinshenSlow").stages[0].statFactors[0], "value", ConservativePowerTuning.Bonus, 1f, 1f);

            var mindDamage = HediffComp<HediffCompProperties_ZhaoliMinshenDamage>("MXZL_ZhaoliMinshenDamage");
            p.ScaleField(mindDamage, "damagePerTick", ConservativePowerTuning.Damage, mindDamage.damagePerTick);

            foreach (string name in new[] { "Duanzhan", "DeathField", "Guiyi", "Dingshu", "Minghuo", "Minshen" })
                p.Description(AbilityDef("MX_Zhaoli_" + name), "MX_Power_Zhaoli_" + name);
            p.Description(Hediff("MXZL_ZhaoliRebirth"), "MX_Power_Zhaoli_Passives");
            p.Description(Hediff("MXZL_ZhaoliKarma"), "MX_Power_Zhaoli_Passives");
            p.Description(Hediff("MXZL_ZhaoliKarmaLink"), "MX_Power_Zhaoli_Passives");
            p.Description(Hediff("MX_AbnormalDeathSentence"), "MX_Power_Zhaoli_DeathField");
            p.Description(Hediff("MXZL_ZhaoliDeathFieldActive"), "MX_Power_Zhaoli_DeathField");
            p.Description(Hediff("MXZL_ZhaoliMinshenSlow"), "MX_Power_Zhaoli_Minshen");
            p.Description(Hediff("MXZL_ZhaoliMinshenDamage"), "MX_Power_Zhaoli_Minshen");
            p.Description(Hediff("MXZL_ZhaoliMinghuo"), "MX_Power_Zhaoli_Minghuo");
            p.Description(Hediff("MXZL_ZhaoliShieldLayers"), "MX_Power_Zhaoli_Shield");
            p.Description(clothes, "MX_Power_BalancedEquipment");
            p.Description(Thing("MX_ZhaoliHood"), "MX_Power_BalancedEquipment");
            p.Apply();
        }

        public static float GrowthFactor(float original) => IsOriginal ? original : Sealed ? 1f : original == 0f ? 0f : Mathf.Lerp(1f, original, ConservativePowerTuning.Bonus);
    }

    public abstract class CompAbilityEffect_ZhaoliPowerLimited : CompAbilityEffect_CharacterPowerLimited
    {
        protected override bool PowerSealed => ZhaoliPowerBalance.Sealed;
    }

    [StaticConstructorOnStartup]
    internal static class ZhaoliPowerBalanceBootstrap
    {
        static ZhaoliPowerBalanceBootstrap() { ZhaoliPowerBalance.Initialize(); }
    }
}

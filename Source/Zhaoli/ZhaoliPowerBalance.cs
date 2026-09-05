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
            p.Weapon("MX_Zhaoli_DuanzhanBlade", 24f, .30f, 2.2f, 16f);
            p.Armor("MX_ZhaoliNormal", .85f, .4f, .5f);
            p.Armor("MX_ZhaoliHood", .4f, .3f, .3f);
            var clothes = Thing("MX_ZhaoliNormal");
            p.Stat(clothes, "CarryingCapacity", 12f, 0f, true);
            p.Stat(clothes, "MeleeDodgeChance", .06f, 0f, true);
            p.Stat(clothes, "MoveSpeed", .15f, 0f, true);
            p.Stat(Thing("MX_ZhaoliHood"), "MentalBreakThreshold", -.05f, 0f, true);

            p.Ability("MX_Zhaoli_Duanzhan", 12000, 5f);
            var slash = AbilityComp<CompProperties_AbilityDuanzhan>("MX_Zhaoli_Duanzhan");
            p.Field(slash, "damageAmount", 26f, 0f);
            p.Field(slash, "lineDamageMultiplier", 1.15f, 1f);
            p.Field(slash, "armorPenetration", .3f, 0f);
            p.Field(slash, "impactRadius", 3f, 0f);
            p.Field(slash, "lineLengthCells", 5f, 0f);

            p.Ability("MX_Zhaoli_DeathField", 18000, 24f);
            p.Field(AbilityComp<CompProperties_AbilityZhaoliDeathField>("MX_Zhaoli_DeathField"), "radius", 4.9f, 0f);
            var field = HediffComp<HediffCompProperties_ZhaoliDeathField>("MXZL_ZhaoliDeathFieldActive");
            p.Field(field, "radius", 4.9f, 0f);
            // Disappears ticks before the field comp; keep the ninth pulse even on a 60-tick boundary.
            p.Field(field, "fieldDurationTicks", 541, 0);
            p.Field(HediffComp<HediffCompProperties_ZhaoliDeathSentence>("MX_AbnormalDeathSentence"), "cutSeverity", 1f, 0f);

            p.Ability("MX_Zhaoli_Guiyi", 15000, 20f);
            p.Field(AbilityComp<CompProperties_AbilityZhaoliGuiyi>("MX_Zhaoli_Guiyi"), "karmaCost", 2f, 2f);
            p.Ability("MX_Zhaoli_Dingshu", 180000, 15f);
            var revive = AbilityComp<CompProperties_AbilityZhaoliDingshu>("MX_Zhaoli_Dingshu");
            p.Field(revive, "karmaCost", 8f, 8f);
            p.Field(revive, "channelDurationTicks", 5000, 5000);
            var links = HediffComp<HediffCompProperties_ZhaoliKarmaLinks>("MXZL_ZhaoliKarma");
            p.Field(links, "maxLinks", 3, 0);
            p.Field(links, "linkDurationTicks", 300000, 300000);

            p.Ability("MX_Zhaoli_Minghuo", 30000, 1.9f);
            p.Field(AbilityComp<CompProperties_AbilityZhaoliMinghuo>("MX_Zhaoli_Minghuo"), "durationTicks", 5000, 0);
            var flame = HediffComp<HediffCompProperties_ZhaoliMinghuo>("MXZL_ZhaoliMinghuo");
            p.Field(flame, "damageMultiplier", 1.10f, 1f);
            p.Field(flame, "armorPenetrationMultiplier", 1.10f, 1f);
            p.Field(flame, "hitChanceMultiplier", 1.05f, 1f);
            p.Field(flame, "attackSpeedMultiplier", 1.08f, 1f);
            p.Field(flame, "rangeOffset", .5f, 0f);
            p.Field(flame, "fireDamageFactor", .10f, 0f);

            p.Ability("MX_Zhaoli_Minshen", 18000, 24f);
            var mind = AbilityComp<CompProperties_AbilityZhaoliMinshen>("MX_Zhaoli_Minshen");
            p.Field(mind, "areaWidth", 7, 0);
            p.Field(mind, "areaHeight", 7, 0);
            p.Field(mind, "dazeChance", .08f, 0f);
            p.Field(mind, "mentalStateDurationTicks", 360, 0);
            p.Field(mind, "empDamage", 10f, 0f);
            p.Field(Hediff("MXZL_ZhaoliMinshenSlow").stages[0].statFactors[0], "value", .65f, 1f);

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

        public static float GrowthFactor(float original) => IsOriginal ? original : Sealed ? 1f : Mathf.Lerp(1f, original, .25f);
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

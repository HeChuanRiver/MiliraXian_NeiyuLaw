using RimWorld;
using Verse;
using MiliraXian.Characters.Common;
using static MiliraXian.Characters.Common.CharacterPowerProfile;

namespace MiliraXian.Characters.Zhaoli
{
    internal static class ZhaoliPowerBalance
    {
        internal static readonly CharacterPowerProfile Profile = new CharacterPowerProfile();
        public static bool Sealed => Profile.Sealed;
        public static void SetDecorative(bool decorative) => Profile.SetDecorative(decorative);

        internal static void Initialize()
        {
            var p = Profile;
            p.Weapon("MX_Zhaoli_DuanzhanBlade", 16f, 2.5f);
            p.Armor("MX_ZhaoliNormal");
            p.Armor("MX_ZhaoliHood");
            var clothes = Thing("MX_ZhaoliNormal");
            p.Stat(clothes, "CarryingCapacity", 0f, true);
            p.Stat(clothes, "MeleeDodgeChance", 0f, true);
            p.Stat(clothes, "MoveSpeed", 0f, true);
            p.Stat(clothes, "IncomingDamageFactor", 0f, true);
            p.Stat(clothes, "MeleeDamageFactor", 0f, true);
            var hood = Thing("MX_ZhaoliHood");
            p.Stat(hood, "MentalBreakThreshold", 0f, true);
            p.Stat(hood, "MeleeCooldownFactor", 0f, true);
            p.Stat(hood, "StaggerDurationFactor", 0f, true);

            p.Ability("MX_Zhaoli_Duanzhan", 12000, 5f);
            var slash = AbilityComp<CompProperties_AbilityDuanzhan>("MX_Zhaoli_Duanzhan");
            p.Field(slash, "damageAmount", 0f);
            p.Field(slash, "lineDamageMultiplier", 1f);
            p.Field(slash, "armorPenetration", 0f);
            p.Field(slash, "impactRadius", 0f);
            p.Field(slash, "lineLengthCells", 0f);

            p.Ability("MX_Zhaoli_DeathField", 18000, 24f);
            p.Field(AbilityComp<CompProperties_AbilityZhaoliDeathField>("MX_Zhaoli_DeathField"), "radius", 0f);
            var field = HediffComp<HediffCompProperties_ZhaoliDeathField>("MXZL_ZhaoliDeathFieldActive");
            p.Field(field, "radius", 0f);
            p.Field(field, "fieldDurationTicks", 0);
            p.Field(HediffComp<HediffCompProperties_ZhaoliDeathSentence>("MX_AbnormalDeathSentence"), "cutSeverity", 0f);

            p.Ability("MX_Zhaoli_Guiyi", 15000, 20f);
            p.Field(AbilityComp<CompProperties_AbilityZhaoliGuiyi>("MX_Zhaoli_Guiyi"), "karmaCost", 2f);
            p.Ability("MX_Zhaoli_Dingshu", 180000, 15f);
            var revive = AbilityComp<CompProperties_AbilityZhaoliDingshu>("MX_Zhaoli_Dingshu");
            p.Field(revive, "karmaCost", 8f);
            p.Field(revive, "channelDurationTicks", 5000);
            var links = HediffComp<HediffCompProperties_ZhaoliKarmaLinks>("MXZL_ZhaoliKarma");
            p.Field(links, "maxLinks", 0);
            p.Field(links, "linkDurationTicks", 300000);

            p.Ability("MX_Zhaoli_Minghuo", 30000, 1.9f);
            p.Field(AbilityComp<CompProperties_AbilityZhaoliMinghuo>("MX_Zhaoli_Minghuo"), "durationTicks", 0);
            var flame = HediffComp<HediffCompProperties_ZhaoliMinghuo>("MXZL_ZhaoliMinghuo");
            p.Field(flame, "damageMultiplier", 1f);
            p.Field(flame, "armorPenetrationMultiplier", 1f);
            p.Field(flame, "hitChanceMultiplier", 1f);
            p.Field(flame, "attackSpeedMultiplier", 1f);
            p.Field(flame, "rangeOffset", 0f);
            p.Field(flame, "fireDamageFactor", 0f);

            p.Ability("MX_Zhaoli_Minshen", 18000, 24f);
            var mind = AbilityComp<CompProperties_AbilityZhaoliMinshen>("MX_Zhaoli_Minshen");
            p.Field(mind, "areaWidth", 0);
            p.Field(mind, "areaHeight", 0);
            p.Field(mind, "dazeChance", 0f);
            p.Field(mind, "mentalStateDurationTicks", 0);
            p.Field(mind, "empDamage", 0f);
            p.Field(Hediff("MXZL_ZhaoliMinshenSlow").stages[0].statFactors[0], "value", 1f);

            foreach (string name in new[] { "Duanzhan", "DeathField", "Guiyi", "Dingshu", "Minghuo", "Minshen" })
            {
                if (name != "Guiyi") p.Description(AbilityDef("MX_Zhaoli_" + name));
            }
            var guiYi = AbilityDef("MX_Zhaoli_Guiyi");
            const string animalKey = "MX_ZL_GuiyiAnimalsHealOnly";
            string animalNote = animalKey.CanTranslate() ? animalKey.Translate().ToString() : "动物可以接受治疗，但不会建立因果链接。";
            if (!guiYi.description.EndsWith(animalNote)) guiYi.description += "\n" + animalNote;
            p.Value(() => guiYi.description, value => guiYi.description = value,
                "MX_Power_SealedDescription".Translate().ToString());
            p.Description(Hediff("MXZL_ZhaoliRebirth"));
            p.Description(Hediff("MXZL_ZhaoliKarma"));
            p.Description(Hediff("MXZL_ZhaoliKarmaLink"));
            p.Description(Hediff("MX_AbnormalDeathSentence"));
            p.Description(Hediff("MXZL_ZhaoliDeathFieldActive"));
            p.Description(Hediff("MXZL_ZhaoliMinshenSlow"));
            p.Description(Hediff("MXZL_ZhaoliMinshenDamage"));
            p.Description(Hediff("MXZL_ZhaoliMinghuo"));
            var shield = Hediff("MXZL_ZhaoliShieldLayers");
            p.Value(() => shield.description, value => shield.description = value,
                "MX_Power_PassiveInactive".Translate().ToString());
            foreach (ThingDef equipment in new[] { clothes, hood, Thing("MX_Zhaoli_DuanzhanBlade") })
                p.Value(() => equipment.description, value => equipment.description = value,
                    "MX_Power_EquipmentInactive".Translate().ToString());
            p.Apply();
        }

        public static float GrowthFactor(float original) => Sealed ? 1f : original;
    }

    public abstract class CompAbilityEffect_ZhaoliPowerLimited : CompAbilityEffect_CharacterPowerLimited
    {
        protected override bool PowerSealed => ZhaoliPowerBalance.Sealed;

        public override bool AICanTargetNow(LocalTargetInfo target)
        {
            // Raid casts are scheduled by the raid state, including its phase and weave rules.
            return !ZhaoliScenarioUtility.IsRaidState(parent?.pawn) && base.AICanTargetNow(target);
        }
    }

    [StaticConstructorOnStartup]
    internal static class ZhaoliPowerBalanceBootstrap
    {
        static ZhaoliPowerBalanceBootstrap() { ZhaoliPowerBalance.Initialize(); }
    }
}

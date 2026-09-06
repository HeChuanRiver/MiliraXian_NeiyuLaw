using System.Collections.Generic;
using HarmonyLib;
using MiliraXian.Characters.Neiyu;
using RimWorld;
using UnityEngine;
using Verse;
using static MiliraXian.Characters.CharacterPowerProfile;

namespace MiliraXian.Characters.Mingyuan
{
    internal static class MingyuanPowerBalance
    {
        internal static readonly CharacterPowerProfile Profile = new CharacterPowerProfile();
        internal static DamageDef ArrowDamage;
        public static bool IsOriginal => Profile.Original;
        public static bool IsBalanced => Profile.Balanced;
        public static bool Sealed => Profile.Sealed;
        public static void SetLevel(CharacterPowerLevel level) => Profile.SetLevel(level);

        internal static void Initialize()
        {
            ArrowDamage = DefDatabase<DamageDef>.GetNamed("Arrow");
            var p = Profile;
            p.LibraryPassives("MiliraXian_Mingyuan");
            p.Weapon("MX_Mingyuan_CinderSword", 16f, 2.5f);
            var sword = Thing("MX_Mingyuan_CinderSword");
            p.ScaleStat(sword, "MeleeDodgeChance", ConservativePowerTuning.Bonus, 0f, true);
            p.ScaleStat(sword, "MeleeWeapon_DamageMultiplier", ConservativePowerTuning.Bonus, 0f, true);
            p.Armor("MX_Mingyuan_InfernoArmor");
            p.Armor("MX_Mingyuan_BurningFeatherCrown");
            var armor = Thing("MX_Mingyuan_InfernoArmor");
            p.ScaleStat(armor, "CarryingCapacity", ConservativePowerTuning.Bonus, 0f, true);
            p.ScaleStat(armor, "MoveSpeed", ConservativePowerTuning.Bonus, 0f, true);
            p.ScaleStat(armor, "MeleeDodgeChance", ConservativePowerTuning.Bonus, 0f, true);
            p.ScaleStat(armor, "IncomingDamageFactor", ConservativePowerTuning.Bonus, 0f, true);
            var crown = Thing("MX_Mingyuan_BurningFeatherCrown");
            p.ScaleStat(crown, "MeleeDodgeChance", ConservativePowerTuning.Bonus, 0f, true);
            p.ScaleStat(crown, "IncomingDamageFactor", ConservativePowerTuning.Bonus, 0f, true);
            p.ScaleStat(crown, "AimingDelayFactor", ConservativePowerTuning.Bonus, 0f, true);
            p.ScaleStat(crown, "RangedCooldownFactor", ConservativePowerTuning.Bonus, 0f, true);

            var bow = ThingComp<CompProperties_MingyuanRainbowBow>("MX_Mingyuan_RainbowBow");
            p.ScaleField(bow, "focusWarmupSeconds", ConservativePowerTuning.Cooldown, 3.2f);
            p.KeepField(bow, "focusRange", 25.9f);
            p.KeepField(bow, "radiationRange", 9f);
            p.KeepField(bow, "radiationArcDegrees", 90f);
            p.KeepField(bow, "radiationMinIntervalTicks", 120);
            p.ScaleField(bow, "radiationDamage", ConservativePowerTuning.Damage, 0f);
            p.ScaleField(bow, "radiationLayerFraction", .9f, 0f);
            var bowDef = Thing("MX_Mingyuan_RainbowBow");
            p.KeepField(bowDef.Verbs[0], "range", 25.9f);
            p.ScaleField(bowDef.Verbs[0], "warmupTime", ConservativePowerTuning.Cooldown, 3.2f);
            p.ScaleStat(bowDef, "AccuracyTouch", 1f, .7f);
            p.ScaleStat(bowDef, "AccuracyShort", 1f, .7f);
            p.ScaleStat(bowDef, "AccuracyMedium", 1f, .7f);
            p.ScaleStat(bowDef, "AccuracyLong", 1f, .6f);
            p.Description(bowDef, "MX_Power_Mingyuan_Bow");
            p.Description(sword, "MX_Power_Mingyuan_Sword");
            p.Description(armor, "MX_Power_BalancedEquipment");
            p.Description(crown, "MX_Power_BalancedEquipment");
            p.ScaleField(Thing("MX_Bullet_Mingyuan_RainbowArrow").projectile, "armorPenetrationBase", ConservativePowerTuning.Defense, .1f);

            p.Ability("MX_Mingyuan_AscendantFlameDash", 2500, 24f);
            var dash = AbilityComp<CompProperties_AbilityMingyuanAscendantFlameDash>("MX_Mingyuan_AscendantFlameDash");
            p.KeepField(dash, "maxDistance", 24f);
            p.ScaleField(dash, "pathDamage", ConservativePowerTuning.Damage, 0f);
            p.ScaleField(dash, "lifeBurnLayers", .9f, 0f);
            p.KeepField(dash, "selfLifeBurnLayers", 0f);
            p.KeepField(dash, "stunTicks", 0);
            var scorch = ThingComp<CompProperties_MingyuanAscendantFlameScorch>("MX_Mingyuan_AscendantFlameScorchController");
            p.KeepField(scorch, "durationTicks", 0);
            p.KeepField(scorch, "pulseIntervalTicks", 120);
            p.ScaleField(scorch, "lifeBurnLayers", .9f, 0f);

            p.Ability("MX_Mingyuan_InstantCombustion", 30000, 8f);
            var flash = AbilityComp<CompProperties_AbilityMingyuanInstantCombustion>("MX_Mingyuan_InstantCombustion");
            p.KeepField(flash, "radius", 0f);
            p.ScaleField(flash, "partDamage", ConservativePowerTuning.Damage, 0f);
            p.KeepField(flash, "stunTicks", 0);
            p.ScaleField(flash, "minimumLifeBurnLayers", .9f, 0f);

            p.Ability("MX_Mingyuan_BurningPillar", 45000, 25f);
            var pillar = ThingComp<CompProperties_MingyuanBurningPillarTornado>("MX_Mingyuan_BurningPillarField");
            p.KeepField(pillar, "maxRadius", 0f);
            p.KeepField(pillar, "durationTicks", 0);
            p.KeepField(pillar, "radiusGrowTicks", 180);
            p.KeepField(pillar, "controlUnlockTicks", 180);
            p.KeepField(pillar, "coreGrowTicks", 300);
            p.ScaleField(pillar, "initialHitPoints", .9f, 1);
            p.ScaleField(pillar, "maxHitPoints", .9f, 1);
            p.KeepField(pillar, "pulseIntervalTicks", 120);
            p.ScaleField(pillar, "centerBurnDamage", ConservativePowerTuning.Damage, 0f);
            p.ScaleField(pillar, "edgeBurnDamage", ConservativePowerTuning.Damage, 0f);
            p.ScaleField(pillar, "centerCutDamage", ConservativePowerTuning.Damage, 0f);
            p.ScaleField(pillar, "edgeCutDamage", ConservativePowerTuning.Damage, 0f);
            p.ScaleField(pillar, "buildingDamageFraction", ConservativePowerTuning.Damage, 0f);

            p.Ability("MX_Mingyuan_TimeBurn", 45000, 25f);
            p.ScaleField(AbilityDef("MX_Mingyuan_TimeBurn").verbProperties, "warmupTime", ConservativePowerTuning.Cooldown, 3f);
            p.KeepField(AbilityComp<CompProperties_AbilityMingyuanTimeBurn>("MX_Mingyuan_TimeBurn"), "durationTicks", 0);

            p.Ability("MX_Mingyuan_AshesOfSelf", 12000, 0f);
            var ashes = AbilityComp<CompProperties_AbilityMingyuanAshesOfSelf>("MX_Mingyuan_AshesOfSelf");
            p.ScaleField(ashes, "bloodLossCost", 1f, 0f);
            p.KeepField(ashes, "selfBurnLayers", 0f);
            p.KeepField(ashes, "fieldDurationTicks", 0);
            var aura = ThingComp<CompProperties_MingyuanBurningField>("MX_Mingyuan_AshesField");
            p.KeepField(aura, "durationTicks", 0);
            p.KeepField(aura, "pulseIntervalTicks", 120);
            p.ScaleField(aura, "damageAmount", ConservativePowerTuning.Damage, 0f);
            p.ScaleField(aura, "lifeBurnLayers", .9f, 0f);
            p.ScaleField(aura, "selfBurnLifeBurnPer100", ConservativePowerTuning.Bonus, 0f);
            p.ScaleField(aura, "selfHealAmount", .9f, 0f);
            p.KeepField(aura, "maxSelfBurnGainPerPulse", 0f);
            p.KeepField(aura, "destroyBuildings", false);
            p.KeepField(aura, "destroyAnimals", false);

            var life = HediffComp<HediffCompProperties_MingyuanLifeBurn>("MX_Mingyuan_LifeBurn");
            p.ScaleField(life, "damagePer100Layers", ConservativePowerTuning.Damage, 0f);
            p.ScaleField(life, "baseDamage", ConservativePowerTuning.Damage, 0f);
            p.ScaleField(life, "ageTicksPerLayer", ConservativePowerTuning.Bonus, 0f);
            p.ScaleField(life, "needDrainPer100Layers", ConservativePowerTuning.Bonus, 0f);
            p.KeepField(life, "burnSelfStackFraction", 0f);
            p.KeepField(life, "decayDelayTicks", 0);
            p.KeepField(life, "transferRadius", 0f);
            p.KeepField(life, "maxTransferTargets", 0);
            var self = HediffComp<HediffCompProperties_MingyuanSelfBurn>("MX_Mingyuan_SelfBurn");
            p.ScaleField(self, "overburnDamageFactor", ConservativePowerTuning.Bonus, 1f, 1f);
            p.ScaleField(self, "overburnLifeBurnFactor", ConservativePowerTuning.Bonus, 1f, 1f);
            p.ScaleField(self, "rangedWeaponDamagePerLayer", ConservativePowerTuning.Bonus, 0f);
            p.ScaleField(self, "rangedWeaponDamageBonusCap", ConservativePowerTuning.Bonus, 0f);
            p.KeepField(self, "combatRegenIntervalTicks", 120);
            p.KeepField(self, "combatRegenLayers", 0f);
            p.KeepField(self, "overburnDecayLayers", 0f);
            var body = HediffComp<HediffCompProperties_MingyuanBurningBody>("MX_Mingyuan_BurningBody");
            p.ScaleField(body, "restoreIntervalTicks", ConservativePowerTuning.Cooldown, 2500);
            p.ScaleField(body, "invulnerableTicks", .9f, 0);
            p.ScaleField(body, "reflectLifeBurnLayers", .9f, 0f);
            p.KeepField(body, "selfBurnOnHit", 0f);
            p.ScaleField(body, "meleeLifeBurnLayers", .9f, 0f);
            p.ScaleField(body, "rangedLifeBurnLayers", .9f, 0f);
            p.ScaleField(body, "meleeSelfBurnBonusPer100", ConservativePowerTuning.Bonus, 0f);
            p.ScaleField(body, "rangedSelfBurnBonusPer100", ConservativePowerTuning.Bonus, 0f);
            var shield = HediffComp<HediffCompProperties_MingyuanProtectiveFlameShield>("MX_Mingyuan_ProtectiveFlameShield");
            p.ScaleField(shield, "maxEnergy", .9f, 0f);
            p.ScaleField(shield, "repairIntervalTicks", ConservativePowerTuning.Cooldown, 600);
            p.KeepField(shield, "selfBurnPerEnergy", 4f);
            p.KeepField(shield, "selfBurnRefillMaxFractionOfCap", 0f);
            p.ScaleField(shield, "selfBurnRefillCooldownTicks", ConservativePowerTuning.Cooldown, 2500);

            ConfigureStages();
            foreach (string name in new[] { "AscendantFlameDash", "InstantCombustion", "BurningPillar", "TimeBurn", "AshesOfSelf" })
                p.Description(AbilityDef("MX_Mingyuan_" + name), "MX_Power_Mingyuan_" + name);
            foreach (string name in new[] { "LifeBurn", "SelfBurn", "BurningBody", "ProtectiveFlameShield", "RebirthFlame" })
                p.Description(Hediff("MX_Mingyuan_" + name), "MX_Power_Mingyuan_" + name);
            p.Apply();
        }

        private static void ConfigureStages()
        {
            // Copy the loaded stages instead of replacing them with a featureless profile.
            // Capacity modifiers, need exemptions, thresholds and every curve axis survive.
            foreach (string name in new[] { "BurningBody", "SelfBurn", "LifeBurn" })
            {
                HediffDef def = Hediff("MX_Mingyuan_" + name);
                var balanced = new List<HediffStage>();
                var selfProps = name == "SelfBurn" ? HediffComp<HediffCompProperties_MingyuanSelfBurn>(def.defName) : null;
                foreach (HediffStage original in def.stages)
                {
                    HediffStage stage = (HediffStage)AccessTools.Method(typeof(object), "MemberwiseClone").Invoke(original, null);
                    if (original.statFactors != null)
                    {
                        stage.statFactors = new List<StatModifier>();
                        foreach (StatModifier stat in original.statFactors)
                        {
                            float value = StageFactor(name, stat.stat.defName, stat.value);
                            if (selfProps != null && original.minSeverity > selfProps.overburnThreshold
                                && stat.stat.defName == "MeleeDamageFactor" && selfProps.overburnDamageFactor > 0f)
                            {
                                // Keep melee and skill Overburn multipliers in agreement.
                                value = ConservativePowerTuning.Scale(stat.value / selfProps.overburnDamageFactor, ConservativePowerTuning.Bonus, 1f)
                                    * ConservativePowerTuning.Scale(selfProps.overburnDamageFactor, ConservativePowerTuning.Bonus, 1f);
                            }
                            stage.statFactors.Add(new StatModifier { stat = stat.stat, value = value });
                        }
                    }
                    if (original.statFactorsBySeverity != null)
                    {
                        stage.statFactorsBySeverity = new List<StatModifierBySeverity>();
                        foreach (StatModifierBySeverity stat in original.statFactorsBySeverity)
                        {
                            var curve = new SimpleCurve();
                            foreach (CurvePoint point in stat.valueBySeverity.Points)
                                curve.Add(new CurvePoint(point.x, StageFactor(name, stat.stat.defName, point.y)));
                            stage.statFactorsBySeverity.Add(new StatModifierBySeverity { stat = stat.stat, valueBySeverity = curve });
                        }
                    }
                    balanced.Add(stage);
                }
                Profile.Field(def, "stages", balanced, new List<HediffStage> { new HediffStage() });
            }
        }

        private static float StageFactor(string hediff, string stat, float original)
        {
            if (original == 0f) return original; // Immunity is a mechanic, not a small bonus.
            if (hediff == "BurningBody" && stat == "IncomingDamageFactor")
                return Mathf.Min(1f, original * 1.5f);
            if (hediff == "SelfBurn" && (stat == "MeleeCooldownFactor" || stat == "RangedCooldownFactor"))
                return 1f / ConservativePowerTuning.Scale(1f / original, ConservativePowerTuning.Bonus, 1f);
            return ConservativePowerTuning.Scale(original, ConservativePowerTuning.Bonus, 1f);
        }

    }

    public abstract class CompAbilityEffect_MingyuanPowerLimited : CompAbilityEffect_CharacterPowerLimited
    {
        protected override bool PowerSealed => MingyuanPowerBalance.Sealed;
    }

    [StaticConstructorOnStartup]
    internal static class MingyuanPowerBalanceBootstrap
    {
        static MingyuanPowerBalanceBootstrap() { MingyuanPowerBalance.Initialize(); }
    }
}

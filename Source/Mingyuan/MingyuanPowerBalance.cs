using System.Collections.Generic;
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
            p.Weapon("MX_Mingyuan_CinderSword", 24f, .30f, 2.2f, 16f);
            var sword = Thing("MX_Mingyuan_CinderSword");
            p.Stat(sword, "MeleeDodgeChance", .03f, 0f, true);
            p.Stat(sword, "MeleeWeapon_DamageMultiplier", 0f, 0f, true);
            p.Armor("MX_Mingyuan_InfernoArmor", .8f, .4f, .65f);
            p.Armor("MX_Mingyuan_BurningFeatherCrown", .55f, .3f, .5f);
            var armor = Thing("MX_Mingyuan_InfernoArmor");
            p.Stat(armor, "CarryingCapacity", 12f, 0f, true);
            p.Stat(armor, "MoveSpeed", .10f, 0f, true);
            p.Stat(armor, "MeleeDodgeChance", .04f, 0f, true);
            p.Stat(armor, "IncomingDamageFactor", 0f, 0f, true);
            var crown = Thing("MX_Mingyuan_BurningFeatherCrown");
            p.Stat(crown, "MeleeDodgeChance", .02f, 0f, true);
            p.Stat(crown, "IncomingDamageFactor", 0f, 0f, true);
            p.Stat(crown, "AimingDelayFactor", -.05f, 0f, true);
            p.Stat(crown, "RangedCooldownFactor", 0f, 0f, true);

            var bow = ThingComp<CompProperties_MingyuanRainbowBow>("MX_Mingyuan_RainbowBow");
            p.Field(bow, "focusWarmupSeconds", 3f, 3.2f);
            p.Field(bow, "focusRange", 30.9f, 25.9f);
            p.Field(bow, "radiationRange", 9f, 9f);
            p.Field(bow, "radiationArcDegrees", 90f, 90f);
            p.Field(bow, "radiationMinIntervalTicks", 120, 120);
            p.Field(bow, "radiationDamage", 2f, 0f);
            p.Field(bow, "radiationLayerFraction", .05f, 0f);
            var bowDef = Thing("MX_Mingyuan_RainbowBow");
            p.Field(bowDef.Verbs[0], "range", 30.9f, 25.9f);
            p.Field(bowDef.Verbs[0], "warmupTime", 3f, 3.2f);
            p.Stat(bowDef, "AccuracyTouch", .8f, .7f);
            p.Stat(bowDef, "AccuracyShort", .8f, .7f);
            p.Stat(bowDef, "AccuracyMedium", .85f, .7f);
            p.Stat(bowDef, "AccuracyLong", .7f, .6f);
            p.Description(bowDef, "MX_Power_Mingyuan_Bow");
            p.Description(sword, "MX_Power_Mingyuan_Sword");
            p.Description(armor, "MX_Power_BalancedEquipment");
            p.Description(crown, "MX_Power_BalancedEquipment");
            p.Field(Thing("MX_Bullet_Mingyuan_RainbowArrow").projectile, "armorPenetrationBase", .3f, .1f);

            p.Ability("MX_Mingyuan_AscendantFlameDash", 2500, 24f);
            var dash = AbilityComp<CompProperties_AbilityMingyuanAscendantFlameDash>("MX_Mingyuan_AscendantFlameDash");
            p.Field(dash, "maxDistance", 24f, 24f);
            p.Field(dash, "pathDamage", 8f, 0f);
            p.Field(dash, "lifeBurnLayers", 12f, 0f);
            p.Field(dash, "selfLifeBurnLayers", 20f, 0f);
            p.Field(dash, "stunTicks", 45, 0);
            var scorch = ThingComp<CompProperties_MingyuanAscendantFlameScorch>("MX_Mingyuan_AscendantFlameScorchController");
            p.Field(scorch, "durationTicks", 300, 0);
            p.Field(scorch, "pulseIntervalTicks", 120, 120);
            p.Field(scorch, "lifeBurnLayers", 3f, 0f);

            p.Ability("MX_Mingyuan_InstantCombustion", 30000, 8f);
            var flash = AbilityComp<CompProperties_AbilityMingyuanInstantCombustion>("MX_Mingyuan_InstantCombustion");
            p.Field(flash, "radius", 8f, 0f);
            p.Field(flash, "partDamage", 8f, 0f);
            p.Field(flash, "stunTicks", 90, 0);
            p.Field(flash, "minimumLifeBurnLayers", 10f, 0f);

            p.Ability("MX_Mingyuan_BurningPillar", 45000, 25f);
            var pillar = ThingComp<CompProperties_MingyuanBurningPillarTornado>("MX_Mingyuan_BurningPillarField");
            p.Field(pillar, "maxRadius", 4.5f, 0f);
            p.Field(pillar, "durationTicks", 900, 0);
            p.Field(pillar, "radiusGrowTicks", 180, 180);
            p.Field(pillar, "controlUnlockTicks", 180, 180);
            p.Field(pillar, "coreGrowTicks", 300, 300);
            p.Field(pillar, "initialHitPoints", 80, 1);
            p.Field(pillar, "maxHitPoints", 160, 1);
            p.Field(pillar, "pulseIntervalTicks", 120, 120);
            p.Field(pillar, "centerBurnDamage", 3f, 0f);
            p.Field(pillar, "edgeBurnDamage", 1f, 0f);
            p.Field(pillar, "centerCutDamage", 1f, 0f);
            p.Field(pillar, "edgeCutDamage", 3f, 0f);
            p.Field(pillar, "buildingDamageFraction", .01f, 0f);

            p.Ability("MX_Mingyuan_TimeBurn", 45000, 25f);
            p.Field(AbilityDef("MX_Mingyuan_TimeBurn").verbProperties, "warmupTime", 3f, 3f);
            p.Field(AbilityComp<CompProperties_AbilityMingyuanTimeBurn>("MX_Mingyuan_TimeBurn"), "durationTicks", 240, 0);

            p.Ability("MX_Mingyuan_AshesOfSelf", 12000, 0f);
            var ashes = AbilityComp<CompProperties_AbilityMingyuanAshesOfSelf>("MX_Mingyuan_AshesOfSelf");
            p.Field(ashes, "bloodLossCost", .12f, 0f);
            p.Field(ashes, "selfBurnLayers", 80f, 0f);
            p.Field(ashes, "fieldDurationTicks", 600, 0);
            var aura = ThingComp<CompProperties_MingyuanBurningField>("MX_Mingyuan_AshesField");
            p.Field(aura, "durationTicks", 600, 0);
            p.Field(aura, "pulseIntervalTicks", 120, 120);
            p.Field(aura, "damageAmount", 1f, 0f);
            p.Field(aura, "lifeBurnLayers", 3f, 0f);
            p.Field(aura, "selfBurnLifeBurnPer100", 1f, 0f);
            p.Field(aura, "selfHealAmount", .5f, 0f);
            p.Field(aura, "maxSelfBurnGainPerPulse", 3f, 0f);
            p.Field(aura, "destroyBuildings", false, false);
            p.Field(aura, "destroyAnimals", false, false);

            var life = HediffComp<HediffCompProperties_MingyuanLifeBurn>("MX_Mingyuan_LifeBurn");
            p.Field(life, "damagePer100Layers", 1f, 0f);
            p.Field(life, "baseDamage", .5f, 0f);
            p.Field(life, "ageTicksPerLayer", 0f, 0f);
            p.Field(life, "needDrainPer100Layers", 0f, 0f);
            p.Field(life, "burnSelfStackFraction", 0f, 0f);
            p.Field(life, "decayDelayTicks", 600, 0);
            p.Field(life, "transferRadius", 6f, 0f);
            p.Field(life, "maxTransferTargets", 3, 0);
            var self = HediffComp<HediffCompProperties_MingyuanSelfBurn>("MX_Mingyuan_SelfBurn");
            p.Field(self, "overburnDamageFactor", 1.08f, 1f);
            p.Field(self, "overburnLifeBurnFactor", 1.15f, 1f);
            p.Field(self, "rangedWeaponDamagePerLayer", .0004f, 0f);
            p.Field(self, "rangedWeaponDamageBonusCap", .12f, 0f);
            p.Field(self, "combatRegenIntervalTicks", 120, 120);
            p.Field(self, "combatRegenLayers", 1f, 0f);
            p.Field(self, "overburnDecayLayers", 2f, 0f);
            var body = HediffComp<HediffCompProperties_MingyuanBurningBody>("MX_Mingyuan_BurningBody");
            p.Field(body, "restoreIntervalTicks", 2500, 2500);
            p.Field(body, "invulnerableTicks", 0, 0);
            p.Field(body, "reflectLifeBurnLayers", 2f, 0f);
            p.Field(body, "selfBurnOnHit", 3f, 0f);
            p.Field(body, "meleeLifeBurnLayers", 4f, 0f);
            p.Field(body, "rangedLifeBurnLayers", 2f, 0f);
            p.Field(body, "meleeSelfBurnBonusPer100", 1f, 0f);
            p.Field(body, "rangedSelfBurnBonusPer100", .5f, 0f);
            var shield = HediffComp<HediffCompProperties_MingyuanProtectiveFlameShield>("MX_Mingyuan_ProtectiveFlameShield");
            p.Field(shield, "maxEnergy", 80f, 0f);
            p.Field(shield, "repairIntervalTicks", 600, 600);
            p.Field(shield, "selfBurnPerEnergy", 4f, 4f);
            p.Field(shield, "selfBurnRefillMaxFractionOfCap", .2f, 0f);
            p.Field(shield, "selfBurnRefillCooldownTicks", 2500, 2500);

            ConfigureStages();
            foreach (string name in new[] { "AscendantFlameDash", "InstantCombustion", "BurningPillar", "TimeBurn", "AshesOfSelf" })
                p.Description(AbilityDef("MX_Mingyuan_" + name), "MX_Power_Mingyuan_" + name);
            foreach (string name in new[] { "LifeBurn", "SelfBurn", "BurningBody", "ProtectiveFlameShield", "RebirthFlame" })
                p.Description(Hediff("MX_Mingyuan_" + name), "MX_Power_Mingyuan_" + name);
            p.Apply();
        }

        private static StatModifier Factor(string name, float value) => new StatModifier { stat = DefDatabase<StatDef>.GetNamed(name), value = value };
        private static StatModifierBySeverity Curve(string name, float end)
        {
            return new StatModifierBySeverity { stat = DefDatabase<StatDef>.GetNamed(name),
                valueBySeverity = new SimpleCurve { new CurvePoint(0f, 1f), new CurvePoint(300f, end) } };
        }

        private static void ConfigureStages()
        {
            var body = Hediff("MX_Mingyuan_BurningBody");
            Profile.Field(body, "stages", new List<HediffStage> { new HediffStage {
                statFactors = new List<StatModifier> { Factor("IncomingDamageFactor", .9f) } } },
                new List<HediffStage> { new HediffStage() });
            var self = Hediff("MX_Mingyuan_SelfBurn");
            Profile.Field(self, "stages", new List<HediffStage> {
                new HediffStage { statFactorsBySeverity = new List<StatModifierBySeverity> {
                    Curve("MoveSpeed", 1.10f), Curve("WorkSpeedGlobal", 1.15f), Curve("MeleeDamageFactor", 1.18f),
                    Curve("MeleeCooldownFactor", 1f / 1.12f), Curve("RangedCooldownFactor", 1f / 1.12f) } },
                new HediffStage { minSeverity = 300.01f, statFactors = new List<StatModifier> {
                    Factor("MoveSpeed", 1.10f), Factor("WorkSpeedGlobal", 1.15f), Factor("MeleeDamageFactor", 1.18f * 1.08f),
                    Factor("MeleeCooldownFactor", 1f / 1.12f), Factor("RangedCooldownFactor", 1f / 1.12f) } } },
                new List<HediffStage> { new HediffStage() });
            // 100 layers are an absolute burst threshold, not a percentage of target health.
            var stage = new HediffStage { statFactorsBySeverity = new List<StatModifierBySeverity>() };
            foreach (string name in new[] { "MoveSpeed", "MeleeHitChance", "ShootingAccuracyPawn", "WorkSpeedGlobal", "MeleeDamageFactor" })
            {
                var curve = Curve(name, .9f);
                curve.valueBySeverity = new SimpleCurve { new CurvePoint(0f, 1f), new CurvePoint(100f, .9f) };
                stage.statFactorsBySeverity.Add(curve);
            }
            Profile.Field(Hediff("MX_Mingyuan_LifeBurn"), "stages", new List<HediffStage> { stage }, new List<HediffStage> { new HediffStage() });
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

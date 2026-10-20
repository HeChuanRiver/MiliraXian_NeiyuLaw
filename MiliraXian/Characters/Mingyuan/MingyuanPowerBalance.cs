using System.Collections.Generic;
using RimWorld;
using Verse;
using MiliraXian.Characters.Common;
using static MiliraXian.Characters.Common.CharacterPowerProfile;

namespace MiliraXian.Characters.Mingyuan
{
    internal static class MingyuanPowerBalance
    {
        internal static readonly CharacterPowerProfile Profile = new CharacterPowerProfile();
        internal static DamageDef ArrowDamage;
        public static bool Sealed => Profile.Sealed;
        public static void SetDecorative(bool decorative) => Profile.SetDecorative(decorative);

        internal static void Initialize()
        {
            ArrowDamage = DefDatabase<DamageDef>.GetNamed("Arrow");
            var p = Profile;
            p.Weapon("MX_Mingyuan_CinderSword", 16f, 2.5f);
            var sword = Thing("MX_Mingyuan_CinderSword");
            p.Stat(sword, "MeleeDodgeChance", 0f, true);
            p.Stat(sword, "MeleeWeapon_DamageMultiplier", 0f, true);
            p.Armor("MX_Mingyuan_InfernoArmor");
            p.Armor("MX_Mingyuan_BurningFeatherCrown");
            var armor = Thing("MX_Mingyuan_InfernoArmor");
            p.Stat(armor, "CarryingCapacity", 0f, true);
            p.Stat(armor, "MoveSpeed", 0f, true);
            p.Stat(armor, "MeleeDodgeChance", 0f, true);
            p.Stat(armor, "IncomingDamageFactor", 0f, true);
            var crown = Thing("MX_Mingyuan_BurningFeatherCrown");
            p.Stat(crown, "MeleeDodgeChance", 0f, true);
            p.Stat(crown, "IncomingDamageFactor", 0f, true);
            p.Stat(crown, "AimingDelayFactor", 0f, true);
            p.Stat(crown, "RangedCooldownFactor", 0f, true);

            var bow = ThingComp<CompProperties_MingyuanRainbowBow>("MX_Mingyuan_RainbowBow");
            p.Field(bow, "focusWarmupSeconds", 3.2f);
            p.Field(bow, "focusRange", 25.9f);
            p.Field(bow, "focusBaseDamage", 17f);
            p.Field(bow, "focusDamagePerLayer", 0f);
            p.Field(bow, "focusMaxDamage", 17f);
            p.Field(bow, "radiationRange", 9f);
            p.Field(bow, "radiationArcDegrees", 90f);
            p.Field(bow, "radiationMinIntervalTicks", 120);
            p.Field(bow, "radiationDamage", 0f);
            p.Field(bow, "radiationLayerFraction", 0f);
            p.Field(bow, "radiationBaseLayers", 0f);
            p.Field(bow, "radiationMaxLayers", 0f);
            var bowDef = Thing("MX_Mingyuan_RainbowBow");
            p.Field(bowDef.Verbs[0], "range", 25.9f);
            p.Field(bowDef.Verbs[0], "warmupTime", 3.2f);
            p.Stat(bowDef, "AccuracyTouch", .7f);
            p.Stat(bowDef, "AccuracyShort", .7f);
            p.Stat(bowDef, "AccuracyMedium", .7f);
            p.Stat(bowDef, "AccuracyLong", .6f);
            p.Description(bowDef);
            p.Description(sword);
            p.Description(armor);
            p.Description(crown);
            p.Field(Thing("MX_Bullet_Mingyuan_RainbowArrow").projectile, "armorPenetrationBase", .1f);

            p.Ability("MX_Mingyuan_AscendantFlameDash", 2500, 24f);
            var dash = AbilityComp<CompProperties_AbilityMingyuanAscendantFlameDash>("MX_Mingyuan_AscendantFlameDash");
            p.Field(dash, "maxDistance", 24f);
            p.Field(dash, "pathDamage", 0f);
            p.Field(dash, "lifeBurnLayers", 0f);
            p.Field(dash, "selfLifeBurnLayers", 0f);
            p.Field(dash, "stunTicks", 0);
            var scorch = ThingComp<CompProperties_MingyuanAscendantFlameScorch>("MX_Mingyuan_AscendantFlameScorchController");
            p.Field(scorch, "durationTicks", 0);
            p.Field(scorch, "pulseIntervalTicks", 120);
            p.Field(scorch, "lifeBurnLayers", 0f);

            p.Ability("MX_Mingyuan_InstantCombustion", 30000, 8f);
            var flash = AbilityComp<CompProperties_AbilityMingyuanInstantCombustion>("MX_Mingyuan_InstantCombustion");
            p.Field(flash, "radius", 0f);
            p.Field(flash, "partDamage", 0f);
            p.Field(flash, "stunTicks", 0);
            p.Field(flash, "minimumLifeBurnLayers", 0f);

            p.Ability("MX_Mingyuan_BurningPillar", 45000, 25f);
            var pillar = ThingComp<CompProperties_MingyuanBurningPillarTornado>("MX_Mingyuan_BurningPillarField");
            p.Field(pillar, "maxRadius", 0f);
            p.Field(pillar, "durationTicks", 0);
            p.Field(pillar, "radiusGrowTicks", 180);
            p.Field(pillar, "controlUnlockTicks", 180);
            p.Field(pillar, "coreGrowTicks", 300);
            p.Field(pillar, "initialHitPoints", 1);
            p.Field(pillar, "maxHitPoints", 1);
            p.Field(pillar, "pulseIntervalTicks", 120);
            p.Field(pillar, "centerBurnDamage", 0f);
            p.Field(pillar, "edgeBurnDamage", 0f);
            p.Field(pillar, "centerCutDamage", 0f);
            p.Field(pillar, "edgeCutDamage", 0f);
            p.Field(pillar, "buildingDamageFraction", 0f);

            p.Ability("MX_Mingyuan_TimeBurn", 45000, 25f);
            p.Field(AbilityDef("MX_Mingyuan_TimeBurn").verbProperties, "warmupTime", 3f);
            p.Field(AbilityComp<CompProperties_AbilityMingyuanTimeBurn>("MX_Mingyuan_TimeBurn"), "durationTicks", 0);

            p.Ability("MX_Mingyuan_AshesOfSelf", 12000, 0f);
            var ashes = AbilityComp<CompProperties_AbilityMingyuanAshesOfSelf>("MX_Mingyuan_AshesOfSelf");
            p.Field(ashes, "bloodLossCost", 0f);
            p.Field(ashes, "selfBurnLayers", 0f);
            p.Field(ashes, "fieldDurationTicks", 0);
            var aura = ThingComp<CompProperties_MingyuanBurningField>("MX_Mingyuan_AshesField");
            p.Field(aura, "durationTicks", 0);
            p.Field(aura, "pulseIntervalTicks", 120);
            p.Field(aura, "damageAmount", 0f);
            p.Field(aura, "lifeBurnLayers", 0f);
            p.Field(aura, "selfBurnLifeBurnPer100", 0f);
            p.Field(aura, "selfHealAmount", 0f);
            p.Field(aura, "maxSelfBurnGainPerPulse", 0f);
            p.Field(aura, "destroyBuildings", false);
            p.Field(aura, "destroyAnimals", false);

            var life = HediffComp<HediffCompProperties_MingyuanLifeBurn>("MX_Mingyuan_LifeBurn");
            p.Field(life, "damagePer100Layers", 0f);
            p.Field(life, "baseDamage", 0f);
            p.Field(life, "ageTicksPerLayer", 0f);
            p.Field(life, "needDrainPer100Layers", 0f);
            p.Field(life, "burnSelfStackFraction", 0f);
            p.Field(life, "decayDelayTicks", 0);
            p.Field(life, "transferRadius", 0f);
            p.Field(life, "maxTransferTargets", 0);
            var self = HediffComp<HediffCompProperties_MingyuanSelfBurn>("MX_Mingyuan_SelfBurn");
            p.Field(self, "overburnDamageFactor", 1f);
            p.Field(self, "overburnLifeBurnFactor", 1f);
            p.Field(self, "rangedWeaponDamagePerLayer", 0f);
            p.Field(self, "rangedWeaponDamageBonusCap", 0f);
            p.Field(self, "combatRegenIntervalTicks", 120);
            p.Field(self, "combatRegenLayers", 0f);
            p.Field(self, "overburnDecayLayers", 0f);
            var body = HediffComp<HediffCompProperties_MingyuanBurningBody>("MX_Mingyuan_BurningBody");
            p.Field(body, "restoreIntervalTicks", 2500);
            p.Field(body, "restoreHealAmount", 0f);
            p.Field(body, "invulnerableTicks", 0);
            p.Field(body, "reflectLifeBurnLayers", 0f);
            p.Field(body, "selfBurnOnHit", 0f);
            p.Field(body, "meleeLifeBurnLayers", 0f);
            p.Field(body, "rangedLifeBurnLayers", 0f);
            p.Field(body, "meleeSelfBurnBonusPer100", 0f);
            p.Field(body, "rangedSelfBurnBonusPer100", 0f);
            var shield = HediffComp<HediffCompProperties_MingyuanProtectiveFlameShield>("MX_Mingyuan_ProtectiveFlameShield");
            p.Field(shield, "maxEnergy", 0f);
            p.Field(shield, "repairIntervalTicks", 600);
            p.Field(shield, "selfBurnPerEnergy", 4f);
            p.Field(shield, "selfBurnRefillMaxFractionOfCap", 0f);
            p.Field(shield, "selfBurnRefillCooldownTicks", 2500);

            ConfigureStages();
            foreach (string name in new[] { "AscendantFlameDash", "InstantCombustion", "BurningPillar", "TimeBurn", "AshesOfSelf" })
                p.Description(AbilityDef("MX_Mingyuan_" + name));
            foreach (string name in new[] { "LifeBurn", "SelfBurn", "BurningBody", "ProtectiveFlameShield", "RebirthFlame" })
                p.Description(Hediff("MX_Mingyuan_" + name));
            p.Apply();
        }

        private static void ConfigureStages()
        {
            foreach (string name in new[] { "BurningBody", "SelfBurn", "LifeBurn" })
                Profile.Field(Hediff("MX_Mingyuan_" + name), "stages", new List<HediffStage> { new HediffStage() });
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

using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu
{
    public enum CharacterPowerLevel
    {
        Original,
        Balanced,
        Decorative
    }

    /// <summary>
    /// Applies Neiyu's selected power level once when defs finish loading and again only
    /// when the setting changes. Combat hot paths read the cached enum instead of settings.
    /// </summary>
    internal static class NeiyuPowerBalance
    {
        private interface IDefTuning
        {
            void Apply(CharacterPowerLevel level);
        }

        private sealed class DefTuning<T> : IDefTuning
        {
            private readonly Action<T> setter;
            private readonly T original;
            private readonly T balanced;
            private readonly T decorative;

            public DefTuning(Func<T> getter, Action<T> setter, T balanced, T decorative)
            {
                this.setter = setter;
                original = getter();
                this.balanced = balanced;
                this.decorative = decorative;
            }

            public void Apply(CharacterPowerLevel level)
            {
                switch (level)
                {
                    case CharacterPowerLevel.Balanced:
                        setter(balanced);
                        break;
                    case CharacterPowerLevel.Decorative:
                        setter(decorative);
                        break;
                    default:
                        setter(original);
                        break;
                }
            }
        }

        private static readonly List<IDefTuning> DefTunings = new List<IDefTuning>();
        private static readonly FieldInfo ProjectileDamageAmountBaseField =
            AccessTools.Field(typeof(ProjectileProperties), "damageAmountBase");
        private static CharacterPowerLevel currentLevel = CharacterPowerLevel.Original;
        private static bool defsInitialized;

        public static CharacterPowerLevel CurrentLevel => currentLevel;
        public static int Revision { get; private set; }
        public static bool AbilitiesDisabled => currentLevel == CharacterPowerLevel.Decorative;
        public static bool PassivesDisabled => currentLevel == CharacterPowerLevel.Decorative;
        public static bool IsOriginal => currentLevel == CharacterPowerLevel.Original;
        public static bool IsBalanced => currentLevel == CharacterPowerLevel.Balanced;
        public static int ThunderStrikeCap => AbilitiesDisabled ? 0 : int.MaxValue;
        public static int ThunderDamageCap => IsOriginal ? int.MaxValue : IsBalanced ? balancedThunderDamage : 0;
        public static int ThunderEmpDamageCap => IsOriginal ? int.MaxValue : IsBalanced ? balancedThunderEmpDamage : 0;
        public static int BarrageShotCap => AbilitiesDisabled ? 0 : int.MaxValue;
        private static int balancedThunderDamage, balancedThunderEmpDamage;

        public static float HungerFloorPercent
        {
            get
            {
                switch (currentLevel)
                {
                    case CharacterPowerLevel.Balanced:
                        return 0.20f;
                    case CharacterPowerLevel.Decorative:
                        return 0f;
                    default:
                        return 0.20f;
                }
            }
        }

        public static string AbilitiesDisabledReason => "MX_NL_NeiyuAbilitiesSealed".Translate().ToString();

        public static void SetLevel(CharacterPowerLevel level)
        {
            CharacterPowerLevel next = IsValid(level) ? level : CharacterPowerLevel.Original;
            if (currentLevel != next) Revision++;
            currentLevel = next;
            if (defsInitialized)
            {
                ApplyDefTunings();
            }
        }

        internal static void EnsureDefsInitialized()
        {
            InitializeDefs();
        }

        public static float LimitConsciousnessMinimum(float configuredMinimum)
        {
            switch (currentLevel)
            {
                case CharacterPowerLevel.Balanced:
                    return configuredMinimum;
                case CharacterPowerLevel.Decorative:
                    return 0f;
                default:
                    return configuredMinimum;
            }
        }

        public static void WeakenPassiveProfile(ref MXNeiyuStage3Profile profile)
        {
            if (currentLevel != CharacterPowerLevel.Balanced)
            {
                return;
            }

            const float strength = ConservativePowerTuning.Bonus;
            profile.outgoingDamageFactor = Mathf.Lerp(1f, profile.outgoingDamageFactor, strength);
            profile.aimingDelayFactor = Mathf.Lerp(1f, profile.aimingDelayFactor, strength);
            profile.incomingDamageFactor = Mathf.Lerp(1f, profile.incomingDamageFactor, strength);
            profile.moveSpeedFactor = Mathf.Lerp(1f, profile.moveSpeedFactor, strength);
            profile.injuryHealingFactor = Mathf.Lerp(1f, profile.injuryHealingFactor, strength);
            profile.meleeDodgeChanceFactor = Mathf.Lerp(1f, profile.meleeDodgeChanceFactor, strength);
            profile.rangedDodgeBonusPct *= strength;
            profile.meleeArmorPenetrationFactor = Mathf.Lerp(1f, profile.meleeArmorPenetrationFactor, strength);
        }

        public static void GetWeakPenaltyFactors(out float moveSpeedFactor, out float restFallRateFactor, out float workSpeedGlobalFactor)
        {
            if (currentLevel == CharacterPowerLevel.Balanced)
            {
                moveSpeedFactor = 0.85f;
                restFallRateFactor = 1.15f;
                workSpeedGlobalFactor = 0.80f;
                return;
            }

            moveSpeedFactor = 0.50f;
            restFallRateFactor = 1.50f;
            workSpeedGlobalFactor = 0.20f;
        }

        private static bool IsValid(CharacterPowerLevel level)
        {
            return level == CharacterPowerLevel.Original
                   || level == CharacterPowerLevel.Balanced
                   || level == CharacterPowerLevel.Decorative;
        }

        private static void InitializeDefs()
        {
            if (defsInitialized)
            {
                return;
            }

            BuildWeaponTunings();
            BuildAbilityTunings();
            BuildPassiveTunings();
            defsInitialized = true;
            ApplyDefTunings();
        }

        private static void ApplyDefTunings()
        {
            for (int index = 0; index < DefTunings.Count; index++)
            {
                DefTunings[index].Apply(currentLevel);
            }
        }

        private static void BuildWeaponTunings()
        {
            ThingDef flower = ThingDefNamed("MX_Neiyu_Form_Flower");
            Tool flowerTool = FirstTool(flower);
            AddScaled(flowerTool, () => flowerTool.power, value => flowerTool.power = value, ConservativePowerTuning.Damage, 3f);
            AddScaled(flowerTool, () => flowerTool.cooldownTime, value => flowerTool.cooldownTime = value, 1f, 3f);
            AddStatBase(flower, StatDefOf.MeleeWeapon_CooldownMultiplier, 1f, 3.5f);

            ThingDef sword = ThingDefNamed("MX_Neiyu_Form_Weapon");
            Tool swordTool = FirstTool(sword);
            AddScaled(swordTool, () => swordTool.power, value => swordTool.power = value, ConservativePowerTuning.Damage, 10f);
            AddScaled(swordTool, () => swordTool.armorPenetration, value => swordTool.armorPenetration = value, ConservativePowerTuning.Defense, 0.12f);
            AddScaled(swordTool, () => swordTool.cooldownTime, value => swordTool.cooldownTime = value, 1f, 2.4f);
            AddStatBase(sword, StatDefOf.MeleeWeapon_CooldownMultiplier, 1f, 1.15f);

            ThingDef bow = ThingDefNamed("MX_Neiyu_Form_Bow");
            VerbProperties bowVerb = FirstVerb(bow);
            AddScaled(bowVerb, () => bowVerb.warmupTime, value => bowVerb.warmupTime = value, ConservativePowerTuning.Cooldown, 2.8f);
            AddScaled(bowVerb, () => bowVerb.range, value => bowVerb.range = value, 1f, 24f);

            ThingDef mainArrow = ThingDefNamed("MX_Bullet_BigSplitArrow");
            AddProjectileDamage(mainArrow?.projectile, 12);
            SplitArrowExtension split = mainArrow?.GetModExtension<SplitArrowExtension>();
            AddScaled(split, () => split.splitCount, value => split.splitCount = value, 1f, 1);
            AddScaled(split, () => split.splitRetargetChance, value => split.splitRetargetChance = value, 1f, 0f);
            AddScaled(split, () => split.splitRetargetRadius, value => split.splitRetargetRadius = value, 1f, 1f);

            ThingDef shard = ThingDefNamed("MX_Bullet_HomingShard");
            AddProjectileDamage(shard?.projectile, 6);

            ThingDef barrageArrow = ThingDefNamed("MX_Bullet_BarrageArrow");
            AddProjectileDamage(barrageArrow?.projectile, 6);
        }

        private static void BuildAbilityTunings()
        {
            AbilityDef warp = AbilityDefNamed("MX_Neiyu_WarpFeather");
            AddAbilityDescription(warp);
            AddAbilityCooldown(warp, 60000);
            CompProperties_AbilityNeiyuWarpFeather warpProps = AbilityComp<CompProperties_AbilityNeiyuWarpFeather>(warp);
            AddScaled(warpProps, () => warpProps.featherCountRange, value => warpProps.featherCountRange = value, 1f, new IntRange(1, 2));

            AbilityDef thunder = AbilityDefNamed("MX_Neiyu_ThunderMarkedStorm");
            AddAbilityDescription(thunder);
            AddAbilityCooldown(thunder, 18000);
            CompProperties_AbilityNeiyuThunderSigil thunderProps = AbilityComp<CompProperties_AbilityNeiyuThunderSigil>(thunder);
            balancedThunderDamage = thunderProps == null ? 0 : (int)ConservativePowerTuning.Number(thunderProps.damageAmount, ConservativePowerTuning.Damage);
            balancedThunderEmpDamage = thunderProps == null ? 0 : (int)ConservativePowerTuning.Number(thunderProps.empDamageAmount, ConservativePowerTuning.Damage);
            AddScaled(thunderProps, () => thunderProps.radius, value => thunderProps.radius = value, 1f, 2f);
            AddScaled(thunderProps, () => thunderProps.strikeCountRange, value => thunderProps.strikeCountRange = value, 1f, new IntRange(1, 1));
            AddScaled(thunderProps, () => thunderProps.damageAmount, value => thunderProps.damageAmount = value, ConservativePowerTuning.Damage, 10);
            AddScaled(thunderProps, () => thunderProps.empDamageAmount, value => thunderProps.empDamageAmount = value, ConservativePowerTuning.Damage, 0);

            AbilityDef barrage = AbilityDefNamed("MX_Neiyu_Bow_ArrowBarrage");
            AddAbilityDescription(barrage);
            AddAbilityCooldown(barrage, 24000);
            CompProperties_AbilityNeiyuArrowBarrage barrageProps = AbilityComp<CompProperties_AbilityNeiyuArrowBarrage>(barrage);
            AddScaled(barrageProps, () => barrageProps.shotCount, value => barrageProps.shotCount = value, 1f, 4);
            AddScaled(barrageProps, () => barrageProps.shotIntervalTicks, value => barrageProps.shotIntervalTicks = value, 1f, 10);
            AddScaled(barrageProps, () => barrageProps.maxDistance, value => barrageProps.maxDistance = value, 1f, 30f);
            AddScaled(barrageProps, () => barrageProps.lateralSpread, value => barrageProps.lateralSpread = value, 1f, 2f);

            AbilityDef blessing = AbilityDefNamed("MX_Neiyu_Flower_BlessingField");
            AddAbilityDescription(blessing);
            AddAbilityCooldown(blessing, 18000);
            CompProperties_AbilityNeiyuFlowerBless blessingProps = AbilityComp<CompProperties_AbilityNeiyuFlowerBless>(blessing);
            AddScaled(blessingProps, () => blessingProps.radius, value => blessingProps.radius = value, 1f, 2f);
            AddScaled(blessingProps, () => blessingProps.buffDurationTicks, value => blessingProps.buffDurationTicks = value, 1f, 600);

            AbilityDef toxin = AbilityDefNamed("MX_Neiyu_Flower_ToxinGarden");
            AddAbilityDescription(toxin);
            AddAbilityCooldown(toxin, 24000);
            CompProperties_AbilityNeiyuFlowerToxinField toxinProps = AbilityComp<CompProperties_AbilityNeiyuFlowerToxinField>(toxin);
            AddScaled(toxinProps, () => toxinProps.radius, value => toxinProps.radius = value, 1f, 2f);
            AddScaled(toxinProps, () => toxinProps.severeFoodPoisoningSeverity, value => toxinProps.severeFoodPoisoningSeverity = value, ConservativePowerTuning.Bonus, 0.05f);
            AddScaled(toxinProps, () => toxinProps.berserkChance, value => toxinProps.berserkChance = value, ConservativePowerTuning.Bonus, 0f);

            AbilityDef skyfall = AbilityDefNamed("MX_Neiyu_Sword_Skyfall");
            AddAbilityDescription(skyfall);
            AddAbilityCooldown(skyfall, 24000);
            CompProperties_AbilityNeiyuSwordSkyfall skyfallProps = AbilityComp<CompProperties_AbilityNeiyuSwordSkyfall>(skyfall);
            AddScaled(skyfallProps, () => skyfallProps.impactRadius, value => skyfallProps.impactRadius = value, 1f, 1.5f);
            AddScaled(skyfallProps, () => skyfallProps.impactDamage, value => skyfallProps.impactDamage = value, ConservativePowerTuning.Damage, 18);
            AddScaled(skyfallProps, () => skyfallProps.impactArmorPen, value => skyfallProps.impactArmorPen = value, ConservativePowerTuning.Defense, 0.10f);
            AddScaled(skyfallProps, () => skyfallProps.vulnerabilityDurationTicks, value => skyfallProps.vulnerabilityDurationTicks = value, 1f, 300);

            AbilityDef execution = AbilityDefNamed("MX_Neiyu_Sword_ExecuteHead");
            AddAbilityDescription(execution);
            AddAbilityCooldown(execution, 24000);
            CompProperties_AbilityNeiyuSwordExecution executionProps = AbilityComp<CompProperties_AbilityNeiyuSwordExecution>(execution);
            AddScaled(executionProps, () => executionProps.dashDamage, value => executionProps.dashDamage = value, ConservativePowerTuning.Damage, 18);
        }

        private static void BuildPassiveTunings()
        {
            ThingDef outerwear = ThingDefNamed("MiliraXian_NeiyuNormal");
            AddStatBase(outerwear, StatDefOf.ArmorRating_Sharp, ConservativePowerTuning.Defense, 0.15f);
            AddStatBase(outerwear, StatDefOf.ArmorRating_Blunt, ConservativePowerTuning.Defense, 0.08f);
            AddStatBase(outerwear, StatDefOf.ArmorRating_Heat, ConservativePowerTuning.Defense, 0.10f);
            AddStatBase(outerwear, StatDefOf.Insulation_Cold, 1f, 8f);
            AddStatBase(outerwear, StatDefOf.Insulation_Heat, 1f, 5f);
            AddEquippedOffset(outerwear, StatDefOf.CarryingCapacity, ConservativePowerTuning.Bonus, 0f);
            AddEquippedOffset(outerwear, StatDefOf.MeleeDodgeChance, ConservativePowerTuning.Bonus, 0f);
            AddEquippedOffset(outerwear, StatDefOf.MoveSpeed, ConservativePowerTuning.Bonus, 0f);

            ThingDef innerwear = ThingDefNamed("MiliraXian_NeiyuInner");
            AddStatBase(innerwear, StatDefOf.ArmorRating_Sharp, ConservativePowerTuning.Defense, 0f);
            AddStatBase(innerwear, StatDefOf.ArmorRating_Blunt, ConservativePowerTuning.Defense, 0f);
            AddStatBase(innerwear, StatDefOf.ArmorRating_Heat, ConservativePowerTuning.Defense, 0f);
            AddStatBase(innerwear, StatDefOf.Insulation_Cold, 1f, 2f);
            AddStatBase(innerwear, StatDefOf.Insulation_Heat, 1f, 2f);

            ThingDef earrings = ThingDefNamed("MX_Apparel_EarringsZhenzhu");
            AddEquippedOffset(earrings, StatDefOf.ImmunityGainSpeed, ConservativePowerTuning.Bonus, 0f);

            HediffDef blessing = HediffDefNamed("MX_Neiyu_FlowerBlessed");
            AddHediffFactor(blessing, StatDefOf.MoveSpeed, ConservativePowerTuning.Bonus, 1f);
            AddHediffFactor(blessing, StatDefOf.IncomingDamageFactor, ConservativePowerTuning.Bonus, 1f);
            AddHediffFactor(blessing, StatDefOf.ImmunityGainSpeed, ConservativePowerTuning.Bonus, 1f);

            HediffDef vulnerability = HediffDefNamed("MX_Neiyu_SwordVulnerability");
            AddHediffFactor(vulnerability, StatDefOf.IncomingDamageFactor, ConservativePowerTuning.Bonus, 1f);

            ThoughtDef blessingThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("MX_Neiyu_FlowerBlessedThought");
            AddThoughtMood(blessingThought, 0f);
            AddThoughtMood(DefDatabase<ThoughtDef>.GetNamedSilentFail("MX_Neiyu_Joyful"), 0f);
            AddThoughtMood(DefDatabase<ThoughtDef>.GetNamedSilentFail("MX_Neiyu_RelaxedNearNeiyu"), 0f);

            HediffDef shieldDef = HediffDefNamed("MXNL_NeiyuShield");
            HediffCompProperties_MXNeiyuCountShield shieldProps = HediffComp<HediffCompProperties_MXNeiyuCountShield>(shieldDef);
            AddScaled(shieldProps, () => shieldProps.phase2Threshold, value => shieldProps.phase2Threshold = value, 1f, 1f);
            AddScaled(shieldProps, () => shieldProps.phase2MaxChargesNormal, value => shieldProps.phase2MaxChargesNormal = value, .9f, 0);
            AddScaled(shieldProps, () => shieldProps.phase2MaxChargesWeak, value => shieldProps.phase2MaxChargesWeak = value, .9f, 0);
            AddScaled(shieldProps, () => shieldProps.phase2RecoverTicksNoChange, value => shieldProps.phase2RecoverTicksNoChange = value, ConservativePowerTuning.Cooldown, 60000);
            AddScaled(shieldProps, () => shieldProps.stage3AbsorbTicks, value => shieldProps.stage3AbsorbTicks = value, 1f, 1);
            AddScaled(shieldProps, () => shieldProps.stage3BuffTicks, value => shieldProps.stage3BuffTicks = value, 1f, 1);
            AddScaled(shieldProps, () => shieldProps.stage3DurationTicks, value => shieldProps.stage3DurationTicks = value, 1f, 2);
            AddScaled(shieldProps, () => shieldProps.weakDurationTicks, value => shieldProps.weakDurationTicks = value, 1f, 60000);
            AddScaled(shieldProps, () => shieldProps.stage3TierA_MaxDamage, value => shieldProps.stage3TierA_MaxDamage = value, 1f, 100f);
            AddScaled(shieldProps, () => shieldProps.stage3TierB_MaxDamage, value => shieldProps.stage3TierB_MaxDamage = value, 1f, 500f);
            AddScaled(shieldProps, () => shieldProps.stage3TierC_MaxDamage, value => shieldProps.stage3TierC_MaxDamage = value, 1f, 1000f);
            AddScaled(shieldProps, () => shieldProps.stage3TierD_ExtraStepDamage, value => shieldProps.stage3TierD_ExtraStepDamage = value, 1f, 500f);
            AddScaled(shieldProps, () => shieldProps.bloodLossTierB, value => shieldProps.bloodLossTierB = value, 1f, 0f);
            AddScaled(shieldProps, () => shieldProps.bloodLossTierC, value => shieldProps.bloodLossTierC = value, 1f, 0f);
            AddScaled(shieldProps, () => shieldProps.bloodLossTierD, value => shieldProps.bloodLossTierD = value, 1f, 0f);
        }

        private static void AddAbilityCooldown(AbilityDef def, int decorativeTicks)
        {
            AddScaled(def, () => def.cooldownTicksRange, value => def.cooldownTicksRange = value,
                ConservativePowerTuning.Cooldown, new IntRange(decorativeTicks, decorativeTicks));
        }

        private static void AddAbilityDescription(AbilityDef def)
        {
            if (def == null)
            {
                return;
            }

            string balanced = ("MX_Power_" + def.defName).Translate().ToString();
            string decorative = "MX_NL_NeiyuAbilityDecorativeNotice".Translate().ToString();
            Add(def, () => def.description, value => def.description = value, balanced, decorative);
        }

        private static void AddStatBase(ThingDef def, StatDef stat, float balanced, float decorative)
        {
            AddStatModifier(def?.statBases, stat, balanced, decorative);
        }

        private static void AddEquippedOffset(ThingDef def, StatDef stat, float balanced, float decorative)
        {
            AddStatModifier(def?.equippedStatOffsets, stat, balanced, decorative);
        }

        private static void AddHediffFactor(HediffDef def, StatDef stat, float balanced, float decorative)
        {
            HediffStage stage = def?.stages != null && def.stages.Count > 0 ? def.stages[0] : null;
            AddStatModifier(stage?.statFactors, stat, balanced, decorative, 1f);
        }

        private static void AddThoughtMood(ThoughtDef def, float decorative)
        {
            ThoughtStage stage = def?.stages != null && def.stages.Count > 0 ? def.stages[0] : null;
            AddScaled(stage, () => stage.baseMoodEffect, value => stage.baseMoodEffect = value, ConservativePowerTuning.Bonus, decorative);
        }

        private static void AddStatModifier(List<StatModifier> modifiers, StatDef stat, float balanced, float decorative, float neutral = 0f)
        {
            if (modifiers == null || stat == null)
            {
                return;
            }

            for (int index = 0; index < modifiers.Count; index++)
            {
                StatModifier modifier = modifiers[index];
                if (modifier?.stat == stat)
                {
                    AddScaled(modifier, () => modifier.value, value => modifier.value = value, balanced, decorative, neutral);
                    return;
                }
            }
        }

        private static void AddProjectileDamage(ProjectileProperties projectile, int decorative)
        {
            if (projectile == null || ProjectileDamageAmountBaseField == null)
            {
                return;
            }

            AddScaled(
                projectile,
                () => (int)ProjectileDamageAmountBaseField.GetValue(projectile),
                value => ProjectileDamageAmountBaseField.SetValue(projectile, value),
                ConservativePowerTuning.Damage,
                decorative);
        }

        private static void AddScaled<TTarget, TValue>(TTarget target, Func<TValue> getter, Action<TValue> setter, float scale, TValue decorative, float neutral = 0f)
            where TTarget : class
        {
            if (target != null)
                Add(target, getter, setter, (TValue)ConservativePowerTuning.Number(getter(), scale, neutral), decorative);
        }

        private static void Add<TTarget, TValue>(TTarget target, Func<TValue> getter, Action<TValue> setter, TValue balanced, TValue decorative)
            where TTarget : class
        {
            if (target != null)
            {
                DefTunings.Add(new DefTuning<TValue>(getter, setter, balanced, decorative));
            }
        }

        private static ThingDef ThingDefNamed(string defName)
        {
            return DefDatabase<ThingDef>.GetNamedSilentFail(defName);
        }

        private static AbilityDef AbilityDefNamed(string defName)
        {
            return DefDatabase<AbilityDef>.GetNamedSilentFail(defName);
        }

        private static HediffDef HediffDefNamed(string defName)
        {
            return DefDatabase<HediffDef>.GetNamedSilentFail(defName);
        }

        private static Tool FirstTool(ThingDef def)
        {
            return def?.tools != null && def.tools.Count > 0 ? def.tools[0] : null;
        }

        private static VerbProperties FirstVerb(ThingDef def)
        {
            return def?.Verbs != null && def.Verbs.Count > 0 ? def.Verbs[0] : null;
        }

        private static T AbilityComp<T>(AbilityDef def) where T : class
        {
            if (def?.comps == null)
            {
                return null;
            }

            for (int index = 0; index < def.comps.Count; index++)
            {
                if (def.comps[index] is T comp)
                {
                    return comp;
                }
            }

            return null;
        }

        private static T HediffComp<T>(HediffDef def) where T : class
        {
            if (def?.comps == null)
            {
                return null;
            }

            for (int index = 0; index < def.comps.Count; index++)
            {
                if (def.comps[index] is T comp)
                {
                    return comp;
                }
            }

            return null;
        }
    }

    [StaticConstructorOnStartup]
    internal static class NeiyuPowerBalanceBootstrap
    {
        static NeiyuPowerBalanceBootstrap()
        {
            NeiyuPowerBalance.EnsureDefsInitialized();
        }
    }
}

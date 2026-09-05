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
        public static bool AbilitiesDisabled => currentLevel == CharacterPowerLevel.Decorative;
        public static bool PassivesDisabled => currentLevel == CharacterPowerLevel.Decorative;
        public static bool IsOriginal => currentLevel == CharacterPowerLevel.Original;
        public static bool IsBalanced => currentLevel == CharacterPowerLevel.Balanced;
        public static int ThunderStrikeCap => currentLevel == CharacterPowerLevel.Original ? int.MaxValue : currentLevel == CharacterPowerLevel.Balanced ? 2 : 0;
        public static int ThunderDamageCap => currentLevel == CharacterPowerLevel.Original ? int.MaxValue : currentLevel == CharacterPowerLevel.Balanced ? 20 : 0;
        public static int ThunderEmpDamageCap => currentLevel == CharacterPowerLevel.Original ? int.MaxValue : currentLevel == CharacterPowerLevel.Balanced ? 8 : 0;
        public static int BarrageShotCap => currentLevel == CharacterPowerLevel.Original ? int.MaxValue : currentLevel == CharacterPowerLevel.Balanced ? 10 : 0;
        public static float FlowerHealingCap => currentLevel == CharacterPowerLevel.Balanced ? 12f : 0f;
        public static float ExecutionArmorPenetration => currentLevel == CharacterPowerLevel.Balanced ? 0.30f : 0.10f;

        public static float HungerFloorPercent
        {
            get
            {
                switch (currentLevel)
                {
                    case CharacterPowerLevel.Balanced:
                        return 0.10f;
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
            currentLevel = IsValid(level) ? level : CharacterPowerLevel.Original;
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
                    return Mathf.Min(configuredMinimum, 0.20f);
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

            const float strength = 0.35f;
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
            Add(flowerTool, () => flowerTool.power, value => flowerTool.power = value, 6f, 3f);
            Add(flowerTool, () => flowerTool.cooldownTime, value => flowerTool.cooldownTime = value, 2.4f, 3f);
            AddStatBase(flower, StatDefOf.MeleeWeapon_CooldownMultiplier, 3.2f, 3.5f);

            ThingDef sword = ThingDefNamed("MX_Neiyu_Form_Weapon");
            Tool swordTool = FirstTool(sword);
            Add(swordTool, () => swordTool.power, value => swordTool.power = value, 26f, 10f);
            Add(swordTool, () => swordTool.armorPenetration, value => swordTool.armorPenetration = value, 0.30f, 0.12f);
            Add(swordTool, () => swordTool.cooldownTime, value => swordTool.cooldownTime = value, 2f, 2.4f);
            AddStatBase(sword, StatDefOf.MeleeWeapon_CooldownMultiplier, 0.90f, 1.15f);

            ThingDef bow = ThingDefNamed("MX_Neiyu_Form_Bow");
            VerbProperties bowVerb = FirstVerb(bow);
            Add(bowVerb, () => bowVerb.warmupTime, value => bowVerb.warmupTime = value, 1.8f, 2.8f);
            Add(bowVerb, () => bowVerb.range, value => bowVerb.range = value, 30.9f, 24f);

            ThingDef mainArrow = ThingDefNamed("MX_Bullet_BigSplitArrow");
            AddProjectileDamage(mainArrow?.projectile, 36, 12);
            SplitArrowExtension split = mainArrow?.GetModExtension<SplitArrowExtension>();
            Add(split, () => split.splitCount, value => split.splitCount = value, 5, 1);
            Add(split, () => split.splitRetargetChance, value => split.splitRetargetChance = value, 0.25f, 0f);
            Add(split, () => split.splitRetargetRadius, value => split.splitRetargetRadius = value, 8f, 1f);

            ThingDef shard = ThingDefNamed("MX_Bullet_HomingShard");
            AddProjectileDamage(shard?.projectile, 10, 6);

            ThingDef barrageArrow = ThingDefNamed("MX_Bullet_BarrageArrow");
            AddProjectileDamage(barrageArrow?.projectile, 10, 6);
        }

        private static void BuildAbilityTunings()
        {
            AbilityDef warp = AbilityDefNamed("MX_Neiyu_WarpFeather");
            AddAbilityDescription(warp);
            AddAbilityCooldown(warp, 60000, 60000);
            CompProperties_AbilityNeiyuWarpFeather warpProps = AbilityComp<CompProperties_AbilityNeiyuWarpFeather>(warp);
            Add(warpProps, () => warpProps.featherCountRange, value => warpProps.featherCountRange = value, new IntRange(4, 8), new IntRange(1, 2));

            AbilityDef thunder = AbilityDefNamed("MX_Neiyu_ThunderMarkedStorm");
            AddAbilityDescription(thunder);
            AddAbilityCooldown(thunder, 30000, 18000);
            CompProperties_AbilityNeiyuThunderSigil thunderProps = AbilityComp<CompProperties_AbilityNeiyuThunderSigil>(thunder);
            Add(thunderProps, () => thunderProps.radius, value => thunderProps.radius = value, 2.5f, 2f);
            Add(thunderProps, () => thunderProps.strikeCountRange, value => thunderProps.strikeCountRange = value, new IntRange(2, 2), new IntRange(1, 1));
            Add(thunderProps, () => thunderProps.damageAmount, value => thunderProps.damageAmount = value, 20, 10);
            Add(thunderProps, () => thunderProps.empDamageAmount, value => thunderProps.empDamageAmount = value, 8, 0);

            AbilityDef barrage = AbilityDefNamed("MX_Neiyu_Bow_ArrowBarrage");
            AddAbilityDescription(barrage);
            AddAbilityCooldown(barrage, 45000, 24000);
            CompProperties_AbilityNeiyuArrowBarrage barrageProps = AbilityComp<CompProperties_AbilityNeiyuArrowBarrage>(barrage);
            Add(barrageProps, () => barrageProps.shotCount, value => barrageProps.shotCount = value, 10, 4);
            Add(barrageProps, () => barrageProps.shotIntervalTicks, value => barrageProps.shotIntervalTicks = value, 6, 10);
            Add(barrageProps, () => barrageProps.maxDistance, value => barrageProps.maxDistance = value, 45f, 30f);
            Add(barrageProps, () => barrageProps.lateralSpread, value => barrageProps.lateralSpread = value, 3f, 2f);

            AbilityDef blessing = AbilityDefNamed("MX_Neiyu_Flower_BlessingField");
            AddAbilityDescription(blessing);
            AddAbilityCooldown(blessing, 30000, 18000);
            CompProperties_AbilityNeiyuFlowerBless blessingProps = AbilityComp<CompProperties_AbilityNeiyuFlowerBless>(blessing);
            Add(blessingProps, () => blessingProps.radius, value => blessingProps.radius = value, 4f, 2f);
            Add(blessingProps, () => blessingProps.buffDurationTicks, value => blessingProps.buffDurationTicks = value, 3000, 600);

            AbilityDef toxin = AbilityDefNamed("MX_Neiyu_Flower_ToxinGarden");
            AddAbilityDescription(toxin);
            AddAbilityCooldown(toxin, 45000, 24000);
            CompProperties_AbilityNeiyuFlowerToxinField toxinProps = AbilityComp<CompProperties_AbilityNeiyuFlowerToxinField>(toxin);
            Add(toxinProps, () => toxinProps.radius, value => toxinProps.radius = value, 5f, 2f);
            Add(toxinProps, () => toxinProps.severeFoodPoisoningSeverity, value => toxinProps.severeFoodPoisoningSeverity = value, 0.12f, 0.05f);
            Add(toxinProps, () => toxinProps.berserkChance, value => toxinProps.berserkChance = value, 0.03f, 0f);

            AbilityDef skyfall = AbilityDefNamed("MX_Neiyu_Sword_Skyfall");
            AddAbilityDescription(skyfall);
            AddAbilityCooldown(skyfall, 45000, 24000);
            CompProperties_AbilityNeiyuSwordSkyfall skyfallProps = AbilityComp<CompProperties_AbilityNeiyuSwordSkyfall>(skyfall);
            Add(skyfallProps, () => skyfallProps.impactRadius, value => skyfallProps.impactRadius = value, 2.2f, 1.5f);
            Add(skyfallProps, () => skyfallProps.impactDamage, value => skyfallProps.impactDamage = value, 40, 18);
            Add(skyfallProps, () => skyfallProps.impactArmorPen, value => skyfallProps.impactArmorPen = value, 0.30f, 0.10f);
            Add(skyfallProps, () => skyfallProps.vulnerabilityDurationTicks, value => skyfallProps.vulnerabilityDurationTicks = value, 2000, 300);

            AbilityDef execution = AbilityDefNamed("MX_Neiyu_Sword_ExecuteHead");
            AddAbilityDescription(execution);
            AddAbilityCooldown(execution, 30000, 24000);
            CompProperties_AbilityNeiyuSwordExecution executionProps = AbilityComp<CompProperties_AbilityNeiyuSwordExecution>(execution);
            Add(executionProps, () => executionProps.dashDamage, value => executionProps.dashDamage = value, 40, 18);
        }

        private static void BuildPassiveTunings()
        {
            ThingDef outerwear = ThingDefNamed("MiliraXian_NeiyuNormal");
            AddStatBase(outerwear, StatDefOf.ArmorRating_Sharp, 0.80f, 0.15f);
            AddStatBase(outerwear, StatDefOf.ArmorRating_Blunt, 0.40f, 0.08f);
            AddStatBase(outerwear, StatDefOf.ArmorRating_Heat, 0.50f, 0.10f);
            AddStatBase(outerwear, StatDefOf.Insulation_Cold, 30f, 8f);
            AddStatBase(outerwear, StatDefOf.Insulation_Heat, 15f, 5f);
            AddEquippedOffset(outerwear, StatDefOf.CarryingCapacity, 10f, 0f);
            AddEquippedOffset(outerwear, StatDefOf.MeleeDodgeChance, 0.08f, 0f);
            AddEquippedOffset(outerwear, StatDefOf.MoveSpeed, 0.15f, 0f);

            ThingDef innerwear = ThingDefNamed("MiliraXian_NeiyuInner");
            AddStatBase(innerwear, StatDefOf.ArmorRating_Sharp, 0.10f, 0f);
            AddStatBase(innerwear, StatDefOf.ArmorRating_Blunt, 0.10f, 0f);
            AddStatBase(innerwear, StatDefOf.ArmorRating_Heat, 0.10f, 0f);
            AddStatBase(innerwear, StatDefOf.Insulation_Cold, 4f, 2f);
            AddStatBase(innerwear, StatDefOf.Insulation_Heat, 4f, 2f);

            ThingDef earrings = ThingDefNamed("MX_Apparel_EarringsZhenzhu");
            AddEquippedOffset(earrings, StatDefOf.ImmunityGainSpeed, 0.10f, 0f);

            HediffDef blessing = HediffDefNamed("MX_Neiyu_FlowerBlessed");
            AddHediffFactor(blessing, StatDefOf.MoveSpeed, 1.10f, 1f);
            AddHediffFactor(blessing, StatDefOf.IncomingDamageFactor, 0.95f, 1f);
            AddHediffFactor(blessing, StatDefOf.ImmunityGainSpeed, 1.20f, 1f);

            HediffDef vulnerability = HediffDefNamed("MX_Neiyu_SwordVulnerability");
            AddHediffFactor(vulnerability, StatDefOf.IncomingDamageFactor, 1.12f, 1f);

            ThoughtDef blessingThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("MX_Neiyu_FlowerBlessedThought");
            AddThoughtMood(blessingThought, 5f, 0f);
            AddThoughtMood(DefDatabase<ThoughtDef>.GetNamedSilentFail("MX_Neiyu_Joyful"), 3f, 0f);
            AddThoughtMood(DefDatabase<ThoughtDef>.GetNamedSilentFail("MX_Neiyu_RelaxedNearNeiyu"), 2f, 0f);

            HediffDef shieldDef = HediffDefNamed("MXNL_NeiyuShield");
            HediffCompProperties_MXNeiyuCountShield shieldProps = HediffComp<HediffCompProperties_MXNeiyuCountShield>(shieldDef);
            Add(shieldProps, () => shieldProps.phase2Threshold, value => shieldProps.phase2Threshold = value, 10f, 1f);
            Add(shieldProps, () => shieldProps.phase2MaxChargesNormal, value => shieldProps.phase2MaxChargesNormal = value, 8, 0);
            Add(shieldProps, () => shieldProps.phase2MaxChargesWeak, value => shieldProps.phase2MaxChargesWeak = value, 2, 0);
            Add(shieldProps, () => shieldProps.phase2RecoverTicksNoChange, value => shieldProps.phase2RecoverTicksNoChange = value, 12000, 60000);
            Add(shieldProps, () => shieldProps.stage3AbsorbTicks, value => shieldProps.stage3AbsorbTicks = value, 180, 1);
            Add(shieldProps, () => shieldProps.stage3BuffTicks, value => shieldProps.stage3BuffTicks = value, 3000, 1);
            Add(shieldProps, () => shieldProps.stage3DurationTicks, value => shieldProps.stage3DurationTicks = value, 3180, 2);
            Add(shieldProps, () => shieldProps.weakDurationTicks, value => shieldProps.weakDurationTicks = value, 15000, 60000);
            Add(shieldProps, () => shieldProps.stage3TierA_MaxDamage, value => shieldProps.stage3TierA_MaxDamage = value, 30f, 100f);
            Add(shieldProps, () => shieldProps.stage3TierB_MaxDamage, value => shieldProps.stage3TierB_MaxDamage = value, 100f, 500f);
            Add(shieldProps, () => shieldProps.stage3TierC_MaxDamage, value => shieldProps.stage3TierC_MaxDamage = value, 250f, 1000f);
            Add(shieldProps, () => shieldProps.stage3TierD_ExtraStepDamage, value => shieldProps.stage3TierD_ExtraStepDamage = value, 200f, 500f);
            Add(shieldProps, () => shieldProps.bloodLossTierB, value => shieldProps.bloodLossTierB = value, 0.02f, 0f);
            Add(shieldProps, () => shieldProps.bloodLossTierC, value => shieldProps.bloodLossTierC = value, 0.06f, 0f);
            Add(shieldProps, () => shieldProps.bloodLossTierD, value => shieldProps.bloodLossTierD = value, 0.12f, 0f);
        }

        private static void AddAbilityCooldown(AbilityDef def, int balancedTicks, int decorativeTicks)
        {
            Add(def, () => def.cooldownTicksRange, value => def.cooldownTicksRange = value,
                new IntRange(balancedTicks, balancedTicks), new IntRange(decorativeTicks, decorativeTicks));
        }

        private static void AddAbilityDescription(AbilityDef def)
        {
            if (def == null)
            {
                return;
            }

            string original = def.description;
            string balanced = original + "\n\n" + "MX_NL_NeiyuAbilityBalancedNotice".Translate();
            string decorative = original + "\n\n" + "MX_NL_NeiyuAbilityDecorativeNotice".Translate();
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
            AddStatModifier(stage?.statFactors, stat, balanced, decorative);
        }

        private static void AddThoughtMood(ThoughtDef def, float balanced, float decorative)
        {
            ThoughtStage stage = def?.stages != null && def.stages.Count > 0 ? def.stages[0] : null;
            Add(stage, () => stage.baseMoodEffect, value => stage.baseMoodEffect = value, balanced, decorative);
        }

        private static void AddStatModifier(List<StatModifier> modifiers, StatDef stat, float balanced, float decorative)
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
                    Add(modifier, () => modifier.value, value => modifier.value = value, balanced, decorative);
                    return;
                }
            }
        }

        private static void AddProjectileDamage(ProjectileProperties projectile, int balanced, int decorative)
        {
            if (projectile == null || ProjectileDamageAmountBaseField == null)
            {
                return;
            }

            Add(
                projectile,
                () => (int)ProjectileDamageAmountBaseField.GetValue(projectile),
                value => ProjectileDamageAmountBaseField.SetValue(projectile, value),
                balanced,
                decorative);
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

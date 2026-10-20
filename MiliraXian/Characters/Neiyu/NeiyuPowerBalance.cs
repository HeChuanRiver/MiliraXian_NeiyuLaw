using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using MiliraXian.Characters.Neiyu.Cultivation;
using MiliraXian.Characters.Common;

namespace MiliraXian.Characters.Neiyu
{
    // The shared profile owns both the loaded baseline and the decorative overrides.
    internal static class NeiyuPowerBalance
    {
        internal static readonly CharacterPowerProfile Profile = new();
        private static readonly FieldInfo ProjectileDamageAmountBaseField =
            AccessTools.Field(typeof(ProjectileProperties), "damageAmountBase");
        private static readonly FieldInfo ProjectileArmorPenetrationBaseField =
            AccessTools.Field(typeof(ProjectileProperties), "armorPenetrationBase");
        private static bool defsInitialized;

        public static int Revision => Profile.Revision;
        public static bool AbilitiesDisabled => Profile.Sealed;
        public static bool PassivesDisabled => Profile.Sealed;
        public static int BarrageShotCap => AbilitiesDisabled ? 0 : int.MaxValue;
        public static float HungerFloorPercent => PassivesDisabled ? 0f : 0.20f;
        public static string AbilitiesDisabledReason => "MX_NeiyuDecorativeAbilitiesSealed".CanTranslate()
            ? "MX_NeiyuDecorativeAbilitiesSealed".Translate().ToString()
            : "观赏模式下无法使用主动技能。";

        public static void SetDecorative(bool decorative) => Profile.SetDecorative(decorative);

        internal static void EnsureDefsInitialized()
        {
            if (defsInitialized) return;
            BuildWeaponTunings();
            BuildAbilityTunings();
            BuildPassiveTunings();
            defsInitialized = true;
            Profile.Apply();
        }

        public static float LimitConsciousnessMinimum(float configuredMinimum) =>
            PassivesDisabled ? 0f : configuredMinimum;

        public static void GetWeakPenaltyFactors(out float moveSpeedFactor, out float restFallRateFactor, out float workSpeedGlobalFactor)
        {
            moveSpeedFactor = 0.50f;
            restFallRateFactor = 1.50f;
            workSpeedGlobalFactor = 0.20f;
        }

        private static void BuildWeaponTunings()
        {
            ThingDef flower = ThingDefNamed("MX_Neiyu_Form_Flower");
            Tool flowerTool = FirstTool(flower);
            CultivationPower.RegisterTool(flowerTool, 3f, 0.045f);
            Add(flowerTool, () => flowerTool.power, value => flowerTool.power = value, 3f);
            Add(flowerTool, () => flowerTool.armorPenetration, value => flowerTool.armorPenetration = value, 0.045f);
            Add(flowerTool, () => flowerTool.cooldownTime, value => flowerTool.cooldownTime = value, 3f);
            AddStatBase(flower, StatDefOf.MeleeWeapon_CooldownMultiplier, 3.5f);

            ThingDef sword = ThingDefNamed("MX_Neiyu_Form_Weapon");
            Tool swordTool = FirstTool(sword);
            CultivationPower.RegisterTool(swordTool, 10f, 0.12f);
            Add(swordTool, () => swordTool.power, value => swordTool.power = value, 10f);
            Add(swordTool, () => swordTool.armorPenetration, value => swordTool.armorPenetration = value, 0.12f);
            Add(swordTool, () => swordTool.cooldownTime, value => swordTool.cooldownTime = value, 2.4f);
            AddStatBase(sword, StatDefOf.MeleeWeapon_CooldownMultiplier, 1.15f);

            ThingDef bow = ThingDefNamed("MX_Neiyu_Form_Bow");
            VerbProperties bowVerb = FirstVerb(bow);
            Add(bowVerb, () => bowVerb.warmupTime, value => bowVerb.warmupTime = value, 2.8f);
            Add(bowVerb, () => bowVerb.range, value => bowVerb.range = value, 24f);

            ThingDef mainArrow = ThingDefNamed("MX_Bullet_BigSplitArrow");
            AddProjectileDamage(mainArrow?.projectile, 12);
            SplitArrowExtension split = mainArrow?.GetModExtension<SplitArrowExtension>();
            Add(split, () => split.splitCount, value => split.splitCount = value, 1);
            Add(split, () => split.splitRetargetChance, value => split.splitRetargetChance = value, 0f);
            Add(split, () => split.splitRetargetRadius, value => split.splitRetargetRadius = value, 1f);

            ThingDef shard = ThingDefNamed("MX_Bullet_HomingShard");
            AddProjectileDamage(shard?.projectile, 6);

            ThingDef barrageArrow = ThingDefNamed("MX_Bullet_BarrageArrow");
            AddProjectileDamage(barrageArrow?.projectile, 6);
            AddEquipmentDescription(flower);
            AddEquipmentDescription(sword);
            AddEquipmentDescription(bow);
        }

        private static void BuildAbilityTunings()
        {
            AbilityDef warp = AbilityDefNamed("MX_Neiyu_WarpFeather");
            // Warp Feather keeps its authored yield and cooldown; the shared gate seals its casts.
            if (warp != null)
                Add(warp, () => warp.description, value => warp.description = value,
                    "MX_NL_NeiyuAbilityDecorativeNotice".Translate().ToString());

            // Thunder Sigil keeps its authored rules.
            // The shared decorative gate still prevents casts and cancels pending work.

            AbilityDef barrage = AbilityDefNamed("MX_Neiyu_Bow_ArrowBarrage");
            AddAbilityDescription(barrage);
            AddAbilityCooldown(barrage, 24000);
            CompProperties_AbilityNeiyuArrowBarrage barrageProps = AbilityComp<CompProperties_AbilityNeiyuArrowBarrage>(barrage);
            Add(barrageProps, () => barrageProps.shotCount, value => barrageProps.shotCount = value, 4);
            Add(barrageProps, () => barrageProps.shotIntervalTicks, value => barrageProps.shotIntervalTicks = value, 10);
            Add(barrageProps, () => barrageProps.maxDistance, value => barrageProps.maxDistance = value, 30f);
            Add(barrageProps, () => barrageProps.lateralSpread, value => barrageProps.lateralSpread = value, 2f);

            AbilityDef blessing = AbilityDefNamed("MX_Neiyu_Flower_BlessingField");
            AddAbilityDescription(blessing);
            AddAbilityCooldown(blessing, 18000);
            CompProperties_AbilityNeiyuFlowerBless blessingProps = AbilityComp<CompProperties_AbilityNeiyuFlowerBless>(blessing);
            Add(blessingProps, () => blessingProps.radius, value => blessingProps.radius = value, 2f);
            Add(blessingProps, () => blessingProps.buffDurationTicks, value => blessingProps.buffDurationTicks = value, 600);

            AbilityDef toxin = AbilityDefNamed("MX_Neiyu_Flower_ToxinGarden");
            AddAbilityDescription(toxin);
            AddAbilityCooldown(toxin, 24000);
            CompProperties_AbilityNeiyuFlowerToxinField toxinProps = AbilityComp<CompProperties_AbilityNeiyuFlowerToxinField>(toxin);
            Add(toxinProps, () => toxinProps.radius, value => toxinProps.radius = value, 2f);
            Add(toxinProps, () => toxinProps.severeFoodPoisoningSeverity, value => toxinProps.severeFoodPoisoningSeverity = value, 0.05f);
            Add(toxinProps, () => toxinProps.berserkChance, value => toxinProps.berserkChance = value, 0f);

            AbilityDef skyfall = AbilityDefNamed("MX_Neiyu_Sword_Skyfall");
            AddAbilityDescription(skyfall);
            AddAbilityCooldown(skyfall, 24000);
            CompProperties_AbilityNeiyuSwordSkyfall skyfallProps = AbilityComp<CompProperties_AbilityNeiyuSwordSkyfall>(skyfall);
            Add(skyfallProps, () => skyfallProps.impactRadius, value => skyfallProps.impactRadius = value, 1.5f);
            Add(skyfallProps, () => skyfallProps.impactDamage, value => skyfallProps.impactDamage = value, 18);
            Add(skyfallProps, () => skyfallProps.impactArmorPen, value => skyfallProps.impactArmorPen = value, 0.10f);
            Add(skyfallProps, () => skyfallProps.vulnerabilityDurationTicks, value => skyfallProps.vulnerabilityDurationTicks = value, 300);

            AbilityDef execution = AbilityDefNamed("MX_Neiyu_Sword_ExecuteHead");
            AddAbilityDescription(execution);
            AddAbilityCooldown(execution, 24000);
            CompProperties_AbilityNeiyuSwordExecution executionProps = AbilityComp<CompProperties_AbilityNeiyuSwordExecution>(execution);
            Add(executionProps, () => executionProps.dashDamage, value => executionProps.dashDamage = value, 18);
        }

        private static void BuildPassiveTunings()
        {
            ThingDef outerwear = ThingDefNamed("MiliraXian_NeiyuNormal");
            AddStatBase(outerwear, StatDefOf.ArmorRating_Sharp, 0.15f);
            AddStatBase(outerwear, StatDefOf.ArmorRating_Blunt, 0.08f);
            AddStatBase(outerwear, StatDefOf.ArmorRating_Heat, 0.10f);
            AddStatBase(outerwear, StatDefOf.Insulation_Cold, 8f);
            AddStatBase(outerwear, StatDefOf.Insulation_Heat, 5f);
            AddEquippedOffset(outerwear, StatDefOf.CarryingCapacity, 0f);
            AddEquippedOffset(outerwear, StatDefOf.MeleeDodgeChance, 0f);
            AddEquippedOffset(outerwear, StatDefOf.MoveSpeed, 0f);
            AddEquippedOffset(outerwear, StatDefOf.IncomingDamageFactor, 0f);
            AddEquipmentDescription(outerwear);

            ThingDef innerwear = ThingDefNamed("MiliraXian_NeiyuInner");
            AddStatBase(innerwear, StatDefOf.ArmorRating_Sharp, 0f);
            AddStatBase(innerwear, StatDefOf.ArmorRating_Blunt, 0f);
            AddStatBase(innerwear, StatDefOf.ArmorRating_Heat, 0f);
            AddStatBase(innerwear, StatDefOf.Insulation_Cold, 2f);
            AddStatBase(innerwear, StatDefOf.Insulation_Heat, 2f);
            AddEquippedOffset(innerwear, StatDefOf.IncomingDamageFactor, 0f);
            AddEquippedOffset(innerwear, StatDefOf.MeleeCooldownFactor, 0f);
            AddEquippedOffset(innerwear, StatDefOf.WorkSpeedGlobal, 0f);
            AddEquipmentDescription(innerwear);

            ThingDef earrings = ThingDefNamed("MX_Apparel_EarringsZhenzhu");
            AddStatBase(earrings, StatDefOf.ArmorRating_Sharp, 0f);
            AddStatBase(earrings, StatDefOf.ArmorRating_Blunt, 0f);
            AddStatBase(earrings, StatDefOf.ArmorRating_Heat, 0f);
            AddEquippedOffset(earrings, StatDefOf.ImmunityGainSpeed, 0f);
            AddEquippedOffset(earrings, StatDefOf.AimingDelayFactor, 0f);
            AddEquippedOffset(earrings, StatDefOf.RangedCooldownFactor, 0f);
            AddEquipmentDescription(earrings);

            HediffDef blessing = HediffDefNamed("MX_Neiyu_FlowerBlessed");
            AddHediffFactor(blessing, StatDefOf.MoveSpeed, 1f);
            AddHediffFactor(blessing, StatDefOf.IncomingDamageFactor, 1f);
            AddHediffFactor(blessing, StatDefOf.ImmunityGainSpeed, 1f);

            HediffDef vulnerability = HediffDefNamed("MX_Neiyu_SwordVulnerability");
            AddHediffFactor(vulnerability, StatDefOf.IncomingDamageFactor, 1f);

            ThoughtDef blessingThought = DefDatabase<ThoughtDef>.GetNamedSilentFail("MX_Neiyu_FlowerBlessedThought");
            AddThoughtMood(blessingThought, 0f);
            AddThoughtMood(DefDatabase<ThoughtDef>.GetNamedSilentFail("MX_Neiyu_Joyful"), 0f);
            AddThoughtMood(DefDatabase<ThoughtDef>.GetNamedSilentFail("MX_Neiyu_RelaxedNearNeiyu"), 0f);

            HediffDef shieldDef = HediffDefNamed("MXNL_NeiyuShield");
            if (shieldDef != null) shieldDef.description = ShieldDescription();
            Add(shieldDef, () => shieldDef.description, value => shieldDef.description = value,
                "MX_NL_NeiyuPassiveSealedDesc".Translate().ToString());
            HediffCompProperties_MXNeiyuCountShield shieldProps = HediffComp<HediffCompProperties_MXNeiyuCountShield>(shieldDef);
            Add(shieldProps, () => shieldProps.phase2Threshold, value => shieldProps.phase2Threshold = value, 1f);
            Add(shieldProps, () => shieldProps.phase2MaxChargesNormal, value => shieldProps.phase2MaxChargesNormal = value, 0);
            Add(shieldProps, () => shieldProps.phase2MaxChargesWeak, value => shieldProps.phase2MaxChargesWeak = value, 0);
            Add(shieldProps, () => shieldProps.phase2RecoverTicksNoChange, value => shieldProps.phase2RecoverTicksNoChange = value, 60000);
            Add(shieldProps, () => shieldProps.stage3AbsorbTicks, value => shieldProps.stage3AbsorbTicks = value, 1);
            Add(shieldProps, () => shieldProps.stage3BuffTicks, value => shieldProps.stage3BuffTicks = value, 1);
            Add(shieldProps, () => shieldProps.stage3DurationTicks, value => shieldProps.stage3DurationTicks = value, 2);
            Add(shieldProps, () => shieldProps.weakDurationTicks, value => shieldProps.weakDurationTicks = value, 60000);
            Add(shieldProps, () => shieldProps.stage3TierA_MaxDamage, value => shieldProps.stage3TierA_MaxDamage = value, 100f);
            Add(shieldProps, () => shieldProps.stage3TierB_MaxDamage, value => shieldProps.stage3TierB_MaxDamage = value, 500f);
            Add(shieldProps, () => shieldProps.stage3TierC_MaxDamage, value => shieldProps.stage3TierC_MaxDamage = value, 1000f);
            Add(shieldProps, () => shieldProps.stage3TierD_ExtraStepDamage, value => shieldProps.stage3TierD_ExtraStepDamage = value, 500f);
            Add(shieldProps, () => shieldProps.bloodLossTierB, value => shieldProps.bloodLossTierB = value, 0f);
            Add(shieldProps, () => shieldProps.bloodLossTierC, value => shieldProps.bloodLossTierC = value, 0f);
            Add(shieldProps, () => shieldProps.bloodLossTierD, value => shieldProps.bloodLossTierD = value, 0f);
        }

        private static string ShieldDescription()
        {
            const string key = "MX_NL_CultivatedShieldDescription";
            const string source = "光环修行依次解锁18、42、108层计数护盾，第三级解锁三阶循环。首击被抵消后展开计数护盾；解锁三阶后，致命攻击可直接触发三阶。\n{0}60秒未消耗盾层时恢复首击防护。常态下，剩余盾层抵挡整次伤害；耗尽后，未解锁三阶时等待恢复，已解锁时进入三阶。\n三阶先无敌并蓄伤3小时，再根据蓄伤量获得12小时增益，最多叠加5层。结束后虚弱150秒，护盾上限为24层，无法触发三阶；单次消耗超过剩余盾层时，耗尽盾层且无法抵挡本次伤害。";
            string rule = CountShieldUtility.RuleDescription(36f, true);
            return key.CanTranslate() ? key.Translate(rule).ToString() : string.Format(source, rule);
        }

        private static void AddAbilityCooldown(AbilityDef def, int decorativeTicks)
        {
            Add(def, () => def.cooldownTicksRange, value => def.cooldownTicksRange = value,
                new IntRange(decorativeTicks, decorativeTicks));
        }

        private static void AddAbilityDescription(AbilityDef def)
        {
            Add(def, () => def.description, value => def.description = value,
                "MX_NL_NeiyuAbilityDecorativeNotice".Translate().ToString());
        }

        private static void AddStatBase(ThingDef def, StatDef stat, float decorative)
        {
            AddStatModifier(def?.statBases, stat, decorative);
            CultivationPower.RegisterStat(def, stat, decorative, offset: false);
        }

        private static void AddEquipmentDescription(ThingDef def)
        {
            if (def != null) Profile.Description(def);
        }

        private static void AddEquippedOffset(ThingDef def, StatDef stat, float decorative)
        {
            AddStatModifier(def?.equippedStatOffsets, stat, decorative);
            CultivationPower.RegisterStat(def, stat, decorative, offset: true);
        }

        private static void AddHediffFactor(HediffDef def, StatDef stat, float decorative)
        {
            HediffStage stage = def?.stages != null && def.stages.Count > 0 ? def.stages[0] : null;
            AddStatModifier(stage?.statFactors, stat, decorative);
        }

        private static void AddThoughtMood(ThoughtDef def, float decorative)
        {
            ThoughtStage stage = def?.stages != null && def.stages.Count > 0 ? def.stages[0] : null;
            Add(stage, () => stage.baseMoodEffect, value => stage.baseMoodEffect = value, decorative);
        }

        private static void AddStatModifier(List<StatModifier> modifiers, StatDef stat, float decorative)
        {
            if (modifiers == null || stat == null) return;
            foreach (StatModifier modifier in modifiers)
            {
                if (modifier.stat == stat)
                {
                    Add(modifier, () => modifier.value, value => modifier.value = value, decorative);
                    return;
                }
            }
        }

        private static void AddProjectileDamage(ProjectileProperties projectile, int decorative)
        {
            CultivationPower.RegisterProjectile(projectile, decorative, decorative * 0.015f);
            if (projectile == null || ProjectileDamageAmountBaseField == null || ProjectileArmorPenetrationBaseField == null)
                return;
            Add(projectile, () => (int)ProjectileDamageAmountBaseField.GetValue(projectile),
                value => ProjectileDamageAmountBaseField.SetValue(projectile, value), decorative);
            Add(projectile, () => (float)ProjectileArmorPenetrationBaseField.GetValue(projectile),
                value => ProjectileArmorPenetrationBaseField.SetValue(projectile, value), decorative * 0.015f);
        }

        private static void Add<TTarget, TValue>(TTarget target, Func<TValue> getter, Action<TValue> setter, TValue decorative)
            where TTarget : class
        {
            if (target != null) Profile.Value(getter, setter, decorative);
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

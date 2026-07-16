using System.Collections.Generic;
using System.Text;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Zhaoli
{
    internal static class ZhaoliProgressionUtility
    {
        public const int BossLinkCount = 3;
        public const int TransitionDurationTicks = 540;
        public const int PhaseTeleportIntervalTicks = 900;

        private const float RecruitGrowthStepRatio = 0.1f;
        private const float RecruitCarryOffset = 6f;
        private const float RecruitToxicResistanceOffset = 0.15f;
        private const float RecruitCommonFactor = 1.05f;

        private const string IncomingDamageFactorStat = "IncomingDamageFactor";
        private const string MeleeArmorPenetrationStat = "MeleeArmorPenetration";
        private const string MeleeCooldownFactorStat = "MeleeCooldownFactor";
        private const string MeleeDamageFactorStat = "MeleeDamageFactor";
        private const string MeleeDodgeChanceStat = "MeleeDodgeChance";
        private const string MeleeHitChanceStat = "MeleeHitChance";
        private const string PawnTrapSpringChanceStat = "PawnTrapSpringChance";
        private const string StaggerDurationFactorStat = "StaggerDurationFactor";
        private const string MeleeDoorDamageFactorStat = "MeleeDoorDamageFactor";
        private const string MoveSpeedStat = "MoveSpeed";
        private const string CarryingCapacityStat = "CarryingCapacity";
        private const string ImmunityGainSpeedStat = "ImmunityGainSpeed";
        private const string InjuryHealingFactorStat = "InjuryHealingFactor";
        private const string ToxicEnvironmentResistanceStat = "ToxicEnvironmentResistance";
        private const string MedicalOperationSpeedStat = "MedicalOperationSpeed";
        private const string MedicalSurgerySuccessChanceStat = "MedicalSurgerySuccessChance";
        private const string MedicalTendQualityStat = "MedicalTendQuality";
        private const string GeneralLaborSpeedStat = "GeneralLaborSpeed";

        private static readonly HashSet<string> AffectedStatNames = new()
        {
            IncomingDamageFactorStat,
            MeleeArmorPenetrationStat,
            MeleeCooldownFactorStat,
            MeleeDamageFactorStat,
            MeleeDodgeChanceStat,
            MeleeHitChanceStat,
            PawnTrapSpringChanceStat,
            StaggerDurationFactorStat,
            MeleeDoorDamageFactorStat,
            MoveSpeedStat,
            CarryingCapacityStat,
            ImmunityGainSpeedStat,
            InjuryHealingFactorStat,
            ToxicEnvironmentResistanceStat,
            MedicalOperationSpeedStat,
            MedicalSurgerySuccessChanceStat,
            MedicalTendQualityStat,
            GeneralLaborSpeedStat
        };

        public static float GetTransitionRadiusBonus(int phase)
        {
            return Mathf.Clamp(phase, 0, 3) switch
            {
                1 => 5f,
                2 => 10f,
                3 => 20f,
                _ => 0f,
            };
        }

        public static string BuildRaidBossSummary(int phase)
        {
            StringBuilder stringBuilder = new();
            stringBuilder.Append("MX_ZL_CurrentPhaseBonuses".Translate().ToString());
            if (phase <= 0)
            {
                stringBuilder.Append("MX_ZL_NoSubstituteBonus".Translate().ToString());
                return stringBuilder.ToString();
            }

            AppendRaidFactorLine(stringBuilder, IncomingDamageFactorStat, phase);
            AppendRaidFactorLine(stringBuilder, MeleeArmorPenetrationStat, phase);
            AppendRaidFactorLine(stringBuilder, MeleeCooldownFactorStat, phase);
            AppendRaidFactorLine(stringBuilder, MeleeDamageFactorStat, phase);
            AppendRaidFactorLine(stringBuilder, MeleeDodgeChanceStat, phase);
            AppendRaidFactorLine(stringBuilder, MeleeHitChanceStat, phase);
            AppendRaidFactorLine(stringBuilder, PawnTrapSpringChanceStat, phase);
            AppendRaidFactorLine(stringBuilder, StaggerDurationFactorStat, phase);
            AppendRaidFactorLine(stringBuilder, MeleeDoorDamageFactorStat, phase);
            if (phase >= 3)
            {
                AppendRaidFactorLine(stringBuilder, MoveSpeedStat, phase);
            }

            return stringBuilder.ToString();
        }

        public static string BuildRecruitGrowthSummary(int deathCount)
        {
            StringBuilder stringBuilder = new();
            stringBuilder.Append("MX_ZL_CurrentGrowthBonuses".Translate().ToString());
            AppendOffsetLine(stringBuilder, CarryingCapacityStat, RecruitCarryOffset);
            AppendOffsetLine(stringBuilder, ToxicEnvironmentResistanceStat, RecruitToxicResistanceOffset, usePercent: true);
            AppendFactorLine(stringBuilder, ImmunityGainSpeedStat, RecruitCommonFactor);
            AppendFactorLine(stringBuilder, InjuryHealingFactorStat, RecruitCommonFactor);
            AppendFactorLine(stringBuilder, MedicalOperationSpeedStat, RecruitCommonFactor);
            AppendFactorLine(stringBuilder, MedicalSurgerySuccessChanceStat, RecruitCommonFactor);
            AppendFactorLine(stringBuilder, MedicalTendQualityStat, RecruitCommonFactor);
            AppendFactorLine(stringBuilder, GeneralLaborSpeedStat, RecruitCommonFactor);

            stringBuilder.AppendLine();
            stringBuilder.Append("MX_ZL_DeathGrowth".Translate().ToString());
            if (deathCount <= 0)
            {
                stringBuilder.Append("MX_ZL_NoGrowthStacks".Translate().ToString());
                return stringBuilder.ToString();
            }

            AppendRecruitGrowthFactorLine(stringBuilder, IncomingDamageFactorStat, deathCount);
            AppendRecruitGrowthFactorLine(stringBuilder, MeleeArmorPenetrationStat, deathCount);
            AppendRecruitGrowthFactorLine(stringBuilder, MeleeCooldownFactorStat, deathCount);
            AppendRecruitGrowthFactorLine(stringBuilder, MeleeDamageFactorStat, deathCount);
            AppendRecruitGrowthFactorLine(stringBuilder, MeleeDodgeChanceStat, deathCount);
            AppendRecruitGrowthFactorLine(stringBuilder, MeleeHitChanceStat, deathCount);
            AppendRecruitGrowthFactorLine(stringBuilder, PawnTrapSpringChanceStat, deathCount);
            AppendRecruitGrowthFactorLine(stringBuilder, StaggerDurationFactorStat, deathCount);
            AppendRecruitGrowthFactorLine(stringBuilder, MeleeDoorDamageFactorStat, deathCount);
            return stringBuilder.ToString();
        }

        public static bool IsAffectedStat(StatDef stat)
        {
            return stat != null && AffectedStatNames.Contains(stat.defName);
        }

        public static void ApplyStatModifiers(Pawn pawn, StatDef stat, ref float result)
        {
            if (pawn == null || !IsAffectedStat(stat) || !ZhaoliKarmaUtility.IsZhaoli(pawn))
            {
                return;
            }

            ApplyRaidBossModifiers(pawn, stat, ref result);
            ApplyRecruitGrowthModifiers(pawn, stat, ref result);
        }

        private static void ApplyRaidBossModifiers(Pawn pawn, StatDef stat, ref float result)
        {
            HediffComp_ZhaoliRaidState raidComp = ZhaoliScenarioUtility.GetRaidStateComp(pawn);
            int phase = raidComp?.SubstituteDeathsUsed ?? 0;
            if (phase <= 0)
            {
                return;
            }

            float factor = GetRaidBossFactor(stat, phase);
            if (Mathf.Abs(factor - 1f) > 0.0001f)
            {
                result *= factor;
            }
        }

        private static void ApplyRecruitGrowthModifiers(Pawn pawn, StatDef stat, ref float result)
        {
            if (!ZhaoliRebirthUtility.ShouldUseRecruitGrowth(pawn))
            {
                return;
            }

            ApplyRecruitBaseBonuses(stat, ref result);

            int deathCount = ZhaoliRebirthUtility.GetRebirthComp(pawn)?.RecruitGrowthDeaths ?? 0;
            if (deathCount <= 0 || !TryGetPhaseOneFactor(stat, out float fullFactor))
            {
                return;
            }

            result *= RecruitGrowthFactor(fullFactor, deathCount);
        }

        private static void ApplyRecruitBaseBonuses(StatDef stat, ref float result)
        {
            if (IsStat(stat, CarryingCapacityStat))
            {
                result += RecruitCarryOffset;
                return;
            }

            if (IsStat(stat, ToxicEnvironmentResistanceStat))
            {
                result += RecruitToxicResistanceOffset;
                return;
            }

            if (IsStat(stat, ImmunityGainSpeedStat)
                || IsStat(stat, InjuryHealingFactorStat)
                || IsStat(stat, MedicalOperationSpeedStat)
                || IsStat(stat, MedicalSurgerySuccessChanceStat)
                || IsStat(stat, MedicalTendQualityStat)
                || IsStat(stat, GeneralLaborSpeedStat))
            {
                result *= RecruitCommonFactor;
            }
        }

        private static float GetRaidBossFactor(StatDef stat, int phase)
        {
            if (phase <= 0)
            {
                return 1f;
            }

            float magnitudeScale = phase == 1 ? 1f : 0.8f;
            if (phase >= 3)
            {
                if (IsStat(stat, MoveSpeedStat))
                {
                    return 0.99f;
                }

                if (IsStat(stat, StaggerDurationFactorStat))
                {
                    return 1.25f;
                }
            }

            if (IsStat(stat, IncomingDamageFactorStat))
            {
                return 1f - 0.5f * magnitudeScale;
            }

            if (IsStat(stat, MeleeArmorPenetrationStat))
            {
                return 1f + 0.66f * magnitudeScale;
            }

            if (IsStat(stat, MeleeCooldownFactorStat))
            {
                return 1f - 0.33f * magnitudeScale;
            }

            if (IsStat(stat, MeleeDamageFactorStat) || IsStat(stat, MeleeDodgeChanceStat) || IsStat(stat, MeleeHitChanceStat))
            {
                return 1f + 0.33f * magnitudeScale;
            }

            if (IsStat(stat, PawnTrapSpringChanceStat))
            {
                return Mathf.Max(0f, 1f - 1f * magnitudeScale);
            }

            if (IsStat(stat, StaggerDurationFactorStat))
            {
                return 1f - 0.6f * magnitudeScale;
            }

            if (ModsConfig.BiotechActive && IsStat(stat, MeleeDoorDamageFactorStat))
            {
                return 1f + 15f * magnitudeScale;
            }

            return 1f;
        }

        private static bool TryGetPhaseOneFactor(StatDef stat, out float factor)
        {
            factor = 1f;
            if (stat == null)
            {
                return false;
            }

            if (IsStat(stat, IncomingDamageFactorStat))
            {
                factor = 0.5f;
                return true;
            }

            if (IsStat(stat, MeleeArmorPenetrationStat))
            {
                factor = 1.66f;
                return true;
            }

            if (IsStat(stat, MeleeCooldownFactorStat))
            {
                factor = 0.67f;
                return true;
            }

            if (IsStat(stat, MeleeDamageFactorStat) || IsStat(stat, MeleeDodgeChanceStat) || IsStat(stat, MeleeHitChanceStat))
            {
                factor = 1.33f;
                return true;
            }

            if (IsStat(stat, PawnTrapSpringChanceStat))
            {
                factor = 0f;
                return true;
            }

            if (IsStat(stat, StaggerDurationFactorStat))
            {
                factor = 0.4f;
                return true;
            }

            if (ModsConfig.BiotechActive && IsStat(stat, MeleeDoorDamageFactorStat))
            {
                factor = 16f;
                return true;
            }

            return false;
        }

        private static void AppendRaidFactorLine(StringBuilder stringBuilder, string statDefName, int phase)
        {
            StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(statDefName);
            if (stat == null)
            {
                return;
            }

            float factor = GetRaidBossFactor(stat, phase);
            if (Mathf.Abs(factor - 1f) < 0.0001f)
            {
                return;
            }

            AppendFactorLine(stringBuilder, statDefName, factor);
        }

        private static void AppendRecruitGrowthFactorLine(StringBuilder stringBuilder, string statDefName, int deathCount)
        {
            if (deathCount <= 0)
            {
                return;
            }

            StatDef stat = DefDatabase<StatDef>.GetNamedSilentFail(statDefName);
            if (stat == null || !TryGetPhaseOneFactor(stat, out float fullFactor))
            {
                return;
            }

            float factor = RecruitGrowthFactor(fullFactor, deathCount);
            AppendFactorLine(stringBuilder, statDefName, factor);
        }

        private static float RecruitGrowthFactor(float fullFactor, int deathCount)
        {
            float progress = Mathf.Clamp01(Mathf.Max(0, deathCount) * RecruitGrowthStepRatio);
            return Mathf.Lerp(1f, fullFactor, progress);
        }

        private static void AppendFactorLine(StringBuilder stringBuilder, string statDefName, float factor)
        {
            if (Mathf.Abs(factor - 1f) < 0.0001f)
            {
                return;
            }

            stringBuilder.AppendLine();
            stringBuilder.Append("- ");
            stringBuilder.Append(GetStatLabel(statDefName));
            stringBuilder.Append(" x");
            stringBuilder.Append(factor.ToStringPercent());
        }

        private static void AppendOffsetLine(StringBuilder stringBuilder, string statDefName, float value, bool usePercent = false)
        {
            if (Mathf.Abs(value) < 0.0001f)
            {
                return;
            }

            stringBuilder.AppendLine();
            stringBuilder.Append("- ");
            stringBuilder.Append(GetStatLabel(statDefName));
            stringBuilder.Append(" ");
            if (value > 0f)
            {
                stringBuilder.Append("+");
            }

            stringBuilder.Append(usePercent ? value.ToStringPercent() : value.ToString("0.##"));
        }

        private static string GetStatLabel(string statDefName)
        {
            return DefDatabase<StatDef>.GetNamedSilentFail(statDefName)?.LabelCap ?? statDefName;
        }

        private static bool IsStat(StatDef stat, string defName)
        {
            return stat != null && stat.defName == defName;
        }
    }

}

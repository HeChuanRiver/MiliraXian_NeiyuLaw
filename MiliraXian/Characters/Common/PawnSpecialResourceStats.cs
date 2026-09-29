using System.Collections.Generic;
using HarmonyLib;
using MiliraXian.Characters.Common.Stats;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Common
{
    public static class PawnSpecialResourceStats
    {
        private static readonly Dictionary<HediffDef, StatDef> maxValueStats = new();
        private static readonly Dictionary<HediffDef, StatDef> gainFactorStats = new();

        public static StatDef MaxValueStatFor(HediffDef resourceDef)
        {
            return resourceDef != null && maxValueStats.TryGetValue(resourceDef, out StatDef stat) ? stat : null;
        }

        public static StatDef GainFactorStatFor(HediffDef resourceDef)
        {
            return resourceDef != null && gainFactorStats.TryGetValue(resourceDef, out StatDef stat) ? stat : null;
        }

        internal static void Generate(bool hotReload)
        {
            StatCategoryDef category = DefDatabase<StatCategoryDef>.GetNamed("PawnCombat");
            foreach (HediffDef resourceDef in DefDatabase<HediffDef>.AllDefsListForReading)
            {
                HediffCompProperties_PawnSpecialResource props = ResourcePropsOf(resourceDef);
                if (props == null)
                {
                    continue;
                }

                StatDef maxValueStat = new StatDef
                {
                    defName = resourceDef.defName + "MaxValue",
                    label = resourceDef.label + "上限",
                    description = resourceDef.label + "上限的基础值。",
                    workerClass = typeof(StatWorker_Mutable),
                    category = category,
                    defaultBaseValue = props.maxValue,
                    toStringStyle = ToStringStyle.FloatOne,
                    showIfHediffsPresent = new List<HediffDef> { resourceDef },
                    neverDisabled = true
                };
                DefGenerator.AddImpliedDef(maxValueStat, hotReload);
                maxValueStats[resourceDef] = maxValueStat;

                StatDef gainFactorStat = new StatDef
                {
                    defName = resourceDef.defName + "GainFactor",
                    label = resourceDef.label + "获取乘数",
                    description = "每次获得" + resourceDef.label + "时，获取量乘以该值。",
                    workerClass = typeof(StatWorker_Mutable),
                    category = category,
                    defaultBaseValue = 1f,
                    toStringStyle = ToStringStyle.PercentZero,
                    showIfHediffsPresent = new List<HediffDef> { resourceDef },
                    neverDisabled = true
                };
                DefGenerator.AddImpliedDef(gainFactorStat, hotReload);
                gainFactorStats[resourceDef] = gainFactorStat;
            }
        }

        private static HediffCompProperties_PawnSpecialResource ResourcePropsOf(HediffDef def)
        {
            if (def?.comps == null)
            {
                return null;
            }

            for (int i = 0; i < def.comps.Count; i++)
            {
                if (def.comps[i] is HediffCompProperties_PawnSpecialResource props)
                {
                    return props;
                }
            }
            return null;
        }
    }

    [HarmonyPatch(typeof(DefGenerator), nameof(DefGenerator.GenerateImpliedDefs_PreResolve))]
    internal static class Patch_DefGenerator_GenerateImpliedDefs_PreResolve
    {
        public static void Postfix(bool hotReload)
        {
            PawnSpecialResourceStats.Generate(hotReload);
        }
    }
}

using System;
using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    public class Hediff_SeverityScaledStats : HediffWithComps
    {
        private HediffStage cachedStage;
        private float cachedSeverity = float.NaN;
        private int cachedStageIndex = -1;

        public override HediffStage CurStage
        {
            get
            {
                HediffStage baseStage = base.CurStage;
                if (baseStage == null)
                {
                    cachedStage = null;
                    return null;
                }

                int stageIndex = base.CurStageIndex;
                if (cachedStage == null || cachedStageIndex != stageIndex || Math.Abs(cachedSeverity - Severity) > float.Epsilon)
                {
                    RebuildCachedStage(baseStage, stageIndex);
                }

                return cachedStage;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                cachedStage = null;
                cachedSeverity = float.NaN;
                cachedStageIndex = -1;
            }
        }

        private void RebuildCachedStage(HediffStage baseStage, int stageIndex)
        {
            cachedSeverity = Severity;
            cachedStageIndex = stageIndex;

            List<StatModifier> statOffsets = CopyStatModifiers(baseStage.statOffsets);
            List<StatModifier> statFactors = CopyStatModifiers(baseStage.statFactors);
            AddSeverityOffsets(ref statOffsets, baseStage.statOffsetsBySeverity);
            AddSeverityFactors(ref statFactors, baseStage.statFactorsBySeverity);

            cachedStage = new HediffStage
            {
                minSeverity = baseStage.minSeverity,
                label = baseStage.label,
                untranslatedLabel = baseStage.untranslatedLabel,
                overrideLabel = baseStage.overrideLabel,
                becomeVisible = baseStage.becomeVisible,
                statOffsets = statOffsets,
                statFactors = statFactors
            };
        }

        private void AddSeverityOffsets(ref List<StatModifier> statOffsets, List<StatModifierBySeverity> modifiers)
        {
            if (modifiers == null)
            {
                return;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                StatModifierBySeverity modifier = modifiers[i];
                if (modifier.stat == null)
                {
                    continue;
                }

                if (!HasNonZeroOffset(statOffsets, modifier.stat))
                {
                    AddOffset(ref statOffsets, modifier.stat, modifier.valueBySeverity.Evaluate(Severity));
                }
            }
        }

        private void AddSeverityFactors(ref List<StatModifier> statFactors, List<StatModifierBySeverity> modifiers)
        {
            if (modifiers == null)
            {
                return;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                StatModifierBySeverity modifier = modifiers[i];
                if (modifier.stat == null)
                {
                    continue;
                }

                if (!HasNonDefaultFactor(statFactors, modifier.stat))
                {
                    AddFactor(ref statFactors, modifier.stat, modifier.valueBySeverity.Evaluate(Severity));
                }
            }
        }

        private static List<StatModifier> CopyStatModifiers(List<StatModifier> modifiers)
        {
            if (modifiers == null)
            {
                return null;
            }

            List<StatModifier> result = new List<StatModifier>(modifiers.Count);
            for (int i = 0; i < modifiers.Count; i++)
            {
                StatModifier modifier = modifiers[i];
                result.Add(new StatModifier { stat = modifier.stat, value = modifier.value });
            }

            return result;
        }

        private static void AddOffset(ref List<StatModifier> modifiers, StatDef stat, float value)
        {
            if (value == 0f)
            {
                return;
            }

            if (modifiers == null)
            {
                modifiers = new List<StatModifier>();
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].stat == stat)
                {
                    modifiers[i] = new StatModifier { stat = stat, value = modifiers[i].value + value };
                    return;
                }
            }

            modifiers.Add(new StatModifier { stat = stat, value = value });
        }

        private static bool HasNonZeroOffset(List<StatModifier> modifiers, StatDef stat)
        {
            if (modifiers == null)
            {
                return false;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].stat == stat && modifiers[i].value != 0f)
                {
                    return true;
                }
            }

            return false;
        }

        private static void AddFactor(ref List<StatModifier> modifiers, StatDef stat, float value)
        {
            if (Math.Abs(value - 1f) <= float.Epsilon)
            {
                return;
            }

            if (modifiers == null)
            {
                modifiers = new List<StatModifier>();
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].stat == stat)
                {
                    modifiers[i] = new StatModifier { stat = stat, value = modifiers[i].value * value };
                    return;
                }
            }

            modifiers.Add(new StatModifier { stat = stat, value = value });
        }

        private static bool HasNonDefaultFactor(List<StatModifier> modifiers, StatDef stat)
        {
            if (modifiers == null)
            {
                return false;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                if (modifiers[i].stat == stat && Math.Abs(modifiers[i].value - 1f) > float.Epsilon)
                {
                    return true;
                }
            }

            return false;
        }
    }
}

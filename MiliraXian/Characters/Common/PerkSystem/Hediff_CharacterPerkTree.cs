using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.PerkSystem
{
    public class Hediff_CharacterPerkTree : HediffWithComps, IPerkEventListener
    {
        private HediffStage cachedStage;
        private bool stageDirty = true;

        public override HediffStage CurStage
        {
            get
            {
                if (stageDirty)
                {
                    RebuildCachedStage();
                }

                return cachedStage;
            }
        }

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            RebuildCachedStage();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                stageDirty = true;
            }
        }

        public void Notify_PerkTreeChanged(Pawn pawn, HediffComp_CharacterPerkTree state)
        {
            RebuildCachedStage();
        }

        private void RebuildCachedStage()
        {
            stageDirty = false;

            Dictionary<StatDef, float> offsets = new();
            Dictionary<StatDef, float> factors = new();

            HediffStage baseStage = base.CurStage;
            AddOffsets(offsets, baseStage?.statOffsets, 1f);
            AddFactors(factors, baseStage?.statFactors, 1f);

            HediffComp_CharacterPerkTree state = GetComp<HediffComp_CharacterPerkTree>();
            if (state != null)
            {
                foreach (CharacterPerkNodeDef node in state.LearnedNodes)
                {
                    int level = state.EffectiveNodeLevel(node);
                    if (level <= 0)
                    {
                        continue;
                    }

                    AddOffsets(offsets, node.statOffsets, level);
                    AddFactors(factors, node.statFactors, level);
                }
            }

            if (offsets.Count == 0 && factors.Count == 0)
            {
                cachedStage = baseStage;
                return;
            }

            cachedStage = new HediffStage
            {
                statOffsets = ToStatModifierList(offsets),
                statFactors = ToStatModifierList(factors)
            };
        }

        private static void AddOffsets(Dictionary<StatDef, float> values, List<StatModifier> modifiers, float multiplier)
        {
            if (modifiers == null)
            {
                return;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                StatModifier modifier = modifiers[i];
                if (modifier.stat == null)
                {
                    continue;
                }

                float value = modifier.value * multiplier;
                values.TryGetValue(modifier.stat, out float current);
                values[modifier.stat] = current + value;
            }
        }

        private static void AddFactors(Dictionary<StatDef, float> values, List<StatModifier> modifiers, float scale)
        {
            if (modifiers == null)
            {
                return;
            }

            for (int i = 0; i < modifiers.Count; i++)
            {
                StatModifier modifier = modifiers[i];
                if (modifier.stat == null)
                {
                    continue;
                }

                float factor = scale == 1f ? modifier.value : StatWorker.ScaleFactor(modifier.value, scale);
                if (values.TryGetValue(modifier.stat, out float current))
                {
                    values[modifier.stat] = current * factor;
                }
                else
                {
                    values[modifier.stat] = factor;
                }
            }
        }

        private static List<StatModifier> ToStatModifierList(Dictionary<StatDef, float> values)
        {
            if (values.Count == 0)
            {
                return null;
            }

            List<StatModifier> result = new(values.Count);
            foreach (KeyValuePair<StatDef, float> pair in values)
            {
                result.Add(new StatModifier { stat = pair.Key, value = pair.Value });
            }

            return result;
        }
    }
}

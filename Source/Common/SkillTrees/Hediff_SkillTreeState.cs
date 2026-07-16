using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public class Hediff_SkillTreeState : HediffWithComps, ISkillTreeStateListener
    {
        private HediffStage cachedStage;
        private bool stageDirty = true;

        public override bool Visible => true;

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

        public void Notify_SkillTreeStateChanged(Pawn pawn, HediffComp_SkillTreeState state)
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

            HediffComp_SkillTreeState state = GetComp<HediffComp_SkillTreeState>();
            if (state != null)
            {
                foreach (SkillNodeDef node in state.LearnedNodes)
                {
                    int level = state.GetNodeLevel(node);
                    if (level <= 0)
                    {
                        continue;
                    }

                    AddOffsets(offsets, node.statOffsets, 1f);
                    AddFactors(factors, node.statFactors, 1f);
                    AddOffsets(offsets, node.statOffsetsPerLevel, level);
                    AddPerLevelFactors(factors, node.statFactorsPerLevel, level);
                }

                HashSet<SkillNodeCollectionDef> collections = new();
                foreach (SkillNodeDef node in state.LearnedNodes)
                {
                    if (node?.collection != null)
                    {
                        collections.Add(node.collection);
                    }
                }

                foreach (SkillNodeCollectionDef collection in collections)
                {
                    if (state.IsCollectionCompleted(collection))
                    {
                        AddOffsets(offsets, collection.statOffsets, 1f);
                        AddFactors(factors, collection.statFactors, 1f);
                    }
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

        private static void AddPerLevelFactors(Dictionary<StatDef, float> values, List<StatModifier> modifiers, int level)
        {
            AddFactors(values, modifiers, Mathf.Max(0, level));
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

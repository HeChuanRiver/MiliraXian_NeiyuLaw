using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace MiliraXian.Characters
{
    public class SkillNodeDef : Def
    {
        private const int TicksPerSkillPoint = 5000;

        public SkillNodeCategoryDef category;
        public SkillNodeCollectionDef collection;
        public int displayOrder;
        public int column;
        public float y = -1f;
        public bool initiallyLearned;
        public bool important;
        public int maxLevel = 1;
        public int skillPoints = 1;
        public string bookName;
        public string bookDescription;
        public int bookAcquirePriority;
        public float bookAcquireWeight = 1f;
        public List<MX_BookSpawnCondition> spawnConditions;
        public List<StatModifier> statOffsets;
        public List<StatModifier> statFactors;
        public List<StatModifier> statOffsetsPerLevel;
        public List<StatModifier> statFactorsPerLevel;
        public List<AbilityDef> grantedAbilities;
        public List<HediffDef> grantedHediffs;

        public int MaxLevel => maxLevel < 1 ? 1 : maxLevel;

        public int SkillPoints => Mathf.Max(1, skillPoints);

        public int RequiredReadingTicks => SkillPoints * TicksPerSkillPoint;

        public float BookAcquireWeight => Mathf.Max(0.01f, bookAcquireWeight);

        public bool CanSpawnFor(Pawn pawn)
        {
            return MX_BookSpawnConditionUtility.Allows(spawnConditions, pawn);
        }
    }

}

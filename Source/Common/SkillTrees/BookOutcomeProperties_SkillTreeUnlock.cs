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
    public class BookOutcomeProperties_SkillTreeUnlock : BookOutcomeProperties
    {
        public SkillNodeCategoryDef skillCategory;
        public SkillNodeDef node;
        public float learningSpeed = 1f;
        public float extraNodeChance;
        public float extractedNodeAcquireWeightFactor = 0.35f;
        public string combinedBookName;
        public string missingStateReasonKey = "MX_Common_SkillBookMissingState";
        public string dataMissingReasonKey = "MX_Common_SkillBookDataMissing";
        public string benefitKey = "MX_Common_SkillBookBenefitNodeLine";
        public string qualitySummaryKey = "MX_Common_SkillBookQualitySummary";
        public string parenthesesKey = "MX_Common_Parentheses";
        public string learnedLetterLabelKey = "MX_Common_SkillNodesLearnedLetterLabel";
        public string learnedLetterTextKey = "MX_Common_SkillNodesLearnedLetterText";
        public string levelTextKey = "MX_Common_SkillTreeStateLevel";
        public string statCategoryKey = "MX_Common_SkillBookStatCategory";

        public override Type DoerClass => typeof(BookOutcomeDoer_SkillTreeUnlock);
    }

}

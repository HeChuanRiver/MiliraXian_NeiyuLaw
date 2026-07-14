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
    public class SkillNodeCollectionDef : Def
    {
        public SkillNodeCategoryDef category;
        public int displayOrder;
        public string completionEffectLabel;
        public string completionEffectDescription;
        public List<StatModifier> statOffsets;
        public List<StatModifier> statFactors;

        public bool HasCompletionEffect => !completionEffectLabel.NullOrEmpty() || !completionEffectDescription.NullOrEmpty();
    }

}

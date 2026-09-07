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
    public class HediffCompProperties_SkillTreeState : HediffCompProperties
    {
        public List<SkillNodeCategoryDef> categories;

        public HediffCompProperties_SkillTreeState()
        {
            compClass = typeof(HediffComp_SkillTreeState);
        }
    }

}

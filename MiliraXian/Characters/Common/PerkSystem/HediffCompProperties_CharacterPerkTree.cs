using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace MiliraXian.Characters.Common.PerkSystem
{
    public class HediffCompProperties_CharacterPerkTree : HediffCompProperties
    {
        public List<CharacterPerkCategoryDef> categories;

        public HediffCompProperties_CharacterPerkTree()
        {
            compClass = typeof(HediffComp_CharacterPerkTree);
        }
    }

}

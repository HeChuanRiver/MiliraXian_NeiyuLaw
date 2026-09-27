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
    public interface ISkillTreeStateListener
    {
        void Notify_SkillTreeStateChanged(Pawn pawn, HediffComp_SkillTreeState state);
    }

}

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
    public interface IPerkEventListener
    {
        void Notify_PerkTreeChanged(Pawn pawn, HediffComp_CharacterPerkTree state);
    }

}

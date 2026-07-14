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
    public class MX_BookSpawnCondition
    {
        public List<PawnKindDef> relatedColonistPawnKinds;

        public bool Allows(Pawn pawn)
        {
            if (relatedColonistPawnKinds.NullOrEmpty())
            {
                return true;
            }

            return MX_BookSpawnConditionUtility.HasRelatedColonistOfKind(pawn, relatedColonistPawnKinds);
        }
    }

}

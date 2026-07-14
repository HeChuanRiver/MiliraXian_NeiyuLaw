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
    public static class MX_BookSpawnConditionUtility
    {
        public static bool Allows(List<MX_BookSpawnCondition> conditions, Pawn pawn)
        {
            if (conditions.NullOrEmpty())
            {
                return true;
            }

            for (int i = 0; i < conditions.Count; i++)
            {
                if (conditions[i] != null && !conditions[i].Allows(pawn))
                {
                    return false;
                }
            }

            return true;
        }

        public static bool HasRelatedColonistOfKind(Pawn pawn, List<PawnKindDef> pawnKinds)
        {
            if (pawn?.relations == null || pawnKinds.NullOrEmpty())
            {
                return false;
            }

            foreach (Pawn otherPawn in pawn.relations.PotentiallyRelatedPawns)
            {
                if (otherPawn != null
                    && !otherPawn.Dead
                    && otherPawn.IsColonist
                    && otherPawn.kindDef != null
                    && pawnKinds.Contains(otherPawn.kindDef))
                {
                    return true;
                }
            }

            return false;
        }
    }

}

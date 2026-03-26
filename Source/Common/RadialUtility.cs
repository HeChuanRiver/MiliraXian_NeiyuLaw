using System.Collections.Generic;
using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    public static class RadialUtility
    {
        public static List<Pawn> CollectHostilePawns(Map map, IntVec3 center, Pawn caster, float radius)
        {
            List<Pawn> result = new List<Pawn>();
            if (map == null || caster == null)
            {
                return result;
            }

            HashSet<Pawn> unique = new HashSet<Pawn>();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Pawn pawn = things[i] as Pawn;
                    if (pawn == null || pawn.Dead || pawn.Destroyed || pawn == caster)
                    {
                        continue;
                    }

                    if (!GenHostility.HostileTo(caster, pawn))
                    {
                        continue;
                    }

                    if (unique.Add(pawn))
                    {
                        result.Add(pawn);
                    }
                }
            }

            return result;
        }

        public static List<Pawn> CollectAllPawns(Map map, IntVec3 center, float radius)
        {
            List<Pawn> result = new List<Pawn>();
            if (map == null)
            {
                return result;
            }

            HashSet<Pawn> unique = new HashSet<Pawn>();
            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (!cell.InBounds(map))
                {
                    continue;
                }

                List<Thing> things = cell.GetThingList(map);
                for (int i = 0; i < things.Count; i++)
                {
                    Pawn pawn = things[i] as Pawn;
                    if (pawn == null || pawn.Dead || pawn.Destroyed)
                    {
                        continue;
                    }

                    if (unique.Add(pawn))
                    {
                        result.Add(pawn);
                    }
                }
            }

            return result;
        }
    }
}
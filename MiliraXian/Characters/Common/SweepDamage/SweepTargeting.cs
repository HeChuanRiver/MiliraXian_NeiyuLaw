using RimWorld;
using Verse;

namespace MiliraXian.Characters.Common.SweepDamage
{
    /// <summary>What a sweep struck, for picking its damage type.</summary>
    public enum SweepHitKind
    {
        Pawn,
        Building,
        /// <summary>Natural rock and ore (<see cref="Mineable"/>).</summary>
        Terrain,
    }

    /// <summary>
    /// Who an area attack may hit, shared by every sweep, ring and burst (and the sword's instant
    /// cone). The mod settings widen the def's faction rule: neutral things (no faction, or a faction
    /// that is neither the caster's nor its ally) and the terrain (rock walls and ore).
    /// </summary>
    public static class SweepTargeting
    {
        public static SweepHitKind KindOf(Thing thing)
        {
            if (thing is Pawn)
            {
                return SweepHitKind.Pawn;
            }
            return thing is Mineable || thing.def.mineable ? SweepHitKind.Terrain : SweepHitKind.Building;
        }

        /// <summary>The faction rule alone (the kind of thing is checked by the caller).</summary>
        public static bool FactionAllows(Pawn caster, Thing thing, SweepTargetFaction rule)
        {
            if (caster == null)
            {
                return false;
            }
            if (KindOf(thing) == SweepHitKind.Terrain)
            {
                // Rock has no side: only the setting decides.
                return NeiyuLawMod.Instance?.Settings?.SweepHitsTerrain == true;
            }
            if (rule == SweepTargetFaction.All)
            {
                return true;
            }
            if (GenHostility.HostileTo(caster, thing))
            {
                return true;
            }
            if (rule == SweepTargetFaction.HostileAndNeutral || NeiyuLawMod.Instance?.Settings?.SweepHitsNeutral == true)
            {
                return IsNeutral(caster, thing);
            }
            return false;
        }

        /// <summary>
        /// Line of sight from <paramref name="from"/> to <paramref name="thing"/>, for an area hit. A
        /// thing that blocks sight itself (a wall, a rock) is seen when the line reaches its nearest
        /// cell: the plain cell test would always fail on it, so walls and rock were never hit.
        /// </summary>
        public static bool InSight(IntVec3 from, Thing thing, Map map)
        {
            if (thing is Pawn || thing.def.Fillage != FillCategory.Full)
            {
                return GenSight.LineOfSight(from, thing.Position, map, skipFirstCell: true);
            }
            CellRect rect = thing.OccupiedRect();
            if (rect.Contains(from))
            {
                return true;
            }
            IntVec3 end = rect.ClosestCellTo(from);
            bool clear = true;
            GenSight.PointsOnLineOfSight(from, end, c =>
            {
                if (clear && c != from && !rect.Contains(c) && !c.CanBeSeenOverFast(map))
                {
                    clear = false;
                }
            });
            return clear;
        }

        /// <summary>Neither the caster's side nor its ally, nor held by it (a prisoner, a slave, a guest).</summary>
        public static bool IsNeutral(Pawn caster, Thing thing)
        {
            Faction own = caster.Faction;
            if (thing is Pawn pawn && own != null && pawn.HostFaction == own)
            {
                return false;
            }
            Faction other = thing.Faction;
            if (other == null || own == null)
            {
                return true;
            }
            return other != own && other.RelationKindWith(own) != FactionRelationKind.Ally;
        }
    }
}

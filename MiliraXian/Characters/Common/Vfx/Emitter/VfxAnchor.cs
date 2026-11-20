using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    internal enum VfxAnchorKind
    {
        /// <summary>No source: the effect only exists while someone maintains it.</summary>
        None,

        Cell,

        Position,

        Pawn,

        Thing,

        ThingComp,
    }

    /// <summary>
    /// Where an effect sits. Resolved every frame, so a destroyed pawn or despawned thing simply
    /// stops resolving — that is the whole "the source went away" story, no event hook involved.
    ///
    /// It holds the source on purpose: an anchor is only kept while the emitter lives, and the
    /// emitter is released when the source stops maintaining it.
    /// </summary>
    internal struct VfxAnchor
    {
        internal VfxAnchorKind Kind;
        internal Vector3 Position;
        internal IntVec3 Cell;
        internal Pawn Pawn;
        internal Thing Thing;
        internal ThingComp Comp;

        internal static VfxAnchor At(Vector3 position)
        {
            return new VfxAnchor { Kind = VfxAnchorKind.Position, Position = position };
        }

        internal static VfxAnchor At(IntVec3 cell)
        {
            return new VfxAnchor { Kind = VfxAnchorKind.Cell, Cell = cell };
        }

        internal static VfxAnchor Follow(Pawn pawn)
        {
            return new VfxAnchor { Kind = VfxAnchorKind.Pawn, Pawn = pawn };
        }

        internal static VfxAnchor Attached(Thing thing)
        {
            return new VfxAnchor { Kind = VfxAnchorKind.Thing, Thing = thing };
        }

        internal static VfxAnchor Attached(ThingComp comp)
        {
            return new VfxAnchor { Kind = VfxAnchorKind.ThingComp, Comp = comp };
        }

        /// <summary>Current world position on <paramref name="map"/>, or false if the source is gone.</summary>
        internal bool TryResolve(Map map, out Vector3 position)
        {
            position = Position;
            switch (Kind)
            {
                case VfxAnchorKind.Position:
                    return true;

                case VfxAnchorKind.Cell:
                    position = Cell.ToVector3Shifted();
                    return true;

                case VfxAnchorKind.Pawn:
                    if (Pawn == null || !Pawn.Spawned || Pawn.Destroyed || Pawn.Map != map)
                    {
                        return false;
                    }
                    position = Pawn.DrawPos;
                    return true;

                case VfxAnchorKind.Thing:
                    if (Thing == null || !Thing.Spawned || Thing.Destroyed || Thing.Map != map)
                    {
                        return false;
                    }
                    position = Thing.DrawPos;
                    return true;

                case VfxAnchorKind.ThingComp:
                    if (Comp?.parent == null || !Comp.parent.Spawned || Comp.parent.Destroyed || Comp.parent.Map != map)
                    {
                        return false;
                    }
                    position = Comp.parent.DrawPos;
                    return true;

                default:
                    return false;
            }
        }
    }
}

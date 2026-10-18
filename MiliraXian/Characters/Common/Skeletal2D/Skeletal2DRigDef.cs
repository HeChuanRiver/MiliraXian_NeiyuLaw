using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Skeletal2D
{
    /// <summary>
    /// One bone of a 2D skeletal rig. Coordinate contract (shared with the Blender
    /// exporter): positions are 2D in the rig plane (x right, y up, unit-quad space),
    /// mapped to game-local (x, 0, y) at draw time; angles are degrees CCW from +x.
    /// </summary>
    public class Skeletal2DBone
    {
        public string name;
        public string parent;
        public Vector2 restHead;
        public Vector2 restTail;
    }

    public class Skeletal2DRigDef : Def
    {
        /// <summary>JSON mesh asset path relative to the mod root (see Skeletal2DSkinnedMesh).</summary>
        public string meshAssetPath;
        public List<Skeletal2DBone> bones = new();
    }
}

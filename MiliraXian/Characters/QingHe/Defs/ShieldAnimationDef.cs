using MiliraXian.Characters.Common.Skeletal2D;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Defs
{
    /// <summary>
    /// Per-state wiring of the aura shield petal animation: which skeletal clip to play
    /// and how long the pose crossfade takes. Clip timing and looping live on the clip;
    /// dissolve is a shader parameter curve evaluated over normalized state time (0..1).
    /// </summary>
    public class ShieldAnimStateParams
    {
        public int transitionTicks = 12;
        public Skeletal2DClipDef clip;
        /// <summary>Playback rate multiplier for the clip (0.5 = half speed, clip lasts twice as long).</summary>
        public float speed = 1f;
        /// <summary>
        /// Clip tick to restart from when the state retriggers itself (e.g. hit while already in
        /// the hit state). The crossfade still blends from the current pose. 0 = restart from the
        /// first frame. Entering the state from another state always starts at tick 0.
        /// </summary>
        public float retriggerStartTick;
        public SimpleCurve dissolve;
        /// <summary>
        /// Display-level uniform scale of the whole shield (mesh + glows) over normalized state
        /// time. Missing curve = 1. Crossfades from the previous state's scale over transitionTicks.
        /// </summary>
        public SimpleCurve scale;
    }

    public class ShieldAnimationDef : Def
    {
        public Skeletal2DRigDef rig;
        /// <summary>
        /// Pivot of the display-level scale, in the mesh's unit-quad space (x right, y up,
        /// (0, 0) = quad center). The pivot stays fixed on screen while the shield scales.
        /// </summary>
        public Vector2 scalePivot = Vector2.zero;
        public ShieldAnimStateParams idle = new();
        public ShieldAnimStateParams hit = new();
        public ShieldAnimStateParams breakState = new();
    }
}

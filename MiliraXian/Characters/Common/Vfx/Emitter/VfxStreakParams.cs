using System.Collections.Generic;
using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// What a streak stroke needs: a width, how much the tail fades, and where the streak sits.
    ///
    /// A streak is one thin quad per particle: the particle's position is its head, its velocity
    /// gives the direction to the tail and its size gives the length.
    /// </summary>
    internal sealed class VfxStreakParams
    {
        internal VfxStreakParams()
        {
            Width = 0.02f;
            TailAlpha = 0.25f;
            Altitude = 0f;
            Bindings = new List<VfxUniformBinding>();
        }

        /// <summary>Cross-section width in cells.</summary>
        internal float Width;

        /// <summary>Alpha at the tail, as a share of the head's alpha.</summary>
        internal float TailAlpha;

        /// <summary>World height of the streak; zero or less keeps the particle's own height.</summary>
        internal float Altitude;

        internal List<VfxUniformBinding> Bindings;
    }
}

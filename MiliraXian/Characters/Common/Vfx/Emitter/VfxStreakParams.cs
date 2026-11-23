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
            UseAltitude = false;
            Altitude = 0f;
            AltitudeOffset = 0f;
            Bindings = new List<VfxUniformBinding>();
        }

        /// <summary>Cross-section width in cells.</summary>
        internal float Width;

        /// <summary>Alpha at the tail, as a share of the head's alpha.</summary>
        internal float TailAlpha;

        internal bool UseAltitude;

        /// <summary>World height of the streak when <see cref="UseAltitude"/> is true (includes <see cref="AltitudeOffset"/>).</summary>
        internal float Altitude;

        internal float AltitudeOffset;

        internal List<VfxUniformBinding> Bindings;
    }
}

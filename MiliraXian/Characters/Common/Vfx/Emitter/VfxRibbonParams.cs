using System.Collections.Generic;
using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// Runtime parameters for <see cref="RibbonRenderer"/>: strip width, UV layout and optional channels.
    /// </summary>
    internal sealed class VfxRibbonParams
    {
        internal VfxRibbonParams()
        {
            Width = 0.2f;
            UseParticleSize = true;
            UseParticleColor = true;
            Tint = Color.white;
            VMin = 0f;
            VMax = 1f;
            VFrames = 1;
            FrameChannel = -1;
            NormalizeU = false;
            UScale = 1f;
            UChannel = -1;
            RibbonIdChannel = -1;
            UseAltitude = false;
            Altitude = 0f;
            AltitudeOffset = 0f;
            Bindings = new List<VfxUniformBinding>();
        }

        internal float Width;
        internal bool UseParticleSize;
        internal bool UseParticleColor;
        internal Color Tint;
        internal float VMin;
        internal float VMax;
        internal int VFrames;
        internal int FrameChannel;
        internal bool NormalizeU;
        internal float UScale;
        internal int UChannel;
        internal int RibbonIdChannel;
        internal bool UseAltitude;
        internal float Altitude;
        internal float AltitudeOffset;
        internal List<VfxUniformBinding> Bindings;
    }
}

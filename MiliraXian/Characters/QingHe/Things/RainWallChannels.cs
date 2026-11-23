using MiliraXian.Characters.Common.Vfx.Emitter;
using Verse;

namespace MiliraXian.Characters.QingHe.Things
{
    /// <summary>
    /// The rain wall's vec4 channels. Registered at startup so they are ready before
    /// <c>VfxDefCompiler</c> compiles and prewarms effects.
    /// </summary>
    [StaticConstructorOnStartup]
    internal static class RainWallChannels
    {
        /// <summary>The wall's frame: (along.x, along.y, across.x, across.y).</summary>
        internal static readonly int Axis = VfxChannels.RegisterVector("rainWallAxis");

        /// <summary>The wall's band: (halfLength, halfThickness, seconds, strength).</summary>
        internal static readonly int Band = VfxChannels.RegisterVector("rainWallBand");

        /// <summary>The quad that covers the band: (halfX, halfZ, 0, 0), in cells.</summary>
        internal static readonly int Quad = VfxChannels.RegisterVector("rainWallQuad");
    }
}

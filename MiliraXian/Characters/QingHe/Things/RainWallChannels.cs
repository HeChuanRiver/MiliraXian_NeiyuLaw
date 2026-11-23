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

        /// <summary>Four lotus flowers: (along, across, radius, openStartSeconds).</summary>
        internal static readonly int LotusA = VfxChannels.RegisterVector("rainWallLotusA");
        internal static readonly int LotusB = VfxChannels.RegisterVector("rainWallLotusB");
        internal static readonly int LotusC = VfxChannels.RegisterVector("rainWallLotusC");
        internal static readonly int LotusD = VfxChannels.RegisterVector("rainWallLotusD");

        /// <summary>Five wind wave packets for water refraction: (along, across, wavelength, amplitude).</summary>
        internal static readonly int WindA = VfxChannels.RegisterVector("rainWallWindA");
        internal static readonly int WindB = VfxChannels.RegisterVector("rainWallWindB");
        internal static readonly int WindC = VfxChannels.RegisterVector("rainWallWindC");
        internal static readonly int WindD = VfxChannels.RegisterVector("rainWallWindD");
        internal static readonly int WindE = VfxChannels.RegisterVector("rainWallWindE");
    }
}

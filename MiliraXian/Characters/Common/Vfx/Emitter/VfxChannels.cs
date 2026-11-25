namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// Conventional channel names shared by the built-in renderers and defs.
    /// Actual storage slots are allocated per <see cref="VfxEffect"/>, so effects never compete for
    /// a global channel budget.
    /// </summary>
    internal static class VfxChannels
    {
        internal const string Radius = "radius";
        internal const string Strength = "strength";
        internal const string Progress = "progress";
        internal const string Alpha = "alpha";
        internal const string Angle = "angle";
    }
}

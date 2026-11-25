namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// A reference to one emitter on one map's host.
    ///
    /// The slot index is reused when an emitter is released, so <see cref="Version"/> is what makes
    /// a stale handle inert: writing through it after the effect died does nothing instead of
    /// touching whatever took the slot next.
    /// </summary>
    internal readonly struct VfxEmitterHandle
    {
        internal readonly MapComponent_VfxEmitterHost Host;
        internal readonly int Slot;
        internal readonly int Version;

        internal VfxEmitterHandle(MapComponent_VfxEmitterHost host, int slot, int version)
        {
            Host = host;
            Slot = slot;
            Version = version;
        }

        internal bool IsNone => Host == null || Slot < 0;

        internal static VfxEmitterHandle None => default;
    }
}

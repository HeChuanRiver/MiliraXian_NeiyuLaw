using RimWorld;

namespace MiliraXian.Characters.Biography
{
    // Keep the public type for old XML/patch references. The active entry is now
    // Neiyu's cultivation command, which opens an independent responsive window.
    public sealed class ITab_Biography : ITab
    {
        public override bool IsVisible => false;
        protected override void FillTab() { }
    }
}

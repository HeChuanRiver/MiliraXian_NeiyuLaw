using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// Resolved per-Def assets a renderer needs: the stroke's material and its bloom settings.
    ///
    /// Both are produced once when the def is compiled (see <see cref="VfxEffect"/>), so a renderer
    /// never touches the material cache or the def database while drawing.
    /// </summary>
    internal sealed class VfxAssets
    {
        private readonly MapComponent_VfxEmitterHost host;

        internal VfxAssets(MapComponent_VfxEmitterHost host)
        {
            this.host = host;
        }

        /// <summary>The material of a stroke, or null when its texture or shader was missing.</summary>
        internal Material Material(VfxPass pass)
        {
            return pass.Material;
        }

        /// <summary>The bloom settings of a stroke, or null when it asks for none.</summary>
        internal ScreenBloomProperties Bloom(VfxPass pass)
        {
            return host.BloomOf(pass);
        }
    }
}

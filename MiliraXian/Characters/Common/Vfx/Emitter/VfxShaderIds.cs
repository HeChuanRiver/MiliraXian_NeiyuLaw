using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// The shader property ids every VFX material in this mod shares, declared once so that
    /// "which uniform is the colour / the brightness / the vertex alpha scale" stops being
    /// re-decided per effect.
    ///
    /// These are the names the mod's own shaders declare (see <c>1.7/Shaders/Map/*.frag.glsl</c>),
    /// not vanilla ids; renaming one here means renaming it in the shader too.
    /// </summary>
    internal static class VfxShaderIds
    {
        /// <summary>Per-draw tint, multiplied into the texture.</summary>
        public static readonly int Color = Shader.PropertyToID("_Color");

        public static readonly int MainTex = Shader.PropertyToID("_MainTex");

        /// <summary>Brightness multiplier; the trail shader scales the final colour with it.</summary>
        public static readonly int Brightness = Shader.PropertyToID("_Brightness");

        /// <summary>Vertex alpha is stored divided by this, so opacity above 1 survives 8-bit colour.</summary>
        public static readonly int AlphaScale = Shader.PropertyToID("_AlphaScale");

        /// <summary>Upper clamp of the final opacity; a large value means "no clamp".</summary>
        public static readonly int AlphaMax = Shader.PropertyToID("_AlphaMax");

        /// <summary>1 = additive blending, 0 = premultiplied alpha.</summary>
        public static readonly int Additive = Shader.PropertyToID("_Additive");

        /// <summary>Written by <see cref="ScreenBloom"/> for its off-screen submission only.</summary>
        public static readonly int BloomTint = Shader.PropertyToID("_BloomTint");

        /// <summary>Written by <see cref="ScreenBloom"/> for its off-screen submission only.</summary>
        public static readonly int BloomIntensity = Shader.PropertyToID("_BloomIntensity");

        /// <summary>The generic vec4 an effect packs its own tuning into.</summary>
        public static readonly int Parameters = Shader.PropertyToID("_Params");

        /// <summary>Pass-level quad extents: (quadSize.x, quadSize.y, quadSize.y / quadSize.x, 0).</summary>
        public static readonly int QuadSize = Shader.PropertyToID("_QuadSize");
    }
}

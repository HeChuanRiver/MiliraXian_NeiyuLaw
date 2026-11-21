using System.Collections.Generic;
using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// What a field stroke needs beyond the generic pass: the quad it covers, how that quad follows
    /// the field's radius, and which uniforms its shader reads.
    ///
    /// The uniform list is data on purpose: Spring Flow wants `_Params`/`_FieldRadius`, Rain Wall
    /// wants `_Axis`/`_Band`/lotus columns — a renderer that hard-coded either would have to know
    /// the effect.
    /// </summary>
    internal sealed class VfxFieldParams
    {
        internal VfxFieldParams()
        {
            QuadSize = new Vector2(1f, 1f);
            ExtraHeight = 0f;
            SizeFromRadius = false;
            RadiusPadding = 3f;
            Altitude = 0f;
            CenterUniform = 0;
            SizeUniform = 0;
            RadiusChannel = -1;
            StrengthChannel = -1;
            SizeChannel = -1;
            Bindings = new List<VfxUniformBinding>();
        }

        /// <summary>Quad extent in cells, before any radius scaling or extra height.</summary>
        internal Vector2 QuadSize;

        /// <summary>Extra north-south extent (a rising curtain spills past the field).</summary>
        internal float ExtraHeight;

        /// <summary>When true the quad follows the <c>radius</c> channel: size = 2R + <see cref="RadiusPadding"/>.</summary>
        internal bool SizeFromRadius;

        internal float RadiusPadding;

        /// <summary>World height of the quad; zero or less keeps the particle's own height.</summary>
        internal float Altitude;

        /// <summary>
        /// Uniform the renderer writes the quad's centre into as a vec4, for shaders that rebuild
        /// their geometry from it. Zero means "do not write one".
        /// </summary>
        internal int CenterUniform;

        /// <summary>
        /// Uniform the renderer writes the quad's extents into as (sizeX, sizeZ, 0, 0). A field shader
        /// that rebuilds its geometry from uv needs them; one edge length is not enough for a quad
        /// that covers an oriented band.
        /// </summary>
        internal int SizeUniform;

        /// <summary>
        /// A vec4 channel carrying the quad's half extents in cells as (halfX, halfZ). Needed when
        /// the quad only has to cover an oriented band, so its size is not a pass constant.
        /// </summary>
        internal int SizeChannel;

        /// <summary>Uniforms written every frame, on top of the two geometry uniforms above.</summary>
        internal List<VfxUniformBinding> Bindings;

        /// <summary>
        /// The channel carrying <c>radius</c>, or -1 for none. The instanced path reads channels
        /// directly instead of binding them to uniforms, because they travel with the instance.
        /// </summary>
        internal int RadiusChannel;

        /// <summary>The channel carrying <c>strength</c> (0..1), or -1 for none.</summary>
        internal int StrengthChannel;
    }

    /// <summary>
    /// What a sprite stroke needs: how each particle becomes a quad (size, rotation, colour), and how
    /// its texture is sliced.
    ///
    /// Like the field, everything here is data so one renderer serves every sprite effect.
    /// </summary>
    internal sealed class VfxSpriteParams
    {
        internal VfxSpriteParams()
        {
            QuadSize = new Vector2(1f, 1f);
            UseParticleSize = true;
            UseParticleRotation = true;
            UseParticleColor = true;
            Tint = Color.white;
            AtlasColumns = 1;
            AtlasRows = 1;
            RotationRate = 0f;
            FrameChannel = -1;
            UseUvRect = false;
            UvMin = Vector2.zero;
            UvMax = Vector2.one;
            Altitude = 0f;
            Bindings = new List<VfxUniformBinding>();
        }

        /// <summary>Base size in cells, multiplied by the particle's size when that is used.</summary>
        internal Vector2 QuadSize;

        internal bool UseParticleSize;

        internal bool UseParticleRotation;

        internal bool UseParticleColor;

        /// <summary>Colour multiplier applied on top of the particle's own colour.</summary>
        internal Color Tint;

        /// <summary>Horizontal and vertical slice counts of the texture; 1 × 1 means "whole texture".</summary>
        internal int AtlasColumns;
        internal int AtlasRows;

        /// <summary>
        /// Degrees per second the instanced mode hands to the shader. There is no per-particle rotation
        /// field on purpose: the shader derives the angle from <c>spawnSeconds</c> and the clock.
        /// </summary>
        internal float RotationRate;

        /// <summary>Channel holding the atlas frame index, or -1 for a fixed frame 0.</summary>
        internal int FrameChannel;

        /// <summary>
        /// Use <see cref="UvMin"/>/<see cref="UvMax"/> verbatim instead of an atlas slice. Some shaders
        /// read the uv as an offset in cells from the quad's centre rather than as a texture coordinate.
        /// </summary>
        internal bool UseUvRect;

        internal Vector2 UvMin;
        internal Vector2 UvMax;

        /// <summary>World height of every quad; zero or less keeps the particle's own height.</summary>
        internal float Altitude;

        internal List<VfxUniformBinding> Bindings;
    }
}

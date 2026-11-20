using System.Collections.Generic;
using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// One stroke of an effect: which renderer draws it, on which layer, with which material and
    /// parameters.
    ///
    /// An effect is a list of these, not a single renderer. That is what lets an effect grow —
    /// Spring Flow starts as one boundary curtain today and may later add a ground stain, ripples
    /// or droplets — by appending passes, without replacing the existing ones or teaching a
    /// renderer about the effect.
    /// </summary>
    internal sealed class VfxPass
    {
        internal VfxPass()
        {
            Renderer = null;
            Texture = null;
            TexturePath = null;
            ShaderDefName = null;
            MaterialDefName = null;
            Material = null;
            Layer = 0;
            BloomGroup = null;
            BloomIntensity = 1f;
            ParticleGroup = -1;
            Mesh = null;
            InstanceVectors = 0;
            Field = null;
            Sprite = null;
            Streak = null;
            Name = "pass";
        }

        /// <summary>
        /// The material this stroke draws with, resolved once when the def is compiled. It belongs to
        /// the def-level spec, not to any emitter: every emitter of the def draws with the same one.
        /// </summary>
        internal Material Material;

        /// <summary>The renderer kind that draws this stroke. It draws one stroke, nothing else.</summary>
        internal IVfxRenderer Renderer;

        /// <summary>
        /// A texture handed over directly, for shaders that want an engine-owned texture with no
        /// content path (the white base texture, for instance). Takes precedence over <see cref="TexturePath"/>.
        /// </summary>
        internal Texture2D Texture;

        /// <summary>Texture path resolved through the material cache; empty for shader-only strokes.</summary>
        internal string TexturePath;

        /// <summary>A ShaderDef defName the material cache resolves.</summary>
        internal string ShaderDefName;

        /// <summary>An XML MaterialDef defName, for materials authored in XML instead of by shader.</summary>
        internal string MaterialDefName;

        /// <summary>The render layer of this stroke; content decides it and nothing else may guess.</summary>
        internal int Layer;

        /// <summary>A ScreenBloomGroupDef defName, or null for no bloom on this stroke.</summary>
        internal string BloomGroup;

        internal float BloomIntensity;

        /// <summary>
        /// Which particles this stroke draws: a group id, or -1 for all of them. It lets one emitter
        /// hold several kinds of particle (rain strands and their splashes) and still keep each
        /// stroke drawing only its own.
        /// </summary>
        internal int ParticleGroup;

        /// <summary>Parameters read by <see cref="FieldRenderer"/>; null for other renderer kinds.</summary>
        internal VfxFieldParams Field;

        /// <summary>
        /// The shared mesh an instanced stroke draws: one copy of the shape, reused for every instance.
        /// Null for strokes that build their own geometry per emitter.
        /// </summary>
        internal Mesh Mesh;

        /// <summary>
        /// How many vec4 of per-instance data this stroke's shader reads (1..4). Zero means the stroke
        /// does not use instancing.
        /// </summary>
        internal int InstanceVectors;

        /// <summary>Parameters read by <see cref="SpriteRenderer"/>; null for other renderer kinds.</summary>
        internal VfxSpriteParams Sprite;

        /// <summary>Parameters read by <see cref="StreakRenderer"/>; null for other renderer kinds.</summary>
        internal VfxStreakParams Streak;

        /// <summary>Diagnostics only.</summary>
        internal string Name;
    }

    /// <summary>What a renderer may be handed: the emitter, its own pass, and the services it may use.</summary>
    internal interface IVfxRenderer
    {
        void Render(VfxEmitter emitter, VfxPass pass, int passIndex, in VfxRenderContext context);
    }
}

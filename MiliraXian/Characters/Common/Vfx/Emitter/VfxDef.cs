using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// The authored definition of one effect: how long it lives and which strokes draw it.
    ///
    /// Content lives here rather than in C# constants: tuning, textures, layers and the pass list
    /// are what an author edits, and <see cref="ConfigErrors"/> turns every typo into a startup
    /// error instead of a silently missing effect.
    /// </summary>
    public class VfxDef : Def
    {
        /// <summary>How long the emitter takes to fade once <c>Maintain</c> stops.</summary>
        public float fadeOutSeconds = 0.35f;

        /// <summary>Total life; zero or less means "until <c>Maintain</c> stops".</summary>
        public float lifetimeSeconds;

        /// <summary>Upper bound on simultaneous particles; the emitter allocates for this once.</summary>
        public int capacity = 1;

        /// <summary>The strokes, drawn in order. Add new ones on the end; never rewrite the old ones.</summary>
        public List<VfxPassDef> passes = new();

        /// <summary>
        /// A <see cref="VfxSpec"/> subclass that backs this effect's per-vfx object — one instance
        /// for every effect of this def. Optional: defs whose look the data already describes leave
        /// it empty.
        /// </summary>
        public string vfxClass;

        /// <summary>
        /// A <see cref="VfxEmitter"/> subclass instantiated once per spawn, for code paths that keep
        /// state of their own. Optional.
        /// </summary>
        public string emitterClass;

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (capacity < 1)
            {
                yield return "capacity must be at least 1";
            }
            if (fadeOutSeconds < 0f)
            {
                yield return "fadeOutSeconds must not be negative";
            }
            if (lifetimeSeconds < 0f)
            {
                yield return "lifetimeSeconds must not be negative";
            }
            if (!vfxClass.NullOrEmpty() && !VfxDefCompiler.IsValidVfxClass(vfxClass))
            {
                yield return "vfxClass '" + vfxClass + "' is not a VfxSpec";
            }
            if (!emitterClass.NullOrEmpty() && !VfxDefCompiler.IsValidEmitterClass(emitterClass))
            {
                yield return "emitterClass '" + emitterClass + "' is not a VfxEmitter";
            }
            if (passes.NullOrEmpty())
            {
                yield return "has no passes";
                yield break;
            }

            for (int i = 0; i < passes.Count; i++)
            {
                VfxPassDef pass = passes[i];
                if (pass == null)
                {
                    yield return "passes[" + i + "] is null";
                    continue;
                }

                string prefix = "passes[" + i + "] (" + pass.name + "): ";
                if (!VfxRenderers.TryGet(pass.renderer, out _))
                {
                    yield return prefix + "unknown renderer '" + pass.renderer + "'";
                }
                if (!pass.shaderDef.NullOrEmpty() && DefDatabase<ShaderDef>.GetNamedSilentFail(pass.shaderDef) == null)
                {
                    yield return prefix + "missing shader '" + pass.shaderDef + "'";
                }
                if (!pass.materialDef.NullOrEmpty() && DefDatabase<MaterialDef>.GetNamedSilentFail(pass.materialDef) == null)
                {
                    yield return prefix + "missing material '" + pass.materialDef + "'";
                }
                // Texture existence is deliberately not checked here: ConfigErrors may run off the
                // main thread, where ContentFinder is not allowed. The material compiler checks it
                // on the main thread and logs once.
                if (!pass.texture.NullOrEmpty() && pass.texture != "White" && pass.texture.Contains("\\"))
                {
                    yield return prefix + "texture path must use '/' separators: '" + pass.texture + "'";
                }
                if (!pass.bloomGroup.NullOrEmpty()
                    && DefDatabase<ScreenBloomGroupDef>.GetNamedSilentFail(pass.bloomGroup) == null)
                {
                    yield return prefix + "missing bloom group '" + pass.bloomGroup + "'";
                }

                if (pass.renderer == VfxRenderers.Field && pass.field == null)
                {
                    yield return prefix + "the Field renderer needs a <field> block";
                }
                if (pass.renderer == VfxRenderers.Sprite && pass.sprite == null)
                {
                    yield return prefix + "the Sprite renderer needs a <sprite> block";
                }
                if (pass.renderer == VfxRenderers.Streak && pass.streak == null)
                {
                    yield return prefix + "the Streak renderer needs a <streak> block";
                }

                ValidateBindings(pass.field?.bindings, prefix + "field");
                ValidateBindings(pass.sprite?.bindings, prefix + "sprite");
                ValidateBindings(pass.streak?.bindings, prefix + "streak");

                if (pass.field != null && pass.field.sizeFromRadius && pass.field.radiusPadding < 0f)
                {
                    yield return prefix + "field.radiusPadding must not be negative";
                }
                if (pass.sprite != null)
                {
                    if (pass.sprite.atlasColumns < 1 || pass.sprite.atlasRows < 1)
                    {
                        yield return prefix + "sprite.atlasColumns/atlasRows must be at least 1";
                    }
                    if (!pass.sprite.frameChannel.NullOrEmpty()
                        && !VfxChannels.TryFind(pass.sprite.frameChannel, out _))
                    {
                        yield return prefix + "sprite.frameChannel uses unknown channel '" + pass.sprite.frameChannel + "'";
                    }
                }
            }
        }

        /// <summary>Shared checks for a pass's uniform bindings, whichever renderer reads them.</summary>
        private static IEnumerable<string> ValidateBindings(List<VfxBindingDef> bindings, string label)
        {
            if (bindings == null)
            {
                yield break;
            }
            for (int b = 0; b < bindings.Count; b++)
            {
                VfxBindingDef binding = bindings[b];
                if (binding == null)
                {
                    yield return label + ".bindings[" + b + "] is null";
                    continue;
                }
                if (binding.uniform.NullOrEmpty())
                {
                    yield return label + ".bindings[" + b + "] has no uniform";
                    continue;
                }
                if (binding.component < -1 || binding.component > 3)
                {
                    yield return label + ".bindings[" + b + "].component must be -1..3";
                }
                if (!binding.channel.NullOrEmpty() && !VfxChannels.TryFind(binding.channel, out _))
                {
                    yield return label + ".bindings[" + b + "] uses unknown channel '" + binding.channel + "'";
                }
                if (!binding.vecChannel.NullOrEmpty() && !VfxChannels.TryFindVector(binding.vecChannel, out _))
                {
                    yield return label + ".bindings[" + b + "] uses unknown vec4 channel '" + binding.vecChannel + "'";
                }
            }
        }
    }

    /// <summary>One stroke: which renderer draws it, with which material, on which layer.</summary>
    public class VfxPassDef
    {
        public string name = "pass";

        /// <summary>A renderer kind name; see <see cref="VfxRenderers"/>.</summary>
        public string renderer = VfxRenderers.Field;

        /// <summary>A texture path, or <c>White</c> for the engine's white base texture.</summary>
        public string texture;

        public string shaderDef;

        /// <summary>An XML MaterialDef, used instead of shader + texture when set.</summary>
        public string materialDef;

        public int layer;

        public string bloomGroup;

        public float bloomIntensity = 1f;

        /// <summary>Which particle group this stroke draws, or -1 for every particle.</summary>
        public int particleGroup = -1;

        /// <summary>
        /// Draws this stroke as instances of one shared unit mesh: <c>quad</c>/<c>plane</c> selects the
        /// built-in shape, and <see cref="instanceVectors"/> says how many vec4 the shader reads per
        /// instance. Empty means "build the geometry per emitter" (the non-instanced path).
        /// </summary>
        public string instanceMesh;

        /// <summary>Vec4 of per-instance data the shader reads (1..4); 0 for the non-instanced path.</summary>
        public int instanceVectors;

        /// <summary>Parameters read by the Field renderer; required for that kind.</summary>
        public VfxFieldDef field;

        /// <summary>Parameters read by the Sprite renderer; required for that kind.</summary>
        public VfxSpriteDef sprite;

        /// <summary>Parameters read by the Streak renderer; required for that kind.</summary>
        public VfxStreakDef streak;
    }

    /// <summary>How a Streak stroke turns each particle into a thin quad.</summary>
    public class VfxStreakDef
    {
        /// <summary>Cross-section width in cells.</summary>
        public float width = 0.02f;

        /// <summary>Alpha at the tail, as a share of the head's alpha.</summary>
        public float tailAlpha = 0.25f;

        public bool useAltitude;

        public AltitudeLayer altitude = AltitudeLayer.MoteOverhead;

        public List<VfxBindingDef> bindings = new();
    }

    /// <summary>How a Sprite stroke turns each particle into a quad.</summary>
    public class VfxSpriteDef
    {
        /// <summary>Ground (flat on the floor), Vertical (standing) or Billboard (camera-facing).</summary>
        public VfxSpritePlane plane = VfxSpritePlane.Ground;

        /// <summary>Base size in cells; multiplied by the particle's size when that is used.</summary>
        public Vector2 quadSize = new(1f, 1f);

        public bool useParticleSize = true;

        public bool useParticleRotation = true;

        public bool useParticleColor = true;

        /// <summary>Colour multiplier applied on top of each particle's colour.</summary>
        public Color tint = Color.white;

        public int atlasColumns = 1;

        public int atlasRows = 1;

        /// <summary>Channel holding the atlas frame index; empty for a fixed frame.</summary>
        public string frameChannel;

        /// <summary>Use the uv rect below verbatim instead of an atlas slice (uv as cell offsets).</summary>
        public bool useUvRect;

        public Vector2 uvMin = Vector2.zero;

        public Vector2 uvMax = Vector2.one;

        public bool useAltitude;

        public AltitudeLayer altitude = AltitudeLayer.MoteLow;

        public List<VfxBindingDef> bindings = new();
    }

    /// <summary>Which way a sprite quad faces. Mirrors the runtime plane enum.</summary>
    public enum VfxSpritePlane
    {
        Ground,
        Vertical,
        Billboard,
    }

    /// <summary>What a Field stroke covers and which uniforms its shader reads.</summary>
    public class VfxFieldDef
    {
        /// <summary>Quad extent in cells, used when <see cref="sizeFromRadius"/> is off.</summary>
        public Vector2 quadSize = new(1f, 1f);

        /// <summary>Extra north-south extent (a rising curtain spills past the field).</summary>
        public float extraHeight;

        /// <summary>When true the quad follows the <c>radius</c> channel: size = 2R + padding.</summary>
        public bool sizeFromRadius;

        public float radiusPadding = 3f;

        /// <summary>When true the stroke sits at <see cref="altitude"/> instead of the particle's height.</summary>
        public bool useAltitude = true;

        public AltitudeLayer altitude = AltitudeLayer.MoteLow;

        /// <summary>Uniform for the quad's centre (vec4), or empty for none.</summary>
        public string centerUniform;

        /// <summary>Uniform for the quad's edge length, or empty for none.</summary>
        public string sizeUniform;

        /// <summary>Channel carrying the field's radius, for the instanced path.</summary>
        public string radiusChannel;

        /// <summary>Channel carrying the field's strength 0..1, for the instanced path.</summary>
        public string strengthChannel;

        public List<VfxBindingDef> bindings = new();
    }

    /// <summary>One uniform write: a constant, or a slot filled from a particle channel.</summary>
    public class VfxBindingDef
    {
        public string uniform;

        /// <summary>-1 writes a float uniform; 0..3 fills that slot of a vec4.</summary>
        public int component = -1;

        /// <summary>A channel name, or empty to write <see cref="value"/> as-is.</summary>
        public string channel;

        /// <summary>A vec4 channel name; when set it replaces the whole uniform with that vector.</summary>
        public string vecChannel;

        /// <summary>A camera-derived frame value: <c>screenPerCell</c>. Same for every stroke, per frame.</summary>
        public string frame;

        public Vector4 value;
    }
}

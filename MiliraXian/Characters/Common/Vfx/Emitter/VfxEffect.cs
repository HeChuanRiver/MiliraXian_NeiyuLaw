using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// What an effect is: a name, how it lives and dies, and the list of strokes that draw it.
    ///
    /// This is the effect's per-vfx object — the one instance every emitter of the def shares, like
    /// vanilla's <c>DamageDef.Worker</c> or the <c>Graphic</c> built from a GraphicData. A def may
    /// name its own subclass through <c>vfxClass</c> when the effect needs code beyond the data.
    /// </summary>
    internal class VfxEffect
    {
        /// <summary>
        /// Builds the whole runtime description from the def: the effect's own data lives here, and
        /// there is no separate compile step for a caller to forget.
        /// </summary>
        internal VfxEffect(VfxDef def)
        {
            Name = def?.defName ?? "vfx";
            FadeOutSeconds = 0.35f;
            LifetimeSeconds = 0f;
            Capacity = 1;
            Passes = new List<VfxPass>();
            EmitterType = typeof(VfxEmitter);
            Build(def);
        }

        /// <summary>Diagnostics only; never used to find anything.</summary>
        internal string Name { get; }

        /// <summary>How long the emitter takes to fade once <c>Maintain</c> stops.</summary>
        internal float FadeOutSeconds { get; set; }

        /// <summary>Total life; zero or less means "until <c>Maintain</c> stops".</summary>
        internal float LifetimeSeconds { get; set; }

        /// <summary>Upper bound on simultaneous particles; the emitter allocates for this once.</summary>
        internal int Capacity { get; set; }

        /// <summary>The strokes, drawn in order. Add new ones on the end; never rewrite the old ones.</summary>
        internal List<VfxPass> Passes { get; }

        /// <summary>The class each spawn instantiates, from the def's <c>emitterClass</c>.</summary>
        internal System.Type EmitterType { get; set; }

        /// <summary>
        /// Fills this spec from the def: the default reads the def's authored passes and resolves
        /// their names into runtime handles. Runs once per def, from the constructor, so no caller
        /// has to remember a separate compile step.
        ///
        /// A <c>vfxClass</c> subclass overrides it to add strokes of its own; call the base first.
        /// </summary>
        internal virtual void Build(VfxDef def)
        {
            if (def == null)
            {
                return;
            }
            FadeOutSeconds = Mathf.Max(0f, def.fadeOutSeconds);
            LifetimeSeconds = Mathf.Max(0f, def.lifetimeSeconds);
            Capacity = Mathf.Max(1, def.capacity);

            if (def.passes == null)
            {
                return;
            }
            for (int i = 0; i < def.passes.Count; i++)
            {
                VfxPass pass = BuildPass(def.passes[i]);
                if (pass != null)
                {
                    Passes.Add(pass);
                }
            }
        }

        private static VfxPass BuildPass(VfxPassDef source)
        {
            if (source == null || !VfxRenderers.TryGet(source.renderer, out IVfxRenderer renderer))
            {
                return null;
            }

            VfxPass pass = new VfxPass
            {
                Name = source.name,
                Renderer = renderer,
                Texture = source.texture == "White" ? BaseContent.WhiteTex : null,
                TexturePath = source.texture == "White" ? null : source.texture,
                ShaderDefName = source.shaderDef,
                MaterialDefName = source.materialDef.NullOrEmpty() ? null : source.materialDef,
                Material = ResolveMaterial(source),
                Layer = source.layer,
                BloomGroup = source.bloomGroup.NullOrEmpty() ? null : source.bloomGroup,
                BloomIntensity = source.bloomIntensity,
                ParticleGroup = source.particleGroup,
                Mesh = InstanceMesh(source.instanceMesh),
                InstanceVectors = Mathf.Clamp(source.instanceVectors, 0, VfxInstanceBatch.MaxVectors),
            };

            if (source.field != null)
            {
                pass.Field = BuildField(source.field);
            }            if (source.sprite != null)
            {
                pass.Sprite = BuildSprite(source.sprite);
            }
            if (source.streak != null)
            {
                pass.Streak = BuildStreak(source.streak);
            }
            return pass;
        }

        private static VfxStreakParams BuildStreak(VfxStreakDef source)
        {
            VfxStreakParams streak = new VfxStreakParams
            {
                Width = source.width,
                TailAlpha = Mathf.Clamp01(source.tailAlpha),
                Altitude = source.useAltitude ? source.altitude.AltitudeFor() : 0f,
            };
            AddBindings(streak.Bindings, source.bindings);
            return streak;
        }

        private static VfxSpriteParams BuildSprite(VfxSpriteDef source)
        {
            VfxSpriteParams sprite = new VfxSpriteParams
            {
                Plane = source.plane == VfxSpritePlane.Vertical ? VfxPlane.Vertical
                    : source.plane == VfxSpritePlane.Billboard ? VfxPlane.Billboard
                    : VfxPlane.Ground,
                QuadSize = source.quadSize,
                UseParticleSize = source.useParticleSize,
                UseParticleRotation = source.useParticleRotation,
                UseParticleColor = source.useParticleColor,
                Tint = source.tint,
                AtlasColumns = Mathf.Max(1, source.atlasColumns),
                AtlasRows = Mathf.Max(1, source.atlasRows),
                FrameChannel = source.frameChannel.NullOrEmpty()
                    ? -1
                    : VfxChannels.TryFind(source.frameChannel, out int frame) ? frame : -1,
                UseUvRect = source.useUvRect,
                UvMin = source.uvMin,
                UvMax = source.uvMax,
                Altitude = source.useAltitude ? source.altitude.AltitudeFor() : 0f,
            };

            AddBindings(sprite.Bindings, source.bindings);
            return sprite;
        }

        /// <summary>
        /// The stroke's material, resolved once at compile time. Pooled materials are never destroyed
        /// here; they belong to <see cref="MaterialPool"/>.
        /// </summary>
        private static Material ResolveMaterial(VfxPassDef source)
        {
            if (!source.materialDef.NullOrEmpty())
            {
                return VfxMaterialCache.FromMaterialDef(source.materialDef);
            }
            if (source.texture == "White")
            {
                return VfxMaterialCache.FromShader(BaseContent.WhiteTex, source.shaderDef);
            }
            return VfxMaterialCache.FromTexture(source.texture, source.shaderDef);
        }

        private static VfxFieldParams BuildField(VfxFieldDef source)
        {
            VfxFieldParams field = new VfxFieldParams
            {
                QuadSize = source.quadSize,
                ExtraHeight = source.extraHeight,
                SizeFromRadius = source.sizeFromRadius,
                RadiusPadding = source.radiusPadding,
                Altitude = source.useAltitude ? source.altitude.AltitudeFor() : 0f,
                CenterUniform = UniformOf(source.centerUniform),
                SizeUniform = UniformOf(source.sizeUniform),
                RadiusChannel = ChannelOf(source.radiusChannel),
                StrengthChannel = ChannelOf(source.strengthChannel),
            };

            AddBindings(field.Bindings, source.bindings);
            return field;
        }

        /// <summary>Turns authored bindings into resolved ones; a name that resolves to nothing is skipped.</summary>
        private static void AddBindings(List<VfxUniformBinding> target, List<VfxBindingDef> source)
        {
            if (source == null)
            {
                return;
            }
            for (int i = 0; i < source.Count; i++)
            {
                VfxBindingDef binding = source[i];
                if (binding == null)
                {
                    continue;
                }
                int uniform = UniformOf(binding.uniform);
                if (uniform == 0)
                {
                    continue;
                }

                if (!binding.vecChannel.NullOrEmpty())
                {
                    if (VfxChannels.TryFindVector(binding.vecChannel, out int vecChannel))
                    {
                        target.Add(VfxUniformBinding.FromVectorChannel(uniform, vecChannel));
                    }
                    continue;
                }

                if (!binding.frame.NullOrEmpty())
                {
                    int frame = FrameUniformOf(binding.frame);
                    if (frame > 0)
                    {
                        target.Add(VfxUniformBinding.FromFrame(uniform, frame));
                    }
                    continue;
                }

                if (binding.channel.NullOrEmpty())
                {
                    target.Add(binding.component < 0
                        ? VfxUniformBinding.FloatConstant(uniform, binding.value.x)
                        : VfxUniformBinding.Vector(uniform, binding.value));
                    continue;
                }
                if (!VfxChannels.TryFind(binding.channel, out int channel))
                {
                    continue;
                }
                target.Add(binding.component < 0
                    ? VfxUniformBinding.Float(uniform, channel)
                    : VfxUniformBinding.VectorChannel(uniform, binding.component, channel, binding.value));
            }
        }

        private static int UniformOf(string name)
        {
            return name.NullOrEmpty() ? 0 : Shader.PropertyToID(name);
        }

        /// <summary>A channel id from an authored name, or -1 when the name is empty or unknown.</summary>
        private static int ChannelOf(string name)
        {
            return name.NullOrEmpty() || !VfxChannels.TryFind(name, out int channel) ? -1 : channel;
        }

        /// <summary>The frame-uniform id named by a def, or 0 for none/unknown.</summary>
        private static int FrameUniformOf(string name)
        {
            if (name.NullOrEmpty())
            {
                return 0;
            }
            switch (name.Trim())
            {
                case "screenPerCell":
                    return VfxUniformBinding.ScreenPerCell;
                default:
                    Log.ErrorOnce("[MiliraXian] Unknown VFX frame uniform '" + name + "'.",
                        GenText.StableStringHash("MX_Vfx_Frame_" + name));
                    return 0;
            }
        }

        /// <summary>The shared mesh an instanced stroke draws, or null when the name is empty.</summary>
        private static Mesh InstanceMesh(string name)
        {
            if (name.NullOrEmpty())
            {
                return null;
            }
            switch (name.Trim())
            {
                case "quad":
                case "plane":
                    return MeshPool.plane10;
                default:
                    Log.ErrorOnce("[MiliraXian] VFX instanceMesh '" + name + "' is not a known shape.",
                        GenText.StableStringHash("MX_Vfx_InstanceMesh_" + name));
                    return null;
            }
        }

        /// <summary>
        /// Drops whatever this def-level object created for itself. Pooled materials are not touched;
        /// a subclass that builds its own meshes or textures must destroy them here.
        /// </summary>
        internal virtual void Release()
        {
        }
    }
}

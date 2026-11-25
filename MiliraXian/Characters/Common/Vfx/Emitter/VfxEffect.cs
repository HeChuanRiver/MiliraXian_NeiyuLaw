using System;
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
            Def = def;
            Name = def?.defName ?? "vfx";
            FadeOutSeconds = 0.35f;
            LifetimeSeconds = 0f;
            RequireMaintain = true;
            Capacity = 0;
            Passes = new List<VfxPass>();
            EmitterType = typeof(VfxEmitter);
            Build(def);
        }

        internal VfxDef Def { get; }

        /// <summary>Diagnostics only; never used to find anything.</summary>
        internal string Name { get; }

        /// <summary>How long the emitter takes to fade once <c>Maintain</c> stops or lifetime ends.</summary>
        internal float FadeOutSeconds { get; set; }

        /// <summary>Total life; zero or less means "until <c>Maintain</c> stops".</summary>
        internal float LifetimeSeconds { get; set; }

        internal bool RequireMaintain { get; set; }

        /// <summary>Upper bound on simultaneous particles; the emitter allocates for this once.</summary>
        internal int Capacity { get; set; }

        /// <summary>The strokes, drawn in order. Add new ones on the end; never rewrite the old ones.</summary>
        internal List<VfxPass> Passes { get; }

        private readonly Dictionary<string, int> emitterFloatSlots = new();
        private readonly Dictionary<string, int> particleFloatSlots = new();
        private readonly Dictionary<string, int> vectorSlots = new();
        private readonly Dictionary<string, int> textureSlots = new();

        internal int EmitterFloatChannelCount => emitterFloatSlots.Count;
        internal int ParticleFloatChannelCount => particleFloatSlots.Count;
        internal int VectorChannelCount => vectorSlots.Count;
        internal int TextureChannelCount => textureSlots.Count;

        internal int EmitterFloatSlot(string name) => FindSlot(emitterFloatSlots, name);
        internal int ParticleFloatSlot(string name) => FindSlot(particleFloatSlots, name);
        internal int VectorSlot(string name) => FindSlot(vectorSlots, name);
        internal int TextureSlot(string name) => FindSlot(textureSlots, name);

        internal int RequireEmitterFloatSlot(string name) => RequireSlot(emitterFloatSlots, "channel", name);
        internal int RequireParticleFloatSlot(string name) => RequireSlot(particleFloatSlots, "particle channel", name);
        internal int RequireVectorSlot(string name) => RequireSlot(vectorSlots, "vec4 channel", name);
        internal int RequireTextureSlot(string name) => RequireSlot(textureSlots, "texture channel", name);

        /// <summary>The class each spawn instantiates, from the def's <c>emitterClass</c>.</summary>
        internal Type EmitterType { get; set; }

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
            RequireMaintain = def.requireMaintain;
            Capacity = Mathf.Max(0, def.capacity);

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

        private VfxPass BuildPass(VfxPassDef source)
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
                BloomTint = source.bloomTint,
                BloomIntensity = source.bloomIntensity,
                ParticleGroup = source.particleGroup,
                Mesh = InstanceMesh(source.instanceMesh),
                InstanceVectors = Mathf.Clamp(source.instanceVectors, 0, VfxInstanceBatch.MaxVectors),
            };

            if (source.field != null)
            {
                pass.Field = BuildField(source.field);
            }
            if (source.sprite != null)
            {
                pass.Sprite = BuildSprite(source.sprite);
            }
            if (source.streak != null)
            {
                pass.Streak = BuildStreak(source.streak);
            }
            if (source.ribbon != null)
            {
                pass.Ribbon = BuildRibbon(source.ribbon);
            }
            return pass;
        }

        private VfxRibbonParams BuildRibbon(VfxRibbonDef source)
        {
            VfxRibbonParams ribbon = new VfxRibbonParams
            {
                Width = source.width,
                UseParticleSize = source.useParticleSize,
                UseParticleColor = source.useParticleColor,
                Tint = source.tint,
                VMin = source.vMin,
                VMax = source.vMax,
                VFrames = Mathf.Max(1, source.vFrames),
                FrameChannel = InternEmitterFloat(source.frameChannel),
                NormalizeU = source.normalizeU,
                UScale = source.uScale,
                UChannel = InternParticleFloat(source.uChannel),
                RibbonIdChannel = InternParticleFloat(source.ribbonIdChannel),
                UseAltitude = source.useAltitude,
                Altitude = source.useAltitude ? source.altitude.AltitudeFor() + source.altitudeOffset : 0f,
                AltitudeOffset = source.altitudeOffset,
            };
            AddBindings(ribbon.Bindings, source.bindings);
            return ribbon;
        }

        private VfxStreakParams BuildStreak(VfxStreakDef source)
        {
            VfxStreakParams streak = new VfxStreakParams
            {
                Width = source.width,
                TailAlpha = Mathf.Clamp01(source.tailAlpha),
                UseAltitude = source.useAltitude,
                Altitude = source.useAltitude ? source.altitude.AltitudeFor() + source.altitudeOffset : 0f,
                AltitudeOffset = source.altitudeOffset,
            };
            AddBindings(streak.Bindings, source.bindings);
            return streak;
        }

        private VfxSpriteParams BuildSprite(VfxSpriteDef source)
        {
            VfxSpriteParams sprite = new VfxSpriteParams
            {
                QuadSize = source.quadSize,
                UseParticleSize = source.useParticleSize,
                UseParticleRotation = source.useParticleRotation,
                UseParticleColor = source.useParticleColor,
                Tint = source.tint,
                AtlasColumns = Mathf.Max(1, source.atlasColumns),
                AtlasRows = Mathf.Max(1, source.atlasRows),
                RotationRate = source.rotationRate,
                FrameChannel = InternParticleFloat(source.frameChannel),
                UseUvRect = source.useUvRect,
                UvMin = source.uvMin,
                UvMax = source.uvMax,
                FlipU = source.flipU,
                FlipV = source.flipV,
                UseAltitude = source.useAltitude,
                Altitude = source.useAltitude ? source.altitude.AltitudeFor() + source.altitudeOffset : 0f,
                AltitudeOffset = source.altitudeOffset,
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

        private VfxFieldParams BuildField(VfxFieldDef source)
        {
            string radiusName = source.radiusChannel.NullOrEmpty() && source.sizeFromRadius
                ? VfxChannels.Radius
                : source.radiusChannel;
            VfxFieldParams field = new VfxFieldParams
            {
                QuadSize = source.quadSize,
                ExtraHeight = source.extraHeight,
                SizeFromRadius = source.sizeFromRadius,
                RadiusPadding = source.radiusPadding,
                UseAltitude = source.useAltitude,
                Altitude = source.useAltitude ? source.altitude.AltitudeFor() + source.altitudeOffset : 0f,
                AltitudeOffset = source.altitudeOffset,
                CenterUniform = UniformOf(source.centerUniform),
                SizeUniform = UniformOf(source.sizeUniform),
                RadiusChannel = InternEmitterFloat(radiusName),
                StrengthChannel = InternEmitterFloat(source.strengthChannel),
                SizeChannel = InternVector(source.sizeChannel),
            };

            AddBindings(field.Bindings, source.bindings);
            return field;
        }

        /// <summary>Turns authored bindings into resolved ones, allocating local channel slots on this effect.</summary>
        private void AddBindings(List<VfxUniformBinding> target, List<VfxBindingDef> source)
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

                if (!binding.texChannel.NullOrEmpty())
                {
                    int texChannel = InternTexture(binding.texChannel);
                    if (texChannel >= 0)
                    {
                        target.Add(VfxUniformBinding.FromTextureChannel(uniform, texChannel));
                    }
                    continue;
                }

                if (!binding.texture.NullOrEmpty())
                {
                    Texture2D tex = binding.texture == "White"
                        ? BaseContent.WhiteTex
                        : VfxMaterialCache.TextureOf(binding.texture);
                    if (tex != null)
                    {
                        target.Add(VfxUniformBinding.FromTexture(uniform, tex));
                    }
                    continue;
                }

                if (!binding.vecChannel.NullOrEmpty())
                {
                    int vecChannel = InternVector(binding.vecChannel);
                    if (vecChannel >= 0)
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
                    target.Add(!float.IsNaN(binding.floatValue)
                        ? VfxUniformBinding.FloatConstant(uniform, binding.floatValue)
                        : VfxUniformBinding.Vector(uniform, binding.value));
                    continue;
                }
                int channel = InternEmitterFloat(binding.channel);
                if (channel < 0)
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

        private int InternEmitterFloat(string name) => InternSlot(emitterFloatSlots, name);
        private int InternParticleFloat(string name) => InternSlot(particleFloatSlots, name);
        private int InternVector(string name) => InternSlot(vectorSlots, name);
        private int InternTexture(string name) => InternSlot(textureSlots, name);

        private static int InternSlot(Dictionary<string, int> table, string name)
        {
            if (name.NullOrEmpty())
            {
                return -1;
            }
            if (!table.TryGetValue(name, out int slot))
            {
                slot = table.Count;
                table[name] = slot;
            }
            return slot;
        }

        private static int FindSlot(Dictionary<string, int> table, string name)
        {
            return !name.NullOrEmpty() && table.TryGetValue(name, out int slot) ? slot : -1;
        }

        private int RequireSlot(Dictionary<string, int> table, string kind, string name)
        {
            int slot = FindSlot(table, name);
            if (slot < 0 && !name.NullOrEmpty())
            {
                Log.WarningOnce("[MiliraXian] VFX '" + Name + "' has no " + kind + " named '" + name + "'.",
                    GenText.StableStringHash("MX_Vfx_MissingSlot_" + Name + "_" + kind + "_" + name));
            }
            return slot;
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
                case "time":
                    return VfxUniformBinding.Time;
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

using System;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// One effect alive on one map: its effect, where it sits, its particles and their channels.
    ///
    /// It owns no map reference and no engine object — only arrays and numbers. Releasing it is
    /// therefore free of teardown order concerns; the host decides when.
    /// </summary>
    internal class VfxEmitter
    {
        internal VfxEffect Effect;

        internal VfxAnchor Anchor;

        /// <summary>Resolved world position of the emitter's anchor.</summary>
        internal Vector3 Position;

        internal VfxParticle[] Particles;

        /// <summary>
        /// One scratch mesh per pass, owned by this emitter. Drawing is deferred to the end of the
        /// frame, so two emitters can never share a mesh; the host destroys them when this emitter
        /// is released.
        /// </summary>
        internal Mesh[] Meshes;

        /// <summary>Flat per-particle store: <c>particle * ParticleChannelStride + channel</c>.</summary>
        internal float[] Channels;

        internal int ParticleChannelStride;

        /// <summary>Emitter-level float channels, indexed by this effect's local slot id.</summary>
        internal float[] EmitterChannels;

        /// <summary>Emitter-level vec4 channels, indexed by this effect's local slot id.</summary>
        internal Vector4[] Vectors;

        /// <summary>Emitter-level dynamic texture channels (e.g. RenderTextures), indexed by this effect's local slot id.</summary>
        internal Texture[] Textures;

        /// <summary>How many particles of the array are in use.</summary>
        internal int Count;

        /// <summary>Seconds since this emitter was spawned.</summary>
        internal float AgeSeconds;

        /// <summary>Tick of the last <c>Maintain</c>; a source that stops calling it has gone away.</summary>
        internal int LastMaintainTick;

        /// <summary>Seconds spent fading since the last maintain.</summary>
        internal float FadeSeconds;

        /// <summary>Bumped per spawn, so a handle from a previous occupant of the slot is inert.</summary>
        internal int Version;

        internal bool Released;

        internal float Alpha =>
            Effect == null || Effect.FadeOutSeconds <= 0f
                ? 1f
                : Mathf.Clamp01(1f - FadeSeconds / Effect.FadeOutSeconds);

        /// <summary>
        /// Prepares this emitter for one play: allocates the particle array, clears the channels and
        /// places the anchor particle. The host calls it once per spawn, on a fresh or on a reused
        /// emitter.
        ///
        /// An <c>emitterClass</c> subclass overrides it to lay down its own starting particles (a
        /// burst, a swarm) and to clear its own state — call <c>base.Reset</c> first, otherwise the
        /// arrays do not exist yet and <see cref="Emit"/> returns -1.
        /// </summary>
        internal virtual void Reset(VfxEffect effect, VfxAnchor anchor, int version, int nowTick, Vector3 position)
        {
            Effect = effect;
            Anchor = anchor;
            Position = position;
            Version = version;
            Released = false;
            AgeSeconds = 0f;
            FadeSeconds = 0f;
            LastMaintainTick = nowTick;
            Count = 0;

            int needed = Mathf.Max(0, effect.Capacity);
            ParticleChannelStride = effect.ParticleFloatChannelCount;
            if (needed > 0)
            {
                if (Particles == null || Particles.Length < needed)
                {
                    Particles = new VfxParticle[needed];
                }
                int neededChannels = needed * ParticleChannelStride;
                if (neededChannels > 0)
                {
                    if (Channels == null || Channels.Length < neededChannels)
                    {
                        Channels = new float[neededChannels];
                    }
                    else
                    {
                        Array.Clear(Channels, 0, neededChannels);
                    }
                }
            }

            int emitterFloats = effect.EmitterFloatChannelCount;
            if (emitterFloats > 0)
            {
                if (EmitterChannels == null || EmitterChannels.Length < emitterFloats)
                {
                    EmitterChannels = new float[emitterFloats];
                }
                else
                {
                    Array.Clear(EmitterChannels, 0, emitterFloats);
                }
            }

            int vectorCount = effect.VectorChannelCount;
            if (vectorCount > 0)
            {
                if (Vectors == null || Vectors.Length < vectorCount)
                {
                    Vectors = new Vector4[vectorCount];
                }
                else
                {
                    Array.Clear(Vectors, 0, vectorCount);
                }
            }

            int texCount = effect.TextureChannelCount;
            if (texCount > 0)
            {
                if (Textures == null || Textures.Length < texCount)
                {
                    Textures = new Texture[texCount];
                }
                else
                {
                    Array.Clear(Textures, 0, texCount);
                }
            }

            // Meshes are destroyed on release, so a reused slot always starts from a fresh array.
            int passes = effect.Passes.Count;
            if (Meshes == null || Meshes.Length != passes)
            {
                Meshes = passes > 0 ? new Mesh[passes] : null;
            }
        }

        /// <summary>
        /// Advances one frame. Returns false when the host should release this emitter.
        ///
        /// This is the emitter's own code path: a subclass named by the def's <c>emitterClass</c>
        /// overrides it to read the game (a Thing's state), record a history trail, or emit over
        /// time — call <c>base.Advance</c> first so ageing, fade and anchoring still happen.
        /// </summary>
        internal virtual bool Advance(float deltaSeconds, int nowTick, Map map)
        {
            if (Released || Effect == null)
            {
                return false;
            }

            AgeSeconds += deltaSeconds;

            bool maintained = !Effect.RequireMaintain || nowTick - LastMaintainTick <= 0;
            bool outlived = Effect.LifetimeSeconds > 0f && AgeSeconds >= Effect.LifetimeSeconds;

            if (maintained && !outlived)
            {
                FadeSeconds = 0f;
            }
            else
            {
                FadeSeconds += deltaSeconds;
                if (Effect.FadeOutSeconds <= 0f)
                {
                    return false;
                }
                if (FadeSeconds >= Effect.FadeOutSeconds)
                {
                    return false;
                }
            }

            if (Anchor.TryResolve(map, out Vector3 position))
            {
                Position = position;
            }
            else if (Anchor.Kind == VfxAnchorKind.Pawn
                || Anchor.Kind == VfxAnchorKind.Thing
                || Anchor.Kind == VfxAnchorKind.ThingComp)
            {
                // The source went away: drop the emitter.
                return false;
            }

            for (int i = 0; i < Count; i++)
            {
                VfxParticle particle = Particles[i];
                if (!particle.Alive)
                {
                    continue;
                }

                particle.AgeSeconds += deltaSeconds;
                if (particle.LifetimeSeconds > 0f && particle.AgeSeconds >= particle.LifetimeSeconds)
                {
                    particle.Alive = false;
                    Particles[i] = particle;
                    continue;
                }

                particle.Position += particle.Velocity * deltaSeconds;
                Particles[i] = particle;
            }

            return true;
        }

        /// <summary>
        /// Appends one particle at <paramref name="position"/> and returns its index, or -1 when the
        /// emitter is full. Dead particle slots are reused automatically.
        /// </summary>
        internal int Emit(Vector3 position, float lifetimeSeconds, float size, Color color, int particleGroup = 0)
        {
            if (Particles == null || Particles.Length == 0)
            {
                return -1;
            }

            int index;
            if (Count < Particles.Length)
            {
                index = Count;
                Count = index + 1;
            }
            else
            {
                index = -1;
                for (int i = 0; i < Particles.Length; i++)
                {
                    if (!Particles[i].Alive)
                    {
                        index = i;
                        break;
                    }
                }
                if (index < 0)
                {
                    Log.WarningOnce("[MiliraXian] VFX '" + Effect?.Name + "' is full ("
                        + Particles.Length + " particles); an emission was skipped.",
                        GenText.StableStringHash("MX_Vfx_Full_" + Effect?.Name));
                    return -1;
                }
            }

            Particles[index] = new VfxParticle
            {
                Position = position,
                Velocity = Vector3.zero,
                AgeSeconds = 0f,
                LifetimeSeconds = lifetimeSeconds,
                Size = size,
                RotationDegrees = 0f,
                Color = color,
                Alive = true,
                ParticleGroup = particleGroup,
                SpawnSeconds = VfxFrameUniforms.CurrentTimeSeconds(),
                Side = Vector3.zero,
            };

            if (Channels != null && ParticleChannelStride > 0)
            {
                Array.Clear(Channels, index * ParticleChannelStride, ParticleChannelStride);
            }
            return index;
        }

        internal float EmitterChannel(int channel)
        {
            return EmitterChannels != null && channel >= 0 && channel < EmitterChannels.Length
                ? EmitterChannels[channel]
                : 0f;
        }

        internal float EmitterChannel(string name)
        {
            return Effect == null ? 0f : EmitterChannel(Effect.EmitterFloatSlot(name));
        }

        internal void SetEmitterChannel(int channel, float value)
        {
            if (EmitterChannels != null && channel >= 0 && channel < EmitterChannels.Length)
            {
                EmitterChannels[channel] = value;
            }
        }

        internal void SetEmitterChannel(string name, float value)
        {
            if (Effect != null)
            {
                SetEmitterChannel(Effect.EmitterFloatSlot(name), value);
            }
        }

        internal Vector4 VectorChannel(int channel)
        {
            return Vectors != null && channel >= 0 && channel < Vectors.Length ? Vectors[channel] : Vector4.zero;
        }

        internal void SetVectorChannel(int channel, Vector4 value)
        {
            if (Vectors != null && channel >= 0 && channel < Vectors.Length)
            {
                Vectors[channel] = value;
            }
        }

        internal void SetVectorChannel(string name, Vector4 value)
        {
            if (Effect != null)
            {
                SetVectorChannel(Effect.VectorSlot(name), value);
            }
        }

        internal Texture TextureChannel(int channel)
        {
            return Textures != null && channel >= 0 && channel < Textures.Length ? Textures[channel] : null;
        }

        internal void SetTextureChannel(int channel, Texture texture)
        {
            if (Textures != null && channel >= 0 && channel < Textures.Length)
            {
                Textures[channel] = texture;
            }
        }

        internal void SetTextureChannel(string name, Texture texture)
        {
            if (Effect != null)
            {
                SetTextureChannel(Effect.TextureSlot(name), texture);
            }
        }

        internal float Channel(int particleIndex, int channel)
        {
            if (Channels == null || ParticleChannelStride <= 0 || channel < 0 || channel >= ParticleChannelStride)
            {
                return 0f;
            }
            int index = particleIndex * ParticleChannelStride + channel;
            return index >= 0 && index < Channels.Length ? Channels[index] : 0f;
        }

        internal void SetChannel(int particleIndex, int channel, float value)
        {
            if (Channels == null || ParticleChannelStride <= 0 || channel < 0 || channel >= ParticleChannelStride)
            {
                return;
            }
            int index = particleIndex * ParticleChannelStride + channel;
            if (index >= 0 && index < Channels.Length)
            {
                Channels[index] = value;
            }
        }

        /// <summary>The scratch mesh of one stroke, created on first use.</summary>
        internal Mesh MeshAt(int passIndex)
        {
            if (Meshes == null || passIndex < 0 || passIndex >= Meshes.Length)
            {
                return null;
            }
            Mesh mesh = Meshes[passIndex];
            if (mesh == null)
            {
                mesh = new Mesh { name = (Effect?.Name ?? "vfx") + "#" + passIndex };
                mesh.MarkDynamic();
                Meshes[passIndex] = mesh;
            }
            return mesh;
        }

        /// <summary>Drops all references so a released emitter costs nothing until reused.</summary>
        internal void Clear()
        {
            Released = true;
            Effect = null;
            Anchor = default;
            Position = default;
            Count = 0;
            AgeSeconds = 0f;
            FadeSeconds = 0f;
            Meshes = null;
            if (Textures != null)
            {
                // Drop texture references on release so pooled emitters never pin external RenderTextures.
                Array.Clear(Textures, 0, Textures.Length);
            }
        }
    }
}

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

        internal VfxParticle[] Particles;

        /// <summary>
        /// One scratch mesh per pass, owned by this emitter. Drawing is deferred to the end of the
        /// frame, so two emitters can never share a mesh; the host destroys them when this emitter
        /// is released.
        /// </summary>
        internal Mesh[] Meshes;

        /// <summary>Flat store: <c>particle * VfxChannels.Capacity + channel</c>.</summary>
        internal float[] Channels;

        /// <summary>Emitter-level vec4 channels, indexed by vec4 channel id.</summary>
        internal Vector4[] Vectors;

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
            Version = version;
            Released = false;
            AgeSeconds = 0f;
            FadeSeconds = 0f;
            LastMaintainTick = nowTick;
            Count = 1;

            int needed = Mathf.Max(1, effect.Capacity);
            if (Particles == null || Particles.Length < needed)
            {
                Particles = new VfxParticle[needed];
                Channels = new float[needed * VfxChannels.Capacity];
            }
            else
            {
                System.Array.Clear(Channels, 0, needed * VfxChannels.Capacity);
            }

            if (Vectors == null)
            {
                Vectors = new Vector4[VfxChannels.VectorCapacity];
            }
            else
            {
                System.Array.Clear(Vectors, 0, Vectors.Length);
            }

            // Meshes are destroyed on release, so a reused slot always starts from a fresh array.
            int passes = effect.Passes.Count;
            if (Meshes == null || Meshes.Length != passes)
            {
                Meshes = passes > 0 ? new Mesh[passes] : null;
            }

            VfxParticle particle = Particles[0];
            particle.Position = position;
            particle.Velocity = Vector3.zero;
            particle.AgeSeconds = 0f;
            particle.LifetimeSeconds = 0f;
            particle.Size = 1f;
            particle.RotationDegrees = 0f;
            particle.Color = Color.white;
            particle.Alive = true;
            Particles[0] = particle;
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

            // A tick without Maintain means the source stopped asking for this effect.
            bool maintained = nowTick - LastMaintainTick <= 0;
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

            Vector3 position;
            bool anchored = Anchor.TryResolve(map, out position);

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

                // Only the first particle follows the anchor; the rest are placed by whoever
                // emitted them (that is what makes a swarm of ghosts anchored to one pawn possible).
                if (i == 0)
                {
                    if (anchored)
                    {
                        particle.Position = position;
                    }
                    else if (Anchor.Kind == VfxAnchorKind.Pawn
                        || Anchor.Kind == VfxAnchorKind.Thing
                        || Anchor.Kind == VfxAnchorKind.ThingComp)
                    {
                        // The source went away even though it kept maintaining: drop the emitter.
                        return false;
                    }
                }

                particle.Position += particle.Velocity * deltaSeconds;
                Particles[i] = particle;
            }

            return true;
        }

        /// <summary>
        /// Appends one particle at <paramref name="position"/> and returns its index, or -1 when the
        /// emitter is full. This is how an <c>emitterClass</c> emits over time (petals along a path,
        /// droplets, ghosts); the emitter owns the array, so the caller never allocates.
        ///
        /// A dead particle's slot is reused: the array is its own free list, so there is no second
        /// bookkeeping that could disagree with the <c>Alive</c> flag. Index 0 is the particle that
        /// follows the anchor and is never recycled.
        /// </summary>
        internal int Emit(Vector3 position, float lifetimeSeconds, float size, Color color, int particleGroup = 0)
        {
            if (Particles == null)
            {
                return -1;
            }

            int index;
            if (Count < Particles.Length)
            {
                // Nothing has been emitted past the high-water mark yet.
                index = Count;
                Count = index + 1;
            }
            else
            {
                index = -1;
                for (int i = 1; i < Particles.Length; i++)
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
            };

            if (Channels != null)
            {
                System.Array.Clear(Channels, index * VfxChannels.Capacity, VfxChannels.Capacity);
            }
            return index;
        }

        /// <summary>
        /// The first live particle of <paramref name="particleGroup"/>, or of any group when it is
        /// negative. Returns -1 when there is none; single-particle strokes use it to pick the one
        /// particle they draw.
        /// </summary>
        internal int FirstParticleOfGroup(int particleGroup)
        {
            if (Particles == null)
            {
                return -1;
            }
            for (int i = 0; i < Count; i++)
            {
                if (!Particles[i].Alive)
                {
                    continue;
                }
                if (particleGroup < 0 || Particles[i].ParticleGroup == particleGroup)
                {
                    return i;
                }
            }
            return -1;
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

        internal float Channel(int particleIndex, int channel)
        {
            if (Channels == null || channel < 0 || channel >= VfxChannels.Capacity)
            {
                return 0f;
            }
            int index = particleIndex * VfxChannels.Capacity + channel;
            return index >= 0 && index < Channels.Length ? Channels[index] : 0f;
        }

        internal void SetChannel(int particleIndex, int channel, float value)
        {
            if (Channels == null || channel < 0 || channel >= VfxChannels.Capacity)
            {
                return;
            }
            int index = particleIndex * VfxChannels.Capacity + channel;
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
            Count = 0;
            AgeSeconds = 0f;
            FadeSeconds = 0f;
            Meshes = null;
            Vectors = null;
        }
    }
}

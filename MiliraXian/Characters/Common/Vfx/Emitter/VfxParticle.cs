using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// One particle: plain data, nothing it owns and nothing that owns a map.
    /// A renderer reads it and turns it into vertices; the emitter advances its age.
    /// </summary>
    internal struct VfxParticle
    {
        internal Vector3 Position;

        internal Vector3 Velocity;

        internal float AgeSeconds;

        /// <summary>Zero or less means "as long as the emitter lives".</summary>
        internal float LifetimeSeconds;

        internal float Size;

        internal float RotationDegrees;

        internal Color Color;

        internal bool Alive;

        /// <summary>
        /// Which strokes draw this particle. A pass declares the group it draws (-1 = any), so one
        /// emitter can hold several kinds of particle — rain strands and their splashes — without a
        /// renderer having to know which is which.
        /// </summary>
        internal int ParticleGroup;
    }
}

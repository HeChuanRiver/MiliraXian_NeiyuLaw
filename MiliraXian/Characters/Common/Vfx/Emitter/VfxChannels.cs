using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// The named per-particle data channels, registered once at startup.
    ///
    /// A channel is one float on a particle; its meaning belongs to the effect, its destination
    /// (a uniform or a vertex component) belongs to the renderer. That is what keeps
    /// "block.SetFloat(_FieldRadius, ...)" from being repeated in every effect.
    /// </summary>
    internal static class VfxChannels
    {
        /// <summary>How many channels a particle can carry. Part of the per-emitter allocation.</summary>
        internal const int Capacity = 16;

        private static readonly List<string> Names = new();

        // Declared after Names so their registration runs against an initialised list.
        internal static readonly int Radius = Register("radius");
        internal static readonly int Strength = Register("strength");
        internal static readonly int Progress = Register("progress");
        internal static readonly int Alpha = Register("alpha");
        internal static readonly int Angle = Register("angle");

        internal static int Count => Names.Count;

        internal static int Register(string name)
        {
            if (name.NullOrEmpty())
            {
                return 0;
            }
            int existing = Names.IndexOf(name);
            if (existing >= 0)
            {
                return existing;
            }
            if (Names.Count >= Capacity)
            {
                Log.ErrorOnce("[MiliraXian] Too many VFX channels; '" + name + "' was ignored.",
                    GenText.StableStringHash("MX_Vfx_ChannelOverflow_" + name));
                return Capacity - 1;
            }
            Names.Add(name);
            return Names.Count - 1;
        }

        internal static string NameOf(int channel)
        {
            return channel >= 0 && channel < Names.Count ? Names[channel] : "?";
        }

        /// <summary>Looks up a channel by the name an authored def uses, without registering it.</summary>
        internal static bool TryFind(string name, out int channel)
        {
            channel = -1;
            if (name.NullOrEmpty())
            {
                return false;
            }
            channel = Names.IndexOf(name);
            return channel >= 0;
        }

        // ---------------------------------------------------------------- vec4 channels

        /// <summary>
        /// How many vec4 channels an emitter can carry. They are emitter-level, not per particle:
        /// they describe the shape a whole stroke covers (a wall's axis, a field's band), which is a
        /// property of the effect, not of each droplet.
        /// </summary>
        internal const int VectorCapacity = 16;

        private static readonly List<string> VectorNames = new();

        internal static int VectorCount => VectorNames.Count;

        internal static int RegisterVector(string name)
        {
            if (name.NullOrEmpty())
            {
                return 0;
            }
            int existing = VectorNames.IndexOf(name);
            if (existing >= 0)
            {
                return existing;
            }
            if (VectorNames.Count >= VectorCapacity)
            {
                Log.ErrorOnce("[MiliraXian] Too many VFX vec4 channels; '" + name + "' was ignored.",
                    GenText.StableStringHash("MX_Vfx_VecChannelOverflow_" + name));
                return VectorCapacity - 1;
            }
            VectorNames.Add(name);
            return VectorNames.Count - 1;
        }

        internal static bool TryFindVector(string name, out int channel)
        {
            channel = -1;
            if (name.NullOrEmpty())
            {
                return false;
            }
            channel = VectorNames.IndexOf(name);
            return channel >= 0;
        }
    }
}

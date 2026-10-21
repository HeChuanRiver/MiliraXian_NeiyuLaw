using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx
{
    /// <summary>
    /// Per-Def bloom settings: which group's post-processing the glow joins, plus the tint and
    /// intensity of that object's own halo. Every object of a Def shares these values, so glows of
    /// different things can differ in colour without spending one group layer each.
    /// </summary>
    public class ScreenBloomProperties
    {
        /// <summary>The shared capture and blur parameters; its layer receives this glow.</summary>
        public ScreenBloomGroupDef group;

        /// <summary>
        /// Multiplies the captured glow's rgb. Only the halo is tinted: the object draws itself
        /// after the bloom submission has already taken its copy, so its own colour is untouched.
        /// </summary>
        public Color tint = Color.white;

        /// <summary>Scales the halo's brightness, independently of the object's own brightness.</summary>
        public float intensity = 1f;
    }
}

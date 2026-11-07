using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.BladeBody
{
    /// <summary>
    /// Calibration of a weapon texture for the rigid 3D blade. UVs have v growing upward
    /// (v = 1 is the top row of the image). The texture is drawn on a strip along the blade axis
    /// (pommel to tip); its real length is <see cref="length"/> metres.
    /// A melee weapon without one gets a default at startup (<see cref="For"/>): its whole map
    /// texture, bottom to top, as long as its draw size.
    /// </summary>
    public class BladeGraphicExtension : DefModExtension
    {
        public string texPath;
        public Vector2 pommelUV = new(0.5f, 0f);
        public Vector2 tipUV = new(0.5f, 1f);
        /// <summary>The hand: the blade turns about this point.</summary>
        public Vector2 gripUV = new(0.5f, 0.1f);
        /// <summary>Side of the texture the cutting edge is on: 1 = toward larger u, -1 = smaller u.</summary>
        public float edgeSide = 1f;
        /// <summary>Pommel to tip, metres (1 cell = 1 m).</summary>
        public float length = 1.1f;
        /// <summary>Part of the texture that holds the blade; only this area is drawn.</summary>
        public Vector2 uvMin = Vector2.zero;
        public Vector2 uvMax = Vector2.one;

        /// <summary>
        /// The weapon has a scabbard: drawn at the waist (rest mouth and end, body frame: right, up,
        /// forward, metres) outside a clip's own scabbard keys. Without one none is drawn and a clip's
        /// sheathing does not hide the blade.
        /// </summary>
        public bool scabbard;
        public Vector3 scabbardRestMouth;
        public Vector3 scabbardRestEnd;
        /// <summary>Scabbard tint; it is drawn with the blade texture (from the grip to the tip), darkened.</summary>
        public Color scabbardColor = new(0.24f, 0.16f, 0.14f, 1f);

        /// <summary>Texture of a default calibration (the weapon's own map graphic).</summary>
        private Texture2D texture;

        private static readonly Dictionary<ThingDef, BladeGraphicExtension> Defaults = new();

        /// <summary>The blade texture; null when it cannot be found.</summary>
        public Texture2D Texture => texture ??= texPath.NullOrEmpty() ? null : ContentFinder<Texture2D>.Get(texPath, reportFailure: false);

        /// <summary>The weapon's calibration, or its startup default; null for a def that is not a melee weapon.</summary>
        public static BladeGraphicExtension For(ThingDef def)
        {
            if (def == null)
            {
                return null;
            }
            return def.GetModExtension<BladeGraphicExtension>()
                ?? (Defaults.TryGetValue(def, out BladeGraphicExtension d) ? d : null);
        }

        /// <summary>Builds the defaults on the main thread at startup; the render pass only reads them.</summary>
        internal static void BuildDefaults()
        {
            Defaults.Clear();
            foreach (ThingDef def in DefDatabase<ThingDef>.AllDefsListForReading)
            {
                if (!def.IsMeleeWeapon || def.HasModExtension<BladeGraphicExtension>() || def.graphicData == null)
                {
                    continue;
                }
                if (def.graphic?.MatSingle?.mainTexture is not Texture2D tex)
                {
                    continue;
                }
                Vector2 size = def.graphicData.drawSize;
                // Fixed sampling of the whole image: bottom to top, held a quarter of the way up.
                Defaults[def] = new BladeGraphicExtension
                {
                    texture = tex,
                    gripUV = new Vector2(0.5f, 0.25f),
                    length = Mathf.Max(0.3f, Mathf.Max(size.x, size.y)),
                };
            }
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string e in base.ConfigErrors())
            {
                yield return e;
            }
            if (texPath.NullOrEmpty())
            {
                yield return "texPath is not set";
            }
            if ((tipUV - pommelUV).sqrMagnitude < 1e-6f)
            {
                yield return "tipUV and pommelUV are the same point";
            }
            if (length <= 0f)
            {
                yield return "length must be positive";
            }
        }
    }

    [StaticConstructorOnStartup]
    internal static class BladeGraphicDefaults
    {
        static BladeGraphicDefaults()
        {
            BladeGraphicExtension.BuildDefaults();
        }
    }
}

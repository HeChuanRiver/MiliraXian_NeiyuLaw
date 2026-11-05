using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.BladeBody
{
    /// <summary>
    /// Calibration of a weapon texture for the rigid 3D blade. UVs have v growing upward
    /// (v = 1 is the top row of the image). The texture is drawn on a strip along the blade axis
    /// (pommel to tip); its real length is <see cref="length"/> metres.
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
}

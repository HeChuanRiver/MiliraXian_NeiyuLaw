using System;
using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.BladeBody
{
    /// <summary>
    /// How much of a pawn's held weapon is drawn, 0..1 (a skill that makes the weapon vanish and
    /// come back). The 3D blade dissolves by it; the flat vanilla weapon is shown from 0.5 up.
    /// Character code registers its sources at startup; the lowest value wins.
    /// </summary>
    public static class BladeOpacity
    {
        private static readonly List<Func<Pawn, float>> Sources = new();

        public static void Register(Func<Pawn, float> source)
        {
            if (source != null && !Sources.Contains(source))
            {
                Sources.Add(source);
            }
        }

        public static float For(Pawn pawn)
        {
            float opacity = 1f;
            for (int i = 0; i < Sources.Count; i++)
            {
                float v = Sources[i](pawn);
                if (v < opacity)
                {
                    opacity = v;
                }
            }
            return opacity < 0f ? 0f : opacity;
        }
    }
}

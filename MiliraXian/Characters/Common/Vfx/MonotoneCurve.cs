using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters.Common.Vfx
{
    /// <summary>
    /// Monotone cubic (Fritsch-Carlson) evaluation of a <see cref="SimpleCurve"/>: keys are poses,
    /// the value eases between them and never overshoots. Clamps outside the keys.
    /// </summary>
    public static class MonotoneCurve
    {
        public static float Evaluate(SimpleCurve curve, float x)
        {
            if (curve == null || curve.PointsCount == 0)
            {
                return 0f;
            }
            List<CurvePoint> p = curve.Points;
            int n = p.Count;
            if (n == 1 || x <= p[0].x)
            {
                return p[0].y;
            }
            if (x >= p[n - 1].x)
            {
                return p[n - 1].y;
            }
            int i = 0;
            while (i < n - 2 && x > p[i + 1].x)
            {
                i++;
            }
            float h = p[i + 1].x - p[i].x;
            if (h <= 1e-6f)
            {
                return p[i + 1].y;
            }
            float m0 = Tangent(p, i);
            float m1 = Tangent(p, i + 1);
            float t = (x - p[i].x) / h;
            float t2 = t * t;
            float t3 = t2 * t;
            return (2f * t3 - 3f * t2 + 1f) * p[i].y + (t3 - 2f * t2 + t) * h * m0
                + (-2f * t3 + 3f * t2) * p[i + 1].y + (t3 - t2) * h * m1;
        }

        private static float Slope(List<CurvePoint> p, int k)
        {
            float dx = p[k + 1].x - p[k].x;
            return dx > 1e-6f ? (p[k + 1].y - p[k].y) / dx : 0f;
        }

        private static float Tangent(List<CurvePoint> p, int k)
        {
            int n = p.Count;
            if (k == 0 || k == n - 1)
            {
                return 0f;
            }
            float a = Slope(p, k - 1);
            float b = Slope(p, k);
            // A key between a rise and a fall (or a flat side) is a held pose: zero tangent.
            if (a * b <= 0f)
            {
                return 0f;
            }
            return 2f / (1f / a + 1f / b);
        }
    }
}

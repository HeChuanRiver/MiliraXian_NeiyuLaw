using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Things.Weapons;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities.Zantetsuken
{
    internal static class ZantetsukenMarks
    {
        internal struct Mark
        {
            public Vector2 centre;
            public float angle;
            public float length;
            public float tilt;
        }

        /// <summary>The cut marks of the landing, scaled with the lotus radius (laid out for a radius of 3).</summary>
        internal static List<Mark> Make(CompProperties_AbilityZantetsuken props, int seed)
        {
            int n = props.landHits;
            float k = props.lotusRadius / 3f;
            List<Mark> marks = new(n);
            Rand.PushState(seed);
            float step = Mathf.PI / n;
            List<float> sides = new();
            for (int i = 0; i < n; i++)
            {
                sides.Add(n == 1 ? 0f : Mathf.Lerp(-1f, 1f, i / (float)(n - 1)));
            }
            sides.Shuffle();
            for (int j = 0; j < n; j++)
            {
                int o = n % 2 == 1 ? (j * 2) % n : (j * 3) % n;
                float ang = o * step + Rand.Range(-0.25f, 0.25f) * step;
                Vector2 nrm = new(-Mathf.Sin(ang), Mathf.Cos(ang));
                Vector2 c = (nrm * sides[o] + new Vector2(Rand.Gaussian(0f, 0.15f), Rand.Gaussian(0f, 0.15f))) * k;
                marks.Add(new Mark
                {
                    centre = c,
                    angle = ang,
                    length = 2f * props.lotusRadius * Rand.Range(0.9f, 1.02f),
                    tilt = Rand.Range(-1f, 1f) * props.visuals.markTilt * k,
                });
            }
            Rand.PopState();
            return marks;
        }

        internal static void Spawn(Map map, CompProperties_AbilityZantetsuken props, QingheImpactVfxProps vfx, Vector3 lotusCentre,
            in Mark m, float delay = 0f)
        {
            float depth = props.depthToScreen;
            Vector2 d = new Vector2(Mathf.Cos(m.angle), Mathf.Sin(m.angle)) * (m.length * 0.5f);
            Vector3 lift = new(0f, 0f, m.tilt * props.heightToScreen);
            Vector3 up = new(0f, 0f, props.visuals.markLift);
            Vector3 a = new Vector3(lotusCentre.x + m.centre.x - d.x, 0f, lotusCentre.z + (m.centre.y - d.y) * depth) - lift + up;
            Vector3 b = new Vector3(lotusCentre.x + m.centre.x + d.x, 0f, lotusCentre.z + (m.centre.y + d.y) * depth) + lift + up;
            Vector3 along = b - a;
            QingheImpactVfx.Spawn(map, vfx.WithCut(along.magnitude * props.visuals.markLengthShare, delay), (a + b) * 0.5f, along,
                lotusCentre.ToIntVec3(), sound: false);
        }

        internal static void SpawnSplit(Map map, CompProperties_AbilityZantetsuken props, Vector3 lotusCentre, Vector3 aim, float apex)
        {
            Vector3 ground = lotusCentre + new Vector3(aim.x, 0f, aim.z * props.depthToScreen) * props.visuals.splitForward;
            float height = apex * props.heightToScreen;
            Vector3 mid = ground + new Vector3(0f, 0f, height * 0.5f);
            // A cut is drawn from its first end: pointing down the screen, it opens from the top of the leap.
            QingheImpactVfx.Spawn(map, props.visuals.splitVfx.WithCut(height), mid, Vector3.back, lotusCentre.ToIntVec3(), sound: false);
        }
    }
}

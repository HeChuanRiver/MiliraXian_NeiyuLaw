using System;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu;

public sealed partial class JobDriver_NeiyuPostureExecution
{
    private const int MultiRingSegments = 64;
    private const int MultiRingTrailTicks = 27;
    private const int MultiRingFadeTicks = 10;
    private const int MultiSlashFadeTicks = 15;

    // Pixel pairs just inside the hole and outside the rim of BladeLight_Neiyu_E.
    // Unwrap this continuous blue-to-pink brush stroke onto an open annular strip.
    private static readonly (Vector2 inner, Vector2 outer)[] MultiBrush =
    [
        (new(172, 272), new(132, 304)),
        (new(155, 212), new(81, 220)),
        (new(158, 155), new(67, 100)),
        (new(218, 137), new(172, 41)),
        (new(255, 155), new(272, 82)),
        (new(277, 174), new(330, 129)),
        (new(294, 183), new(354, 155))
    ];

    private Mesh multiRing;
    private Vector3[] multiRingVertices;
    private Vector2[] multiRingUvs;
    private Color[] multiRingColors;

    internal void DrawMultiVfx(Vector3 body)
    {
        if (!NeiyuSwordCombatVfx.Enabled || NeiyuSwordCombatVfx.Intensity <= 0f || pawn.IsHiddenFromPlayer()
            || Find.UIRoot.HideMotes || Find.ScreenshotModeHandler.Active) return;
        try
        {
            float tick = SceneFrame;
            if (tick >= MultiPinAt && tick < MultiReleaseAt + MultiRingFadeTicks)
            {
                float tail = MultiYaw(tick - MultiRingTrailTicks);
                float front = MultiYaw(tick);
                float fade = 1f - ProgressBetween(tick, MultiReleaseAt, MultiReleaseAt + MultiRingFadeTicks);
                DrawMultiRing(body, tail, front - tail, MultiRadius, fade, closed: false);
            }
            if (tick >= MultiHitAt - 2f && tick < MultiHitAt + MultiSlashFadeTicks)
            {
                float p = ProgressBetween(tick, MultiHitAt, MultiHitAt + MultiSlashFadeTicks);
                float cut = ProgressBetween(tick, MultiHitAt - 2, MultiHitAt);
                float fade = cut * (1f - p) * (1f - p);
                float length = Mathf.Lerp(2.5f, 3.1f, p);
                // Align the convex leading edge with the airborne hit location.
                Vector3 target = strikeCommitted ? aimPoint : MultiTargetAtFrame(tick);
                Vector3 at = target - Forward(throwYaw) * (length * .52f);
                at.y = AltitudeLayer.MoteOverhead.AltitudeFor() + .015f;
                DrawSlashArtwork(at, throwYaw, Mathf.Lerp(8f, 9.2f, p), length, fade);
                if (strikeCommitted)
                {
                    float expand = Mathf.SmoothStep(0f, 1f, ProgressBetween(tick, MultiHitAt, MultiHitAt + 6));
                    DrawMultiRing(body, throwYaw - 90f, 360f, Mathf.Lerp(2.1f, MultiRadius, expand),
                        .78f * (1f - p), closed: true);
                }
            }
        }
        catch (Exception exception)
        {
            Log.ErrorOnce($"[霓羽弹刀] 第二支线绘制失败：{exception}", 197632111);
        }
    }

    private void DrawMultiRing(Vector3 center, float startYaw, float sweep, float radius, float fade, bool closed)
    {
        if (sweep < .5f || fade <= .001f) return;
        if (multiRing == null) CreateMultiRing();
        for (int i = 0; i <= MultiRingSegments; i++)
        {
            float u = i / (float)MultiRingSegments;
            Vector3 radial = Forward(startYaw + sweep * u);
            float width = closed ? .62f : Mathf.Lerp(.12f, .86f, Mathf.Sqrt(u));
            float alpha = closed ? 1f : Mathf.SmoothStep(0f, 1f, u) * (1f - .18f * Mathf.Pow(u, 32f));
            // Fold the brush back on itself for the closed floor wave so its join has matching UVs.
            float brush = closed ? .5f - .5f * Mathf.Cos(u * 2f * Mathf.PI) : u;
            float sample = brush * (MultiBrush.Length - 1);
            int section = Mathf.Min((int)sample, MultiBrush.Length - 2);
            int vertex = i * 2;
            multiRingVertices[vertex] = radial * (radius - width);
            multiRingVertices[vertex + 1] = radial * radius;
            multiRingUvs[vertex] = MultiBrushUv(Vector2.Lerp(MultiBrush[section].inner,
                MultiBrush[section + 1].inner, sample - section));
            multiRingUvs[vertex + 1] = MultiBrushUv(Vector2.Lerp(MultiBrush[section].outer,
                MultiBrush[section + 1].outer, sample - section));
            multiRingColors[vertex] = multiRingColors[vertex + 1] = Color.white.WithAlpha(alpha);
        }
        multiRing.vertices = multiRingVertices;
        multiRing.uv = multiRingUvs;
        multiRing.colors = multiRingColors;
        multiRing.RecalculateBounds();
        center.y = AltitudeLayer.MoteLow.AltitudeFor();
        DrawMesh(multiRing, Matrix4x4.Translate(center), NeiyuExecutionMaterials.Wave, Color.white, fade);
    }

    private static Vector2 MultiBrushUv(Vector2 pixel) => new(pixel.x / 512f, 1f - pixel.y / 512f);

    private void CreateMultiRing()
    {
        int count = (MultiRingSegments + 1) * 2;
        multiRingVertices = new Vector3[count];
        multiRingUvs = new Vector2[count];
        multiRingColors = new Color[count];
        int[] triangles = new int[MultiRingSegments * 6];
        for (int i = 0; i < MultiRingSegments; i++)
        {
            int v = i * 2, t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 3;
            triangles[t + 3] = v; triangles[t + 4] = v + 3; triangles[t + 5] = v + 2;
        }
        multiRing = new Mesh { name = "MX_NeiyuExecutionRing", vertices = multiRingVertices, triangles = triangles };
    }
}

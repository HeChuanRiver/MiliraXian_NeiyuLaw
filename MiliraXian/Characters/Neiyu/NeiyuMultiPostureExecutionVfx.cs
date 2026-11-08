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
    private const int MultiQuickCutFadeTicks = 10;
    private const int MultiSlashFadeTicks = 15;
    private const float MultiWaveRadius = 9.5f;

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
                DrawMultiRing(origin.ToVector3Shifted(), tail, front - tail, MultiWaveRadius, fade, closed: false);
            }
            for (int cut = 0; cut <= MultiLastCut; cut++)
            {
                bool final = cut == MultiLastCut;
                int hit = MultiCutAt(cut);
                int fadeTicks = final ? MultiSlashFadeTicks : MultiQuickCutFadeTicks;
                if (tick < hit - 2f) break;
                if (tick >= hit + fadeTicks) continue;
                float p = ProgressBetween(tick, hit, hit + fadeTicks);
                float entry = ProgressBetween(tick, hit - 2, hit);
                float fade = entry * (1f - Mathf.SmoothStep(0f, 1f, p));
                float length = final ? Mathf.Lerp(7.8f, 9f, p) : Mathf.Lerp(5.8f, 7f, p);
                float width = final ? Mathf.Lerp(20f, 23f, p) : Mathf.Lerp(16f, 19f, p);
                float yaw = MultiCutYaw(cut);
                // Each flash crosses its own airborne contact and then fades at that location.
                Vector3 at = MultiCutContact(cut, tick) + Forward(yaw) * (p * .68f - length * .52f);
                at.y = AltitudeLayer.MoteOverhead.AltitudeFor() + .015f + cut * .0002f;
                DrawSlashArtwork(at, yaw, width, length, fade, NeiyuExecutionMaterials.MultiWave);
            }
            if (strikeCommitted && tick >= MultiHitAt - 1f && tick < MultiHitAt + MultiSlashFadeTicks)
            {
                float p = ProgressBetween(tick, MultiHitAt, MultiHitAt + MultiSlashFadeTicks);
                float expand = Mathf.SmoothStep(0f, 1f, ProgressBetween(tick, MultiHitAt, MultiHitAt + 6));
                DrawMultiRing(body, MultiCutYaw(MultiLastCut) - 90f, 360f, Mathf.Lerp(3.5f, MultiWaveRadius, expand),
                    .94f * (1f - p), closed: true);
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
            float width = closed ? 1.35f : Mathf.Lerp(.15f, 1.55f, Mathf.Sqrt(u));
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
        DrawMesh(multiRing, Matrix4x4.Translate(center), NeiyuExecutionMaterials.MultiWave, Color.white, fade);
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

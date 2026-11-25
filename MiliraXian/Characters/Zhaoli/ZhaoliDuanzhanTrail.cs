using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx.Emitter;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Zhaoli;

internal sealed class ZhaoliDuanzhanTrail : VfxEmitter
{
    internal const int LifetimeTicks = 22;
    private const int MaxSamples = 18;
    private const int Subdivisions = 3;
    private readonly record struct Sample(Vector3 Grip, float Angle, float Scale, int Tick);
    private readonly List<Sample> samples = new(MaxSamples);
    private int observedFrame;
    private int endTick;
    private bool sweeping;
    private Sample current;

    internal static ZhaoliDuanzhanTrail Start(Pawn pawn, int endTick, out VfxEmitterHandle handle)
    {
        var host = pawn.Map.GetComponent<MapComponent_VfxEmitterHost>();
        handle = host.Spawn(ZhaoliVisualDefOf.MX_ZL_DuanzhanTrail, VfxAnchor.Follow(pawn));
        if (!host.TryGetEmitter(handle, out ZhaoliDuanzhanTrail trail)) return null;
        trail.endTick = endTick;
        trail.samples.Clear();
        trail.sweeping = false;
        return trail;
    }

    internal void Capture(Vector3 grip, float angle, float scale, int now, bool inSweep)
    {
        observedFrame = Time.frameCount;
        sweeping = inSweep;
        current = new Sample(grip, angle, scale, now);
        Trim(now);
        if (inSweep) Record(current);
    }

    private void Record(Sample sample)
    {
        if (samples.Count > 0)
        {
            Sample last = samples[^1];
            if ((sample.Grip - last.Grip).sqrMagnitude < .012f * .012f
                && Mathf.Abs(Mathf.DeltaAngle(last.Angle, sample.Angle)) < 1.75f) return;
        }
        if (samples.Count == MaxSamples) samples.RemoveAt(0);
        samples.Add(sample);
    }

    private void Trim(int now)
    {
        while (samples.Count > 0 && now - samples[0].Tick > LifetimeTicks) samples.RemoveAt(0);
    }

    internal override bool Advance(float deltaSeconds, int nowTick, Map map)
    {
        Count = 0;
        return nowTick < endTick && base.Advance(deltaSeconds, nowTick, map);
    }

    internal void Populate(Map map)
    {
        Count = 0;
        Pawn pawn = Anchor.Pawn;
        if (pawn == null || !pawn.Spawned || pawn.Map != map || pawn.Dead || Time.frameCount - observedFrame > 1
            || !DebugViewSettings.drawThingsDynamic || pawn.IsHiddenFromPlayer()) return;
        int now = Find.TickManager.TicksGame;
        if (now >= endTick) return;
        Trim(now);
        for (int i = 0; i < samples.Count; i++)
        {
            Sample from = samples[i];
            int depth = samples.Count - i;
            Ghost(from, depth, now);
            if (i + 1 == samples.Count) continue;
            Sample to = samples[i + 1];
            for (int j = 1; j <= Subdivisions; j++)
            {
                float t = j / (float)(Subdivisions + 1);
                Ghost(new Sample(Vector3.Lerp(from.Grip, to.Grip, t), Mathf.LerpAngle(from.Angle, to.Angle, t),
                    Mathf.Lerp(from.Scale, to.Scale, t), Mathf.RoundToInt(Mathf.Lerp(from.Tick, to.Tick, t))), depth, now);
            }
        }
        if (sweeping)
            Glow(current.Grip + Altitudes.AltIncVect * .015f, current.Angle, current.Scale * 1.06f, .88f);
    }

    private void Ghost(Sample sample, int depth, int now)
    {
        float alpha = Mathf.Lerp(.72f, .04f, (now - sample.Tick) / (float)LifetimeTicks);
        Vector3 forward = Quaternion.AngleAxis(sample.Angle, Vector3.up) * Vector3.forward;
        Glow(sample.Grip - forward * (.045f * depth) + Altitudes.AltIncVect * (.006f * depth),
            sample.Angle, sample.Scale * (1.04f + .035f * depth), alpha);
    }

    private void Glow(Vector3 grip, float angle, float scale, float alpha)
    {
        Vector3 center = grip + Quaternion.AngleAxis(angle, Vector3.up) * Vector3.forward * (.5f * scale);
        // Match the original FadedMaterialPool opacity steps in vertex colour.
        float opacity = Mathf.Floor(alpha * 30f) / 30f;
        int i = Emit(center, 0f, scale, Color.white.WithAlpha(opacity));
        if (i >= 0) Particles[i].RotationDegrees = -angle;
    }
}

using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx.Emitter;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Zhaoli;

internal sealed class ZhaoliDuanzhanTrail : VfxEmitter
{
    private const int LifetimeTicks = 22;
    private const int MaxSamples = 18;
    private const int Subdivisions = 3;
    private readonly record struct Sample(Vector3 Grip, float Angle, float Scale, int Tick);
    private readonly List<Sample> samples = new(MaxSamples);
    internal DuanzhanVisualState Owner;
    private int observedFrame;
    private Vector3 handOffset;

    internal static void Capture(DuanzhanVisualState state, Pawn pawn, Vector3 hand, Vector3 grip,
        float angle, float scale, bool sweeping, int now)
    {
        var trail = state.trail.Host?.Resolve(state.trail) as ZhaoliDuanzhanTrail;
        if (trail == null || trail.Owner != state || state.trail.Host.Map != pawn.Map)
        {
            state.EndTrail();
            var host = pawn.Map.GetComponent<MapComponent_VfxEmitterHost>();
            state.trail = host.Spawn(ZhaoliVisualDefOf.MX_ZL_DuanzhanTrail, VfxAnchor.Follow(pawn));
            if (!host.TryGetEmitter(state.trail, out trail)) return;
            trail.Owner = state;
            trail.samples.Clear();
        }
        trail.observedFrame = Time.frameCount;
        trail.handOffset = hand - pawn.DrawPos;
        trail.Trim(now);
        if (state.restoreTrail)
        {
            // While the caster is still on the saved cast cell, the previous sweep poses
            // are determined by the same deadlines and fixed equipment pivot as the live attack.
            int sweep = state.sequenceStartTick + state.chargeTicks;
            for (int t = Mathf.Max(sweep, now - LifetimeTicks); t < Mathf.Min(now, sweep + state.sweepTicks); t++)
            {
                state.Pose(t, out float pastScale, out float pastAngle, out Vector3 offset, out _);
                trail.Record(new Sample(hand + offset, pastAngle, pastScale * Patch_ZhaoliDuanzhan_DrawEquipmentAiming.BladeScale, t));
            }
            state.restoreTrail = false;
        }
        if (sweeping) trail.Record(new Sample(grip, angle, scale, now));
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
        LastMaintainTick = nowTick;
        if (Owner != null && nowTick < Owner.sequenceEndTick && Anchor.Pawn is { Dead: false } pawn
            && pawn.equipment?.Primary?.thingIDNumber == Owner.weaponThingId && base.Advance(deltaSeconds, nowTick, map)) return true;
        Owner = null;
        return false;
    }

    internal void Populate(Map map)
    {
        Count = 0;
        Pawn pawn = Anchor.Pawn;
        if (Owner == null || pawn == null || !pawn.Spawned || pawn.Map != map || pawn.Dead
            || pawn.equipment?.Primary?.thingIDNumber != Owner.weaponThingId || Time.frameCount - observedFrame > 1
            || !DebugViewSettings.drawThingsDynamic || pawn.IsHiddenFromPlayer()) return;
        int now = Find.TickManager.TicksGame;
        if (!Owner.Pose(now, out float scale, out float angle, out Vector3 offset, out bool sweeping)) return;
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
            Glow(pawn.DrawPos + handOffset + offset + Altitudes.AltIncVect * .015f, angle,
                scale * Patch_ZhaoliDuanzhan_DrawEquipmentAiming.BladeScale * 1.06f, .88f);
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

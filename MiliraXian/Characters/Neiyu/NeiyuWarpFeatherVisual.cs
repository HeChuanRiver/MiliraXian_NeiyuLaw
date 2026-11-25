using System;
using MiliraXian.Characters.Common.Vfx.Emitter;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu;

// The ability owns the real stack and its landing cell. The visual can outlive the cast for its short fade.
internal sealed class NeiyuWarpFeatherVisual : INeiyuVisualSource
{
    private const int ParticleCount = 32;
    private const int FinishTicks = 24;
    private const int CancelTicks = 12;
    private static readonly Color Ice = new(0.62f, 0.86f, 1f);
    private static readonly Color Pink = new(1f, 0.76f, 0.87f);
    private static readonly ParticleSeed[] Particles = CreateParticles();
    private readonly Map map;
    private NeiyuVisualBinding binding;
    private CompAbilityEffect_NeiyuWarpFeather owner;
    private Texture featherTexture;
    private Vector2 featherSize;
    private Vector3 sourcePosition;
    private Vector3 landingPosition;
    private Color featherColor;
    private float featherRotation;
    private float progress;
    private int endingTick = -1;
    private bool completed;

    internal NeiyuWarpFeatherVisual(CompAbilityEffect_NeiyuWarpFeather comp)
    {
        owner = comp;
        map = comp.parent.pawn.Map;
        ResolveFeatherGraphic(comp.PreviewThing, comp.PreparedFeathers.stackCount);
        UpdatePose();
        binding.Bind(this, map, NeiyuVisualDefOf.MX_NY_WarpFeather, VfxAnchor.At(sourcePosition));
    }

    bool INeiyuVisualSource.ActiveOn(Map current)
    {
        if (current != map) return false;
        if (endingTick < 0 && (owner?.CastActive != true || owner.parent.pawn.Map != map)) Cancel();
        return endingTick < 0 || Find.TickManager.TicksGame - endingTick < (completed ? FinishTicks : CancelTicks);
    }

    internal void Complete(Thing placedFeathers)
    {
        if (endingTick >= 0) return;
        completed = true;
        progress = 1f;
        landingPosition = placedFeathers.DrawPos;
        endingTick = Find.TickManager.TicksGame;
        owner = null;
    }

    internal void Cancel()
    {
        if (endingTick >= 0) return;
        endingTick = Find.TickManager.TicksGame;
        owner = null;
    }

    private void UpdatePose()
    {
        Pawn caster = owner.parent.pawn;
        sourcePosition = caster.DrawPos;
        landingPosition = owner.LandingCell.ToVector3Shifted();
        progress = owner.parent.verb.WarmingUp ? Mathf.Clamp01(owner.parent.verb.WarmupProgress) : 0f;
    }

    private void ResolveFeatherGraphic(Thing feather, int addedCount)
    {
        Graphic graphic = feather.Graphic;
        if (graphic is Graphic_RandomRotated rotated) graphic = rotated.SubGraphic;
        int count = feather == owner.PreparedFeathers ? addedCount : feather.stackCount + addedCount;
        if (graphic is Graphic_StackCount stack) graphic = stack.SubGraphicForStackCount(count, feather.def);
        featherTexture = graphic.MatSingle.mainTexture;
        featherColor = graphic.Color;
        featherSize = feather.def.graphicData.drawSize;
        float maxAngle = feather.def.graphicData.onGroundRandomRotateAngle;
        // Match Graphic_RandomRotated's item-ID rotation without changing the real item graphic.
        featherRotation = maxAngle > 0f
            ? -maxAngle + unchecked(feather.thingIDNumber * 542) % (maxAngle * 2f)
            : feather.Rotation.AsAngle;
    }

    void INeiyuVisualSource.Populate(NeiyuVisualEmitter emitter)
    {
        if (endingTick < 0) UpdatePose();
        IntVec3 cell = (completed ? landingPosition : sourcePosition).ToIntVec3();
        if (featherTexture == null || !DebugViewSettings.drawThingsDynamic || !cell.InBounds(map) || cell.Fogged(map)
            || !Find.CameraDriver.CurrentViewRect.ExpandedBy(1).Contains(cell)) return;
        emitter.SetTextureChannel("featherTexture", featherTexture);

        float elapsed = progress * 3f;
        float tail = endingTick < 0 ? 0f : Mathf.Clamp01((Find.TickManager.TicksGame - endingTick)
            / (float)(completed ? FinishTicks : CancelTicks));
        float opacity = endingTick < 0 ? 1f : 1f - Smooth(tail);
        float gather = SmoothRange(elapsed, 0.8f, 1.9f);
        float shape = SmoothRange(elapsed, 1.9f, 2.7f);
        float settle = SmoothRange(elapsed, 2.7f, 3f);
        float altitude = AltitudeLayer.MoteOverheadLow.AltitudeFor();
        Vector3 center = landingPosition;
        center.y = altitude;
        center.z += 0.42f * (1f - settle);

        float particleAlpha = SmoothRange(elapsed, 0f, 0.45f)
            * (1f - SmoothRange(elapsed, 2.35f, 3f)) * opacity;
        if (particleAlpha > 0.001f)
        {
            Quaternion rotation = Quaternion.AngleAxis(featherRotation, Vector3.up);
            foreach (ref readonly ParticleSeed particle in Particles.AsSpan())
            {
                float angle = particle.Phase + elapsed * particle.Speed;
                (float sin, float cos) = MathF.SinCos(angle);
                Vector3 orbit = sourcePosition + new Vector3(cos * particle.Radius, 0f,
                    sin * particle.Radius * 0.62f + 0.12f);
                orbit.y = altitude;
                (sin, cos) = MathF.SinCos(angle + gather * 2.2f);
                Vector3 swirl = center + new Vector3(cos, 0f, sin * 0.65f) * (0.11f * (1f - gather));
                Vector3 position = Vector3.Lerp(orbit, swirl, gather);
                position = Vector3.Lerp(position, center + rotation * particle.FeatherPoint, shape);
                Color color = particle.Color;
                color.a *= particleAlpha;
                float size = particle.Size * Mathf.Lerp(1f, 0.65f, shape);
                emitter.Emit(position, 0f, size, color, (int)NeiyuLayer.Glow);
            }
        }

        Color glow = Color.Lerp(Ice, Pink, 0.18f);
        glow.a = (0.04f + gather * 0.16f) * SmoothRange(elapsed, 0.2f, 1f) * opacity;
        float glowSize = Mathf.Lerp(0.7f, 0.38f, shape);
        emitter.Emit(center, 0f, glowSize, glow, (int)NeiyuLayer.Glow);

        if (!completed)
        {
            Color color = Color.Lerp(Color.Lerp(Ice, Pink, 0.12f), featherColor, settle);
            color.a = shape * opacity;
            Vector2 size = featherSize * Mathf.Lerp(1.12f, 1f, settle);
            center.y += 0.002f;
            Matrix4x4 matrix = Matrix4x4.TRS(center, Quaternion.AngleAxis(featherRotation, Vector3.up),
                new Vector3(size.x, 1f, size.y));
            emitter.Quad(NeiyuLayer.Feather, matrix, color);
        }
    }

    private readonly record struct ParticleSeed(float Phase, float Speed, float Radius,
        float Size, Color Color, Vector3 FeatherPoint);

    private static ParticleSeed[] CreateParticles()
    {
        var particles = new ParticleSeed[ParticleCount];
        for (int i = 0; i < particles.Length; i++)
        {
            float t = (i % 16) / 15f;
            float side = i < 16 ? -1f : 1f;
            float width = MathF.Sin(t * MathF.PI) * 0.18f;
            Color color = i % 4 == 0 ? Pink : Ice;
            color.a = 0.22f + (i % 3) * 0.06f;
            particles[i] = new ParticleSeed(i * 2.39996323f, 0.55f + (i % 3) * 0.07f,
                0.52f + (i % 5) * 0.105f, 0.055f + (i % 4) * 0.012f, color,
                new Vector3((t - 0.5f) * 0.42f + side * width, 0f,
                    (t - 0.5f) * 0.64f - side * width * 0.3f));
        }
        return particles;
    }

    private static float SmoothRange(float value, float from, float to) => Smooth(Mathf.InverseLerp(from, to, value));
    private static float Smooth(float value) => value * value * (3f - 2f * value);
}

using MiliraXian.Characters.Common.Vfx.Emitter;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu;

/// <summary>One-shot floating "断" glyph following the caster during single execution.</summary>
internal sealed class NeiyuExecGlyphEmitter : VfxEmitter
{
    private Vector3 offset;
    private float holdSeconds;
    private float fadeSeconds;

    public NeiyuExecGlyphEmitter()
    {
    }

    internal void Configure(Vector3 localOffset, int holdTicks, int fadeTicks)
    {
        offset = localOffset;
        holdSeconds = Mathf.Max(0f, holdTicks / 60f);
        fadeSeconds = Mathf.Max(1f / 60f, fadeTicks / 60f);
        Emit(Position + offset, holdSeconds + fadeSeconds, 1f, Tint(1f), particleGroup: 0);
    }

    internal override void Reset(VfxEffect effect, VfxAnchor anchor, int version, int nowTick, Vector3 position)
    {
        base.Reset(effect, anchor, version, nowTick, position);
        offset = Vector3.zero;
        holdSeconds = 0f;
        fadeSeconds = 1f / 60f;
    }

    internal override bool Advance(float deltaSeconds, int nowTick, Map map)
    {
        if (!base.Advance(deltaSeconds, nowTick, map)) return false;
        if (Count <= 0 || AgeSeconds >= holdSeconds + fadeSeconds) return false;

        float fade = 1f - Mathf.Clamp01((AgeSeconds - holdSeconds) / fadeSeconds);
        VfxParticle p = Particles[0];
        p.Position = Position + offset;
        p.Color = CanDraw ? Tint(fade) : Color.clear;
        Particles[0] = p;
        return true;
    }

    private static bool CanDraw =>
        !Find.UIRoot.HideMotes && !Find.ScreenshotModeHandler.Active
        && NeiyuSwordCombatVfx.Enabled && NeiyuSwordCombatVfx.Intensity > 0f;

    private static Color Tint(float alpha) =>
        Color.white.WithAlpha(Mathf.Clamp01(alpha * NeiyuSwordCombatVfx.Intensity));
}

/// <summary>
/// One-shot sheared slash arc used by both the advancing single-target first cut wave
/// and each airborne slash of the multi-target combo.
/// </summary>
internal sealed class NeiyuExecSlashEmitter : VfxEmitter
{
    // Turn the diagonal artwork to face forward and center its visible arc.
    private static readonly Matrix4x4 SlashArtwork = Matrix4x4.TRS(
        new Vector3(-.011f, 0f, -.07f), Quaternion.AngleAxis(35f, Vector3.up), Vector3.one);

    private float yaw;
    private float startWidth, endWidth;
    private float startLength, endLength;
    private float startTravel, endTravel;
    private float durationSeconds;
    private float fadeStartShare;
    private float entrySeconds;
    private float altitudeBias;
    private int stripSlot;

    public NeiyuExecSlashEmitter()
    {
    }

    internal void ConfigureSingleWave(float initialYaw, float slashWidth, int waveTicks)
    {
        yaw = initialYaw + 11f;
        startWidth = slashWidth * .97f;
        endWidth = slashWidth * 1.07f;
        startLength = slashWidth * .38f * .92f;
        endLength = slashWidth * .38f * 1.13f;
        startTravel = .18f;
        endTravel = 4f;
        durationSeconds = Mathf.Max(1, waveTicks) / 60f;
        fadeStartShare = .28f;
        entrySeconds = 0f;
        altitudeBias = 0f;
        Sync(0f);
    }

    internal void ConfigureMultiCut(float cutYaw, bool final, int fadeTicks, int cutIndex)
    {
        yaw = cutYaw;
        startWidth = final ? 20f : 16f;
        endWidth = final ? 23f : 19f;
        startLength = final ? 7.8f : 5.8f;
        endLength = final ? 9f : 7f;
        startTravel = 0f;
        endTravel = .68f;
        durationSeconds = Mathf.Max(1, fadeTicks) / 60f;
        fadeStartShare = 0f;
        entrySeconds = 2f / 60f;
        altitudeBias = cutIndex * .0002f;
        Sync(0f);
    }

    internal override void Reset(VfxEffect effect, VfxAnchor anchor, int version, int nowTick, Vector3 position)
    {
        base.Reset(effect, anchor, version, nowTick, position);
        durationSeconds = 0f;
        stripSlot = effect.RequireParticleFloatSlot("strip");
    }

    internal override bool Advance(float deltaSeconds, int nowTick, Map map)
    {
        Count = 0;
        if (!base.Advance(deltaSeconds, nowTick, map)) return false;
        if (durationSeconds <= 0f || AgeSeconds >= durationSeconds) return false;
        Sync(Mathf.Clamp01(AgeSeconds / durationSeconds));
        return true;
    }

    private void Sync(float p)
    {
        Count = 0;
        if (Find.UIRoot.HideMotes || Find.ScreenshotModeHandler.Active
            || !NeiyuSwordCombatVfx.Enabled || NeiyuSwordCombatVfx.Intensity <= 0f)
        {
            return;
        }

        float entry = entrySeconds > 0f ? Mathf.Clamp01(AgeSeconds / entrySeconds) : 1f;
        float fade = entry * (1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(fadeStartShare, 1f, p)));
        Color color = Color.white.WithAlpha(Mathf.Clamp01(fade * NeiyuSwordCombatVfx.Intensity));
        if (color.a <= .001f) return;

        float width = Mathf.Lerp(startWidth, endWidth, p);
        float length = Mathf.Lerp(startLength, endLength, p);
        float forwardYaw = entrySeconds > 0f ? yaw : yaw - 11f;
        Vector3 forward = Quaternion.AngleAxis(forwardYaw, Vector3.up) * Vector3.forward;
        float travel = entrySeconds > 0f ? p * endTravel - length * .52f : Mathf.Lerp(startTravel, endTravel, p);
        Vector3 at = Position + forward * travel;
        at.y = AltitudeLayer.MoteOverhead.AltitudeFor() + .015f + altitudeBias;

        // The rotated arc covers 52% of the texture width and 81% of its length.
        Matrix4x4 matrix = Matrix4x4.TRS(at, Quaternion.AngleAxis(yaw, Vector3.up),
            new Vector3(width / .52f, 1f, length / .81f)) * SlashArtwork;
        Vector3 center = matrix.MultiplyPoint3x4(Vector3.zero);
        Vector3 right = matrix.MultiplyVector(Vector3.right * .5f);
        Vector3 back = matrix.MultiplyVector(Vector3.back * .5f);

        int i0 = Emit(center - right, 0f, 1f, color, particleGroup: 0);
        if (i0 >= 0) { Particles[i0].Side = back; SetChannel(i0, stripSlot, 1f); }
        int i1 = Emit(center + right, 0f, 1f, color, particleGroup: 0);
        if (i1 >= 0) { Particles[i1].Side = back; SetChannel(i1, stripSlot, 1f); }
    }
}

/// <summary>
/// One-shot spirit-sword impact burst: a slash decal plus drifting glow sparks driven by
/// native particle velocities.
/// </summary>
internal sealed class NeiyuExecPierceEmitter : VfxEmitter
{
    private static readonly Color ExecutionPink = new(1f, .64f, .84f);

    private float durationSeconds;

    public NeiyuExecPierceEmitter()
    {
    }

    internal void Configure(Vector3 forward, Vector3 side, int bladeIndex, bool terminal, int pierceTicks, int seedBase)
    {
        durationSeconds = Mathf.Max(1, pierceTicks) / 60f;
        if (!NeiyuSwordCombatVfx.Enabled || NeiyuSwordCombatVfx.Intensity <= 0f) return;

        Color ice = NeiyuSwordCombatVfx.Ice;
        float size = terminal ? 1.65f : .75f;
        Color cutColor = terminal ? Color.white : Color.Lerp(Color.white, ice, .44f);
        int cutIdx = Emit(Position, durationSeconds, size, Tint(cutColor, 1f), particleGroup: 0);
        if (cutIdx >= 0)
        {
            Particles[cutIdx].RotationDegrees = -(terminal ? -10f : 16f * (bladeIndex - 2));
        }

        int count = Mathf.Clamp(Mathf.RoundToInt(7f * NeiyuSwordCombatVfx.Intensity), 1, 10);
        for (int i = 0; i < count; i++)
        {
            int seed = seedBase + bladeIndex * 593 + i * 97;
            float spread = NeiyuSwordCombatVfx.Hash(seed);
            Vector3 drift = forward * (.22f + NeiyuSwordCombatVfx.Hash(seed + 37)) + side * (spread - .5f);
            int idx = Emit(Position, durationSeconds, 1f, Tint(Color.Lerp(ice, ExecutionPink, spread), 1f), particleGroup: 1);
            if (idx >= 0)
            {
                Particles[idx].Velocity = drift / durationSeconds;
                Particles[idx].RotationDegrees = -drift.AngleFlat();
            }
        }
    }

    internal override void Reset(VfxEffect effect, VfxAnchor anchor, int version, int nowTick, Vector3 position)
    {
        base.Reset(effect, anchor, version, nowTick, position);
        durationSeconds = 0f;
    }

    internal override bool Advance(float deltaSeconds, int nowTick, Map map)
    {
        if (!base.Advance(deltaSeconds, nowTick, map)) return false;
        if (durationSeconds <= 0f || AgeSeconds >= durationSeconds) return false;

        bool visible = !Find.UIRoot.HideMotes && !Find.ScreenshotModeHandler.Active && NeiyuSwordCombatVfx.Enabled;
        float p = Mathf.Clamp01(AgeSeconds / durationSeconds);
        float fade = visible ? Mathf.Clamp01((1f - p) * (1f - p) * NeiyuSwordCombatVfx.Intensity) : 0f;
        for (int i = 0; i < Count; i++)
        {
            if (!Particles[i].Alive) continue;
            Particles[i].Color.a = fade;
        }
        return true;
    }

    private static Color Tint(Color color, float alpha) =>
        color.WithAlpha(Mathf.Clamp01(alpha * NeiyuSwordCombatVfx.Intensity));
}

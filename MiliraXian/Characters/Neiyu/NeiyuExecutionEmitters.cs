using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Common.Vfx.Emitter;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu;

/// <summary>
/// Sustained blade-draw visual for single execution: enchantment sparks along the pushed
/// (grip, tip) segment, tip cross flash, and a 6-sample trail ribbon. Owns no weapon or
/// pose math; the job driver pushes each tick's calibrated grip and tip.
/// </summary>
internal sealed class NeiyuBladeChargeEmitter : VfxEmitter
{
    private const int MaxTrailSamples = 6;
    private static readonly Color ExecutionPink = new(1f, .64f, .84f);

    private readonly record struct BladeSample(Vector3 Grip, Vector3 Tip);

    private readonly List<BladeSample> trail = new(MaxTrailSamples);
    private Vector3 grip, tip;
    private float reveal;
    private float phaseTicks;
    private float crossFade;
    private float trailFade;
    private bool showEnchant;
    private bool showTrail;
    private int stripSlot;
    private int stripId;

    public NeiyuBladeChargeEmitter()
    {
    }

    internal override void Reset(VfxEffect effect, VfxAnchor anchor, int version, int nowTick, Vector3 position)
    {
        base.Reset(effect, anchor, version, nowTick, position);
        trail.Clear();
        showEnchant = showTrail = false;
        crossFade = trailFade = reveal = phaseTicks = 0f;
        stripId = 0;
        stripSlot = effect.RequireParticleFloatSlot("strip");
    }

    internal void PushBlade(Vector3 currentGrip, Vector3 currentTip, float currentReveal,
        float enchantTicks, float tipCrossFade, bool swinging, float currentTrailFade)
    {
        grip = currentGrip;
        tip = currentTip;
        reveal = currentReveal;
        phaseTicks = enchantTicks;
        crossFade = tipCrossFade;
        showEnchant = enchantTicks >= 0f && !swinging;
        showTrail = swinging;
        trailFade = currentTrailFade;

        if (swinging)
        {
            if (trail.Count >= MaxTrailSamples) trail.RemoveAt(0);
            trail.Add(new BladeSample(currentGrip, currentTip));
        }
        else if (trail.Count > 0)
        {
            trail.Clear();
        }
    }

    internal override bool Advance(float deltaSeconds, int nowTick, Map map)
    {
        Count = 0;
        if (!base.Advance(deltaSeconds, nowTick, map)) return false;

        stripId = 0;
        if (Find.UIRoot.HideMotes || Find.ScreenshotModeHandler.Active || !DebugViewSettings.drawThingsDynamic
            || !NeiyuSwordCombatVfx.Enabled || NeiyuSwordCombatVfx.Intensity <= 0f
            || Anchor.Pawn?.IsHiddenFromPlayer() == true)
        {
            return true;
        }

        Color ice = NeiyuSwordCombatVfx.Ice;
        if (showEnchant)
        {
            Vector3 axis = tip - grip;
            Vector3 side = Vector3.Cross(Vector3.up, axis).normalized;
            int count = Mathf.Clamp(Mathf.RoundToInt(12f * NeiyuSwordCombatVfx.Intensity), 2, 18);
            for (int i = 0; i < count; i++)
            {
                float particle = Mathf.Repeat(phaseTicks / 12f + i * .618034f, 1f);
                Vector3 at = grip + axis * (particle * reveal) + side * (.12f * Mathf.Sin(i * 2.1f + particle * 6f));
                Emit(at, 0f, .15f, Tint(Color.Lerp(ExecutionPink, ice, particle), reveal * Mathf.Sin(particle * Mathf.PI)), particleGroup: 0);
            }
            float front = Mathf.Clamp01(reveal);
            Emit(Vector3.Lerp(grip, tip, front), 0f, .23f, Tint(Color.white, front), particleGroup: 0);
            if (crossFade > .001f)
            {
                int crossIdx = Emit(tip, 0f, 1.68f, Tint(Color.white, crossFade), particleGroup: 1);
                if (crossIdx >= 0) Particles[crossIdx].RotationDegrees = 10f;
            }
        }

        if (showTrail && trailFade > .001f)
        {
            int count = trail.Count;
            for (int i = 0; i < count; i++)
            {
                BladeSample sample = trail[count - 1 - i];
                Vector3 line = (sample.Tip - sample.Grip).Yto0();
                if (line.sqrMagnitude <= 1e-4f) continue;
                int age = i + 1;
                float fade = (1f - age / 7f) * trailFade;
                float width = .11f + .038f * age;
                Color color = Tint(Color.Lerp(ice, ExecutionPink, age / 6f), .58f * fade);
                EmitTrailQuad(Vector3.Lerp(sample.Grip, sample.Tip, .63f), line.normalized, line.magnitude, width, color);
            }
        }

        return true;
    }

    private void EmitTrailQuad(Vector3 center, Vector3 forward, float length, float width, Color color)
    {
        if (color.a <= .001f) return;
        Vector3 right = Vector3.Cross(Vector3.up, forward) * (width * .5f);
        Vector3 back = -forward * (length * .5f);
        int id = ++stripId;
        int i0 = Emit(center - right, 0f, 1f, color, particleGroup: 2);
        if (i0 >= 0) { Particles[i0].Side = back; SetChannel(i0, stripSlot, id); }
        int i1 = Emit(center + right, 0f, 1f, color, particleGroup: 2);
        if (i1 >= 0) { Particles[i1].Side = back; SetChannel(i1, stripSlot, id); }
    }

    private static Color Tint(Color color, float alpha) =>
        color.WithAlpha(Mathf.Clamp01(alpha * NeiyuSwordCombatVfx.Intensity));
}

/// <summary>One-time configuration pushed when single execution enters its spirit-sword volley.</summary>
internal struct NeiyuVolleyConfig
{
    internal int SceneStart;
    internal float ThrowYaw;
    internal bool AirVolley;
    internal int SwordCount;
    internal int ReleaseAt;
    internal int AimAt;
    internal int VolleyAt;
    internal int Stagger;
    internal int Travel;
    internal int FinalImpactAt;
}

/// <summary>
/// Sustained spirit-sword volley visual: renders flying and pinned spirit swords with trailing
/// glow using standard Sprite passes. Impacts and the floating glyph are separate one-shot emitters.
/// </summary>
internal sealed class NeiyuVolleyEmitter : VfxEmitter
{
    private static readonly Color ExecutionPink = new(1f, .64f, .84f);

    private bool configured;
    private NeiyuVolleyConfig cfg;
    private Vector3 previousTargetDraw;
    private Vector3 aimPoint;
    private int impacts;

    public NeiyuVolleyEmitter()
    {
    }

    internal override void Reset(VfxEffect effect, VfxAnchor anchor, int version, int nowTick, Vector3 position)
    {
        base.Reset(effect, anchor, version, nowTick, position);
        configured = false;
        cfg = default;
        previousTargetDraw = aimPoint = Vector3.zero;
        impacts = 0;
    }

    internal void Configure(in NeiyuVolleyConfig config)
    {
        cfg = config;
        configured = true;
    }

    internal void SetTarget(Vector3 prevTarget, Vector3 currentAim, int impactCount)
    {
        previousTargetDraw = prevTarget;
        aimPoint = currentAim;
        impacts = impactCount;
    }

    internal override bool Advance(float deltaSeconds, int nowTick, Map map)
    {
        Count = 0;
        if (!base.Advance(deltaSeconds, nowTick, map)) return false;

        if (!configured || Find.UIRoot.HideMotes || Find.ScreenshotModeHandler.Active
            || !NeiyuSwordCombatVfx.Enabled || NeiyuSwordCombatVfx.Intensity <= 0f
            || Anchor.Pawn?.IsHiddenFromPlayer() == true)
        {
            return true;
        }

        float tick = VisualClock.TicksSince(cfg.SceneStart);
        if (tick < cfg.ReleaseAt) return true;

        float lerp = Mathf.Clamp01(VisualClock.TicksSince(NeiyuSwordCombat.Now) + 1f);
        Vector3 aim = Vector3.Lerp(previousTargetDraw, aimPoint, lerp);
        NeiyuSingleExecutionProps props = NeiyuSwordCombat.Config.single;
        Vector3 forward = Quaternion.AngleAxis(cfg.ThrowYaw, Vector3.up) * Vector3.forward;
        Vector3 side = Vector3.Cross(Vector3.up, forward);
        float swordScale = props.spiritSwordScale;
        float tipOffset = .70f * swordScale;
        float slotSpacing = Mathf.Lerp(.52f, .52f * swordScale, .65f);
        Color ice = NeiyuSwordCombatVfx.Ice;

        for (int i = 0; i < cfg.SwordCount; i++)
        {
            int launch = cfg.VolleyAt + i * cfg.Stagger;
            int impact = launch + cfg.Travel;
            bool terminal = i == cfg.SwordCount - 1;
            float slot = i == 0 ? 0f : ((i + 1) / 2) * (i % 2 == 1 ? -1f : 1f);
            Vector3 seat = Position.Yto0() + side * (slotSpacing * slot) - forward * (.72f + .16f * Mathf.Abs(slot))
                + Vector3.forward * (.42f + .11f * Mathf.Abs(slot));
            Vector3 contact = terminal ? aim : aim + side * (.13f * slot) + Vector3.forward * (.13f - .065f * Mathf.Abs(slot));
            Vector3 toContact = (contact - seat).Yto0();
            float distance = toContact.magnitude;
            Vector3 direction = distance > .001f ? toContact / distance : forward;
            float yaw = direction.AngleFlat();

            if (tick < impact)
            {
                float p = ProgressBetween(tick, launch, impact);
                float appear = cfg.AirVolley ? 1f : ProgressBetween(tick, cfg.ReleaseAt, cfg.AimAt);
                Vector3 startTip = seat + direction * Mathf.Min(tipOffset, distance * .8f);
                Vector3 tip = Vector3.Lerp(startTip, contact, p * (.72f + .28f * p));
                Vector3 center = tip - direction * tipOffset;
                EmitSprite(center, yaw, swordScale, Tint(Color.white, appear), group: 0);
                if (p > 0f)
                {
                    EmitSprite(center - direction * (.53f * swordScale), yaw, swordScale,
                        Tint(Color.Lerp(ice, ExecutionPink, Mathf.Abs(slot) / 4f), .73f), group: 1);
                }
            }
            else if (impacts > i && !terminal)
            {
                float settle = Mathf.Clamp01((tick - impact) / 6f);
                float pinYaw = yaw + slot * 8f * settle;
                Vector3 pinDir = Quaternion.AngleAxis(pinYaw, Vector3.up) * Vector3.forward;
                Vector3 center = contact - pinDir * (tipOffset * Mathf.Lerp(1f, .68f, settle));
                float fade = 1f - ProgressBetween(tick, cfg.FinalImpactAt, cfg.FinalImpactAt + props.pierceTicks);
                EmitSprite(center, pinYaw, swordScale, Tint(Color.white, .92f * fade), group: 0);
            }
        }

        return true;
    }

    private void EmitSprite(Vector3 at, float yaw, float scale, Color color, int group)
    {
        if (color.a <= .001f) return;
        int idx = Emit(at, 0f, scale, color, group);
        if (idx >= 0) Particles[idx].RotationDegrees = -yaw;
    }

    private static Color Tint(Color color, float alpha) =>
        color.WithAlpha(Mathf.Clamp01(alpha * NeiyuSwordCombatVfx.Intensity));

    private static float ProgressBetween(float tick, float start, float end) =>
        Mathf.Clamp01((tick - start) / Mathf.Max(1f, end - start));
}

/// <summary>
/// Floor arc/ring parameter pusher for multi-target execution; all polar math and curved brush
/// UV mapping live in <c>MX_NeiyuExecutionRing.frag.glsl</c>.
/// </summary>
internal sealed class NeiyuExecutionRingEmitter : VfxEmitter
{
    private Vector3 ringCenter;
    private float startYawDeg;
    private float sweepDeg;
    private float radius;
    private float fade;
    private bool closed;
    private bool active;

    private int ringQuadSlot;
    private int ringArcSlot;
    private int ringMetaSlot;

    public NeiyuExecutionRingEmitter()
    {
    }

    internal override void Reset(VfxEffect effect, VfxAnchor anchor, int version, int nowTick, Vector3 position)
    {
        base.Reset(effect, anchor, version, nowTick, position);
        active = false;
        ringQuadSlot = effect.RequireVectorSlot("ringQuad");
        ringArcSlot = effect.RequireVectorSlot("ringArc");
        ringMetaSlot = effect.RequireVectorSlot("ringMeta");
        SetVectorChannel(ringQuadSlot, Vector4.zero);
    }

    internal void SetArc(Vector3 center, float startYaw, float sweep, float outerRadius, float alpha, bool isClosed)
    {
        ringCenter = center;
        startYawDeg = startYaw;
        sweepDeg = sweep;
        radius = outerRadius;
        fade = alpha;
        closed = isClosed;
        active = true;
    }

    internal void ClearArc() => active = false;

    internal override bool Advance(float deltaSeconds, int nowTick, Map map)
    {
        if (!base.Advance(deltaSeconds, nowTick, map))
        {
            SetVectorChannel(ringQuadSlot, Vector4.zero);
            return false;
        }

        float opacity = active && !Find.UIRoot.HideMotes && !Find.ScreenshotModeHandler.Active
            && DebugViewSettings.drawThingsDynamic && NeiyuSwordCombatVfx.Enabled
            && Anchor.Pawn?.IsHiddenFromPlayer() != true
            ? Mathf.Clamp01(fade * NeiyuSwordCombatVfx.Intensity)
            : 0f;

        if (sweepDeg < .5f || opacity <= .001f || radius <= .01f)
        {
            SetVectorChannel(ringQuadSlot, Vector4.zero);
            return true;
        }

        float offsetX = ringCenter.x - Position.x;
        float offsetZ = ringCenter.z - Position.z;
        SetVectorChannel(ringQuadSlot, new Vector4(radius + Mathf.Abs(offsetX) + .1f, radius + Mathf.Abs(offsetZ) + .1f, 0f, 0f));
        SetVectorChannel(ringArcSlot, new Vector4(startYawDeg * Mathf.Deg2Rad, sweepDeg * Mathf.Deg2Rad, radius, opacity));
        SetVectorChannel(ringMetaSlot, new Vector4(offsetX, offsetZ, closed ? 1f : 0f, 0f));
        return true;
    }
}

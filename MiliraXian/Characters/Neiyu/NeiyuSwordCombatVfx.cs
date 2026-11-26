using System;
using System.Collections.Generic;
using HarmonyLib;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Common.Vfx.Emitter;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.Neiyu;

[StaticConstructorOnStartup]
internal static class NeiyuSwordCombatVfx
{
    internal static readonly Color Gold = new(1f, .65f, .15f), Hot = new(1f, .96f, .76f);
    internal static readonly Color Red = new(1f, .19f, .12f), Ice = new(.65f, .87f, 1f);
    private static readonly FleckDef Spark = DefDatabase<FleckDef>.GetNamed("MX_NeiyuSwordSpark");
    private static readonly FleckDef ParryFire = DefDatabase<FleckDef>.GetNamed("MX_NeiyuSwordParryFire");
    private static readonly FleckDef ParryDust = DefDatabase<FleckDef>.GetNamed("MX_NeiyuSwordParryDust");
    private static readonly FleckDef Slash = DefDatabase<FleckDef>.GetNamed("MXNL_Fleck_BladeLight_Neiyu_A");
    internal static bool Enabled => NeiyuLawMod.Instance?.Settings?.EnableNeiyuSwordVfx != false;
    internal static float Intensity => Mathf.Clamp(NeiyuSwordCombat.Config.visualIntensity
        * (NeiyuLawMod.Instance?.Settings?.NeiyuSwordVfxIntensity ?? 1f), 0f, 2f);

    static NeiyuSwordCombatVfx()
    {
        // Load the impact textures during startup, rather than on the first parry.
        foreach (FleckDef def in new[] { Spark, ParryFire, ParryDust })
            foreach (GraphicData graphic in def.randomGraphics)
                _ = graphic.Graphic;
    }

    internal static void AdjustExecutionWeapon(Thing equipment, ref Vector3 drawLoc, ref float aimAngle)
    {
        if (equipment?.def != NeiyuSwordCombat.Config.sword) return;
        Pawn pawn = MXNeiyuShieldUtility.TryGetEquipmentOwnerPawn(equipment);
        if (pawn?.jobs?.curDriver is not JobDriver_NeiyuPostureExecution { HasScene: false } driver) return;
        float p = driver.Progress;
        NeiyuSwordCombatDef c = NeiyuSwordCombat.Config;
        float angle = p < c.dashFraction ? Mathf.Lerp(-25f, -75f, p / c.dashFraction)
            : p < c.strikeFraction ? Mathf.Lerp(-75f, 65f, Mathf.InverseLerp(c.dashFraction, c.strikeFraction, p))
            : Mathf.Lerp(65f, -15f, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(c.recoverFraction, 1f, p)));
        aimAngle = driver.FacingAngle + angle;
        Vector3 direction = Quaternion.AngleAxis(driver.FacingAngle, Vector3.up) * Vector3.forward;
        drawLoc += direction * (p < c.dashFraction ? -.12f : .18f * (1f - p));
    }

    private static VfxEmitterHandle Emit(Pawn pawn, Vector3 position, int kind, SoundDef sound, Vector3 direction)
    {
        if (pawn?.Spawned != true) return default;
        try
        {
            sound?.PlayOneShot(new TargetInfo(position.ToIntVec3(), pawn.Map));
        }
        catch (Exception exception)
        {
            Log.ErrorOnce($"[霓羽弹刀] 事件音效不可用：{exception}", 197632103);
        }
        try
        {
            if (!Enabled || Intensity <= 0f || pawn.IsHiddenFromPlayer()
                || Find.CameraDriver?.CurrentViewRect.ExpandedBy(3).Contains(position.ToIntVec3()) == false) return default;
            VfxEmitterHandle handle = kind is not (0 or 1)
                ? NeiyuSwordFlashEmitter.Spawn(pawn, position, direction, kind)
                : default;
            // Native flecks own the whole parry burst; no parallel procedural parry flash.
            Rand.PushState(unchecked(pawn.thingIDNumber * 397 ^ NeiyuSwordCombat.Now));
            try
            {
                if (kind is 0 or 1) EmitParry(pawn, position, direction, kind == 1);
                if (kind is 0 or 1 or 2 or 4)
                {
                    int count = Mathf.RoundToInt(NeiyuSwordCombat.Config.sparksPerDeflection * Intensity * (kind == 0 ? 1f : 1.6f));
                    float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                    for (int i = 0; i < Mathf.Min(40, count); i++)
                    {
                        float seed = Hash(pawn.thingIDNumber + NeiyuSwordCombat.Now * 43 + i * 97);
                        FleckCreationData data = FleckMaker.GetDataStatic(position, pawn.Map, Spark, .28f + seed * .16f);
                        data.velocityAngle = angle + (seed - .5f) * 160f;
                        data.velocitySpeed = 2.5f + seed * 4f;
                        data.rotation = data.velocityAngle;
                        data.instanceColor = kind is 2 or 4 ? Color.Lerp(Red, Gold, seed) : Color.Lerp(Gold, Hot, seed);
                        pawn.Map.flecks.CreateFleck(data);
                    }
                }
            }
            finally { Rand.PopState(); }
            return handle;
        }
        catch (Exception exception)
        {
            Log.ErrorOnce($"[霓羽弹刀] 事件特效不可用：{exception}", 197632101);
            return default;
        }
    }

    private static void EmitParry(Pawn pawn, Vector3 position, Vector3 direction, bool perfect)
    {
        float intensity = Intensity;
        float angle = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
        FleckCreationData fire = FleckMaker.GetDataStatic(position, pawn.Map, ParryFire,
            (perfect ? 2.1f : 1.65f) * Mathf.Lerp(.85f, 1.1f, intensity * .5f));
        fire.rotation = angle - 45f;
        fire.solidTimeOverride = perfect ? .055f : .025f;
        fire.instanceColor = new Color(1f, 1f, 1f, Mathf.Min(1f, intensity));
        pawn.Map.flecks.CreateFleck(fire);

        int count = Mathf.Clamp(Mathf.RoundToInt((perfect ? 12f : 8f) * intensity), 1, 20);
        for (int i = 0; i < count; i++)
        {
            int seed = pawn.thingIDNumber + NeiyuSwordCombat.Now * 43 + i * 97;
            float spread = Hash(seed), size = Hash(seed + 31), drift = Hash(seed + 73);
            Vector3 at = position + new Vector3((spread - .5f) * .3f, 0f, (drift - .5f) * .12f);
            FleckCreationData dust = FleckMaker.GetDataStatic(at, pawn.Map, ParryDust, .24f + size * .2f);
            dust.rotation = spread * 360f;
            dust.rotationRate = (drift - .5f) * 100f;
            dust.velocityAngle = angle + (spread - .5f) * 210f;
            dust.velocitySpeed = .18f + drift * .45f;
            dust.airTimeLeft = .85f;
            dust.instanceColor = new Color(1f, 1f, 1f, .75f * Mathf.Min(1f, intensity));
            pawn.Map.flecks.CreateFleck(dust);
        }
    }

    private static void Safely(Action action)
    {
        try { action(); }
        catch (Exception exception)
        {
            Log.ErrorOnce($"[霓羽弹刀] 视觉回调已中断，战斗结算继续：{exception}", 197632102);
        }
    }

    internal static void SingleSound(Pawn pawn, Vector3 at, SoundDef sound) =>
        Safely(() => sound?.PlayOneShot(new TargetInfo(at.ToIntVec3(), pawn.Map)));

    internal static void SingleLandingBurst(Pawn pawn, IntVec3 cell)
    {
        const float radius = 5f;
        DamageDef bomb = DamageDefOf.Bomb;
        SingleSound(pawn, cell.ToVector3Shifted(), bomb.soundExplosion);
        if (!Enabled || Intensity <= 0f) return;
        Safely(() => VanillaExplosionVfx.Play(cell, pawn.Map, radius, bomb,
            unchecked(pawn.thingIDNumber * 397 ^ NeiyuSwordCombat.Now)));
    }

    internal static void SingleAfterimage(Pawn pawn, Vector3 at, Rot4 facing, int startTick, int duration,
        JobDriver_NeiyuPostureExecution owner)
    {
        if (!Enabled || Intensity <= 0f) return;
        Safely(() => pawn.Map.GetComponent<MapComponent_PawnAfterimages>().AddAfterimage(
            pawn, at, facing, duration, .42f * Mathf.Min(1f, Intensity), Ice, owner, startTick));
    }

    internal static void Deflect(Pawn defender, Pawn attacker, bool perfect) => Safely(() =>
    {
        Vector3 direction = attacker.DrawPos - defender.DrawPos;
        Emit(defender, Vector3.Lerp(defender.DrawPos, attacker.DrawPos, .5f), perfect ? 1 : 0,
            perfect ? NeiyuSwordCombat.Config.perfectSound : NeiyuSwordCombat.Config.normalSound, direction);
        NeiyuWeaponVisualHooks.Notify(defender, perfect ? "SwordDeflect_Perfect" : "SwordDeflect_Normal",
            attacker.Position, 1.2f, direction.AngleFlat());
        if (perfect) Emit(attacker, attacker.DrawPos, 7, null, direction);
    });

    internal static void Break(Pawn target) => Emit(target, target.DrawPos, 2,
        NeiyuSwordCombat.Config.breakSound, Vector3.forward);

    internal static void Reflect(Pawn defender, Vector3 contact, Vector3 returnDirection, bool perfect) => Safely(() =>
    {
        Emit(defender, contact, perfect ? 1 : 0,
            perfect ? NeiyuSwordCombat.Config.perfectSound : NeiyuSwordCombat.Config.normalSound, returnDirection);
        NeiyuWeaponVisualHooks.Notify(defender, "SwordDeflect_Projectile", contact.ToIntVec3(), 1.2f, returnDirection.AngleFlat());
    });

    internal static VfxEmitterHandle Execution(Pawn executor, IntVec3 target, NeiyuExecutionPhase phase)
    {
        VfxEmitterHandle handle = default;
        Safely(() =>
        {
            NeiyuSwordCombatDef c = NeiyuSwordCombat.Config;
            int kind = phase == NeiyuExecutionPhase.ExecutionStart ? 3 : phase == NeiyuExecutionPhase.ExecutionStrike ? 4 : 5;
            handle = Emit(executor, kind == 3 ? executor.DrawPos : target.ToVector3Shifted(), kind,
                kind == 3 ? c.prepareSound : kind == 4 ? c.strikeSound : c.endSound,
                target.ToVector3Shifted() - executor.DrawPos);
            NeiyuWeaponVisualHooks.Notify(executor, "SwordPosture_" + phase, target, 1.5f,
                (target.ToVector3Shifted() - executor.DrawPos).AngleFlat());
            if (kind == 4 && Enabled)
                Patch_MXNL_MeleeSlashGhost_TryCastShot.SpawnDirectionalFleck(executor.Map,
                    target.ToVector3Shifted(), Slash, 2f, 35f);
        });
        return handle;
    }

    internal static void Dash(Pawn executor, Pawn victim) => Safely(() =>
    {
        Emit(executor, victim.DrawPos, 6, null, victim.DrawPos - executor.DrawPos);
        if (!Enabled) return;
        MapComponent_PawnAfterimages ghosts = executor.Map.GetComponent<MapComponent_PawnAfterimages>();
        for (int i = 0; i < 3; i++)
            ghosts.AddAfterimage(executor, Vector3.Lerp(executor.DrawPos, victim.DrawPos, i / 3f),
                executor.Rotation, 15 - i * 3, .28f - i * .04f, Color.Lerp(Ice, Gold, i / 3f));
        NeiyuWeaponVisualHooks.Notify(executor, "SwordPosture_Dash", victim.Position, 1.8f,
            (victim.DrawPos - executor.DrawPos).AngleFlat());
    });

    // Reconstruct remaining light geometry from the saved job clock. Audio and
    // hit callbacks are deliberately absent; their saved flags stay authoritative.
    internal static VfxEmitterHandle Resume(Pawn pawn, IntVec3 target, int start, int dash, int strike, int recover,
        bool dashed, bool committed, bool ended)
    {
        VfxEmitterHandle startFlash = default;
        Safely(() =>
        {
            if (!Enabled || !pawn.Spawned) return;
            Vector3 at = target.ToVector3Shifted(), direction = at - pawn.DrawPos;
            if (!committed)
                startFlash = NeiyuSwordFlashEmitter.SpawnAt(pawn, pawn.DrawPos, direction, 3, start, dash);
            if (dashed) NeiyuSwordFlashEmitter.SpawnAt(pawn, at, direction, 6, start + dash, 15);
            if (committed) NeiyuSwordFlashEmitter.SpawnAt(pawn, at, direction, 4, start + strike, 18);
            if (ended) NeiyuSwordFlashEmitter.SpawnAt(pawn, at, direction, 5, start + recover, 25);
        });
        return startFlash;
    }

    internal static float Hash(int seed)
    {
        uint x = unchecked((uint)seed);
        x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16;
        return (x & 0x00ffffff) / 16777215f;
    }
}

/// <summary>
/// Short-lived procedural sword combat flash (break ring, execution cue, cross slash,
/// recovery sparks, dash slash, or perfect-deflect opening mark) rendered via standard
/// Ribbon and Sprite passes without a dedicated map component.
/// </summary>
internal sealed class NeiyuSwordFlashEmitter : VfxEmitter
{
    private int kind;
    private Vector3 direction;
    private int startTick;
    private int durationTicks;
    private int stripSlot;
    private int stripId;

    public NeiyuSwordFlashEmitter()
    {
    }

    internal static VfxEmitterHandle Spawn(Pawn pawn, Vector3 position, Vector3 direction, int kind)
    {
        int duration = kind switch { 2 => 22, 3 => 22, 4 => 18, 5 => 25, 7 => 35, _ => 15 };
        return SpawnAt(pawn, position, direction, kind, NeiyuSwordCombat.Now, duration);
    }

    internal static VfxEmitterHandle SpawnAt(Pawn pawn, Vector3 position, Vector3 direction, int kind, int tick, int duration)
    {
        if (pawn?.Map == null || NeiyuVisualDefOf.MX_NY_SwordFlashes == null) return default;
        if (duration <= 0 || NeiyuSwordCombat.Now - tick >= duration) return default;

        MapComponent_VfxEmitterHost host = pawn.Map.GetComponent<MapComponent_VfxEmitterHost>();
        if (host == null) return default;

        VfxAnchor anchor = kind is 3 or 7 ? VfxAnchor.Follow(pawn) : VfxAnchor.At(position);
        VfxEmitterHandle handle = host.Spawn(NeiyuVisualDefOf.MX_NY_SwordFlashes, anchor);
        if (host.TryGetEmitter(handle, out NeiyuSwordFlashEmitter emitter))
        {
            emitter.Configure(kind, direction, tick, duration);
        }
        return handle;
    }

    internal override void Reset(VfxEffect effect, VfxAnchor anchor, int version, int nowTick, Vector3 position)
    {
        base.Reset(effect, anchor, version, nowTick, position);
        kind = 0;
        direction = Vector3.forward;
        startTick = nowTick;
        durationTicks = 0;
        stripId = 0;
        stripSlot = effect.RequireParticleFloatSlot("strip");
    }

    private void Configure(int flashKind, Vector3 flashDirection, int flashStartTick, int flashDurationTicks)
    {
        kind = flashKind;
        direction = flashDirection;
        startTick = flashStartTick;
        durationTicks = flashDurationTicks;
        Sync(NeiyuSwordCombat.Now);
    }

    internal override bool Advance(float deltaSeconds, int nowTick, Map map)
    {
        Count = 0;
        if (!base.Advance(deltaSeconds, nowTick, map))
        {
            return false;
        }
        if (durationTicks <= 0 || nowTick - startTick >= durationTicks)
        {
            return false;
        }
        Sync(nowTick);
        return true;
    }

    private void Sync(int now)
    {
        Count = 0;
        stripId = 0;
        if (!NeiyuSwordCombatVfx.Enabled || NeiyuSwordCombatVfx.Intensity <= 0f)
        {
            return;
        }
        Camera camera = Find.Camera;
        if (camera == null)
        {
            return;
        }
        if (Find.CameraDriver != null && !Find.CameraDriver.CurrentViewRect.ExpandedBy(3).Contains(Position.ToIntVec3()))
        {
            return;
        }

        Vector3 right = camera.transform.right;
        Vector3 up = camera.transform.up;
        Vector3 normal = -camera.transform.forward;

        float p = Mathf.Clamp01((now - startTick) / (float)durationTicks);
        float fade = (1f - p) * (1f - p);
        Vector3 at = Position;
        Color gold = NeiyuSwordCombatVfx.Gold;
        Color hot = NeiyuSwordCombatVfx.Hot;
        Color red = NeiyuSwordCombatVfx.Red;
        Color ice = NeiyuSwordCombatVfx.Ice;

        if (kind == 3)
        {
            float charge = Mathf.Sin(p * Mathf.PI);
            Line(at - right * .32f - up * .12f, at + right * .32f + up * .68f, .09f, Tint(ice, charge * .7f), normal);
            for (int i = 0; i < 5; i++)
            {
                float t = (p + i / 5f) % 1f;
                Dot(at + right * Mathf.Sin(i * 2.4f) * .16f + up * t * .7f, .055f, Tint(Color.Lerp(ice, gold, t), charge));
            }
            return;
        }

        if (kind == 5)
        {
            for (int i = 0; i < 6; i++)
            {
                Dot(at + right * (NeiyuSwordCombatVfx.Hash(i + startTick) - .5f) + up * p * .5f,
                    .055f * (1f - p), Tint(gold, fade * .7f));
            }
            return;
        }

        if (kind == 7)
        {
            at += up * .6f;
            for (int i = 0; i < 4; i++)
            {
                float a = (i * .5f + .25f) * Mathf.PI;
                float b = a + Mathf.PI * .5f;
                Line(at + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * .23f,
                    at + (right * Mathf.Cos(b) + up * Mathf.Sin(b)) * .23f, .045f, Tint(red, fade), normal);
            }
            return;
        }

        if (kind != 6)
        {
            float glow = Mathf.Max(0f, 1f - p * 4f);
            Dot(at, .72f, Tint(hot, glow));
        }

        if (kind is 4 or 6)
        {
            float reach = kind == 4 ? 1.25f : .95f;
            Vector3 slash = (right + up * .65f).normalized * reach;
            Line(at - slash, at + slash, .055f * (1f - p) + .015f, Tint(kind == 4 ? red : hot, fade), normal);
            if (kind != 6)
            {
                slash = (right - up * .8f).normalized * reach * .8f;
                Line(at - slash, at + slash, .04f, Tint(gold, fade), normal);
            }
        }

        if (kind is 2 or 4)
        {
            const int segments = 16;
            const float sweep = Mathf.PI * 2f;
            float start = Mathf.Atan2(Vector3.Dot(direction, up), Vector3.Dot(direction, right)) - sweep * .5f;
            float radius = kind == 2 ? Mathf.Lerp(.8f, .12f, p) : Mathf.Lerp(.15f, 1.05f, p);
            for (int i = 0; i < segments; i++)
            {
                float a = start + sweep * i / segments;
                float b = start + sweep * (i + .88f) / segments;
                Line(at + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius,
                    at + (right * Mathf.Cos(b) + up * Mathf.Sin(b)) * radius,
                    kind == 2 ? .045f : .025f, Tint(kind == 2 ? red : gold, fade * .8f), normal);
            }
        }
    }

    private void Line(Vector3 a, Vector3 b, float width, Color color, Vector3 viewNormal)
    {
        if (width <= .001f || color.a <= .001f) return;
        Vector3 dir = b - a;
        float length = dir.magnitude;
        if (length <= .001f) return;
        Vector3 side = Vector3.Cross(viewNormal, dir / length).normalized * (width * .5f);
        int id = ++stripId;
        int i0 = Emit(a, 0f, 1f, color, particleGroup: 0);
        if (i0 >= 0)
        {
            Particles[i0].Side = side;
            SetChannel(i0, stripSlot, id);
        }
        int i1 = Emit(b, 0f, 1f, color, particleGroup: 0);
        if (i1 >= 0)
        {
            Particles[i1].Side = side;
            SetChannel(i1, stripSlot, id);
        }
    }

    private void Dot(Vector3 center, float size, Color color)
    {
        if (size <= .001f || color.a <= .001f) return;
        Emit(center, 0f, size, color, particleGroup: 1);
    }

    private static Color Tint(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha * NeiyuSwordCombatVfx.Intensity);
        return color;
    }
}

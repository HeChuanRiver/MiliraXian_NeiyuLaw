using System;
using System.Collections.Generic;
using HarmonyLib;
using MiliraXian.Characters.Common.Vfx;
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

    private static void Emit(Pawn pawn, Vector3 position, int kind, SoundDef sound, Vector3 direction)
    {
        if (pawn?.Spawned != true) return;
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
                || Find.CameraDriver?.CurrentViewRect.ExpandedBy(3).Contains(position.ToIntVec3()) == false) return;
            if (kind is not (0 or 1))
                pawn.Map.GetComponent<MapComponent_NeiyuSwordVfx>().Add(pawn, position, direction, kind);
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
        }
        catch (Exception exception)
        {
            Log.ErrorOnce($"[霓羽弹刀] 事件特效不可用：{exception}", 197632101);
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
        Vector3 center = cell.ToVector3Shifted();
        SingleSound(pawn, center, bomb.soundExplosion);
        if (!Enabled || Intensity <= 0f) return;
        Safely(() =>
        {
            Map map = pawn.Map;
            if (map == null || !cell.InBounds(map)) return;

            // Use the bomb's visual footprint without starting a damage-dealing Explosion.
            Rand.PushState(unchecked(pawn.thingIDNumber * 397 ^ NeiyuSwordCombat.Now));
            try
            {
                FleckMaker.Static(center, map, FleckDefOf.ExplosionFlash, radius * 6f);
                for (int i = 0; i < 4; i++)
                    FleckMaker.ThrowSmoke(center + Gen.RandomHorizontalVector(radius * .7f), map, radius * .6f);
                foreach (IntVec3 blastCell in bomb.Worker.ExplosionCellsToHit(cell, map, radius))
                {
                    float distance = (blastCell - cell).LengthHorizontal;
                    Color color = Color.Lerp(bomb.explosionColorCenter, bomb.explosionColorEdge, distance / radius);
                    FleckMaker.ThrowExplosionCell(blastCell, map, bomb.explosionCellFleck, color);
                }
            }
            finally { Rand.PopState(); }
            if (map == Find.CurrentMap && Find.Camera != null)
            {
                float distance = Vector3.Distance(center, Find.Camera.transform.position);
                Find.CameraDriver?.shaker.DoShake(4f * radius / Mathf.Max(1f, distance));
            }
        });
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

    internal static void Execution(Pawn executor, IntVec3 target, NeiyuExecutionPhase phase) => Safely(() =>
    {
        NeiyuSwordCombatDef c = NeiyuSwordCombat.Config;
        int kind = phase == NeiyuExecutionPhase.ExecutionStart ? 3 : phase == NeiyuExecutionPhase.ExecutionStrike ? 4 : 5;
        Emit(executor, kind == 3 ? executor.DrawPos : target.ToVector3Shifted(), kind,
            kind == 3 ? c.prepareSound : kind == 4 ? c.strikeSound : c.endSound,
            target.ToVector3Shifted() - executor.DrawPos);
        NeiyuWeaponVisualHooks.Notify(executor, "SwordPosture_" + phase, target, 1.5f,
            (target.ToVector3Shifted() - executor.DrawPos).AngleFlat());
        if (kind == 4 && Enabled)
            Patch_MXNL_MeleeSlashGhost_TryCastShot.SpawnDirectionalFleck(executor.Map,
                target.ToVector3Shifted(), Slash, 2f, 35f);
    });

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
    internal static void Resume(Pawn pawn, IntVec3 target, int start, int dash, int strike, int recover,
        bool dashed, bool committed, bool ended) => Safely(() =>
    {
        if (!Enabled || !pawn.Spawned) return;
        MapComponent_NeiyuSwordVfx visuals = pawn.Map.GetComponent<MapComponent_NeiyuSwordVfx>();
        Vector3 at = target.ToVector3Shifted(), direction = at - pawn.DrawPos;
        visuals.AddAt(pawn, pawn.DrawPos, direction, 3, start, dash);
        if (dashed) visuals.AddAt(pawn, at, direction, 6, start + dash, 15);
        if (committed) visuals.AddAt(pawn, at, direction, 4, start + strike, 18);
        if (ended) visuals.AddAt(pawn, at, direction, 5, start + recover, 25);
    });

    internal static float Hash(int seed)
    {
        uint x = unchecked((uint)seed);
        x ^= x >> 16; x *= 0x7feb352d; x ^= x >> 15; x *= 0x846ca68b; x ^= x >> 16;
        return (x & 0x00ffffff) / 16777215f;
    }
}

// Visual events only. Saved gameplay lives in the shared posture comp and native Job.
// Two snapshots can overlap; this bounded batch uses the existing Waylith mesh path.
public sealed class MapComponent_NeiyuSwordVfx(Map map) : MapComponent(map)
{
    private readonly record struct Flash(Pawn Pawn, Vector3 Position, Vector3 Direction, int Kind, int Tick, int Duration);
    private readonly List<Flash> flashes = new(32);
    private readonly HashSet<JobDriver_NeiyuPostureExecution> volleys = new();
    private ArcaneParticleBatch batch;
    private int geometryTick = -1;
    private Vector3 geometryRight, geometryUp;
    private float geometryIntensity = -1f;

    internal void TrackVolley(JobDriver_NeiyuPostureExecution execution) => volleys.Add(execution);
    internal void UntrackVolley(JobDriver_NeiyuPostureExecution execution) => volleys.Remove(execution);

    internal void Add(Pawn pawn, Vector3 position, Vector3 direction, int kind)
    {
        int duration = kind switch { 2 => 22, 3 => 22, 4 => 18, 5 => 25, 7 => 35, _ => 15 };
        AddAt(pawn, position, direction, kind, NeiyuSwordCombat.Now, duration);
    }

    internal void AddAt(Pawn pawn, Vector3 position, Vector3 direction, int kind, int tick, int duration)
    {
        if (NeiyuSwordCombat.Now - tick >= duration) return;
        if (flashes.Count >= Mathf.Clamp(NeiyuSwordCombat.Config.maxVisuals, 1, 128)) flashes.RemoveAt(0);
        flashes.Add(new Flash(pawn, position, direction, kind, tick, duration));
        geometryTick = -1;
    }

    public override void MapComponentTick()
    {
        int now = NeiyuSwordCombat.Now;
        for (int i = flashes.Count - 1; i >= 0; i--)
            if (now - flashes[i].Tick >= flashes[i].Duration) flashes.RemoveAt(i);
        if (flashes.Count == 0 && batch != null) { batch.Dispose(); batch = null; geometryTick = -1; }
    }

    public override void MapComponentDraw()
    {
        if (Find.CurrentMap != map || !NeiyuSwordCombatVfx.Enabled || (flashes.Count == 0 && volleys.Count == 0)
            || Find.UIRoot.HideMotes || Find.ScreenshotModeHandler.Active || Find.Camera == null) return;
        foreach (JobDriver_NeiyuPostureExecution execution in volleys)
            execution.DrawSingleVolley();
        if (flashes.Count == 0) return;
        batch ??= new ArcaneParticleBatch(MaterialPool.MatFrom(
            "Common/WorldArcaneStrike/neiyu_halo_vfx/SoftGlow", ShaderDatabase.MoteGlow, Color.white));
        int now = NeiyuSwordCombat.Now;
        Vector3 right = Find.Camera.transform.right, up = Find.Camera.transform.up;
        if (geometryTick != now || right != geometryRight || up != geometryUp || geometryIntensity != NeiyuSwordCombatVfx.Intensity)
        {
            batch.Begin();
            // Latest impacts retain the clearest feedback if the shared mesh budget is full.
            for (int i = flashes.Count - 1; i >= 0; i--) Build(flashes[i], now, right, up);
            batch.Upload();
            geometryTick = now; geometryRight = right; geometryUp = up;
            geometryIntensity = NeiyuSwordCombatVfx.Intensity;
        }
        batch.Draw(now / 60f, 0, Find.Camera);
    }

    private void Line(Vector3 a, Vector3 b, float width, Color color)
    {
        Vector3 direction = b - a;
        float length = direction.magnitude;
        if (length <= .001f) return;
        direction /= length;
        Vector3 perpendicular = Vector3.Cross(-Find.Camera.transform.forward, direction).normalized;
        batch.Add((a + b) * .5f, direction, perpendicular, length, width, color);
    }

    private static Color Alpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha * NeiyuSwordCombatVfx.Intensity);
        return color;
    }

    private void Build(Flash f, int now, Vector3 right, Vector3 up)
    {
        if (!Find.CameraDriver.CurrentViewRect.ExpandedBy(3).Contains(f.Position.ToIntVec3())) return;
        float p = Mathf.Clamp01((now - f.Tick) / (float)f.Duration), fade = (1f - p) * (1f - p);
        Vector3 at = f.Position; at.y = AltitudeLayer.MoteOverhead.AltitudeFor();
        Color gold = NeiyuSwordCombatVfx.Gold, hot = NeiyuSwordCombatVfx.Hot;
        Color red = NeiyuSwordCombatVfx.Red, ice = NeiyuSwordCombatVfx.Ice;
        if (f.Kind == 3)
        {
            if (f.Pawn?.jobs?.curDriver is not JobDriver_NeiyuPostureExecution driver || driver.Phase != NeiyuExecutionPhase.ExecutionStart) return;
            at = f.Pawn.DrawPos; at.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            float charge = Mathf.Sin(p * Mathf.PI);
            Line(at - right * .32f - up * .12f, at + right * .32f + up * .68f, .09f, Alpha(ice, charge * .7f));
            for (int i = 0; i < 5; i++)
            {
                float t = (p + i / 5f) % 1f;
                batch.Add(at + right * Mathf.Sin(i * 2.4f) * .16f + up * t * .7f,
                    right, up, .055f, .055f, Alpha(Color.Lerp(ice, gold, t), charge));
            }
            return;
        }
        if (f.Kind == 5)
        {
            for (int i = 0; i < 6; i++)
                batch.Add(at + right * (NeiyuSwordCombatVfx.Hash(i + f.Tick) - .5f) + up * p * .5f,
                    right, up, .055f * (1f - p), .055f * (1f - p), Alpha(gold, fade * .7f));
            return;
        }
        if (f.Kind == 7)
        {
            if (f.Pawn?.Spawned != true || NeiyuSwordCombat.Posture(f.Pawn)?.HasOpening != true) return;
            at = f.Pawn.DrawPos + up * .6f; at.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            for (int i = 0; i < 4; i++)
            {
                float a = (i * .5f + .25f) * Mathf.PI;
                float b = a + Mathf.PI * .5f;
                Line(at + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * .23f,
                    at + (right * Mathf.Cos(b) + up * Mathf.Sin(b)) * .23f, .045f, Alpha(red, fade));
            }
            return;
        }
        if (f.Kind != 6)
        {
            float glow = Mathf.Max(0f, 1f - p * 4f);
            batch.Add(at, right, up, .72f, .72f, Alpha(hot, glow));
        }
        if (f.Kind is 4 or 6)
        {
            float reach = f.Kind == 4 ? 1.25f : .95f;
            Vector3 slash = (right + up * .65f).normalized * reach;
            Line(at - slash, at + slash, .055f * (1f - p) + .015f, Alpha(f.Kind == 4 ? red : hot, fade));
            if (f.Kind != 6)
            {
                slash = (right - up * .8f).normalized * reach * .8f;
                Line(at - slash, at + slash, .04f, Alpha(gold, fade));
            }
        }
        if (f.Kind is 2 or 4)
        {
            const int segments = 16;
            const float sweep = Mathf.PI * 2f;
            float start = Mathf.Atan2(Vector3.Dot(f.Direction, up), Vector3.Dot(f.Direction, right)) - sweep * .5f;
            float radius = f.Kind == 2 ? Mathf.Lerp(.8f, .12f, p) : Mathf.Lerp(.15f, 1.05f, p);
            for (int i = 0; i < segments; i++)
            {
                float a = start + sweep * i / segments, b = start + sweep * (i + .88f) / segments;
                Line(at + (right * Mathf.Cos(a) + up * Mathf.Sin(a)) * radius,
                    at + (right * Mathf.Cos(b) + up * Mathf.Sin(b)) * radius,
                    f.Kind == 2 ? .045f : .025f, Alpha(f.Kind == 2 ? red : gold, fade * .8f));
            }
        }
    }

    public override void MapRemoved()
    {
        volleys.Clear();
        flashes.Clear(); batch?.Dispose(); batch = null;
        base.MapRemoved();
    }
}

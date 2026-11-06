using System;
using System.Collections.Generic;
using MiliraXian.Characters.Common;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.Mingyuan;

public class CompProperties_AbilityMingyuanTimeBurn : CompProperties_AbilityEffect
{
    public int openingTicks = 10;
    public int enchantTicks = 32;
    public int chargeTicks = 21;
    public int swingTicks = 12;
    public int durationTicks = 1200;
    public int tickIntervalTicks = 6;
    public ThingDef projectileDef;
    public ThingDef startMoteDef;
    public ThingDef collapseMoteDef;
    public SoundDef effectSoundDef;
    public float startMoteScale = 2.2f;
    public float collapseMoteScale = 2.4f;

    public CompProperties_AbilityMingyuanTimeBurn() => compClass = typeof(CompAbilityEffect_MingyuanTimeBurn);
}

public class CompAbilityEffect_MingyuanTimeBurn : CompAbilityEffect_MingyuanPowerLimited
{
    internal new CompProperties_AbilityMingyuanTimeBurn Props => (CompProperties_AbilityMingyuanTimeBurn)props;

    public override bool Valid(LocalTargetInfo target, bool throwMessages = false) =>
        base.Valid(target, throwMessages) && MingyuanTimeBurnUtility.IsTarget(target.Pawn, parent.pawn, parent.pawn.Map);

    public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) =>
        base.CanApplyOn(target, dest) && Valid(target);

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        if (!Valid(target) || Props.projectileDef == null || parent.verb is not Verb_MingyuanTimeBurn verb) return;
        Pawn caster = parent.pawn;
        Map map = caster.Map;
        Vector3 origin = caster.Position.ToVector3Shifted();
        Vector3 direction = (target.CenterVector3 - origin).Yto0().normalized;
        if (direction.sqrMagnitude < 0.001f) return;

        base.Apply(target, dest);
        var wave = (Projectile_MingyuanTimeBurnWave)GenSpawn.Spawn(Props.projectileDef, caster.Position, map);
        wave.LaunchWave(caster, target, direction, verb.EffectiveRange);
        Props.effectSoundDef?.PlayOneShot(new TargetInfo(caster.Position, map));
    }
}

public class Verb_MingyuanTimeBurn : Verb_CastAbility
{
    internal CompProperties_AbilityMingyuanTimeBurn Props => Ability?.CompOfType<CompAbilityEffect_MingyuanTimeBurn>()?.Props;
    internal int CastTicks => Props is { } p ? p.openingTicks + p.enchantTicks + p.chargeTicks + p.swingTicks : 1;
    public override float WarmupTime => Mathf.Max(1, CastTicks) / 60f;

    public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false,
        bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
    {
        if (!ValidateTarget(castTarg, false)
            || !base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast))
            return false;

        if (WarmupStance is { } stance)
        {
            stance.ticksLeft = CastTicks;
            MingyuanFireVfx.PlayWarmup(CasterPawn, Ability.def, stance, castTarg.Cell);
        }
        return true;
    }

    protected override bool TryCastShot() => ValidateTarget(currentTarget, false) && base.TryCastShot();
}

internal static class MingyuanTimeBurnUtility
{
    internal static bool CanAge(Pawn pawn) => pawn is { Dead: false, Destroyed: false, ageTracker: not null }
        && pawn.RaceProps.IsFlesh && !pawn.RaceProps.IsMechanoid;

    internal static bool IsTarget(Pawn pawn, Pawn caster, Map map) =>
        map != null && pawn?.Map == map && pawn.Spawned && pawn != caster && CanAge(pawn) && pawn.HostileTo(caster);

    internal static CompProperties_AbilityMingyuanTimeBurn PropsFor(Pawn caster) =>
        caster?.abilities?.GetAbility(MX_MingyuanDefOf.MX_Mingyuan_TimeBurn)?.verb is Verb_MingyuanTimeBurn verb
            ? verb.Props : null;
}

public class Projectile_MingyuanTimeBurnWave : Projectile
{
    internal const float HalfWidth = 1.05f;
    internal const float ArcDepth = 0.52f;
    private const float HitTolerance = 0.28f;
    private List<int> attemptedIds = new(3);
    private int hits;
    private float damageFactor = 1f;
    private Vector3 previous;
    private Vector3 Direction => (destination - origin).Yto0().normalized;
    public override int UpdateRateTicks => 1;
    public override int DamageAmount => Mathf.RoundToInt(base.DamageAmount * damageFactor);

    internal void LaunchWave(Pawn caster, LocalTargetInfo target, Vector3 direction, float range)
    {
        Vector3 start = caster.Position.ToVector3Shifted();
        Vector3 end = start + direction * range;
        Launch(caster, start, end.ToIntVec3(), target, ProjectileHitFlags.NonTargetWorld, preventFriendlyFire: true);
        destination = end;
        lifetime = ticksToImpact = Mathf.Max(1, Mathf.CeilToInt(StartingTicksToImpact));
        damageFactor = MingyuanUtility.GetSelfBurnSkillDamageFactor(caster) * MingyuanUtility.GetOverburnDamageFactor(caster);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref attemptedIds, "attemptedIds", LookMode.Value);
        Scribe_Values.Look(ref hits, "successfulHits");
        Scribe_Values.Look(ref damageFactor, "damageFactor", 1f);
        if (Scribe.mode == LoadSaveMode.PostLoadInit) attemptedIds ??= new(3);
    }

    protected override void TickInterval(int delta)
    {
        if (MingyuanPowerBalance.Sealed || Launcher is not Pawn { Dead: false } caster || caster.Map != Map)
        {
            Destroy();
            return;
        }

        previous = ExactPosition;
        Vector3 next = Vector3.MoveTowards(previous, destination.WithY(previous.y), def.projectile.SpeedTilesPerTick * delta);
        if (!OpenPath(previous.ToIntVec3(), next.ToIntVec3(), Map))
        {
            Destroy();
            return;
        }

        // Vanilla handles world collision and projectile interceptors before the swept arc.
        base.TickInterval(delta);
        if (!Destroyed) Sweep(previous, ExactPosition, caster);
    }

    protected override void ImpactSomething()
    {
        if (Launcher is Pawn caster) Sweep(previous, ExactPosition, caster);
        if (!Destroyed) base.Impact(null);
    }

    private static bool OpenPath(IntVec3 from, IntVec3 to, Map map) => to.InBounds(map)
        && to.CanBeSeenOverFast(map) && GenSight.LineOfSight(from, to, map, skipFirstCell: true);

    private readonly record struct Contact(Pawn Pawn, float Along, bool Intended);

    private void Sweep(Vector3 from, Vector3 to, Pawn caster)
    {
        Map map = Map;
        Vector3 direction = Direction;
        Vector3 side = new(direction.z, 0f, -direction.x);
        float length = (to - from).MagnitudeHorizontal();
        if (length <= 0f) return;
        List<Contact> contacts = CombatTargetSnapshot.Rent(Array.Empty<Contact>());
        try
        {
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(to.ToIntVec3(), map, 3f, true))
            {
                if (thing is not Pawn pawn || !MingyuanTimeBurnUtility.IsTarget(pawn, caster, map)
                    || attemptedIds.Contains(pawn.thingIDNumber)) continue;

                Vector3 offset = pawn.Position.ToVector3Shifted() - from;
                float lateral = Mathf.Abs(Vector3.Dot(offset, side));
                if (lateral > HalfWidth) continue;
                float relativeWidth = lateral / HalfWidth;
                float along = Vector3.Dot(offset, direction) + ArcDepth * relativeWidth * relativeWidth;
                if (along < -HitTolerance || along > length + HitTolerance
                    || !OpenPath((from + direction * Mathf.Clamp(along, 0f, length)).ToIntVec3(), pawn.Position, map))
                    continue;
                contacts.Add(new Contact(pawn, Mathf.Clamp(along, 0f, length), pawn == intendedTarget.Pawn));
            }

            contacts.Sort(static (a, b) =>
            {
                int order = a.Along.CompareTo(b.Along);
                if (order != 0) return order;
                if (a.Intended != b.Intended) return a.Intended ? -1 : 1;
                return a.Pawn.thingIDNumber.CompareTo(b.Pawn.thingIDNumber);
            });

            foreach (Contact contact in contacts)
            {
                Pawn pawn = contact.Pawn;
                if (!MingyuanTimeBurnUtility.IsTarget(pawn, caster, map)) continue;
                attemptedIds.Add(pawn.thingIDNumber);
                DamageInfo hit = new(DamageDef, DamageAmount, ArmorPenetration, direction.AngleFlat(),
                    caster, intendedTarget: intendedTarget.Thing);
                DamageWorker.DamageResult result = MingyuanUtility.ApplyDamageWithoutLifeBurn(pawn, hit);
                if (result != null && !result.deflected)
                {
                    hits++;
                    if (MingyuanTimeBurnUtility.CanAge(pawn))
                        Current.Game.GetComponent<GameComponent_MingyuanTimeBurn>().Register(pawn, caster);
                }
                if (Destroyed) return;
                if (contact.Intended || hits >= 3)
                {
                    Destroy();
                    return;
                }
            }
        }
        finally
        {
            CombatTargetSnapshot.Return(contacts);
        }
    }

    protected override void DrawAt(Vector3 drawLoc, bool flip = false) =>
        MingyuanFireVfx.DrawTimeBurnWave(drawLoc, Direction, DistanceCoveredFraction);
}

public class MingyuanTimeBurnRecord : IExposable
{
    internal const long AgeBudget = 500L * GenDate.TicksPerYear;
    public Pawn pawn;
    public Pawn caster;
    public int startTick;
    public int endTick;
    public int nextAgeTick;
    public int durationTicks;
    public int tickIntervalTicks;
    public long startAgeTicks;
    public HediffDef markerHediff;
    public ThingDef collapseMoteDef;
    public SoundDef effectSoundDef;
    public float collapseMoteScale = 1f;
    public bool reducedCast;
    public long ageBudget;
    public long ageAdded;

    public void ExposeData()
    {
        Scribe_References.Look(ref pawn, "pawn");
        Scribe_References.Look(ref caster, "caster");
        Scribe_Values.Look(ref startTick, "startTick");
        Scribe_Values.Look(ref endTick, "endTick");
        Scribe_Values.Look(ref nextAgeTick, "nextAgeTick");
        Scribe_Values.Look(ref durationTicks, "durationTicks", 2500);
        Scribe_Values.Look(ref tickIntervalTicks, "tickIntervalTicks", 60);
        Scribe_Values.Look(ref startAgeTicks, "startAgeTicks");
        Scribe_Defs.Look(ref markerHediff, "markerHediff");
        Scribe_Defs.Look(ref collapseMoteDef, "collapseMoteDef");
        Scribe_Defs.Look(ref effectSoundDef, "effectSoundDef");
        Scribe_Values.Look(ref collapseMoteScale, "collapseMoteScale", 1f);
        Scribe_Values.Look(ref reducedCast, "power_reducedCast");
        Scribe_Values.Look(ref ageBudget, "ageBudget");
        Scribe_Values.Look(ref ageAdded, "ageAdded");
    }

    internal void RestoreLegacyErasure()
    {
        if (reducedCast || nextAgeTick <= startTick || pawn?.ageTracker == null || pawn.Dead) return;
        int interval = Math.Max(1, tickIntervalTicks);
        int elapsed = nextAgeTick < endTick ? Math.Max(0, nextAgeTick - startTick - interval)
            : Math.Max(0, (endTick - startTick - 1) / interval * interval);
        long ageAfterErasure = (long)Math.Floor(startAgeTicks
            * (Math.Max(0, endTick - startTick - elapsed) / (double)Math.Max(1, durationTicks)));
        pawn.ageTracker.AgeBiologicalTicks += Math.Max(0L, startAgeTicks - ageAfterErasure);
    }
}

public class GameComponent_MingyuanTimeBurn : GameComponent
{
    private List<MingyuanTimeBurnRecord> records = new();
    private int nextProcessTick;

    public GameComponent_MingyuanTimeBurn(Game game) { }

    public bool Cancel(Pawn pawn)
    {
        if (pawn == null) return false;
        bool removed = false;
        for (int i = records.Count - 1; i >= 0; i--)
        {
            MingyuanTimeBurnRecord record = records[i];
            if (record.pawn != pawn) continue;
            records.RemoveAt(i);
            if (pawn.ageTracker != null && MingyuanUtility.HasHediff(pawn, record.markerHediff))
                pawn.ageTracker.AgeBiologicalTicks = Math.Max(0L, pawn.ageTracker.AgeBiologicalTicks - record.ageAdded);
            removed = true;
        }
        RemoveMarker(pawn, MingyuanUtility.TimeBurnFrozenDef);
        return removed;
    }

    public void Register(Pawn pawn, Pawn caster)
    {
        var props = MingyuanTimeBurnUtility.PropsFor(caster);
        if (MingyuanPowerBalance.Sealed || !MingyuanTimeBurnUtility.CanAge(pawn) || props == null || props.durationTicks <= 0) return;
        HediffDef marker = MingyuanUtility.TimeBurnFrozenDef;
        for (int i = records.Count - 1; i >= 0; i--)
        {
            if (records[i].pawn != pawn) continue;
            // Active applications keep their original deadline and budget.
            if (MingyuanUtility.HasHediff(pawn, marker)) return;
            records.RemoveAt(i);
        }
        if (MingyuanUtility.EnsureHediff(pawn, marker) == null) return;
        int tick = Find.TickManager.TicksGame;
        int interval = Mathf.Clamp(props.tickIntervalTicks, 1, props.durationTicks);
        records.Add(new MingyuanTimeBurnRecord
        {
            pawn = pawn, caster = caster, markerHediff = marker, startTick = tick,
            endTick = tick + props.durationTicks, durationTicks = props.durationTicks,
            tickIntervalTicks = interval, nextAgeTick = tick + interval,
            startAgeTicks = pawn.ageTracker.AgeBiologicalTicks, ageBudget = MingyuanTimeBurnRecord.AgeBudget,
            collapseMoteDef = props.collapseMoteDef, collapseMoteScale = props.collapseMoteScale, effectSoundDef = props.effectSoundDef
        });
        nextProcessTick = Math.Min(nextProcessTick, tick + interval);
        MingyuanUtility.TryMakeStaticMote(pawn.PositionHeld, pawn.MapHeld, props.startMoteDef, props.startMoteScale);
    }

    public override void GameComponentTick()
    {
        int now = Find.TickManager.TicksGame;
        if (records.Count == 0 || now < nextProcessTick) return;
        nextProcessTick = int.MaxValue;

        for (int i = records.Count - 1; i >= 0; i--)
        {
            MingyuanTimeBurnRecord record = records[i];
            Pawn pawn = record.pawn;
            if (MingyuanPowerBalance.Sealed || !MingyuanTimeBurnUtility.CanAge(pawn)
                || !MingyuanUtility.HasHediff(pawn, record.markerHediff))
            {
                records.RemoveAt(i);
                RemoveMarker(pawn, record.markerHediff);
                continue;
            }
            if (now >= record.nextAgeTick)
            {
                long scheduled = record.ageBudget * Math.Clamp(now - record.startTick, 0, record.durationTicks) / record.durationTicks;
                long before = pawn.ageTracker.AgeBiologicalTicks;
                long increase = Math.Max(0L, scheduled - record.ageAdded);
                float expectancy = pawn.RaceProps.lifeExpectancy;
                bool finiteLifespan = float.IsFinite(expectancy) && expectancy > 0f
                    && !(pawn.genes?.BiologicalAgeTickFactor <= 0f);
                double lifespan = (double)expectancy * GenDate.TicksPerYear;
                if (finiteLifespan)
                    increase = Math.Min(increase, (long)Math.Clamp(Math.Ceiling(lifespan - before), 0d, increase));

                pawn.ageTracker.AgeBiologicalTicks = before + increase;
                record.ageAdded += increase;
                bool expired = finiteLifespan && pawn.ageTracker.AgeBiologicalTicks >= lifespan;
                if (expired || now >= record.endTick)
                {
                    records.RemoveAt(i);
                    RemoveMarker(pawn, record.markerHediff);
                    if (expired && !pawn.Dead && !pawn.Destroyed)
                    {
                        if (pawn.MapHeld is { } map)
                            record.effectSoundDef?.PlayOneShot(new TargetInfo(pawn.PositionHeld, map));
                        MingyuanUtility.TryMakeStaticMote(pawn.PositionHeld, pawn.MapHeld, record.collapseMoteDef, record.collapseMoteScale);
                        pawn.Kill(new DamageInfo(MX_MingyuanDefOf.MX_Mingyuan_OldAge, 0f, instigator: record.caster));
                    }
                    continue;
                }
                record.nextAgeTick = Math.Min(now + record.tickIntervalTicks, record.endTick);
            }
            nextProcessTick = Math.Min(nextProcessTick, record.nextAgeTick);
        }
    }

    public override void LoadedGame()
    {
        nextProcessTick = int.MaxValue;
        for (int i = records.Count - 1; i >= 0; i--)
        {
            MingyuanTimeBurnRecord record = records[i];
            if (record != null && record.ageBudget > 0 && record.durationTicks > 0)
            {
                nextProcessTick = Math.Min(nextProcessTick, record.nextAgeTick);
                continue;
            }
            records.RemoveAt(i);
            if (record == null) continue;
            record.RestoreLegacyErasure();
            RemoveMarker(record.pawn, record.markerHediff ?? MingyuanUtility.TimeBurnFrozenDef);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref records, "mingyuanTimeBurnRecords", LookMode.Deep);
        Scribe_Values.Look(ref nextProcessTick, "nextProcessTick");
        if (Scribe.mode == LoadSaveMode.PostLoadInit) records ??= new();
    }

    private static void RemoveMarker(Pawn pawn, HediffDef marker)
    {
        if (marker != null && pawn?.health?.hediffSet.GetFirstHediffOfDef(marker) is { } hediff)
            pawn.health.RemoveHediff(hediff);
    }
}

using System.Collections.Generic;
using MiliraXian.Characters.Common.SweepDamage;
using MiliraXian.Characters.Common.Vfx;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan;

public sealed class MingyuanCombustionFireballDef : SweepDamageDef
{
    public float radius;
    public float damage;
    public float lifeBurnFraction;
    public bool leavesScorch;
}

public sealed class SweepDamageWorker_MingyuanCombustion : SweepDamageWorker
{
    public override bool CanTarget(SweepDamageSource source, Thing thing)
    {
        if (thing is not { Spawned: true, Destroyed: false }) return false;
        if (thing is Building) return def.hitBuildings;
        return MingyuanUtility.IsHostilePawn(thing, source.caster, out _);
    }

    protected override DamageWorker.DamageResult ApplyDamage(SweepDamageSource source, Thing target, DamageInfo dinfo)
    {
        DamageWorker.DamageResult result;
        bool previous = MingyuanUtility.SuppressOnHitLifeBurn;
        try
        {
            MingyuanUtility.SuppressOnHitLifeBurn = true;
            result = base.ApplyDamage(source, target, dinfo);
        }
        finally
        {
            MingyuanUtility.SuppressOnHitLifeBurn = previous;
        }

        if (result is { totalDamageDealt: > 0f } && target is Pawn pawn)
        {
            if (!pawn.Dead && def is MingyuanCombustionFireballDef fireball)
                MingyuanUtility.AddLifeBurnFraction(pawn, source.caster, fireball.lifeBurnFraction, scaleWithOverburn: true);

            var entry = new BattleLogEntry_ExplosionImpact(source.caster, pawn, dinfo.Weapon, null, dinfo.Def);
            Find.BattleLog.Add(entry);
            result.AssociateWithLog(entry);
        }
        return result;
    }
}

public sealed class Thing_MingyuanCombustionVolley : Thing
{
    private Pawn caster;
    private AbilityDef ability;
    private CompProperties_AbilityMingyuanInstantCombustion props;
    private List<IntVec3> landings;
    private int startTick;
    private int nextImpact;
    private MingyuanCombustionVfx visual;

    private CompProperties_AbilityMingyuanInstantCombustion Props => props ??=
        caster?.abilities?.GetAbility(ability)?.CompOfType<CompAbilityEffect_MingyuanInstantCombustion>()?.Props;

    private bool CasterCanSustain => !MingyuanPowerBalance.Sealed
        && caster is { Spawned: true, Dead: false, Destroyed: false } && caster.Map == Map;

    internal void Initialize(Pawn source, AbilityDef abilityDef, CompProperties_AbilityMingyuanInstantCombustion settings,
        IntVec3 center)
    {
        caster = source;
        ability = abilityDef;
        props = settings;
        startTick = Find.TickManager.TicksGame;

        Map map = source.Map;
        var candidates = new List<IntVec3>();
        // The small explosions fit inside the same circle as the finisher.
        foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, props.LandingRadius, useCenter: false))
        {
            if (cell.InBounds(map) && !cell.Impassable(map)) candidates.Add(cell);
        }
        if (candidates.Count == 0) candidates.Add(center);

        Rand.PushState(Gen.HashCombineInt(startTick, source.thingIDNumber));
        try
        {
            // Shuffle once and save the result so reloading cannot reroll a fall.
            for (int i = candidates.Count - 1; i > 0; i--)
            {
                int j = Rand.Range(0, i + 1);
                (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
            }
        }
        finally
        {
            Rand.PopState();
        }

        landings = new List<IntVec3>(props.smallFireballCount);
        landings.Add(center);
        int scatterCount = props.smallFireballCount - landings.Count;
        for (int i = 0; i < scatterCount; i++)
            landings.Add(candidates[i % candidates.Count]);
    }

    protected override void Tick()
    {
        var settings = Props;
        if (!CasterCanSustain || settings == null || landings is not { Count: > 0 })
        {
            Destroy();
            return;
        }

        int elapsed = Find.TickManager.TicksGame - startTick;
        while (nextImpact < landings.Count && elapsed >= settings.SmallImpactAt(nextImpact) && CasterCanSustain)
            Explode(landings[nextImpact++], settings.smallFireball);

        int coreImpact = settings.CoreImpactAt(landings.Count);
        if (nextImpact == landings.Count && elapsed >= coreImpact && CasterCanSustain)
        {
            nextImpact++;
            Explode(Position, settings.coreFireball);
        }

        if (elapsed >= coreImpact + Mathf.Max(MingyuanCombustionVfx.Tuning.coreRemnantTicks,
            MingyuanCombustionVfx.Tuning.smallRemnantTicks)) Destroy();
    }

    private void Explode(IntVec3 cell, MingyuanCombustionFireballDef fireball)
    {
        Map map = Map;
        if (fireball == null || !cell.InBounds(map)) return;

        try
        {
            new AreaDamageBurst
            {
                def = fireball,
                caster = caster,
                center = cell.ToVector3Shifted(),
                radius = fireball.radius,
                damage = fireball.damage,
            }.Fire(map);

            if (fireball.leavesScorch && CasterCanSustain)
            {
                CompMingyuanAscendantFlameScorch scorch = null;
                foreach (IntVec3 ground in GenRadial.RadialCellsAround(cell, fireball.radius, useCenter: true))
                {
                    if (!ground.InBounds(map) || ground.Impassable(map)
                        || !GenSight.LineOfSight(cell, ground, map, skipFirstCell: true))
                        continue;

                    scorch ??= Thing_MingyuanAscendantFlameScorch.For(map, ground);
                    scorch.Ignite(ground, caster);
                }
            }
        }
        finally
        {
            DamageWorker_MingyuanCombustionVisual.PlayImpact(cell, map, fireball.radius);
        }
    }

    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        if (Props is { } settings && landings is { Count: > 0 } && CasterCanSustain)
        {
            visual ??= new MingyuanCombustionVfx(Position, landings, settings, caster.thingIDNumber);
            visual.Draw(Map, VisualClock.TicksSince(startTick), nextImpact);
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref caster, "caster");
        Scribe_Defs.Look(ref ability, "ability");
        Scribe_Collections.Look(ref landings, "landings", LookMode.Value);
        Scribe_Values.Look(ref startTick, "startTick");
        Scribe_Values.Look(ref nextImpact, "nextImpact");
    }
}

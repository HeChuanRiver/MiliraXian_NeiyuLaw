using System.Collections.Generic;
using MiliraXian.Characters.Common;
using MiliraXian.Characters.Common.Abilities;
using MiliraXian.Characters.Common.PerkSystem;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan;

internal static class MingyuanEmberEffects
{
    internal static void SpawnGround(Pawn caster, Map map, IntVec3 cell, MingyuanEmberNodeDef node)
    {
        if (caster == null || map == null || !cell.InBounds(map) || MingyuanPowerBalance.Sealed) return;
        // One passive field per caster and node; refreshing replaces that field.
        var fields = map.listerThings.ThingsOfDef(MingyuanEmberDefOf.MX_Mingyuan_EmberGround);
        for (int i = fields.Count - 1; i >= 0; i--)
            if (fields[i] is Thing_MingyuanEmberGround existing && existing.Caster == caster && existing.Node == node)
                existing.Destroy();
        var ground = (Thing_MingyuanEmberGround)ThingMaker.MakeThing(MingyuanEmberDefOf.MX_Mingyuan_EmberGround);
        ground.Init(caster, node);
        GenSpawn.Spawn(ground, cell, map);
        MingyuanFireVfx.PlayBurst(map, cell.ToVector3Shifted(), node.radius, 22);
    }

    internal static List<Thing> OverburnIgnored(Pawn caster, float radius)
    {
        List<Thing> ignored = new() { caster };
        if (!MingyuanEmberBook.Has(caster, EmberEffect.SafeOverburn)) return ignored;
        foreach (Thing thing in GenRadial.RadialDistinctThingsAround(caster.Position, caster.Map, radius, true))
            if ((thing is Pawn && !thing.HostileTo(caster)) || (thing is Building && thing.Faction == caster.Faction))
                ignored.Add(thing);
        return ignored;
    }
}

/// <summary>Load-only aliases are hidden and unusable until the book removes their old instances.</summary>
public sealed class AbilityRequirement_RetiredEmberArt : AbilityRequirement
{
    public override bool Met(Pawn pawn) => false;
    public override string DisabledReason(Pawn pawn) => EmberText.Get("RetiredArt", "此术式已停用。");
}

// The old type name can be present inside a saved VerbTracker. It has no casting implementation.
public sealed class Verb_MingyuanEmberArt : Verb_CastAbility
{
    protected override bool TryCastShot() => false;
}

public sealed class Thing_MingyuanEmberGround : Thing
{
    private MingyuanVisualBinding visualBinding;

    private Pawn caster;
    private MingyuanEmberNodeDef node;
    private int expires, nextPulse;
    public Pawn Caster => caster;
    public MingyuanEmberNodeDef Node => node;
    internal void Init(Pawn source, MingyuanEmberNodeDef definition)
    {
        caster = source; node = definition;
        int now = Find.TickManager.TicksGame;
        expires = now + Mathf.Max(1, node.durationTicks);
        nextPulse = now;
    }
    public override void ExposeData()
    {
        base.ExposeData(); Scribe_References.Look(ref caster, "emberCaster");
        // All node defs live in the base DefDatabase.
        CharacterPerkNodeDef stored = node;
        Scribe_Defs.Look(ref stored, "emberNode"); node = stored as MingyuanEmberNodeDef;
        Scribe_Values.Look(ref expires, "emberExpires"); Scribe_Values.Look(ref nextPulse, "emberNextPulse");
    }
    protected override void Tick()
    {
        int now = Find.TickManager.TicksGame;
        if (caster is not { Dead: false, Destroyed: false } || caster.MapHeld != Map || node == null
            || node.effect != EmberEffect.EmberGround || !MingyuanEmberBook.Has(caster, node.effect) || now >= expires)
        { Destroy(); return; }
        if (now < nextPulse) return;
        nextPulse = now + 60;
        var targets = CombatTargetSnapshot.Rent(System.Array.Empty<Pawn>());
        try
        {
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(Position, Map, node.radius, true))
                if (MingyuanUtility.IsHostilePawn(thing, caster, out Pawn pawn)
                    && GenSight.LineOfSight(Position, pawn.Position, Map)) targets.Add(pawn);
            foreach (Pawn pawn in targets)
                if (!pawn.Dead && pawn.Spawned && pawn.Map == Map)
                    MingyuanUtility.ApplyTrueDamage(pawn, DamageDefOf.Burn, node.value, caster);
        }
        finally { CombatTargetSnapshot.Return(targets); }
    }
    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        var layers = visualBinding.BeginFrame(this, MingyuanVisualDefOf.MX_MY_Fire);
        if (layers == null) return;
        if (node == null || MingyuanPowerBalance.Sealed) return;
        MingyuanFireVfx.DrawWarning(layers, DrawPos, node.radius);
    }
}

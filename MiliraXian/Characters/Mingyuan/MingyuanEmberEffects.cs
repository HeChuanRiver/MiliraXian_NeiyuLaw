using System.Collections.Generic;
using MiliraXian.Characters.Common;
using MiliraXian.Characters.Common.PerkSystem;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan;

internal static class MingyuanEmberEffects
{
    internal static Command_Target ReturnCommand(MingyuanEmberBook book, IntVec3 origin, Map map) =>
        StepCommand(book, true, origin, map);
    internal static Command_Target EchoStepCommand(MingyuanEmberBook book) =>
        StepCommand(book, false, book.Pawn.Position, book.Pawn.Map);

    private static Command_Target StepCommand(MingyuanEmberBook book, bool returning, IntVec3 origin, Map map)
    {
        Pawn pawn = book.Pawn;
        var node = MingyuanEmberBook.Config.Node(returning ? EmberEffect.DashReturn : EmberEffect.ComboReset);
        bool Valid(IntVec3 cell) => pawn is { Spawned: true, Dead: false, Downed: false } && pawn.Map == map
            && !MingyuanPowerBalance.Sealed && cell.InBounds(map) && cell.Standable(map) && !cell.Fogged(map)
            && cell != pawn.Position && pawn.Position.InHorDistOf(cell, node.radius)
            && GenSight.LineOfSight(pawn.Position, cell, map)
            && (!returning || cell.DistanceToSquared(origin) < pawn.Position.DistanceToSquared(origin));
        var targeting = TargetingParameters.ForCell();
        targeting.validator = target => target.Map == map && Valid(target.Cell);
        return new Command_Target
        {
            defaultLabel = returning ? EmberText.Get("Return", "折返") : EmberText.Get("EchoStep", "余烬步"),
            defaultDesc = node.description,
            icon = node.ResolveIcon(true, out _), targetingParams = targeting,
            action = target =>
            {
                if (!Valid(target.Cell) || !(returning ? book.ConsumeReturn() : book.ConsumeEchoStep())) return;
                IntVec3 from = pawn.Position;
                var flyer = PawnFlyer.MakeFlyer(MingyuanEmberDefOf.MX_Mingyuan_EmberStepFlyer, pawn, target.Cell, null, null);
                GenSpawn.Spawn(flyer, from, map);
                MingyuanFireVfx.PlayBurst(map, from.ToVector3Shifted(), .9f, 18);
            }
        };
    }

    internal static float ArtDamageFactor(Pawn pawn)
    {
        float factor = MingyuanUtility.GetSelfBurnSkillDamageFactor(pawn) * MingyuanUtility.GetOverburnDamageFactor(pawn);
        if (MingyuanUtility.IsOverburning(pawn)) factor *= 1f + MingyuanEmberBook.Value(pawn, EmberEffect.OverburnDamage);
        return factor;
    }

    internal static void SpawnGround(Pawn caster, Map map, IntVec3 cell, MingyuanEmberNodeDef node)
    {
        if (caster == null || map == null || !cell.InBounds(map) || MingyuanPowerBalance.Sealed) return;
        // One field/sequence per caster and node. Recasting replaces only that added effect.
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

public sealed class CompProperties_MingyuanEmberArt : CompProperties_AbilityEffect
{
    public CharacterPerkNodeDef node;
    internal MingyuanEmberNodeDef Node => node as MingyuanEmberNodeDef;
    public CompProperties_MingyuanEmberArt() => compClass = typeof(CompAbilityEffect_MingyuanEmberArt);
}

/// <summary>Independent arts unlocked by the tree. None rewrites a base ability's Def or cooldown.</summary>
public sealed class CompAbilityEffect_MingyuanEmberArt : CompAbilityEffect_MingyuanPowerLimited
{
    private new CompProperties_MingyuanEmberArt Props => (CompProperties_MingyuanEmberArt)props;
    public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
    {
        Pawn caster = parent.pawn;
        if (!base.Valid(target, throwMessages) || caster?.Spawned != true || caster.Dead || caster.Downed
            || Props.Node == null || !MingyuanEmberBook.Has(caster, Props.Node.effect)) return false;
        if (Props.Node.effect == EmberEffect.PillarTurn) return true;
        if (!target.Cell.InBounds(caster.Map) || target.Cell.Fogged(caster.Map)
            || !caster.Position.InHorDistOf(target.Cell, parent.def.verbProperties.range)
            || !GenSight.LineOfSight(caster.Position, target.Cell, caster.Map)) return false;
        if (Props.Node.effect is EmberEffect.CombustionMark or EmberEffect.TimeTargets)
            return MingyuanUtility.IsHostilePawn(target.Thing, caster, out _);
        return target.Cell.Standable(caster.Map);
    }
    public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) => Valid(target);
    public override void DrawEffectPreview(LocalTargetInfo target)
    {
        if (Valid(target) && Props.Node.radius > 0) GenDraw.DrawRadiusRing(target.Cell, Props.Node.radius);
    }
    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        if (!Valid(target)) return;
        base.Apply(target, dest);
        Pawn caster = parent.pawn;
        var node = Props.Node;
        if (node.effect == EmberEffect.PillarTurn)
        {
            MingyuanEmberBook.For(caster)?.GrantEmberWind();
            MingyuanFireVfx.PlayBurst(caster.Map, caster.DrawPos, 1.2f, 24);
        }
        else if (node.effect == EmberEffect.CombustionMark)
        {
            MingyuanUtility.AddLifeBurn(target.Pawn, caster, node.value, scaleWithOverburn: true);
            MingyuanFireVfx.PlayBurst(caster.Map, target.Pawn.DrawPos, 1f, 24);
        }
        else if (node.effect == EmberEffect.TimeTargets)
        {
            var victim = target.Pawn;
            MingyuanUtility.ApplyTrueDamage(victim, DamageDefOf.Burn, node.value * MingyuanEmberEffects.ArtDamageFactor(caster), caster);
            if (!victim.Dead) victim.stances?.stunner?.StunFor(node.durationTicks, caster);
            MingyuanFireVfx.PlayBurst(caster.Map, target.Cell.ToVector3Shifted(), 1f, 24);
        }
        else MingyuanEmberEffects.SpawnGround(caster, caster.Map, target.Cell, node);
    }
}

public sealed class Verb_MingyuanEmberArt : Verb_CastAbility
{
    public override float WarmupTime => Mathf.Max(MingyuanEmberBook.Config.Node(EmberEffect.Preparation).threshold, base.WarmupTime *
        (1f - Mathf.Clamp(MingyuanEmberBook.Value(CasterPawn, EmberEffect.Preparation), 0f, .5f)));
}

public sealed class Thing_MingyuanEmberGround : Thing
{
    private Pawn caster;
    private MingyuanEmberNodeDef node;
    private int expires, nextPulse, remainingPulses;
    public Pawn Caster => caster;
    public MingyuanEmberNodeDef Node => node;
    internal void Init(Pawn source, MingyuanEmberNodeDef definition)
    {
        caster = source; node = definition;
        int now = Find.TickManager.TicksGame;
        expires = now + Mathf.Max(1, node.durationTicks);
        nextPulse = now;
        remainingPulses = node.effect == EmberEffect.TwinStars ? 2 : int.MaxValue;
    }
    public override void ExposeData()
    {
        base.ExposeData(); Scribe_References.Look(ref caster, "emberCaster");
        // All node defs live in the base DefDatabase.
        CharacterPerkNodeDef stored = node;
        Scribe_Defs.Look(ref stored, "emberNode"); node = stored as MingyuanEmberNodeDef;
        Scribe_Values.Look(ref expires, "emberExpires"); Scribe_Values.Look(ref nextPulse, "emberNextPulse");
        Scribe_Values.Look(ref remainingPulses, "emberPulses", int.MaxValue);
    }
    protected override void Tick()
    {
        int now = Find.TickManager.TicksGame;
        if (caster is not { Dead: false, Destroyed: false } || caster.MapHeld != Map || node == null
            || !MingyuanEmberBook.Has(caster, node.effect) || now >= expires || remainingPulses <= 0)
        { Destroy(); return; }
        if (now < nextPulse) return;
        nextPulse = now + (node.effect == EmberEffect.TwinStars ? 18 : 60);
        remainingPulses--;
        var targets = CombatTargetSnapshot.Rent(System.Array.Empty<Pawn>());
        try
        {
            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(Position, Map, node.radius, true))
                if (MingyuanUtility.IsHostilePawn(thing, caster, out Pawn pawn)
                    && GenSight.LineOfSight(Position, pawn.Position, Map)) targets.Add(pawn);
            foreach (Pawn pawn in targets)
                if (!pawn.Dead && pawn.Spawned && pawn.Map == Map)
                    MingyuanUtility.ApplyTrueDamage(pawn, DamageDefOf.Burn,
                        node.value * (node.effect == EmberEffect.EmberGround ? 1f : MingyuanEmberEffects.ArtDamageFactor(caster)), caster);
        }
        finally { CombatTargetSnapshot.Return(targets); }
        if (node.effect == EmberEffect.TwinStars) MingyuanFireVfx.PlayBurst(Map, DrawPos, node.radius, 18);
    }
    protected override void DrawAt(Vector3 drawLoc, bool flip = false)
    {
        if (node == null || MingyuanPowerBalance.Sealed) return;
        // Reuse the character's existing native fire renderer; the game tick freezes on pause.
        MingyuanFireVfx.DrawWarning(drawLoc, node.radius);
    }
}

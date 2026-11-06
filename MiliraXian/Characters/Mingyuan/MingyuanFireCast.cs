using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;
using MiliraXian.Characters.Common;

namespace MiliraXian.Characters.Mingyuan;

public class CompProperties_AbilityMingyuanInstantCombustion : CompProperties_AbilityEffect
{
    public float radius = 12f;
    public int jumpTicks = 12;
    public float lifeBurnDoubleCapFraction = 0.5f;
    public int eyeRequiredStunTicks = 180;
    public bool destroyEyesWhenMovingTowardCaster = true;
    public float movementConeDegrees = 90f;
    public int visualFadeTicks = 30;
    public int maxTargetMotes = 16;
    public ThingDef targetMoteDef;
    public SoundDef warmupLoop;
    public SoundDef etchSound;
    public SoundDef sealSound;
    public SoundDef resolveSound;
    public SoundDef hitSound;

    public CompProperties_AbilityMingyuanInstantCombustion() =>
        compClass = typeof(CompAbilityEffect_MingyuanInstantCombustion);
}

public class CompAbilityEffect_MingyuanInstantCombustion : CompAbilityEffect_MingyuanPowerLimited
{
    internal new CompProperties_AbilityMingyuanInstantCombustion Props => (CompProperties_AbilityMingyuanInstantCombustion)props;

    public override void DrawEffectPreview(LocalTargetInfo target)
    {
        if (parent.pawn.Spawned)
            GenDraw.DrawRadiusRing(parent.pawn.Position, Props.radius, new Color(1f, 0.63f, 0.22f, 0.64f));
    }

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        Pawn caster = parent.pawn;
        if (MingyuanPowerBalance.Sealed || !caster.Spawned || caster.Dead) return;
        base.Apply(target, dest);
        Map map = caster.Map;
        MingyuanFireVfx.PlayBurst(map, caster.Position.ToVector3Shifted(), Props.radius, Props.visualFadeTicks, sigil: true);
        Props.sealSound?.PlayOneShot(new TargetInfo(caster.Position, map));
        Props.resolveSound?.PlayOneShot(new TargetInfo(caster.Position, map));
        List<Thing> targets = CombatTargetSnapshot.Rent(GenRadial.RadialDistinctThingsAround(caster.Position, map, Props.radius, true));
        List<BodyPartRecord> eyes = CombatTargetSnapshot.Rent(Array.Empty<BodyPartRecord>());
        int visualCount = 0;
        try
        {
            foreach (Thing thing in targets)
            {
                if (!MingyuanUtility.IsHostilePawn(thing, caster, out Pawn pawn) || !pawn.Spawned || pawn.Map != map)
                    continue;

                eyes.Clear();
                HediffSet hediffs = pawn.health.hediffSet;
                foreach (BodyPartRecord part in hediffs.GetNotMissingParts())
                    if (part != pawn.RaceProps.body.corePart && (part.def == BodyPartDefOf.Eye
                        || (part.def.alive && part.def.tags?.Contains(BodyPartTagDefOf.SightSource) == true))
                        && hediffs.GetPartHealth(part) > 0f)
                        eyes.Add(part);

                bool destroyEyes = eyes.Count > 0 && Props.destroyEyesWhenMovingTowardCaster
                    && IsWalkingToward(pawn, caster.Position, Props.movementConeDegrees);
                MingyuanUtility.DoubleLifeBurn(pawn, caster, Props.lifeBurnDoubleCapFraction);
                if (eyes.Count > 0)
                    pawn.stances?.stunner?.StunFor(Props.eyeRequiredStunTicks, caster, false, true, false);

                if (destroyEyes)
                {
                    foreach (BodyPartRecord part in eyes)
                    {
                        if (pawn.Dead) break;
                        if (!hediffs.PartIsMissing(part))
                            pawn.health.AddHediff(HediffDefOf.MissingBodyPart, part);
                    }
                }

                if (visualCount < Props.maxTargetMotes && pawn.Spawned)
                {
                    MingyuanUtility.TryMakeAttachedMote(pawn, Props.targetMoteDef);
                    if (destroyEyes) MingyuanFireVfx.PlayBurst(map, pawn.DrawPos, 0.65f, 22);
                    if (visualCount++ == 0) Props.hitSound?.PlayOneShot(new TargetInfo(pawn.Position, map));
                }
            }
        }
        finally
        {
            CombatTargetSnapshot.Return(eyes);
            CombatTargetSnapshot.Return(targets);
        }
    }

    private static bool IsWalkingToward(Pawn pawn, IntVec3 casterPosition, float cone)
    {
        Pawn_PathFollower path = pawn.pather;
        if (pawn.Downed || pawn.Flying || pawn.stances.stunner.Stunned || path?.MovingNow != true
            || path.NextCellDoorToWaitForOrManuallyOpen() != null || path.LastMovedTick < Find.TickManager.TicksGame - 1 || !path.nextCell.IsValid)
            return false;

        Vector3 step = (path.nextCell - pawn.Position).ToVector3();
        Vector3 toCaster = (casterPosition - pawn.Position).ToVector3();
        float threshold = Mathf.Cos(Mathf.Clamp(cone, 0f, 180f) * 0.5f * Mathf.Deg2Rad);
        return step.sqrMagnitude > 0f && toCaster.sqrMagnitude > 0f
            && Vector3.Dot(step.normalized, toCaster.normalized) > Mathf.Max(0f, threshold);
    }
}

public class JobDriver_MingyuanCombustion : JobDriver_CastAbility
{
    public override Vector3 ForcedBodyOffset
    {
        get
        {
            if (job.verbToUse is not Verb_MingyuanFireCast { WarmupStance: { } stance } verb) return Vector3.zero;
            int elapsed = verb.CastTicks - stance.ticksLeft;
            return elapsed < verb.JumpTicks
                ? Vector3.forward * (0.65f * Mathf.Sin(Mathf.PI * Mathf.Clamp01(elapsed / (float)verb.JumpTicks)))
                : Vector3.zero;
        }
    }
}

// The stance owns the countdown and release. The saved visual only observes it.
public class Verb_MingyuanFireCast : Verb_CastAbility
{
    internal CompAbilityEffect_MingyuanInstantCombustion Combustion => Ability?.CompOfType<CompAbilityEffect_MingyuanInstantCombustion>();
    internal CompAbilityEffect_MingyuanBurningPillar Pillar => Ability?.CompOfType<CompAbilityEffect_MingyuanBurningPillar>();
    internal int JumpTicks => Mathf.Max(0, Combustion?.Props.jumpTicks ?? 0);
    internal int CastTicks => Mathf.Max(1, verbProps.warmupTime.SecondsToTicks()) + JumpTicks;
    public override float WarmupTime => CastTicks / 60f;

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

    protected override bool TryCastShot() =>
        ValidateTarget(currentTarget, false) && base.TryCastShot();
}

public class CompProperties_AbilityMingyuanBurningPillar : CompProperties_AbilityEffect
{
    public ThingDef projectileDef;
    public ThingDef bowDef;
    public SoundDef warmupLoop;
    public SoundDef launchSound;

    public CompProperties_AbilityMingyuanBurningPillar() => compClass = typeof(CompAbilityEffect_MingyuanBurningPillar);
}

public class CompAbilityEffect_MingyuanBurningPillar : CompAbilityEffect_MingyuanPowerLimited
{
    internal new CompProperties_AbilityMingyuanBurningPillar Props => (CompProperties_AbilityMingyuanBurningPillar)props;
    private bool HasBow => Props.bowDef != null && parent.pawn.equipment?.Primary?.def == Props.bowDef;

    public override bool GizmoDisabled(out string reason)
    {
        if (base.GizmoDisabled(out reason)) return true;
        if (HasBow) return false;
        reason = "需要装备长虹。";
        return true;
    }

    public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
    {
        if (!base.Valid(target, throwMessages) || !HasBow) return false;
        Pawn caster = parent.pawn;
        Map map = caster.Map;
        if (target.Cell == caster.Position || !CanLand(target.Cell, map) || target.Cell.Fogged(map)
            || !caster.Position.InHorDistOf(target.Cell, parent.def.verbProperties.range)) return false;
        return GenSight.LineOfSight(caster.Position, target.Cell, map, skipFirstCell: true,
            validator: cell => !cell.Impassable(map));
    }

    public override void DrawEffectPreview(LocalTargetInfo target)
    {
        if (Valid(target)) MingyuanFireVfx.DrawWarning(target.Cell.ToVector3Shifted(), 1f);
    }

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        if (!Valid(target)) return;
        base.Apply(target, dest);
        Pawn caster = parent.pawn;
        Map map = caster.Map;
        Vector3 origin = caster.Position.ToVector3Shifted();
        Vector3 direction = (target.Cell.ToVector3Shifted() - origin).Yto0().normalized;
        var crystal = (Projectile_MingyuanPillarCrystal)GenSpawn.Spawn(Props.projectileDef, caster.Position, map);
        crystal.Launch(caster, origin + direction * 0.40f, target.Cell, target.Cell,
            ProjectileHitFlags.NonTargetWorld, equipment: caster.equipment.Primary);
        Props.launchSound?.PlayOneShot(new TargetInfo(caster.Position, map));
    }

    internal static bool CanLand(IntVec3 cell, Map map) =>
        CanOccupy(cell, map) && cell.GetFirstBuilding(map) == null;

    internal static bool CanOccupy(IntVec3 cell, Map map)
    {
        if (map == null || !cell.InBounds(map) || !cell.Walkable(map)
            || cell.GetEdifice(map) is Building_Door { Open: false }
            || cell.GetFirstBuilding(map) is Thing_MingyuanBurningPillarField)
            return false;

        // Stone chunks allow passage but fail the vanilla Standable check.
        foreach (Thing thing in cell.GetThingList(map))
            if (thing.def.passability != Traversability.Standable && !IsLooseStoneChunk(thing.def))
                return false;

        return true;
    }

    internal static bool IsLooseStoneChunk(ThingDef def) =>
        def.category == ThingCategory.Item && def.IsWithinCategory(ThingCategoryDefOf.StoneChunks);
}

[HarmonyPatch(typeof(Projectile), "CanHit")]
internal static class Patch_MingyuanPillarCrystal_CanHit
{
    private static void Postfix(Projectile __instance, Thing thing, ref bool __result)
    {
        if (__result && __instance is Projectile_MingyuanPillarCrystal
            && CompAbilityEffect_MingyuanBurningPillar.IsLooseStoneChunk(thing.def))
            __result = false;
    }
}

public class Projectile_MingyuanPillarCrystal : Projectile
{
    public override int UpdateRateTicks => 1;

    protected override void TickInterval(int delta)
    {
        Vector3 before = ExactPosition;
        Vector3 end = destination.WithY(before.y);
        Vector3 after = Vector3.MoveTowards(before, end, def.projectile.SpeedTilesPerTick * delta);
        IntVec3 from = before.ToIntVec3();
        IntVec3 to = after.ToIntVec3();
        // Vanilla still handles shields and projectile interception. An opaque or
        // impassable cell blocks the crystal even at point-blank range.
        if (from != to && (!to.InBounds(Map) || !to.CanBeSeenOverFast(Map) || to.Impassable(Map)
            || !GenSight.LineOfSight(from, to, Map, skipFirstCell: true)))
        {
            Impact(null, blockedByShield: true);
            return;
        }
        base.TickInterval(delta);
    }

    protected override void DrawAt(Vector3 drawLoc, bool flip = false) =>
        MingyuanFireVfx.DrawProjectile(drawLoc, ExactRotation, DistanceCoveredFraction);

    protected override void Impact(Thing hitThing, bool blockedByShield = false)
    {
        Map map = Map;
        if (!blockedByShield && hitThing == null && Position == DestinationCell
            && !MingyuanPowerBalance.Sealed && Launcher is Pawn { Dead: false, Destroyed: false } caster
            && caster.MapHeld == map && CompAbilityEffect_MingyuanBurningPillar.CanLand(Position, map))
        {
            var pillar = (Thing_MingyuanBurningPillarField)ThingMaker.MakeThing(MX_MingyuanDefOf.MX_Mingyuan_BurningPillarField);
            if (caster.Faction != null) pillar.SetFactionDirect(caster.Faction);
            GenSpawn.Spawn(pillar, Position, map);
            pillar.Tornado.Init(caster);
        }
        base.Impact(hitThing, blockedByShield);
    }
}

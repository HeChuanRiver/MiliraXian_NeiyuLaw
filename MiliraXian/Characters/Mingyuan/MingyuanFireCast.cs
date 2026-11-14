using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace MiliraXian.Characters.Mingyuan;

public class CompProperties_AbilityMingyuanInstantCombustion : CompProperties_AbilityEffect
{
    public int jumpTicks = 12;
    public int smallFireballCount = 12;
    public int shotIntervalTicks = 8;
    public int smallFlightTicks = 18;
    public int finisherDelayTicks = 12;
    public int coreFlightTicks = 24;
    public MingyuanCombustionFireballDef smallFireball;
    public MingyuanCombustionFireballDef coreFireball;
    public SoundDef warmupLoop;
    public SoundDef etchSound;
    public SoundDef sealSound;

    public CompProperties_AbilityMingyuanInstantCombustion() =>
        compClass = typeof(CompAbilityEffect_MingyuanInstantCombustion);

    internal float Radius => coreFireball?.radius ?? 0f;
    internal float LandingRadius => Radius - (smallFireball?.radius ?? 0f);

    internal int SmallLaunchAt(int index) => index * shotIntervalTicks;
    internal int SmallImpactAt(int index) => SmallLaunchAt(index) + smallFlightTicks;
    internal int CoreLaunchAt(int count) => SmallImpactAt(count - 1) + finisherDelayTicks;
    internal int CoreImpactAt(int count) => CoreLaunchAt(count) + coreFlightTicks;

    public override System.Collections.Generic.IEnumerable<string> ConfigErrors(AbilityDef parentDef)
    {
        foreach (string error in base.ConfigErrors(parentDef)) yield return error;
        if (smallFireball == null || coreFireball == null)
            yield return "瞬时燃烧必须指定大小火球的爆炸定义。";
        if (!(Radius < GenRadial.MaxRadialPatternRadius && smallFireball?.radius > 0f && LandingRadius > 0f)
            || smallFireballCount < 1 || shotIntervalTicks < 1 || smallFlightTicks < 1
            || finisherDelayTicks < 0 || coreFlightTicks < 1)
            yield return "瞬时燃烧的法阵范围或火球时序无效。";
    }
}

public class CompAbilityEffect_MingyuanInstantCombustion : CompAbilityEffect_MingyuanPowerLimited
{
    internal new CompProperties_AbilityMingyuanInstantCombustion Props => (CompProperties_AbilityMingyuanInstantCombustion)props;

    public override bool Valid(LocalTargetInfo target, bool throwMessages = false) =>
        CanRelease(new LocalTargetInfo(target.Cell))
        && (!target.HasThing || target.Thing.Map == parent.pawn.Map)
        && base.Valid(target, throwMessages);

    internal bool CanRelease(LocalTargetInfo target) => !MingyuanPowerBalance.Sealed
        && parent.pawn is { Spawned: true, Dead: false } caster
        && target.IsValid && !target.HasThing && target.Cell.InBounds(caster.Map);

    public override void DrawEffectPreview(LocalTargetInfo target)
    {
        if (Valid(target) && parent.verb.CanHitTarget(target))
            MingyuanCombustionVfx.DrawSigilPreview(target.Cell, Props.Radius, parent.pawn.thingIDNumber);
    }

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        if (!CanRelease(target)) return;
        base.Apply(target, dest);
        Pawn caster = parent.pawn;
        var volley = (Thing_MingyuanCombustionVolley)ThingMaker.MakeThing(MX_MingyuanDefOf.MX_Mingyuan_CombustionVolley);
        volley.Initialize(caster, parent.def, Props, target.Cell);
        GenSpawn.Spawn(volley, target.Cell, caster.Map);
        Props.sealSound?.PlayOneShot(new TargetInfo(target.Cell, caster.Map));
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
        if (!ValidateTarget(castTarg, false)) return false;
        bool lockCell = Combustion != null;
        LocalTargetInfo castTarget = lockCell ? new LocalTargetInfo(castTarg.Cell) : castTarg;
        if (!base.TryStartCastOn(castTarget, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast))
            return false;

        if (WarmupStance is { } stance)
        {
            stance.ticksLeft = CastTicks;
            // The native casting job watches target A for despawns even during warmup.
            if (lockCell && CasterPawn.CurJob is { } job && job.verbToUse == this && job.targetA == castTarg)
                job.targetA = castTarget;
            MingyuanFireVfx.PlayWarmup(CasterPawn, Ability.def, stance, castTarget.Cell);
        }
        return true;
    }

    protected override bool TryCastShot() =>
        (Combustion?.CanRelease(currentTarget) ?? ValidateTarget(currentTarget, false)) && base.TryCastShot();
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

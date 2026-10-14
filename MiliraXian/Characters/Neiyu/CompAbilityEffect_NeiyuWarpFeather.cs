using System.Collections.Generic;
using RimWorld;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.Neiyu;

public class CompProperties_AbilityNeiyuWarpFeather : CompProperties_AbilityEffect
{
    public ThingDef featherThingDef;
    public ThingDef warmupMoteDef;
    public SoundDef placementSound;
    public IntRange featherCountRange = new(12, 24);

    [MustTranslate]
    public string noPlacementMessage = "身旁没有可放置羽毛的空地。";

    public CompProperties_AbilityNeiyuWarpFeather()
    {
        compClass = typeof(CompAbilityEffect_NeiyuWarpFeather);
    }
}

public class CompAbilityEffect_NeiyuWarpFeather : CompAbilityEffect
{
    private const float PlacementRadius = 2f;
    private Thing preparedFeathers;
    private IntVec3 landingCell = IntVec3.Invalid;
    private int preparedJobId = -1;
    private Mote_NeiyuWarpFeather warmupMote;

    public new CompProperties_AbilityNeiyuWarpFeather Props => (CompProperties_AbilityNeiyuWarpFeather)props;
    internal IntVec3 LandingCell => landingCell;
    internal Thing PreparedFeathers => preparedFeathers;
    internal bool CastActive => !NeiyuPowerBalance.AbilitiesDisabled
        && parent.pawn is { Spawned: true, Dead: false, Downed: false }
        && parent.pawn.CurJob?.ability == parent;

    public override bool GizmoDisabled(out string reason)
    {
        reason = NeiyuPowerBalance.AbilitiesDisabled ? NeiyuPowerBalance.AbilitiesDisabledReason : null;
        return reason != null;
    }

    // Untargeted commands use CanApplyOn, rather than the targeter's Valid hook.
    public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) =>
        base.CanApplyOn(target, dest) && Valid(target, throwMessages: !parent.Casting);

    public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
    {
        if (NeiyuPowerBalance.AbilitiesDisabled)
        {
            if (throwMessages) Reject(NeiyuPowerBalance.AbilitiesDisabledReason);
            return false;
        }
        if (!base.Valid(target, throwMessages)) return false;
        bool available = TryFindLanding(null, Props.featherCountRange.max, IntVec3.Invalid, out _);
        if (!available && throwMessages) Reject(Props.noPlacementMessage);
        return available;
    }

    public override IEnumerable<Mote> CustomWarmupMotes(LocalTargetInfo target)
    {
        int jobId = parent.pawn.CurJob?.loadID ?? -1;
        if (preparedJobId != jobId || preparedFeathers == null)
        {
            ClearPreparation();
            PrepareFeathers();
            preparedJobId = jobId;
        }
        if (preparedFeathers == null || Props.warmupMoteDef == null) yield break;

        warmupMote = MoteMaker.MakeStaticMote(parent.pawn.DrawPos, parent.pawn.Map,
            Props.warmupMoteDef, 1f, makeOffscreen: true) as Mote_NeiyuWarpFeather;
        if (warmupMote == null) yield break;
        warmupMote.Bind(this);
        yield return warmupMote;
    }

    public override void CompTick()
    {
        base.CompTick();
        // Only an in-flight cast owns an unspawned stack. No map or pawn scans.
        if (preparedFeathers != null && !CastActive) ClearPreparation();
    }

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        Pawn caster = parent.pawn;
        if (NeiyuPowerBalance.AbilitiesDisabled || caster is not { Spawned: true, Dead: false, Downed: false })
        {
            ClearPreparation();
            return;
        }
        base.Apply(target, dest);
        if (preparedFeathers == null) PrepareFeathers();
        if (preparedFeathers == null) return;

        Thing feathers = preparedFeathers;
        if (!TryFindLanding(feathers, feathers.stackCount, landingCell, out landingCell))
        {
            Reject(Props.noPlacementMessage);
            ClearPreparation();
            return;
        }

        // A validated direct placement cannot spill into additional cells.
        bool placed = GenPlace.TryPlaceThing(feathers, landingCell, caster.Map, ThingPlaceMode.Direct,
            out Thing resultingThing);
        if (!placed || resultingThing == null)
        {
            ClearPreparation();
            return;
        }

        preparedFeathers = null;
        warmupMote?.Complete(resultingThing);
        warmupMote = null;
        landingCell = IntVec3.Invalid;
        preparedJobId = -1;
        Props.placementSound?.PlayOneShot(new TargetInfo(resultingThing.Position, caster.Map));
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Deep.Look(ref preparedFeathers, "warpFeatherPreparedStack");
        Scribe_Values.Look(ref landingCell, "warpFeatherLandingCell", IntVec3.Invalid);
        Scribe_Values.Look(ref preparedJobId, "warpFeatherJobId", -1);
    }

    internal Thing PreviewThing
    {
        get
        {
            if (preparedFeathers == null || !landingCell.IsValid || parent.pawn.Map == null) return preparedFeathers;
            var things = landingCell.GetThingList(parent.pawn.Map);
            for (int i = 0; i < things.Count; i++)
                if (things[i].def.category == ThingCategory.Item) return things[i];
            return preparedFeathers;
        }
    }

    private void PrepareFeathers()
    {
        if (Props.featherThingDef == null) return;
        preparedFeathers = ThingMaker.MakeThing(Props.featherThingDef);
        preparedFeathers.stackCount = Props.featherCountRange.RandomInRange;
        if (!TryFindLanding(preparedFeathers, preparedFeathers.stackCount, landingCell, out landingCell))
        {
            ClearPreparation();
            return;
        }
        preparedFeathers.Position = landingCell;
        preparedFeathers.Rotation = Props.featherThingDef.defaultPlacingRot;
    }

    private bool TryFindLanding(Thing feathers, int count, IntVec3 preferred, out IntVec3 result)
    {
        result = IntVec3.Invalid;
        Pawn caster = parent.pawn;
        ThingDef featherDef = Props.featherThingDef;
        if (caster is not { Spawned: true } || featherDef == null || count <= 0 || count > featherDef.stackLimit)
            return false;
        if (CanHold(preferred, feathers, count, allowStack: true))
        {
            result = preferred;
            return true;
        }

        int cells = GenRadial.NumCellsInRadius(PlacementRadius);
        for (int pass = 0; pass < 2; pass++)
        {
            // Try the caster's cell last to keep the materialization beside her.
            for (int i = 1; i <= cells; i++)
            {
                IntVec3 cell = caster.Position + GenRadial.RadialPattern[i % cells];
                if (!CanHold(cell, feathers, count, allowStack: pass == 1)) continue;
                result = cell;
                return true;
            }
        }
        return false;
    }

    private bool CanHold(IntVec3 cell, Thing feathers, int count, bool allowStack)
    {
        Pawn caster = parent.pawn;
        Map map = caster.Map;
        if (!cell.IsValid || !cell.InBounds(map) || cell.DistanceToSquared(caster.Position) > PlacementRadius * PlacementRadius
            || !cell.Standable(map) || cell.Fogged(map) || !GenSpawn.CanSpawnAt(Props.featherThingDef, cell, map)
            || !GenSight.LineOfSight(caster.Position, cell, map, skipFirstCell: true)) return false;

        Thing existing = null;
        var things = cell.GetThingList(map);
        for (int i = 0; i < things.Count; i++)
        {
            Thing thing = things[i];
            if (thing.def.preventDroppingThingsOn || thing is Pawn pawn && pawn != caster) return false;
            if (thing.def.category != ThingCategory.Item) continue;
            if (existing != null) return false;
            existing = thing;
        }
        if (existing == null) return true;
        return allowStack && existing.def == Props.featherThingDef
            && existing.stackCount + count <= existing.def.stackLimit
            && (feathers == null || existing.CanStackWith(feathers));
    }

    private void ClearPreparation()
    {
        warmupMote?.Cancel();
        warmupMote = null;
        if (preparedFeathers is { Destroyed: false, Spawned: false }) preparedFeathers.Destroy(DestroyMode.Vanish);
        preparedFeathers = null;
        landingCell = IntVec3.Invalid;
        preparedJobId = -1;
    }

    private static void Reject(string reason) => Messages.Message(reason, MessageTypeDefOf.RejectInput, false);
}

public class Verb_CastAbilityNeiyuWarpFeather : Verb_CastAbility
{
    public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg,
        bool surpriseAttack = false, bool canHitNonTargetPawns = true,
        bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
    {
        if (!ability.CompOfType<CompAbilityEffect_NeiyuWarpFeather>().Valid(castTarg, throwMessages: true)) return false;
        bool started = base.TryStartCastOn(castTarg, destTarg, surpriseAttack,
            canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
        // This non-combat ritual has a fixed duration, independent of aiming gear.
        if (started && WarmupStance is { } stance) stance.ticksLeft = verbProps.warmupTime.SecondsToTicks();
        return started;
    }
}

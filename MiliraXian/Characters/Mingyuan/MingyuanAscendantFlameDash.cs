using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.Mingyuan;

public class CompProperties_AbilityMingyuanAscendantFlameDash : CompProperties_AbilityEffect
{
    public float pathWidth = 3f;
    public float directBurnDamage = 30f;
    public float directBluntDamage = 30f;
    public int directStunTicks = 60;
    public float directLifeBurnDoubleCapFraction = 0.15f;
    public ThingDef flyerDef;
    public int gatherTicks = 9;
    public int ascentTicks = 27;
    public int hiddenTicks = 7;
    public int nearDiveTicks = 15;
    public int farDiveTicks = 42;
    public int landingVisualTicks = 18;
    public int featherLimit = 20;
    public SoundDef ascentSound;
    public SoundDef diveSound;

    public CompProperties_AbilityMingyuanAscendantFlameDash() =>
        compClass = typeof(CompAbilityEffect_MingyuanAscendantFlameDash);
}

public class CompAbilityEffect_MingyuanAscendantFlameDash : CompAbilityEffect_MingyuanPowerLimited
{
    private readonly List<IntVec3> preview = new(160);
    private new CompProperties_AbilityMingyuanAscendantFlameDash Props => (CompProperties_AbilityMingyuanAscendantFlameDash)props;

    public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
    {
        if (!base.Valid(target, throwMessages)) return false;
        Pawn caster = parent.pawn;
        Map map = caster.Map;
        IntVec3 cell = target.Cell;
        if (map != null && cell.InBounds(map) && cell != caster.Position
            && caster.Position.InHorDistOf(cell, parent.def.verbProperties.range) && cell.Standable(map))
            return true;

        if (throwMessages)
            Messages.Message("MX_Mingyuan_AscendantFlameDash_InvalidTarget".Translate(), caster, MessageTypeDefOf.RejectInput, false);
        return false;
    }

    public override void DrawEffectPreview(LocalTargetInfo target)
    {
        if (!Valid(target)) return;
        Pawn caster = parent.pawn;
        preview.Clear();
        foreach (IntVec3 cell in PawnFlyer_MingyuanAscendantFlameDash.PathCells(caster.Map, caster.Position, target.Cell, Props.pathWidth))
            preview.Add(cell);
        GenDraw.DrawFieldEdges(preview, new Color(1f, 0.52f, 0.18f, 0.72f));
    }

    public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
    {
        if (!Valid(target)) return;
        base.Apply(target, dest);
        Pawn caster = parent.pawn;
        Map map = caster.Map;
        IntVec3 origin = caster.Position;
        caster.Rotation = Rot4.FromAngleFlat((target.Cell - origin).AngleFlat);
        Props.ascentSound?.PlayOneShot(new TargetInfo(origin, map));
        bool selected = Find.Selector.IsSelected(caster);
        PawnFlyer_MingyuanAscendantFlameDash flyer = (PawnFlyer_MingyuanAscendantFlameDash)PawnFlyer.MakeFlyer(
            Props.flyerDef, caster, target.Cell, null, parent.def.verbProperties.soundLanding);
        flyer.Init(parent.def, target.Cell);
        GenSpawn.Spawn(flyer, origin, map);
        if (selected) Find.Selector.Select(caster, false, false);
    }
}

public class PawnFlyer_MingyuanAscendantFlameDash : PawnFlyer
{
    private AbilityDef abilityDef;
    private IntVec3 target = IntVec3.Invalid;
    private HashSet<Pawn> hit = new();
    private readonly List<(IntVec3 Cell, float Progress)> path = new(160);
    private readonly List<Pawn> contacts = new();
    private int processed;
    private int diveTicks;

    private CompProperties_AbilityMingyuanAscendantFlameDash Props =>
        abilityDef?.comps.Find(comp => comp is CompProperties_AbilityMingyuanAscendantFlameDash)
            as CompProperties_AbilityMingyuanAscendantFlameDash;
    private int DiveStart => Props.gatherTicks + Props.ascentTicks + Props.hiddenTicks;
    private float DiveProgress => Mathf.Clamp01((ticksFlying - DiveStart) / (float)diveTicks);

    public void Init(AbilityDef ability, IntVec3 destination)
    {
        abilityDef = ability;
        target = destination;
        Vector3 direction = (target.ToVector3Shifted() - startVec).Yto0();
        Rotation = Rot4.FromAngleFlat(direction.AngleFlat());
        diveTicks = Mathf.Max(1, Mathf.RoundToInt(Mathf.Lerp(Props.nearDiveTicks, Props.farDiveTicks,
            direction.magnitude / Mathf.Max(1f, ability.verbProperties.range))));
    }

    public override void SpawnSetup(Map map, bool respawningAfterLoad)
    {
        base.SpawnSetup(map, respawningAfterLoad);
        if (Props == null) return; // A flyer saved with the former vanilla class finishes via the base implementation.
        ticksFlightTime = DiveStart + diveTicks;
        Vector3 origin = startVec.Yto0();
        Vector3 delta = target.ToVector3Shifted().Yto0() - origin;
        foreach (IntVec3 cell in PathCells(map, startVec.ToIntVec3(), target, Props.pathWidth))
        {
            float progress = Mathf.Clamp01(Vector3.Dot(cell.ToVector3Shifted().Yto0() - origin, delta) / delta.sqrMagnitude);
            path.Add((cell, progress));
        }
        path.Sort((left, right) => left.Progress.CompareTo(right.Progress));
        processed = Mathf.Clamp(processed, 0, path.Count);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Defs.Look(ref abilityDef, "dashAbility");
        Scribe_Values.Look(ref target, "dashTarget", IntVec3.Invalid);
        Scribe_Values.Look(ref diveTicks, "dashDiveTicks");
        Scribe_Values.Look(ref processed, "dashProcessed");
        Scribe_Collections.Look(ref hit, "dashHitPawns", LookMode.Reference);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            hit ??= new();
            hit.Remove(null);
        }
    }

    internal static IEnumerable<IntVec3> PathCells(Map map, IntVec3 from, IntVec3 to, float width)
    {
        Vector3 origin = from.ToVector3Shifted().Yto0();
        Vector3 delta = (to - from).ToVector3();
        float length = delta.magnitude;
        if (length <= 0f) yield break;
        Vector3 direction = delta / length;
        float halfWidth = Mathf.Max(0.5f, width * 0.5f);
        int margin = Mathf.CeilToInt(halfWidth);
        for (int z = Mathf.Max(0, Mathf.Min(from.z, to.z) - margin); z <= Mathf.Min(map.Size.z - 1, Mathf.Max(from.z, to.z) + margin); z++)
        for (int x = Mathf.Max(0, Mathf.Min(from.x, to.x) - margin); x <= Mathf.Min(map.Size.x - 1, Mathf.Max(from.x, to.x) + margin); x++)
        {
            IntVec3 cell = new(x, 0, z);
            Vector3 offset = cell.ToVector3Shifted().Yto0() - origin;
            float along = Vector3.Dot(offset, direction);
            if (along >= -0.001f && along <= length + 0.001f
                && Mathf.Abs(offset.x * direction.z - offset.z * direction.x) <= halfWidth + 0.001f)
                yield return cell;
        }
    }

    private Vector3 Ground => Vector3.Lerp(startVec, target.ToVector3Shifted(), DiveProgress).WithY(startVec.y);

    public override Vector3 DrawPos
    {
        get
        {
            if (Props == null) return base.DrawPos;
            float ascent = Mathf.Clamp01((ticksFlying - Props.gatherTicks) / (float)Mathf.Max(1, Props.ascentTicks));
            float height = ticksFlying < DiveStart ? ascent * ascent * 7f : 2.6f * (1f - DiveProgress);
            return Ground + new Vector3(0f, height * 0.008f, height);
        }
    }

    public override void DynamicDrawPhaseAt(DrawPhase phase, Vector3 drawLoc, bool flip = false)
    {
        if (Props == null)
        {
            base.DynamicDrawPhaseAt(phase, drawLoc, flip);
            return;
        }

        bool hidden = ticksFlying >= DiveStart - Props.hiddenTicks && ticksFlying < DiveStart;
        if (!hidden) FlyingThing?.DynamicDrawPhaseAt(phase, drawLoc);
        if (phase == DrawPhase.Draw && !hidden)
            MingyuanFireVfx.DrawDash(Ground, drawLoc, Rotation, (target.ToVector3Shifted() - startVec).Yto0(),
                ticksFlying, DiveStart, DiveProgress, Props.featherLimit);
    }

    protected override void TickInterval(int delta)
    {
        if (Props == null)
        {
            base.TickInterval(delta);
            return;
        }

        if (FlyingThing == null)
        {
            Destroy();
            return;
        }

        if (!MingyuanPowerBalance.Sealed && FlyingPawn is { Dead: false } caster && ticksFlying >= DiveStart)
        {
            if (ticksFlying == DiveStart) Props.diveSound?.PlayOneShot(new TargetInfo(Position, Map));
            Position = Ground.ToIntVec3();
            ResolveContact(caster);
        }

        if (ticksFlying >= ticksFlightTime)
        {
            RespawnPawn();
            Destroy();
            return;
        }

        base.TickInterval(delta);
    }

    protected override void RespawnPawn()
    {
        if (Props != null && FlyingThing != null && !MingyuanPowerBalance.Sealed)
        {
            Vector3 land = DestinationPos;
            MingyuanFireVfx.PlayBurst(Map, land, 2.7f, Props.landingVisualTicks);
        }
        base.RespawnPawn();
    }

    private void ResolveContact(Pawn caster)
    {
        float progress = DiveProgress;
        if (processed >= path.Count || path[processed].Progress > progress) return;
        CompMingyuanAscendantFlameScorch scorch = Thing_MingyuanAscendantFlameScorch.For(Map, Position);
        contacts.Clear();
        while (processed < path.Count && path[processed].Progress <= progress)
        {
            IntVec3 cell = path[processed++].Cell;
            scorch.Ignite(cell, caster);
            foreach (Thing thing in cell.GetThingList(Map))
                if (MingyuanUtility.IsHostilePawn(thing, caster, out Pawn pawn) && hit.Add(pawn))
                    contacts.Add(pawn);
        }

        foreach (Pawn pawn in contacts)
        {
            if (!pawn.Spawned || pawn.Map != Map || pawn.Dead) continue;
            MingyuanUtility.DoubleLifeBurn(pawn, caster, Props.directLifeBurnDoubleCapFraction);
            MingyuanUtility.ApplyTrueDamage(pawn, DamageDefOf.Burn, Props.directBurnDamage, caster, scaleWithSelfBurn: true);
            if (pawn.Dead) continue;
            MingyuanUtility.ApplyTrueDamage(pawn, DamageDefOf.Blunt, Props.directBluntDamage, caster, scaleWithSelfBurn: true);
            if (!pawn.Dead) pawn.stances?.stunner?.StunFor(Props.directStunTicks, caster);
        }
        contacts.Clear();
    }
}

public class Thing_MingyuanAscendantFlameScorch : ThingWithComps
{
    internal static CompMingyuanAscendantFlameScorch For(Map map, IntVec3 cell)
    {
        ThingDef def = MX_MingyuanDefOf.MX_Mingyuan_AscendantFlameScorchController;
        List<Thing> active = map.listerThings.ThingsOfDef(def);
        Thing controller = active.Count > 0 ? active[0] : GenSpawn.Spawn(def, cell, map);
        return controller.TryGetComp<CompMingyuanAscendantFlameScorch>();
    }

    protected override void DrawAt(Vector3 drawLoc, bool flip = false) =>
        GetComp<CompMingyuanAscendantFlameScorch>().DrawScorch();
}

public class CompProperties_MingyuanAscendantFlameScorch : CompProperties
{
    public int durationTicks = 900;
    public int pulseIntervalTicks = 60;
    public float burnDamagePerSecond = 10f;
    public float igniteChancePerSecond = 0.30f;
    public float lifeBurnFractionPerSecond = 0.15f;

    public CompProperties_MingyuanAscendantFlameScorch() => compClass = typeof(CompMingyuanAscendantFlameScorch);
}

public class CompMingyuanAscendantFlameScorch : ThingComp
{
    public class ScorchedCell : IExposable
    {
        public Pawn Caster;
        public int Expires;
        public int NextPulse;

        public void ExposeData()
        {
            Scribe_References.Look(ref Caster, "caster");
            Scribe_Values.Look(ref Expires, "expires");
            Scribe_Values.Look(ref NextPulse, "nextPulse");
        }
    }

    private Dictionary<IntVec3, ScorchedCell> cells = new(160);
    private List<IntVec3> cellKeys;
    private List<ScorchedCell> cellValues;
    private readonly List<IntVec3> expired = new();
    private readonly Dictionary<Pawn, Pawn> victims = new();
    private Dictionary<Pawn, int> lastPulseByPawn = new();
    private List<Pawn> pulseKeys;
    private List<int> pulseValues;
    private readonly List<Pawn> elapsedCooldowns = new();
    private Mesh mesh;
    private bool meshDirty = true;

    private CompProperties_MingyuanAscendantFlameScorch Props => (CompProperties_MingyuanAscendantFlameScorch)props;

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Collections.Look(ref cells, "scorchedCells", LookMode.Value, LookMode.Deep, ref cellKeys, ref cellValues);
        Scribe_Collections.Look(ref lastPulseByPawn, "lastPulseByPawn", LookMode.Reference, LookMode.Value,
            ref pulseKeys, ref pulseValues, logNullErrors: false);
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            cells ??= new();
            lastPulseByPawn ??= new();
        }
        if (Scribe.mode == LoadSaveMode.LoadingVars && Scribe.loader.curXmlParent["scorchedCells"] == null)
        {
            Scribe_Values.Look(ref legacyExpiry, "expireTick");
            Scribe_Collections.Look(ref legacyPath, "pathCells", LookMode.Value);
        }
        if (Scribe.mode != LoadSaveMode.Saving)
            Scribe_References.Look(ref legacyCaster, "caster");
    }

    private int legacyExpiry;
    private Pawn legacyCaster;
    private List<IntVec3> legacyPath;

    public override void PostSpawnSetup(bool respawningAfterLoad)
    {
        base.PostSpawnSetup(respawningAfterLoad);
        if (legacyPath == null) return;
        cells ??= new();
        int firstPulse = Find.TickManager.TicksGame + Mathf.Max(1, Props.pulseIntervalTicks);
        foreach (IntVec3 cell in legacyPath)
            if (cell.InBounds(parent.Map))
                cells[cell] = new ScorchedCell { Caster = legacyCaster, Expires = legacyExpiry, NextPulse = firstPulse };
        legacyPath = null;
        legacyCaster = null;
    }

    internal void Ignite(IntVec3 cell, Pawn caster)
    {
        int now = Find.TickManager.TicksGame;
        if (!cells.TryGetValue(cell, out ScorchedCell data))
        {
            data = new ScorchedCell { NextPulse = now + Mathf.Max(1, Props.pulseIntervalTicks) };
            cells.Add(cell, data);
        }
        data.Caster = caster;
        data.Expires = now + Mathf.Max(1, Props.durationTicks);
        meshDirty = true;
    }

    public override void CompTick()
    {
        Map map = parent.Map;
        if (map == null) return;
        if (MingyuanPowerBalance.Sealed)
        {
            parent.Destroy();
            return;
        }

        List<Thing> active = map.listerThings.ThingsOfDef(parent.def);
        if (active.Count > 1)
        {
            // Merge old saves before any former controller can pulse on its own.
            CompMingyuanAscendantFlameScorch first = active[0].TryGetComp<CompMingyuanAscendantFlameScorch>();
            if (first != this) return;
            while (active.Count > 1)
            {
                Thing duplicate = active[^1];
                var other = duplicate.TryGetComp<CompMingyuanAscendantFlameScorch>();
                foreach (var (cell, data) in other.cells)
                    if (!cells.TryGetValue(cell, out ScorchedCell existing) || existing.Expires < data.Expires)
                        cells[cell] = data;
                duplicate.Destroy();
            }
            meshDirty = true;
        }

        int now = Find.TickManager.TicksGame;
        int interval = Mathf.Max(1, Props.pulseIntervalTicks);
        expired.Clear();
        foreach (var (cell, data) in cells)
        {
            Pawn caster = data.Caster;
            if (data.Expires < now || caster == null || caster.Dead || caster.Destroyed || caster.MapHeld != map)
            {
                expired.Add(cell);
                continue;
            }

            if (now >= data.NextPulse)
            {
                data.NextPulse = now + interval;
                foreach (Thing thing in cell.GetThingList(map))
                    if (MingyuanUtility.IsHostilePawn(thing, caster, out Pawn pawn)
                        && (!lastPulseByPawn.TryGetValue(pawn, out int previous) || now - previous >= interval))
                        victims.TryAdd(pawn, caster);
            }

            if (data.Expires == now) expired.Add(cell);
        }

        foreach (IntVec3 cell in expired) cells.Remove(cell);
        meshDirty |= expired.Count > 0;
        if (victims.Count > 0) Pulse(map, now, interval);
        if (lastPulseByPawn.Count > 0 && this.parent.IsHashIntervalTick(interval))
        {
            elapsedCooldowns.Clear();
            foreach (var (pawn, tick) in lastPulseByPawn)
                if (now - tick >= interval || pawn.Dead || pawn.Destroyed) elapsedCooldowns.Add(pawn);
            foreach (Pawn pawn in elapsedCooldowns) lastPulseByPawn.Remove(pawn);
        }
        if (cells.Count == 0 && lastPulseByPawn.Count == 0) parent.Destroy();
    }

    private void Pulse(Map map, int now, int interval)
    {
        float seconds = interval / 60f;
        float igniteChance = 1f - Mathf.Pow(1f - Mathf.Clamp01(Props.igniteChancePerSecond), seconds);
        foreach (var (pawn, caster) in victims)
        {
            if (pawn.Dead || !pawn.Spawned || pawn.Map != map || caster.Dead || caster.MapHeld != map) continue;
            lastPulseByPawn[pawn] = now;
            MingyuanUtility.ApplyTrueDamage(pawn, DamageDefOf.Burn, Props.burnDamagePerSecond * seconds, caster, scaleWithSelfBurn: true);
            if (pawn.Dead || !pawn.Spawned || pawn.Map != map) continue;
            MingyuanUtility.AddLifeBurnFraction(pawn, caster, Props.lifeBurnFractionPerSecond * seconds);
            if (pawn.CanEverAttachFire() && pawn.GetStatValue(StatDefOf.Flammability) > 0f && Rand.Chance(igniteChance))
                pawn.TryAttachFire(0.15f, caster);
        }
        victims.Clear();
    }

    internal void DrawScorch()
    {
        if (cells.Count == 0)
        {
            if (mesh != null) Object.Destroy(mesh);
            mesh = null;
            meshDirty = true;
            return;
        }
        if (meshDirty)
        {
            mesh ??= new Mesh { name = "Mingyuan scorch" };
            MingyuanFireVfx.BuildScorch(mesh, cells);
            meshDirty = false;
        }
        MingyuanFireVfx.DrawScorch(mesh, Props.durationTicks);
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        if (mesh != null) Object.Destroy(mesh);
        mesh = null;
        meshDirty = true;
    }
}

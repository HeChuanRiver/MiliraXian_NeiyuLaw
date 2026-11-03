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

public class Thing_MingyuanBurningPillarField : Building, IAttackTarget, IAttackTargetSearcher
{
    internal CompMingyuanBurningPillarTornado Tornado => GetComp<CompMingyuanBurningPillarTornado>();
    Thing IAttackTarget.Thing => this;
    Thing IAttackTargetSearcher.Thing => this;
    public LocalTargetInfo TargetCurrentlyAimingAt => LocalTargetInfo.Invalid;
    public Verb CurrentEffectiveVerb => null;
    public LocalTargetInfo LastAttackedTarget => LocalTargetInfo.Invalid;
    public int LastAttackTargetTick => 0;
    public float TargetPriorityFactor => Tornado.Props.targetPriorityFactor;
    public bool ThreatDisabled(IAttackTargetSearcher disabledFor) => !Tornado.Active;
    public override Vector3 DrawPos => Tornado?.Center ?? base.DrawPos;

    protected override void DrawAt(Vector3 drawLoc, bool flip = false) => Tornado.DrawTornado(drawLoc);
}

public class CompProperties_MingyuanBurningPillarTornado : CompProperties
{
    public float startRadius = 1f;
    public float maxRadius = 10f;
    public int durationTicks = 2400;
    public int radiusGrowTicks = 600;
    public int controlUnlockTicks = 600;
    public int controlGestureTicks = 22;
    public int initialHitPoints = 300;
    public float targetPriorityFactor = 0.65f;
    public float buildingMaxHealthDamagePerSecond = 0.30f;
    public float smallPawnBodySizeThreshold = 1.2f;
    public float pawnLifeDamageFractionPerSecond = 0.15f;
    public int killBonusDurationTicks = 300;
    public bool obeyMovementBlockers = true;
    public float tornadoMoveSpeedCellsPerSecond = 4f;
    public float tornadoPullSpeedCellsPerSecond = 2.5f;
    public float lifeBurnFractionPerSecond = 0.05f;
    public float furEquipmentDurabilityLossFractionPerSecond = 0.20f;
    public int visualFadeTicks = 36;
    public int particleLimit = 28;
    public SoundDef impactSound;
    public SoundDef snapSound;
    public SoundDef loopSound;

    public CompProperties_MingyuanBurningPillarTornado() => compClass = typeof(CompMingyuanBurningPillarTornado);
}

public class CompMingyuanBurningPillarTornado : ThingComp
{
    public class Contact : IExposable
    {
        public int DamageTicks;
        public float StructuralRemainder;
        public Vector3 PullOffset;
        public int Entered;

        public void ExposeData()
        {
            Scribe_Values.Look(ref DamageTicks, "damageTicks");
            Scribe_Values.Look(ref StructuralRemainder, "structuralRemainder");
            Scribe_Values.Look(ref PullOffset, "pullOffset");
            Scribe_Values.Look(ref Entered, "entered");
        }
    }

    private const int ContactInterval = 6;
    private Pawn caster;
    private int spawnTick;
    private int expireTick;
    private int lastContactTick;
    private int grantedCoreHitPoints;
    private int fadeStart = -1;
    private Vector3 center;
    private bool centerInitialized;
    private IntVec3 destinationCell = IntVec3.Invalid;
    private Dictionary<Thing, Contact> contacts = new();
    private List<Thing> contactKeys;
    private List<Contact> contactValues;
    private readonly HashSet<Thing> seen = new();
    private readonly List<Thing> leaving = new();
    private Sustainer sustainer;

    internal CompProperties_MingyuanBurningPillarTornado Props => (CompProperties_MingyuanBurningPillarTornado)props;
    internal Vector3 Center => centerInitialized ? center : parent.Position.ToVector3ShiftedWithAltitude(parent.def.Altitude);
    private int Age => Mathf.Max(0, Find.TickManager.TicksGame - spawnTick);
    private float Growth => Mathf.Clamp01(Age / (float)Mathf.Max(1, Props.radiusGrowTicks));
    private float Radius => Mathf.SmoothStep(Props.startRadius, Props.maxRadius, Growth);
    internal bool Active => parent.Spawned && !MingyuanPowerBalance.Sealed && caster is { Dead: false, Destroyed: false }
        && caster.MapHeld == parent.Map && fadeStart < 0 && Find.TickManager.TicksGame <= expireTick;
    internal bool ControlsUnlocked => Active && caster.Spawned && !caster.Downed
        && Find.TickManager.TicksGame < expireTick
        && Age >= Mathf.Max(Props.radiusGrowTicks, Props.controlUnlockTicks);
    private int CoreCapacity => Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(Props.initialHitPoints, parent.MaxHitPoints, Growth)), 1, parent.MaxHitPoints);
    internal int GestureTicks => Mathf.Max(1, Props.controlGestureTicks);

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_References.Look(ref caster, "caster");
        Scribe_Values.Look(ref spawnTick, "spawnTick");
        Scribe_Values.Look(ref expireTick, "expireTick");
        Scribe_Values.Look(ref grantedCoreHitPoints, "grantedCoreHitPoints");
        Scribe_Values.Look(ref destinationCell, "destinationCell", IntVec3.Invalid);
        Scribe_Values.Look(ref lastContactTick, "lastContactTick");
        Scribe_Values.Look(ref fadeStart, "fadeStart", -1);
        Scribe_Values.Look(ref center, "center");
        Scribe_Values.Look(ref centerInitialized, "centerInitialized");
        Scribe_Collections.Look(ref contacts, "contacts", LookMode.Reference, LookMode.Deep, ref contactKeys, ref contactValues, logNullErrors: false);
        if (Scribe.mode == LoadSaveMode.PostLoadInit) contacts ??= new();
    }

    internal void Init(Pawn source)
    {
        caster = source;
        spawnTick = Find.TickManager.TicksGame;
        expireTick = spawnTick + Mathf.Max(1, Props.durationTicks);
        lastContactTick = spawnTick;
        grantedCoreHitPoints = Mathf.Clamp(Props.initialHitPoints, 1, parent.MaxHitPoints);
        parent.HitPoints = grantedCoreHitPoints;
        Props.impactSound?.PlayOneShot(new TargetInfo(parent.Position, parent.Map));
    }

    public override void CompTick()
    {
        if (!parent.Spawned) return;
        int now = Find.TickManager.TicksGame;
        if (!Active)
        {
            if (fadeStart < 0)
            {
                fadeStart = now;
                Stop();
                contacts.Clear();
            }
            if (now - fadeStart >= Props.visualFadeTicks) parent.Destroy();
            else MaintainSound(LoopVolume * (1f - (now - fadeStart) / (float)Mathf.Max(1, Props.visualFadeTicks)));
            return;
        }

        int capacity = CoreCapacity;
        if (capacity > grantedCoreHitPoints)
        {
            parent.HitPoints = Mathf.Min(parent.MaxHitPoints, parent.HitPoints + capacity - grantedCoreHitPoints);
            grantedCoreHitPoints = capacity;
        }

        if (destinationCell.IsValid && now < expireTick) MoveCore();
        if (lastContactTick == 0) lastContactTick = now;
        int elapsed = now - lastContactTick;
        if (elapsed >= ContactInterval)
        {
            lastContactTick = now;
            ResolveContacts(now, Mathf.Min(elapsed, ContactInterval));
        }
        if (now == expireTick)
        {
            foreach (var (thing, contact) in contacts)
                if (contact.DamageTicks > 0 && thing is Building && thing.Spawned && thing.Map == parent.Map
                    && thing.Faction != Faction.OfPlayer)
                    AffectBuilding(thing, contact, contact.DamageTicks);
        }
        if (parent.Spawned) MaintainSound(LoopVolume);
    }

    private void MoveCore()
    {
        if (Age < Props.controlUnlockTicks) return;
        Vector3 current = Center;
        Vector3 end = destinationCell.ToVector3ShiftedWithAltitude(current.y);
        float remaining = (end - current).magnitude;
        float distance = Mathf.Min(remaining, Mathf.Max(0f, Props.tornadoMoveSpeedCellsPerSecond) / 60f);
        // Substeps keep all crossed cells observable, even with a modded move speed.
        int count = Mathf.Max(1, Mathf.CeilToInt(distance * 4f));
        for (int i = 1; i <= count; i++)
        {
            Vector3 next = Vector3.MoveTowards(current, end, distance * i / count);
            IntVec3 nextCell = next.ToIntVec3();
            if (!nextCell.InBounds(parent.Map) || (Props.obeyMovementBlockers
                && !CanStep(parent.Position, nextCell, parent.Map)))
            {
                Stop();
                return;
            }
            center = next;
            centerInitialized = true;
            if (parent.Position != nextCell) parent.Position = nextCell;
        }
        if (distance >= remaining) Stop();
    }

    private static bool CanStep(IntVec3 from, IntVec3 to, Map map)
    {
        if (from == to) return true;
        if (!CompAbilityEffect_MingyuanBurningPillar.CanOccupy(to, map)) return false;
        if (from.x == to.x || from.z == to.z) return true;
        return CompAbilityEffect_MingyuanBurningPillar.CanOccupy(new IntVec3(from.x, 0, to.z), map)
            && CompAbilityEffect_MingyuanBurningPillar.CanOccupy(new IntVec3(to.x, 0, from.z), map);
    }

    private void ResolveContacts(int now, int elapsed)
    {
        Map map = parent.Map;
        Vector3 point = Center.Yto0();
        float radius = Radius;
        float radiusSquared = radius * radius;
        // Scan a padded radial grid around the indexed cell. Test against the interpolated center.
        List<Thing> targets = CombatTargetSnapshot.Rent(
            GenRadial.RadialDistinctThingsAround(parent.Position, map, radius + 0.72f, true));
        try
        {
            foreach (Thing thing in targets)
            {
                if (!parent.Spawned || MingyuanPowerBalance.Sealed) break;
                if (thing == parent || thing is Thing_MingyuanBurningPillarField || !thing.Spawned || thing.Map != map
                    || !InContact(thing, point, radiusSquared)) continue;

                if (MingyuanUtility.IsHostilePawn(thing, caster, out Pawn pawn))
                {
                    Contact contact = Track(pawn, now);
                    if (pawn.BodySize < Props.smallPawnBodySizeThreshold && !pawn.Downed && pawn.pather != null)
                        Pull(pawn, contact, point, elapsed);
                    if (AdvancePulse(contact, elapsed)) AffectPawn(pawn);
                }
                else if (thing is Building && thing.def.destroyable && thing.def.useHitPoints && thing.Faction != Faction.OfPlayer)
                {
                    Contact contact = Track(thing, now);
                    if (AdvancePulse(contact, elapsed)) AffectBuilding(thing, contact, 60);
                }
                else if (IsLeatherGear(thing))
                {
                    if (AdvancePulse(Track(thing, now), elapsed)) WearGear(thing);
                }
            }
        }
        finally
        {
            CombatTargetSnapshot.Return(targets);
            leaving.Clear();
            foreach (var entry in contacts)
                if (entry.Key.Destroyed || !entry.Key.Spawned || entry.Key is Pawn { Dead: true } || !seen.Contains(entry.Key))
                    leaving.Add(entry.Key);
            foreach (Thing thing in leaving)
            {
                Contact contact = contacts[thing];
                contacts.Remove(thing);
                if (contact.DamageTicks > 0 && thing is Building && thing.Spawned && thing.Map == map
                    && thing.Faction != Faction.OfPlayer && Active)
                    AffectBuilding(thing, contact, contact.DamageTicks);
            }
            seen.Clear();
        }
    }

    private static bool InContact(Thing thing, Vector3 center, float radiusSquared)
    {
        if (thing.def.size == IntVec2.One)
            return (thing.Position.ToVector3Shifted().Yto0() - center).sqrMagnitude <= radiusSquared;
        CellRect rect = thing.OccupiedRect();
        float x = Mathf.Clamp(center.x, rect.minX + 0.5f, rect.maxX + 0.5f);
        float z = Mathf.Clamp(center.z, rect.minZ + 0.5f, rect.maxZ + 0.5f);
        return (new Vector3(x, 0f, z) - center).sqrMagnitude <= radiusSquared;
    }

    private Contact Track(Thing thing, int now)
    {
        seen.Add(thing);
        if (!contacts.TryGetValue(thing, out Contact contact))
            contacts.Add(thing, contact = new Contact { Entered = now });
        return contact;
    }

    private static bool AdvancePulse(Contact contact, int elapsed)
    {
        contact.DamageTicks += elapsed;
        if (contact.DamageTicks < 60) return false;
        contact.DamageTicks -= 60;
        return true;
    }

    private void AffectPawn(Pawn pawn)
    {
        for (int i = (pawn.apparel?.WornApparelCount ?? 0) - 1; i >= 0; i--)
        {
            if (i >= pawn.apparel.WornApparelCount) continue;
            Apparel gear = pawn.apparel.WornApparel[i];
            if (IsLeatherGear(gear)) WearGear(gear);
        }
        List<ThingWithComps> weapons = pawn.equipment?.AllEquipmentListForReading;
        for (int i = (weapons?.Count ?? 0) - 1; i >= 0; i--)
        {
            if (i < weapons.Count && IsLeatherGear(weapons[i])) WearGear(weapons[i]);
        }

        if (pawn.Dead) return;
        MingyuanUtility.AddLifeBurnFraction(pawn, caster, Props.lifeBurnFractionPerSecond, scaleWithOverburn: true);
        float amount = pawn.health.LethalDamageThreshold * Props.pawnLifeDamageFractionPerSecond;
        if (MingyuanUtility.ApplyPillarDamage(pawn, caster, amount))
            expireTick += Mathf.Max(0, Props.killBonusDurationTicks);
    }

    private void AffectBuilding(Thing thing, Contact contact, int ticks)
    {
        float accrued = thing.MaxHitPoints * Props.buildingMaxHealthDamagePerSecond * (ticks / 60f) + contact.StructuralRemainder;
        int damage = Mathf.FloorToInt(accrued);
        contact.StructuralRemainder = accrued - damage;
        if (damage <= 0) return;

        Thing previous = Patch_MingyuanPillarWoodLeavings.Target;
        try
        {
            if (HasStuffCategory(thing, StuffCategoryDefOf.Woody)) Patch_MingyuanPillarWoodLeavings.Target = thing;
            MingyuanUtility.ApplyTrueDamage(thing, MX_MingyuanDefOf.MX_Mingyuan_StructuralBurn, damage, caster);
        }
        finally { Patch_MingyuanPillarWoodLeavings.Target = previous; }
    }

    private static bool HasStuffCategory(Thing thing, StuffCategoryDef category) =>
        (thing.Stuff ?? thing.def).stuffProps?.categories?.Contains(category) == true;

    private static bool IsLeatherGear(Thing thing) => thing.def.useHitPoints
        && (thing.def.IsApparel || thing.def.IsWeapon) && HasStuffCategory(thing, StuffCategoryDefOf.Leathery);

    private void WearGear(Thing thing)
    {
        int damage = GenMath.RoundRandom(thing.MaxHitPoints * Props.furEquipmentDurabilityLossFractionPerSecond);
        if (damage > 0)
            MingyuanUtility.ApplyTrueDamage(thing, MX_MingyuanDefOf.MX_Mingyuan_StructuralBurn, damage, caster);
    }

    private void Pull(Pawn pawn, Contact contact, Vector3 point, int elapsed)
    {
        // Preserve the active job and path destination when moving the pawn across a cell edge.
        Vector3 inward = point - pawn.Position.ToVector3Shifted().Yto0();
        float distance = inward.magnitude;
        Vector3 tangent = new Vector3(inward.z, 0f, -inward.x);
        Vector3 direction = distance > 1.4f ? inward + 0.35f * tangent
            : tangent + inward * (distance > 0.9f ? 0.55f : -0.70f);
        float lift = Mathf.Clamp01((Find.TickManager.TicksGame - contact.Entered) / 24f);
        contact.PullOffset += direction.normalized * (lift * Props.tornadoPullSpeedCellsPerSecond * elapsed / 60f);
        IntVec3 next = (pawn.Position.ToVector3Shifted() + contact.PullOffset).ToIntVec3();
        if (next == pawn.Position) return;
        if (!CanStep(pawn.Position, next, pawn.Map) || PawnUtility.PawnBlockingPathAt(next, pawn) != null)
        {
            contact.PullOffset = Vector3.zero;
            return;
        }
        contact.PullOffset -= (next - pawn.Position).ToVector3();
        pawn.Position = next;
        pawn.pather.ResetToCurrentPosition();
    }

    internal static Vector3 PullDrawOffset(Pawn pawn)
    {
        if (pawn.Downed || MingyuanPowerBalance.Sealed) return Vector3.zero;
        List<Thing> pillars = pawn.Map.listerThings.ThingsOfDef(MX_MingyuanDefOf.MX_Mingyuan_BurningPillarField);
        foreach (Thing thing in pillars)
        {
            var tornado = ((Thing_MingyuanBurningPillarField)thing).Tornado;
            if (pawn.BodySize >= tornado.Props.smallPawnBodySizeThreshold
                || !tornado.contacts.TryGetValue(pawn, out Contact contact)) continue;
            float time = (Find.TickManager.TicksGame - contact.Entered) / 60f;
            float lift = Mathf.Clamp01(time * 2.5f);
            return new Vector3(Mathf.Sin(time * 8f) * 0.12f, 0.035f, 0.34f + Mathf.Cos(time * 8f) * 0.13f) * lift;
        }
        return Vector3.zero;
    }

    private float LoopVolume => Mathf.Lerp(0.20f, 0.80f, Growth);

    private void MaintainSound(float volume)
    {
        if (Props.loopSound == null || parent.Map != Find.CurrentMap) return;
        if (sustainer == null || sustainer.Ended)
            sustainer = Props.loopSound.TrySpawnSustainer(SoundInfo.InMap(parent, MaintenanceType.PerTick));
        if (sustainer == null) return;
        sustainer.externalParams["PillarStrength"] = Mathf.Max(0f, volume);
        sustainer.Maintain();
    }

    public override void PostDeSpawn(Map map, DestroyMode mode = DestroyMode.Vanish)
    {
        base.PostDeSpawn(map, mode);
        sustainer?.End();
        sustainer = null;
    }

    internal void DrawTornado(Vector3 drawLoc)
    {
        float fade = fadeStart < 0 ? 1f : Mathf.Clamp01(1f - (Find.TickManager.TicksGame - fadeStart) / (float)Mathf.Max(1, Props.visualFadeTicks));
        MingyuanFireVfx.DrawTornado(drawLoc, Radius, Growth, Age, fade, Props.particleLimit);
        if (caster is { Spawned: true } && caster.Map == parent.Map && caster.CurJob?.targetA.Thing == parent
            && caster.jobs.curDriver is JobDriver_MingyuanControlPillar gesture)
            MingyuanFireVfx.DrawGesture(caster.DrawPos, caster.Rotation, gesture.GestureProgress);
    }

    public override string CompInspectStringExtra()
    {
        string control = !ControlsUnlocked ? "MX_Mingyuan_BurningPillar_ControlLocked".Translate()
            : destinationCell.IsValid ? "MX_Mingyuan_BurningPillar_ControlMoving".Translate(destinationCell.ToString())
            : "MX_Mingyuan_BurningPillar_ControlStopped".Translate();
        return "MX_Mingyuan_BurningPillar_Inspect".Translate(Radius.ToString("F1"), parent.HitPoints,
            CoreCapacity, Mathf.CeilToInt(Mathf.Max(0, expireTick - Find.TickManager.TicksGame) / 60f),
            control, (Props.buildingMaxHealthDamagePerSecond * 100f).ToString("0.##"));
    }

    public override IEnumerable<Gizmo> CompGetGizmosExtra()
    {
        if (!ControlsUnlocked || parent.Faction != Faction.OfPlayer) yield break;
        TargetingParameters targeting = TargetingParameters.ForCell();
        targeting.validator = target => target.Map == parent.Map && target.Cell != parent.Position
            && CompAbilityEffect_MingyuanBurningPillar.CanOccupy(target.Cell, parent.Map);
        yield return new Command_Target
        {
            defaultLabel = "MX_Mingyuan_BurningPillar_MoveLabel".Translate(),
            defaultDesc = "MX_Mingyuan_BurningPillar_MoveDesc".Translate(),
            icon = TexCommand.GatherSpotActive,
            targetingParams = targeting,
            action = target => OrderMove(target.Cell),
            onUpdate = target =>
            {
                if (target.IsValid && target.Cell.InBounds(parent.Map))
                {
                    MingyuanFireVfx.DrawWarning(target.Cell.ToVector3Shifted(), 0.85f);
                    GenDraw.DrawLineBetween(Center, target.Cell.ToVector3Shifted(), SimpleColor.Yellow, 0.10f);
                }
            }
        };
        yield return new Command_Action
        {
            defaultLabel = "MX_Mingyuan_BurningPillar_StopLabel".Translate(),
            defaultDesc = "MX_Mingyuan_BurningPillar_StopDesc".Translate(),
            icon = TexCommand.CannotShoot,
            action = () =>
            {
                Stop();
                if (caster?.CurJobDef == MX_MingyuanDefOf.MX_Mingyuan_ControlBurningPillar && caster.CurJob.targetA.Thing == parent)
                    caster.jobs.EndCurrentJob(JobCondition.InterruptForced);
            }
        };
    }

    private void OrderMove(IntVec3 target)
    {
        if (!ControlsUnlocked || !CompAbilityEffect_MingyuanBurningPillar.CanOccupy(target, parent.Map)) return;
        Job job = JobMaker.MakeJob(MX_MingyuanDefOf.MX_Mingyuan_ControlBurningPillar, parent, target);
        if (caster.jobs.TryTakeOrderedJob(job)) Stop();
    }

    private void Stop() => destinationCell = IntVec3.Invalid;

    internal void CompleteGesture(Pawn source, IntVec3 destination)
    {
        if (!ControlsUnlocked || source != caster
            || !CompAbilityEffect_MingyuanBurningPillar.CanOccupy(destination, parent.Map)) return;
        Props.snapSound?.PlayOneShot(new TargetInfo(caster.Position, caster.Map));
        MingyuanFireVfx.PlayBurst(caster.Map, caster.DrawPos, 0.85f, 22);
        destinationCell = destination;
    }
}

public class JobDriver_MingyuanControlPillar : JobDriver
{
    private CompMingyuanBurningPillarTornado Tornado => (job.targetA.Thing as Thing_MingyuanBurningPillarField)?.Tornado;
    private int GestureTicks => Tornado?.GestureTicks ?? 1;
    internal float GestureProgress => 1f - Mathf.Clamp01(ticksLeftThisToil / (float)GestureTicks);
    public override Vector3 ForcedBodyOffset => Vector3.forward * (Mathf.Sin(GestureProgress * Mathf.PI) * 0.14f);

    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

    protected override IEnumerable<Toil> MakeNewToils()
    {
        this.FailOnDespawnedOrNull(TargetIndex.A);
        this.FailOn(() => Tornado?.ControlsUnlocked != true || pawn.stances.stunner.Stunned);
        Toil gesture = Toils_General.Wait(GestureTicks);
        gesture.initAction = () =>
        {
            pawn.pather.StopDead();
            pawn.rotationTracker.FaceCell(job.targetA.Cell);
        };
        yield return gesture;
        yield return Toils_General.Do(() => Tornado?.CompleteGesture(pawn, job.targetB.Cell));
    }
}

// The damage worker owns destruction. Suppress only vanilla leavings for this specific wooden victim.
[HarmonyPatch(typeof(GenLeaving), nameof(GenLeaving.DoLeavingsFor),
    typeof(Thing), typeof(Map), typeof(DestroyMode), typeof(CellRect), typeof(Predicate<IntVec3>), typeof(List<Thing>))]
internal static class Patch_MingyuanPillarWoodLeavings
{
    [ThreadStatic] internal static Thing Target;

    private static bool Prefix(Thing diedThing, DestroyMode mode) =>
        diedThing != Target || mode != DestroyMode.KillFinalize;
}

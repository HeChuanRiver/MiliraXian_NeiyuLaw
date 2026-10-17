using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu;

public class CompProperties_AbilityNeiyuThunderSigil : CompProperties_AbilityEffect
{
    public float radius = 3f;
    public int strikeBudget = 108;
    public IntRange firstDelayTicksRange = new(120, 180);
    public IntRange strikeIntervalTicksRange = new(45, 75);
    public int damageAmount = 25;
    public int empDamageAmount = 15;
    public float fireSize = 0.3f;
    public HediffDef markerHediff;
    public ThingDef warmupMote;

    public CompProperties_AbilityNeiyuThunderSigil() => compClass = typeof(CompAbilityEffect_NeiyuThunderSigil);
}

// Existing saves contain vanilla Ability instances. A scoped activation hook keeps
// those objects, IDs, charges and cooldowns intact instead of replacing abilities.
[HarmonyPatch(typeof(Ability), nameof(Ability.Activate), new[] { typeof(LocalTargetInfo), typeof(LocalTargetInfo) })]
internal static class Patch_NeiyuThunderSigil_Activate
{
    private static bool Prefix(Ability __instance, LocalTargetInfo target, ref bool __result)
    {
        CompAbilityEffect_NeiyuThunderSigil effect = __instance.CompOfType<CompAbilityEffect_NeiyuThunderSigil>();
        if (effect == null) return true;
        __result = false;
        // PreActivate starts vanilla cooldowns. Commit the snapshot before reaching it.
        return __instance.CanCast && __instance.verb.ValidateTarget(target) && effect.TrySchedule(target);
    }
}

public class Verb_CastAbilityNeiyuThunderSigil : Verb_CastAbility
{
    public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg,
        bool surpriseAttack = false, bool canHitNonTargetPawns = true,
        bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
    {
        if (!ValidateTarget(castTarg)
            || !ability.CompOfType<CompAbilityEffect_NeiyuThunderSigil>().HasTargets(castTarg)) return false;
        bool started = base.TryStartCastOn(castTarg, destTarg, surpriseAttack,
            canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast);
        if (started && WarmupStance is { } stance) stance.ticksLeft = WarmupTime.SecondsToTicks();
        return started;
    }
}

public class CompAbilityEffect_NeiyuThunderSigil : CompAbilityEffect
{
    private readonly List<Pawn> previewTargets = new();
    private IntVec3 previewCell = IntVec3.Invalid;
    private int previewTick = -1;
    public new CompProperties_AbilityNeiyuThunderSigil Props => (CompProperties_AbilityNeiyuThunderSigil)props;

    public override bool GizmoDisabled(out string reason)
    {
        reason = NeiyuPowerBalance.AbilitiesDisabled ? NeiyuPowerBalance.AbilitiesDisabledReason : null;
        return reason != null;
    }

    public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
    {
        Pawn caster = parent.pawn;
        if (NeiyuPowerBalance.AbilitiesDisabled)
        {
            if (throwMessages) Messages.Message(NeiyuPowerBalance.AbilitiesDisabledReason, MessageTypeDefOf.RejectInput, false);
            return false;
        }
        return caster is { Spawned: true, Dead: false, Downed: false } && target.IsValid
            && target.Cell.InBounds(caster.Map) && !target.HasThing && base.Valid(target, throwMessages);
    }

    // Called once before warmup, not by per-tick job validity checks.
    internal bool HasTargets(LocalTargetInfo target)
    {
        Pawn caster = parent.pawn;
        bool found = NeiyuCombatMapComponent.CollectThunderTargets(caster, target.Cell, Props.radius, null) > 0;
        if (!found) NoTargets();
        return found;
    }

    public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest) =>
        base.CanApplyOn(target, dest) && Valid(target);

    public override void CompTick()
    {
        base.CompTick();
        if (previewTargets.Count == 0 || Find.TickManager.TicksGame - previewTick <= 30) return;
        previewTargets.Clear();
        previewCell = IntVec3.Invalid;
    }

    internal bool TrySchedule(LocalTargetInfo target)
    {
        int count = parent.pawn.Map.GetComponent<NeiyuCombatMapComponent>()
            .ScheduleThunderMarks(parent.pawn, target.Cell, Props);
        if (count == 0) NoTargets();
        return count > 0;
    }

    private void NoTargets() => Messages.Message("MX_NL_NoMarkTargetsInRange".Translate(),
        parent.pawn, MessageTypeDefOf.RejectInput, false);

    public override IEnumerable<Mote> CustomWarmupMotes(LocalTargetInfo target)
    {
        if (Props.warmupMote == null) yield break;
        if (MoteMaker.MakeStaticMote(parent.pawn.DrawPos, parent.pawn.Map, Props.warmupMote,
            1f, makeOffscreen: true) is Mote_NeiyuThunderWarmup mote)
        {
            mote.Bind(parent);
            yield return mote;
        }
    }

    public override void DrawEffectPreview(LocalTargetInfo target)
    {
        Pawn caster = parent.pawn;
        if (caster is not { Spawned: true } || !target.IsValid || !target.Cell.InBounds(caster.Map)) return;
        int now = Find.TickManager.TicksGame;
        if (target.Cell != previewCell || now < previewTick || now - previewTick >= 12)
        {
            previewCell = target.Cell;
            previewTick = now;
            NeiyuCombatMapComponent.CollectThunderTargets(caster, target.Cell, Props.radius, previewTargets);
        }
        NeiyuThunderVfx.DrawPreview(target.Cell, Props.radius, previewTargets, now);
    }
}

public partial class NeiyuCombatMapComponent
{
    // Keep the serialized type and keys so in-flight tasks survive old saves.
    private sealed class ThunderTask : IExposable
    {
        public Pawn caster, target;
        public int remainingStrikes, nextStrikeTick, intervalMin, intervalMax, damageAmount, empDamageAmount;
        public int appliedTick, lastStrikeTick = -1, seed, rulesVersion = 1;
        public float fireSize = 0.3f;
        public HediffDef markerHediff;

        public void ExposeData()
        {
            Scribe_References.Look(ref caster, "caster");
            Scribe_References.Look(ref target, "target");
            Scribe_Values.Look(ref remainingStrikes, "remainingStrikes");
            Scribe_Values.Look(ref nextStrikeTick, "nextStrikeTick");
            Scribe_Values.Look(ref intervalMin, "intervalMin", 45);
            Scribe_Values.Look(ref intervalMax, "intervalMax", 75);
            Scribe_Values.Look(ref damageAmount, "damageAmount", 25);
            Scribe_Values.Look(ref empDamageAmount, "empDamageAmount", 15);
            Scribe_Values.Look(ref appliedTick, "appliedTick");
            Scribe_Values.Look(ref lastStrikeTick, "lastStrikeTick", -1);
            Scribe_Values.Look(ref seed, "seed");
            Scribe_Values.Look(ref rulesVersion, "rulesVersion", 0);
            Scribe_Values.Look(ref fireSize, "fireSize", 0.3f);
            Scribe_Defs.Look(ref markerHediff, "markerHediff");
        }
    }

    private List<ThunderTask> thunderTasks = new();
    private readonly Dictionary<(Pawn, HediffDef), int> thunderMarkerOwners = new();
    private readonly Dictionary<Pawn, ThunderMarkFrame> thunderFrames = new();
    private NeiyuThunderVfx thunderVfx;
    private NeiyuThunderVfx ThunderVfx => thunderVfx ??= new NeiyuThunderVfx(map);

    private void ExposeThunderData()
    {
        Scribe_Collections.Look(ref thunderTasks, "mxnl_thunderTasks", LookMode.Deep);
        if (Scribe.mode == LoadSaveMode.PostLoadInit) thunderTasks ??= new();
    }

    public override void FinalizeInit()
    {
        base.FinalizeInit();
        thunderMarkerOwners.Clear();
        for (int i = thunderTasks.Count - 1; i >= 0; i--)
        {
            ThunderTask task = thunderTasks[i];
            if (task == null || task.remainingStrikes <= 0 || !ThunderTargetValid(task.target))
            {
                if (task != null) RemoveThunderMarker(task.target, task.markerHediff);
                thunderTasks.RemoveAt(i);
                continue;
            }
            if (task.rulesVersion == 0)
            {
                // Migrate damage without resetting spent strikes or the saved deadline.
                task.damageAmount = 25;
                task.empDamageAmount = 15;
                task.appliedTick = Find.TickManager.TicksGame - 60;
                task.seed = task.target.thingIDNumber ^ task.nextStrikeTick;
                task.rulesVersion = 1;
            }
            task.intervalMin = Mathf.Max(1, task.intervalMin);
            task.intervalMax = Mathf.Max(task.intervalMin, task.intervalMax);
        }
        foreach (ThunderTask task in thunderTasks) OwnThunderMarker(task);
        // Once on load, reconcile old orphan marks. Never scan the map in the tick loop.
        HediffDef marker = DefDatabase<HediffDef>.GetNamedSilentFail("MX_Neiyu_ThunderMark");
        foreach (Pawn pawn in map.mapPawns.AllPawnsSpawned)
            if (!thunderMarkerOwners.ContainsKey((pawn, marker))) RemoveThunderMarker(pawn, marker);
    }

    internal static bool IsThunderTarget(Pawn caster, Pawn pawn) => pawn != null && pawn != caster
        && pawn.Spawned && !pawn.Dead && !pawn.Destroyed && pawn.Map == caster.Map
        && (caster.Faction == null || pawn.Faction != caster.Faction);

    internal static int CollectThunderTargets(Pawn caster, IntVec3 center, float radius, List<Pawn> result)
    {
        result?.Clear();
        if (caster is not { Spawned: true } || !center.InBounds(caster.Map)) return 0;
        foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
        {
            if (!cell.InBounds(caster.Map)) continue;
            List<Thing> things = cell.GetThingList(caster.Map);
            for (int i = 0; i < things.Count; i++)
            {
                if (things[i] is not Pawn pawn || !IsThunderTarget(caster, pawn)) continue;
                if (result == null) return 1;
                if (!result.Contains(pawn)) result.Add(pawn);
            }
        }
        return result?.Count ?? 0;
    }

    public int ScheduleThunderMarks(Pawn caster, IntVec3 center, CompProperties_AbilityNeiyuThunderSigil props)
    {
        if (caster is not { Spawned: true, Dead: false, Downed: false } || caster.Map != map
            || props == null || NeiyuPowerBalance.AbilitiesDisabled || props.strikeBudget <= 0) return 0;
        List<Pawn> targets = SimplePool<List<Pawn>>.Get();
        try
        {
            int count = CollectThunderTargets(caster, center, props.radius, targets);
            if (count == 0) return 0;
            int strikes = 1 + (props.strikeBudget - 1) / count;
            int now = Find.TickManager.TicksGame;
            foreach (Pawn target in targets)
            {
                ThunderTask task = new()
                {
                    caster = caster, target = target, remainingStrikes = strikes,
                    nextStrikeTick = now + Mathf.Max(1, props.firstDelayTicksRange.RandomInRange),
                    intervalMin = Mathf.Max(1, props.strikeIntervalTicksRange.min),
                    intervalMax = Mathf.Max(1, props.strikeIntervalTicksRange.max),
                    damageAmount = Mathf.Max(0, props.damageAmount), empDamageAmount = Mathf.Max(0, props.empDamageAmount),
                    fireSize = Mathf.Max(0f, props.fireSize), markerHediff = props.markerHediff,
                    appliedTick = now, seed = unchecked(now * 397 ^ target.thingIDNumber ^ caster.thingIDNumber * 31)
                };
                task.intervalMax = Mathf.Max(task.intervalMin, task.intervalMax);
                thunderTasks.Add(task);
                OwnThunderMarker(task);
            }
            // Visual failures must never turn a committed gameplay cast into a failed shot.
            ThunderVfx.CastPulse(caster.Position.ToVector3Shifted(), now);
            return count;
        }
        finally { targets.Clear(); SimplePool<List<Pawn>>.Return(targets); }
    }

    private bool ThunderTargetValid(Pawn target) => target is { Spawned: true, Dead: false, Destroyed: false }
        && target.Map == map;

    private void TickThunder()
    {
        int now = Find.TickManager.TicksGame;
        for (int i = thunderTasks.Count - 1; i >= 0; i--)
        {
            ThunderTask task = thunderTasks[i];
            bool valid = task != null && task.remainingStrikes > 0 && ThunderTargetValid(task.target);
            if (valid && now >= task.nextStrikeTick)
            {
                Pawn target = task.target;
                IntVec3 hit = target.Position;
                ThunderVfx.Strike(hit, now, unchecked(task.seed + task.remainingStrikes * 7919), task.remainingStrikes == 1);
                if (task.damageAmount > 0)
                {
                    DamageInfo damage = new(DamageDefOf.Burn, task.damageAmount, 0f, -1f, task.caster);
                    damage.SetIgnoreArmor(true);
                    target.TakeDamage(damage);
                }
                if (ThunderTargetValid(target) && task.empDamageAmount > 0)
                    target.TakeDamage(new DamageInfo(DamageDefOf.EMP, task.empDamageAmount, 0f, -1f, task.caster));
                if (ThunderTargetValid(target) && task.fireSize > 0f) target.TryAttachFire(task.fireSize, task.caster);
                task.remainingStrikes--;
                task.lastStrikeTick = now;
                valid = task.remainingStrikes > 0 && ThunderTargetValid(target);
                if (valid) task.nextStrikeTick = now + Rand.RangeInclusive(task.intervalMin, task.intervalMax);
            }
            if (valid) continue;
            thunderTasks.RemoveAt(i);
            if (task != null) ReleaseThunderMarker(task, now);
        }
        thunderVfx?.Tick(now);
        if (thunderTasks.Count == 0) thunderFrames.Clear();
        if (thunderTasks.Count == 0 && thunderVfx?.Idle == true)
        {
            thunderVfx.Dispose();
            thunderVfx = null;
        }
    }

    private void OwnThunderMarker(ThunderTask task)
    {
        if (task.markerHediff == null) return;
        var key = (task.target, task.markerHediff);
        thunderMarkerOwners.TryGetValue(key, out int owners);
        thunderMarkerOwners[key] = owners + 1;
        if (task.target.health?.hediffSet.HasHediff(task.markerHediff) == false)
            task.target.health.AddHediff(task.markerHediff);
    }

    private void ReleaseThunderMarker(ThunderTask task, int now)
    {
        var key = (task.target, task.markerHediff);
        if (task.markerHediff != null && thunderMarkerOwners.TryGetValue(key, out int owners) && owners > 1)
        {
            thunderMarkerOwners[key] = owners - 1;
            return;
        }
        thunderMarkerOwners.Remove(key);
        RemoveThunderMarker(task.target, task.markerHediff);
        if (task.target?.Spawned == true && task.target.Map == map)
            thunderVfx?.Release(task.target.Position.ToVector3Shifted(), now, task.seed);
    }

    private static void RemoveThunderMarker(Pawn pawn, HediffDef marker)
    {
        if (marker == null || pawn?.health?.hediffSet == null) return;
        Hediff hediff;
        while ((hediff = pawn.health.hediffSet.GetFirstHediffOfDef(marker)) != null) pawn.health.RemoveHediff(hediff);
    }

    private void CancelThunder()
    {
        foreach (ThunderTask task in thunderTasks)
            if (task != null) RemoveThunderMarker(task.target, task.markerHediff);
        thunderTasks.Clear();
        thunderMarkerOwners.Clear();
        thunderFrames.Clear();
        thunderVfx?.Dispose();
        thunderVfx = null;
    }

    public override void MapRemoved()
    {
        CancelThunder();
        base.MapRemoved();
    }

    public override void MapComponentDraw()
    {
        base.MapComponentDraw();
        if (Find.CurrentMap != map || Find.UIRoot.HideMotes || Find.ScreenshotModeHandler.Active) return;
        thunderFrames.Clear();
        foreach (ThunderTask task in thunderTasks)
        {
            if (task == null || !ThunderTargetValid(task.target)) continue;
            if (!thunderFrames.TryGetValue(task.target, out ThunderMarkFrame frame))
                frame = new ThunderMarkFrame(task.appliedTick, task.nextStrikeTick, task.lastStrikeTick, task.seed, task.remainingStrikes == 1);
            else
            {
                frame.appliedTick = Mathf.Min(frame.appliedTick, task.appliedTick);
                if (task.nextStrikeTick < frame.nextStrikeTick)
                {
                    frame.nextStrikeTick = task.nextStrikeTick;
                    frame.final = task.remainingStrikes == 1;
                }
                frame.lastStrikeTick = Mathf.Max(frame.lastStrikeTick, task.lastStrikeTick);
            }
            thunderFrames[task.target] = frame;
        }
        if (thunderFrames.Count > 0 || thunderVfx != null) ThunderVfx.Draw(thunderFrames, Find.TickManager.TicksGame);
    }
}

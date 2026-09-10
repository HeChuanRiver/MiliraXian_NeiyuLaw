using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace MiliraXian.Characters.Mingyuan
{
    public enum MingyuanBowMode : byte
    {
        Focus,
        Radiation
    }

    public class CompProperties_MingyuanRainbowBow : CompProperties
    {
        public string modeIconPath = "MiliraXianMingyuan/Items/RainbowBow";

        public float focusWarmupSeconds = 5f;
        public float focusRange = 999f;
        public SoundDef focusChargeSound;
        public SoundDef focusFireSound;
        public FleckDef focusBeamFleck;
        public ThingDef focusMuzzleFlashMote;
        public float focusMuzzleFlashScale = 0.24f;
        public ThingDef focusTargetMote;
        public float focusTargetMoteScale = 0.92f;

        public float radiationWarmupSeconds = 0.35f;
        public float radiationRange = 10f;
        public float radiationArcDegrees = 108f;
        public int radiationMinIntervalTicks = 60;
        public float radiationDamage = 1f;
        public float radiationLayerFraction = 0.23f;
        public SoundDef radiationWarmupSound;
        public SoundDef radiationFireSound;
        public ThingDef radiationHitMote;
        public float radiationHitMoteScale = 0.42f;
        public int radiationHitVisualLimit = 8;
        public int radiationBlastTicks = 30;

        public ThingDef visualControllerDef;

        public CompProperties_MingyuanRainbowBow()
        {
            compClass = typeof(CompEquippable_MingyuanRainbowBow);
        }
    }

    public class CompEquippable_MingyuanRainbowBow : CompEquippable
    {
        private MingyuanBowMode mode;
        private int lastRadiationShotTick = -999999;
        private Texture2D cachedModeIcon;

        public CompProperties_MingyuanRainbowBow PropsBow => (CompProperties_MingyuanRainbowBow)props;

        public MingyuanBowMode Mode => MingyuanPowerBalance.Sealed ? MingyuanBowMode.Focus : mode;

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Values.Look(ref mode, "mx_mingyuanBowMode", MingyuanBowMode.Focus);
            Scribe_Values.Look(ref lastRadiationShotTick, "mx_mingyuanBowLastRadiationTick", -999999);
            if (Scribe.mode == LoadSaveMode.PostLoadInit
                && mode != MingyuanBowMode.Focus
                && mode != MingyuanBowMode.Radiation)
            {
                mode = MingyuanBowMode.Focus;
            }
        }

        public override IEnumerable<Gizmo> CompGetEquippedGizmosExtra()
        {
            foreach (Gizmo gizmo in base.CompGetEquippedGizmosExtra())
            {
                yield return gizmo;
            }

            if (MingyuanPowerBalance.Sealed) yield break;
            Pawn holder = Holder;
            if (holder == null || holder.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            MingyuanBowMode nextMode = mode == MingyuanBowMode.Focus
                ? MingyuanBowMode.Radiation
                : MingyuanBowMode.Focus;
            bool busy = PrimaryVerb?.WarmingUp == true || PrimaryVerb?.Bursting == true;

            Command_Action command = new()
            {
                defaultLabel = "MX_Mingyuan_Bow_ModeCommand".Translate(ModeLabel(mode)).ToString(),
                defaultDesc = "MX_Mingyuan_Bow_ModeDesc".Translate(
                    ModeLabel(mode), ModeLabel(nextMode), PropsBow.focusWarmupSeconds.ToString("0.##"),
                    PropsBow.radiationRange.ToString("0.##"), PropsBow.radiationArcDegrees.ToString("0.##"),
                    (PropsBow.radiationMinIntervalTicks / 60f).ToString("0.##")).ToString(),
                icon = ModeIcon ?? TexCommand.Attack,
                Disabled = busy,
                disabledReason = busy ? "MX_Mingyuan_Bow_ModeBusy".Translate().ToString() : null,
                action = delegate
                {
                    if (PrimaryVerb?.WarmingUp == true || PrimaryVerb?.Bursting == true)
                    {
                        return;
                    }

                    mode = nextMode;
                }
            };
            yield return command;
        }

        public override string CompInspectStringExtra()
        {
            return "MX_Mingyuan_Bow_ModeInspect".Translate(ModeLabel(mode)).ToString();
        }

        public bool CanBeginRadiationShot()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            return currentTick - lastRadiationShotTick >= Mathf.Max(1, PropsBow.radiationMinIntervalTicks);
        }

        public bool TryBeginRadiationShot()
        {
            if (!CanBeginRadiationShot())
            {
                return false;
            }

            lastRadiationShotTick = Find.TickManager?.TicksGame ?? 0;
            return true;
        }

        private Texture2D ModeIcon
        {
            get
            {
                if (cachedModeIcon == null && !PropsBow.modeIconPath.NullOrEmpty())
                {
                    cachedModeIcon = ContentFinder<Texture2D>.Get(PropsBow.modeIconPath, false);
                }

                return cachedModeIcon;
            }
        }

        private static string ModeLabel(MingyuanBowMode value)
        {
            return (value == MingyuanBowMode.Focus
                    ? "MX_Mingyuan_Bow_ModeFocus"
                    : "MX_Mingyuan_Bow_ModeRadiation")
                .Translate()
                .ToString();
        }
    }

    public class Verb_MingyuanRainbowBow : Verb
    {
        private MingyuanBowMode modeAtCastStart;
        private bool castModeLocked;
        private bool automaticFocusTarget;
        private Vector3 castDirection;
        private readonly List<Pawn> radiationTargets = new(32);

        private CompEquippable_MingyuanRainbowBow BowComp =>
            EquipmentCompSource as CompEquippable_MingyuanRainbowBow;

        private CompProperties_MingyuanRainbowBow PropsBow => BowComp?.PropsBow;

        private MingyuanBowMode ActiveMode => MingyuanPowerBalance.Sealed ? MingyuanBowMode.Focus : castModeLocked && (WarmingUp || Bursting)
            ? modeAtCastStart
            : BowComp?.Mode ?? MingyuanBowMode.Focus;

        public override float WarmupTime => ActiveMode == MingyuanBowMode.Focus
            ? Mathf.Max(0f, PropsBow?.focusWarmupSeconds ?? 5f)
            : Mathf.Max(0f, PropsBow?.radiationWarmupSeconds ?? 0.35f);

        public override float EffectiveRange
        {
            get
            {
                if (ActiveMode == MingyuanBowMode.Radiation)
                {
                    return Mathf.Max(1f, PropsBow?.radiationRange ?? 10f);
                }

                Map map = caster?.MapHeld;
                if (MingyuanPowerBalance.Sealed) return PropsBow?.focusRange ?? 30.9f;
                if (map == null)
                {
                    return Mathf.Max(1f, PropsBow?.focusRange ?? 999f);
                }

                return Mathf.Sqrt((float)map.Size.x * map.Size.x + (float)map.Size.z * map.Size.z) + 2f;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref modeAtCastStart, "mx_mingyuanBowCastMode", MingyuanBowMode.Focus);
            Scribe_Values.Look(ref castModeLocked, "mx_mingyuanBowCastModeLocked", false);
            Scribe_Values.Look(ref automaticFocusTarget, "mx_mingyuanBowAutomaticFocusTarget", false);
            Scribe_Values.Look(ref castDirection, "mx_mingyuanBowCastDirection", Vector3.zero);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && !WarmingUp && !Bursting)
            {
                castModeLocked = false;
                automaticFocusTarget = false;
                castDirection = Vector3.zero;
            }
        }

        public override bool Available()
        {
            if (!base.Available())
            {
                return false;
            }

            return ActiveMode != MingyuanBowMode.Radiation || BowComp?.CanBeginRadiationShot() != false;
        }

        public override void OrderForceTarget(LocalTargetInfo target)
        {
            if (ActiveMode != MingyuanBowMode.Radiation)
            {
                base.OrderForceTarget(target);
                return;
            }

            Pawn pawn = CasterPawn;
            if (pawn?.jobs == null)
            {
                return;
            }

            float minimumRange = verbProps.EffectiveMinRange(target, pawn);
            if (pawn.Position.DistanceToSquared(target.Cell) < minimumRange * minimumRange
                && pawn.Position.AdjacentTo8WayOrInside(target.Cell))
            {
                Messages.Message("MessageCantShootInMelee".Translate(), pawn, MessageTypeDefOf.RejectInput, false);
                return;
            }

            Job job = JobMaker.MakeJob(JobDefOf.AttackStatic, target);
            job.verbToUse = this;
            job.endIfCantShootInMelee = true;
            job.maxNumStaticAttacks = 1;
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        public override bool TryStartCastOn(
            LocalTargetInfo castTarg,
            LocalTargetInfo destTarg,
            bool surpriseAttack = false,
            bool canHitNonTargetPawns = true,
            bool preventFriendlyFire = false,
            bool nonInterruptingSelfCast = false)
        {
            modeAtCastStart = BowComp?.Mode ?? MingyuanBowMode.Focus;
            castModeLocked = true;
            automaticFocusTarget = modeAtCastStart == MingyuanBowMode.Focus
                                   && CasterPawn?.jobs?.curJob?.playerForced != true;

            if (automaticFocusTarget)
            {
                Pawn bestTarget = TryFindBestFocusTarget();
                if (bestTarget == null)
                {
                    castModeLocked = false;
                    automaticFocusTarget = false;
                    castDirection = Vector3.zero;
                    return false;
                }

                castTarg = bestTarget;
            }

            castDirection = DirectionTo(castTarg.Cell);

            bool started = base.TryStartCastOn(
                castTarg,
                destTarg,
                surpriseAttack,
                canHitNonTargetPawns,
                preventFriendlyFire,
                nonInterruptingSelfCast);
            if (!started)
            {
                castModeLocked = false;
                automaticFocusTarget = false;
                castDirection = Vector3.zero;
                return false;
            }

            if (modeAtCastStart == MingyuanBowMode.Focus && WarmupStance != null)
            {
                WarmupStance.ticksLeft = MingyuanPowerBalance.IsOriginal ? 300 : Mathf.RoundToInt(WarmupTime * 60f);
            }

            Vector3 direction = castDirection;
            int visualTicks = Mathf.Max(1, WarmupTicksLeft);
            if (modeAtCastStart == MingyuanBowMode.Focus)
            {
                SpawnVisual(MingyuanBowVisualKind.FocusCharge, direction, visualTicks, this, castTarg.Pawn);
                PlaySound(PropsBow?.focusChargeSound);
            }
            else
            {
                SpawnVisual(MingyuanBowVisualKind.RadiationWarning, direction, visualTicks, this, null);
                PlaySound(PropsBow?.radiationWarmupSound);
            }

            return true;
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo target)
        {
            Map map = caster?.Map;
            if (map == null || !target.IsValid || !target.Cell.IsValid || !target.Cell.InBounds(map))
            {
                return false;
            }

            if (target.HasThing && target.Thing.Map != map)
            {
                return false;
            }

            if (!base.CanHitTargetFrom(root, target))
            {
                return false;
            }

            if (ActiveMode == MingyuanBowMode.Radiation)
            {
                return target.Cell != root;
            }

            Pawn targetPawn = target.Pawn;
            return IsValidFocusTarget(targetPawn)
                   && GenSight.LineOfSightToThing(root, targetPawn, map, skipFirstCell: true);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!base.ValidateTarget(target, showMessages))
            {
                return false;
            }

            if (ActiveMode == MingyuanBowMode.Focus && !IsValidFocusTarget(target.Pawn))
            {
                if (showMessages)
                {
                    Messages.Message("MX_Mingyuan_Bow_InvalidFocusTarget".Translate(), MessageTypeDefOf.RejectInput, false);
                }

                return false;
            }

            return CanHitTargetFrom(caster.Position, target);
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            if (caster == null || !caster.Spawned || !target.IsValid)
            {
                return;
            }

            GenDraw.DrawTargetHighlight(target);
            Vector3 direction = DirectionTo(target.Cell);
            if (ActiveMode == MingyuanBowMode.Radiation)
            {
                MingyuanBowVisualDrawer.DrawSectorWarning(
                    caster.DrawPos,
                    direction,
                    EffectiveRange,
                    PropsBow?.radiationArcDegrees ?? 108f,
                    0.9f);
            }
            else
            {
                MingyuanBowVisualDrawer.DrawFocusAim(caster.DrawPos, target.CenterVector3, direction);
            }
        }

        public override void Reset()
        {
            base.Reset();
            castModeLocked = false;
            automaticFocusTarget = false;
            castDirection = Vector3.zero;
        }

        protected override bool TryCastShot()
        {
            bool result = (MingyuanPowerBalance.Sealed || modeAtCastStart == MingyuanBowMode.Focus)
                ? TryCastFocusShot()
                : TryCastRadiationShot();
            if (result)
            {
                lastShotTick = Find.TickManager?.TicksGame ?? lastShotTick;
            }
            castModeLocked = false;
            castDirection = Vector3.zero;
            return result;
        }

        private bool TryCastFocusShot()
        {
            Pawn target = currentTarget.Pawn;
            if (!CanResolveFocusTarget(target))
            {
                if (!automaticFocusTarget)
                {
                    return false;
                }

                target = TryFindBestFocusTarget();
                if (target == null)
                {
                    return false;
                }

                currentTarget = target;
            }

            Map map = CasterPawn.Map;
            Vector3 targetPosition = target.DrawPos;
            Vector3 direction = DirectionTo(target.Position);
            Vector3 emitter = MingyuanBowVisualDrawer.EmitterPosition(CasterPawn.DrawPos, direction);
            if (!MingyuanPowerBalance.Sealed && !MingyuanUtility.TryTriggerLifeBurnBurst(target, CasterPawn))
            {
                return false;
            }

            if (MingyuanPowerBalance.Sealed)
            {
                // Only tier three substitutes an ordinary low-power shot for the burst.
                ShotReport report = ShotReport.HitReportFor(CasterPawn, this, target);
                if (Rand.Chance(report.AimOnTargetChance_IgnoringPosture * report.PassCoverChance))
                {
                    float amount = 17f;
                    bool suppression = MingyuanUtility.SuppressOnHitLifeBurn;
                    try
                    {
                        MingyuanUtility.SuppressOnHitLifeBurn = true;
                        target.TakeDamage(new DamageInfo(MingyuanPowerBalance.ArrowDamage, amount, .1f,
                            -1f, CasterPawn, null, EquipmentSource?.def));
                    }
                    finally { MingyuanUtility.SuppressOnHitLifeBurn = suppression; }
                }
            }

            if (PropsBow?.focusBeamFleck != null)
            {
                FleckMaker.ConnectingLine(emitter, targetPosition, PropsBow.focusBeamFleck, map, 0.12f);
            }

            // Visual flight only: focus damage has already resolved above.
            SpawnVisual(MingyuanBowVisualKind.FocusShot, direction, 18, null, null, targetPosition);
            SpawnMoteAt(emitter, PropsBow?.focusMuzzleFlashMote, PropsBow?.focusMuzzleFlashScale ?? 0.24f);
            PlaySound(PropsBow?.focusFireSound);
            return true;
        }

        private bool TryCastRadiationShot()
        {
            if (BowComp == null || !BowComp.TryBeginRadiationShot() || CasterPawn?.Map == null)
            {
                return false;
            }

            Vector3 direction = castDirection;
            if (direction.sqrMagnitude < 0.001f)
            {
                return false;
            }

            Map map = CasterPawn.Map;
            Vector3 logicalOrigin = CasterPawn.Position.ToVector3Shifted();
            float range = Mathf.Max(1f, PropsBow.radiationRange);
            float rangeSquared = range * range;
            float halfAngle = Mathf.Clamp(PropsBow.radiationArcDegrees * 0.5f, 1f, 179f);
            float minimumDot = Mathf.Cos(halfAngle * Mathf.Deg2Rad);
            int hitVisuals = 0;
            IntVec3 center = CasterPawn.Position;
            int radialCellCount = GenRadial.NumCellsInRadius(range);
            radiationTargets.Clear();

            for (int cellIndex = 0; cellIndex < radialCellCount; cellIndex++)
            {
                IntVec3 cell = center + GenRadial.RadialPattern[cellIndex];
                if (!cell.InBounds(map))
                {
                    continue;
                }

                List<Thing> things = cell.GetThingList(map);
                for (int thingIndex = 0; thingIndex < things.Count; thingIndex++)
                {
                    Pawn target = things[thingIndex] as Pawn;
                    if (target == null || target.Position != cell || !IsHostileEnemy(target))
                    {
                        continue;
                    }

                    Vector3 offset = target.Position.ToVector3Shifted() - logicalOrigin;
                    offset.y = 0f;
                    float distanceSquared = offset.sqrMagnitude;
                    if (distanceSquared <= 0.001f || distanceSquared > rangeSquared)
                    {
                        continue;
                    }

                    float dot = Vector3.Dot(direction, offset) / Mathf.Sqrt(distanceSquared);
                    if (dot + 0.0001f >= minimumDot)
                    {
                        radiationTargets.Add(target);
                    }
                }
            }

            try
            {
                for (int i = 0; i < radiationTargets.Count; i++)
                {
                    Pawn target = radiationTargets[i];
                    ApplyRadiationHit(target);
                    if (hitVisuals < Mathf.Max(0, PropsBow.radiationHitVisualLimit)
                        && !target.Dead
                        && PropsBow.radiationHitMote != null
                        && MingyuanUtility.TryMakeAttachedMote(
                            target,
                            PropsBow.radiationHitMote,
                            PropsBow.radiationHitMoteScale))
                    {
                        hitVisuals++;
                    }
                }
            }
            finally
            {
                radiationTargets.Clear();
            }

            SpawnVisual(
                MingyuanBowVisualKind.RadiationBlast,
                direction,
                Mathf.Max(1, PropsBow.radiationBlastTicks),
                null,
                null);
            PlaySound(PropsBow.radiationFireSound);
            return true;
        }

        private void ApplyRadiationHit(Pawn target)
        {
            if (MingyuanPowerBalance.Sealed) return;
            float threshold = MingyuanUtility.GetLifeBurnExecuteThreshold(target);
            int layers = Mathf.Max(1, Mathf.CeilToInt(threshold * Mathf.Max(0f, PropsBow.radiationLayerFraction)));
            MingyuanUtility.AddLifeBurn(target, CasterPawn, layers);

            float damage = Mathf.Max(0f, PropsBow.radiationDamage);
            if (damage <= 0f || target.Dead)
            {
                return;
            }

            DamageInfo damageInfo = new(
                DamageDefOf.Burn,
                damage,
                0f,
                -1f,
                CasterPawn,
                null,
                EquipmentSource?.def,
                intendedTarget: target);
            bool previousSuppression = MingyuanUtility.SuppressOnHitLifeBurn;
            try
            {
                MingyuanUtility.SuppressOnHitLifeBurn = true;
                target.TakeDamage(damageInfo);
            }
            finally
            {
                MingyuanUtility.SuppressOnHitLifeBurn = previousSuppression;
            }
        }

        private Pawn TryFindBestFocusTarget()
        {
            Map map = CasterPawn?.Map;
            if (map == null)
            {
                return null;
            }

            Pawn best = null;
            float bestRemaining = float.MinValue;
            int bestDistanceSquared = int.MaxValue;
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            IntVec3 origin = CasterPawn.Position;

            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn candidate = pawns[i];
                if (!IsValidFocusTarget(candidate))
                {
                    continue;
                }

                int distanceSquared = origin.DistanceToSquared(candidate.Position);
                if (!CanHitTargetFrom(origin, candidate))
                {
                    continue;
                }

                float remaining = MingyuanUtility.GetLifeBurnRemainingToExecute(candidate);
                bool betterRemaining = remaining > bestRemaining + 0.0001f;
                bool tiedRemaining = Mathf.Abs(remaining - bestRemaining) <= 0.0001f;
                if (best == null
                    || betterRemaining
                    || (tiedRemaining && distanceSquared < bestDistanceSquared)
                    || (tiedRemaining
                        && distanceSquared == bestDistanceSquared
                        && candidate.thingIDNumber < best.thingIDNumber))
                {
                    best = candidate;
                    bestRemaining = remaining;
                    bestDistanceSquared = distanceSquared;
                }
            }

            return best;
        }

        private bool CanResolveFocusTarget(Pawn target)
        {
            return IsValidFocusTarget(target)
                   && target.Map == CasterPawn.Map
                   && CanHitTargetFrom(CasterPawn.Position, target);
        }

        private bool IsValidFocusTarget(Pawn target)
        {
            return IsHostileEnemy(target) && (MingyuanPowerBalance.Sealed || !MingyuanUtility.IsLifeBurnImmunePawn(target));
        }

        private bool IsHostileEnemy(Pawn target)
        {
            Pawn hostilePawn;
            return target != null
                   && target.Spawned
                   && !target.Destroyed
                   && !target.Dead
                   && !target.IsPsychologicallyInvisible()
                   && MingyuanUtility.IsHostilePawn(target, CasterPawn, out hostilePawn);
        }

        private Vector3 DirectionTo(IntVec3 targetCell)
        {
            Vector3 origin = caster?.DrawPos ?? Vector3.zero;
            Vector3 direction = targetCell.ToVector3Shifted() - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f && caster != null)
            {
                direction = caster.Rotation.FacingCell.ToVector3();
            }

            return direction.normalized;
        }

        private void SpawnVisual(
            MingyuanBowVisualKind kind,
            Vector3 direction,
            int durationTicks,
            Verb sourceVerb,
            Pawn focusTarget,
            Vector3? shotEnd = null)
        {
            ThingDef visualDef = PropsBow?.visualControllerDef;
            Pawn source = CasterPawn;
            if (visualDef == null || source?.Map == null || !source.Spawned)
            {
                return;
            }

            Thing_MingyuanBowVisual visual = ThingMaker.MakeThing(visualDef) as Thing_MingyuanBowVisual;
            if (visual == null)
            {
                return;
            }

            GenSpawn.Spawn(visual, source.Position, source.Map, WipeMode.Vanish);
            visual.Init(
                source,
                sourceVerb,
                kind,
                direction,
                durationTicks,
                PropsBow.radiationRange,
                PropsBow.radiationArcDegrees,
                focusTarget,
                PropsBow.focusTargetMote,
                PropsBow.focusTargetMoteScale);
            if (shotEnd.HasValue)
                visual.SetShotPath(MingyuanBowVisualDrawer.EmitterPosition(source.DrawPos, direction), shotEnd.Value);
        }

        private void SpawnMoteAt(Vector3 position, ThingDef moteDef, float scale)
        {
            Map map = CasterPawn?.Map;
            if (map == null || moteDef == null)
            {
                return;
            }

            Mote mote = MoteMaker.MakeStaticMote(
                position,
                map,
                moteDef,
                Mathf.Max(0.05f, scale),
                false,
                0f);
            if (mote != null)
            {
                mote.exactPosition = position;
            }
        }

        private void PlaySound(SoundDef soundDef)
        {
            if (soundDef != null && CasterPawn?.Map != null)
            {
                soundDef.PlayOneShot(new TargetInfo(CasterPawn.Position, CasterPawn.Map));
            }
        }
    }

    public enum MingyuanBowVisualKind : byte
    {
        FocusCharge,
        RadiationWarning,
        RadiationBlast,
        FocusShot
    }

    public class Thing_MingyuanBowVisual : Thing
    {
        private Pawn source;
        private Verb sourceVerb;
        private Pawn focusTarget;
        private ThingDef focusTargetMoteDef;
        private float focusTargetMoteScale = 0.92f;
        private MingyuanBowVisualKind kind;
        private Vector3 direction;
        private int startTick;
        private int durationTicks;
        private float range;
        private float arcDegrees;
        private Mote focusTargetMote;
        private Vector3 shotStart;
        private Vector3 shotEnd;

        public void SetShotPath(Vector3 start, Vector3 end)
        {
            shotStart = start;
            shotEnd = end;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref source, "source");
            Scribe_References.Look(ref sourceVerb, "sourceVerb");
            Scribe_References.Look(ref focusTarget, "focusTarget");
            Scribe_Defs.Look(ref focusTargetMoteDef, "focusTargetMoteDef");
            Scribe_Values.Look(ref focusTargetMoteScale, "focusTargetMoteScale", 0.92f);
            Scribe_Values.Look(ref kind, "kind", MingyuanBowVisualKind.FocusCharge);
            Scribe_Values.Look(ref direction, "direction", Vector3.zero);
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref durationTicks, "durationTicks", 1);
            Scribe_Values.Look(ref range, "range", 1f);
            Scribe_Values.Look(ref arcDegrees, "arcDegrees", 108f);
            Scribe_Values.Look(ref shotStart, "shotStart", Vector3.zero);
            Scribe_Values.Look(ref shotEnd, "shotEnd", Vector3.zero);
        }

        public void Init(
            Pawn newSource,
            Verb newSourceVerb,
            MingyuanBowVisualKind newKind,
            Vector3 newDirection,
            int newDurationTicks,
            float newRange,
            float newArcDegrees,
            Pawn newFocusTarget,
            ThingDef newFocusTargetMoteDef,
            float newFocusTargetMoteScale)
        {
            source = newSource;
            sourceVerb = newSourceVerb;
            focusTarget = newFocusTarget;
            focusTargetMoteDef = newFocusTargetMoteDef;
            focusTargetMoteScale = Mathf.Max(0.1f, newFocusTargetMoteScale);
            kind = newKind;
            direction = newDirection.normalized;
            startTick = Find.TickManager?.TicksGame ?? 0;
            durationTicks = Mathf.Max(1, newDurationTicks);
            range = Mathf.Max(1f, newRange);
            arcDegrees = Mathf.Clamp(newArcDegrees, 2f, 358f);
        }

        protected override void Tick()
        {
            base.Tick();
            if (Destroyed)
            {
                return;
            }

            int age = (Find.TickManager?.TicksGame ?? 0) - startTick;
            bool sourceInvalid = source == null
                                 || source.Destroyed
                                 || source.Dead
                                 || !source.Spawned
                                 || source.Map != Map;
            bool focusTargetInvalid = kind == MingyuanBowVisualKind.FocusCharge
                                      && (focusTarget == null
                                          || focusTarget.Destroyed
                                          || focusTarget.Dead
                                          || !focusTarget.Spawned
                                          || focusTarget.Map != Map);
            bool warmupEnded = kind != MingyuanBowVisualKind.RadiationBlast
                               && sourceVerb != null
                               && age > 1
                               && !sourceVerb.WarmingUp;
            if (sourceInvalid || focusTargetInvalid || warmupEnded || age >= durationTicks)
            {
                Destroy(DestroyMode.Vanish);
                return;
            }

            if (kind == MingyuanBowVisualKind.FocusCharge)
            {
                MaintainFocusTargetMote();
            }
        }

        private void MaintainFocusTargetMote()
        {
            if (focusTargetMoteDef == null || focusTarget == null || !focusTarget.Spawned || focusTarget.Map != Map)
            {
                return;
            }

            if (focusTargetMote == null || focusTargetMote.Destroyed)
            {
                focusTargetMote = MoteMaker.MakeAttachedOverlay(
                    focusTarget,
                    focusTargetMoteDef,
                    Vector3.zero,
                    focusTargetMoteScale);
            }

            focusTargetMote?.Maintain();
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            if (source == null || direction.sqrMagnitude < 0.001f)
            {
                return;
            }

            int age = Mathf.Max(0, (Find.TickManager?.TicksGame ?? 0) - startTick);
            float progress = Mathf.Clamp01(age / (float)Mathf.Max(1, durationTicks));
            switch (kind)
            {
                case MingyuanBowVisualKind.FocusCharge:
                    Vector3 focusDirection = focusTarget != null
                        ? (focusTarget.DrawPos - source.DrawPos).Yto0()
                        : direction;
                    if (focusDirection.sqrMagnitude < 0.001f)
                    {
                        focusDirection = direction;
                    }
                    focusDirection.Normalize();
                    MingyuanBowVisualDrawer.DrawFocusCharge(source.DrawPos, focusDirection, progress);
                    if (focusTarget != null)
                    {
                        MingyuanBowVisualDrawer.DrawFocusAim(
                            source.DrawPos, focusTarget.DrawPos, focusDirection, progress, age / 60f);
                    }
                    break;
                case MingyuanBowVisualKind.RadiationWarning:
                    MingyuanBowVisualDrawer.DrawScatterCharge(source.DrawPos, direction, arcDegrees, progress);
                    break;
                case MingyuanBowVisualKind.RadiationBlast:
                    // Released arrows stay anchored to the saved controller position,
                    // rather than sliding across the map when the archer moves.
                    MingyuanBowVisualDrawer.DrawSectorBlast(drawLoc, direction, range, arcDegrees, progress);
                    break;
                case MingyuanBowVisualKind.FocusShot:
                    MingyuanBowVisualDrawer.DrawFocusArrow(shotStart, shotEnd, progress);
                    break;
            }
        }
    }

    [StaticConstructorOnStartup]
    internal static class MingyuanBowVisualDrawer
    {
        private const int AlphaSteps = 8;
        private const string LineTexturePath = "UI/Overlays/ThingLine";

        private static readonly Color FocusColor = new(1f, 0.94f, 0.72f, 0.92f);
        private static readonly Color FlameColor = new(1f, 0.46f, 0.20f, 0.82f);
        private static readonly Color FocusLinkColor = new(0.68f, 0.075f, 0.025f, 0.96f);

        private static Material[] focusMaterials;
        private static Material[] flameMaterials;
        private static Material[] scatterArrowMaterials;
        private static Material[] focusLinkMaterials;
        private static readonly Material ArrowMaterial = MaterialPool.MatFrom(
            "MiliraXianMingyuan/Projectile/RainbowArrow", ShaderDatabase.Cutout);

        public static void DrawFocusArrow(Vector3 start, Vector3 end, float progress)
        {
            Vector3 direction = (end - start).Yto0();
            if (direction.sqrMagnitude < 0.001f) return;
            float distance = direction.magnitude;
            direction /= distance;
            start.y = end.y = AltitudeLayer.MoteOverheadLow.AltitudeFor();
            // Six ticks of flight, then twelve ticks of fading trail/arrival sparks.
            // This is presentation only; it never launches a damaging Projectile.
            float flight = Mathf.Clamp01(progress * 3f);
            float fade = 1f - Mathf.InverseLerp(1f / 3f, 1f, progress);
            if (fade <= 0.001f) return;
            Vector3 position = Vector3.Lerp(start, end, flight);
            float tailLength = Mathf.Min(distance * flight, Mathf.Lerp(3f, 8f, flight));
            Vector3 tail = position - direction * tailLength;
            DrawFocusStroke(tail, position, 0.42f * fade, fade);
            if (flight < 1f)
            {
                Matrix4x4 matrix = Matrix4x4.TRS(position + Vector3.up * 0.025f,
                    Quaternion.LookRotation(direction), new Vector3(2.3f, 1f, 3.4f));
                Graphics.DrawMesh(MeshPool.plane10, matrix, ArrowMaterial, 0);
                DrawFocusSpark(position, direction, 0.5f, 1f);
            }
            else
            {
                float burst = Mathf.InverseLerp(1f / 3f, 1f, progress);
                Vector3 center = end + Vector3.up * 0.03f;
                DrawArc(center, direction, 0.25f + burst * 1.1f, 360f, 16,
                    GetFlameMaterial(fade), 0.13f * fade);
                for (int i = 0; i < 8; i++)
                {
                    Vector3 ray = Rotate(direction, i * 45f);
                    Vector3 spark = center + ray * (0.2f + burst * 1.45f);
                    DrawFocusSpark(spark, ray, (0.22f + burst * 0.12f) * fade, fade);
                }
            }
        }

        public static Vector3 EmitterPosition(Vector3 pawnDrawPos, Vector3 direction)
        {
            Vector3 result = pawnDrawPos + direction.normalized * 0.58f;
            result.y = AltitudeLayer.MoteOverheadLow.AltitudeFor();
            return result;
        }

        public static void DrawFocusAim(
            Vector3 origin, Vector3 target, Vector3 direction, float progress = 0f, float ageSeconds = -1f)
        {
            Vector3 emitter = EmitterPosition(origin, direction);
            target.y = emitter.y;
            Vector3 path = (target - emitter).Yto0();
            float distance = path.magnitude;
            if (distance < 0.05f) return;
            direction = path / distance;
            float charge = Mathf.SmoothStep(0f, 1f, progress);
            DrawFocusStroke(emitter, target, 0.20f + charge * 0.10f, 1f);
            // Target previews show the line only. Charging adds deterministic mesh
            // particles driven by game ticks, so pause/save/load do not desync them.
            if (ageSeconds < 0f) return;
            Vector3 side = new Vector3(direction.z, 0f, -direction.x);
            int count = Mathf.Clamp(Mathf.CeilToInt(distance / 2.5f), 4, 18);
            float phase = ageSeconds * (5f + 4f * charge) / distance;
            for (int i = 0; i < count; i++)
            {
                float t = Mathf.Repeat(i / (float)count + phase, 1f);
                float envelope = Mathf.Sin(t * Mathf.PI);
                float orbit = ageSeconds * 7f + i * 2.4f;
                Vector3 point = Vector3.Lerp(emitter, target, t)
                    + side * (Mathf.Sin(orbit) * (0.10f + charge * 0.10f) * envelope);
                point.y += 0.035f;
                DrawFocusSpark(point, direction, 0.14f + charge * 0.11f,
                    0.45f + envelope * 0.55f);
            }
            Vector3 targetCenter = target + Vector3.up * 0.035f;
            DrawArc(targetCenter, direction, 0.65f - charge * 0.23f, 360f, 16,
                GetFocusMaterial(0.5f + charge * 0.5f), 0.06f + charge * 0.025f);
        }

        private static void DrawFocusStroke(Vector3 start, Vector3 end, float width, float alpha)
        {
            if ((end - start).sqrMagnitude < 0.0001f || width <= 0.001f) return;
            // Alpha blending keeps the saturated red silhouette visible on bright
            // terrain; separate raised glow layers provide the hot gold center.
            Material outline = GetMaterial(ref focusLinkMaterials, FocusLinkColor, ShaderDatabase.Transparent, alpha);
            GenDraw.DrawLineBetween(start, end, outline, width);
            start.y += 0.012f;
            end.y += 0.012f;
            GenDraw.DrawLineBetween(start, end, GetFlameMaterial(alpha), width * 0.52f);
            start.y += 0.012f;
            end.y += 0.012f;
            GenDraw.DrawLineBetween(start, end, GetFocusMaterial(alpha), width * 0.18f);
        }

        private static void DrawFocusSpark(Vector3 point, Vector3 direction, float size, float alpha)
        {
            if (size <= 0.001f || alpha <= 0.001f) return;
            Vector3 along = direction * size;
            Vector3 across = new Vector3(direction.z, 0f, -direction.x) * size * 0.38f;
            GenDraw.DrawLineBetween(point - along, point + along, GetFlameMaterial(alpha), size * 0.46f);
            point.y += 0.006f;
            Material core = GetFocusMaterial(alpha);
            GenDraw.DrawLineBetween(point - along * 0.65f, point + along * 0.65f, core, size * 0.2f);
            GenDraw.DrawLineBetween(point - across, point + across, core, size * 0.2f);
        }

        public static void DrawFocusCharge(Vector3 origin, Vector3 direction, float progress)
        {
            Vector3 emitter = EmitterPosition(origin, direction);
            float eased = Mathf.SmoothStep(0f, 1f, progress);
            float radius = 0.16f + eased * 0.52f;
            Material material = GetFocusMaterial(0.35f + eased * 0.65f);
            DrawArc(emitter, direction, radius, 360f, 16, material, 0.045f + eased * 0.045f);

            Vector3 side = Rotate(direction, 90f) * radius * 0.72f;
            Vector3 forward = direction * radius * 0.72f;
            GenDraw.DrawLineBetween(emitter - side, emitter + side, material, 0.04f);
            GenDraw.DrawLineBetween(emitter - forward, emitter + forward, material, 0.04f);
        }

        public static void DrawSectorWarning(
            Vector3 origin,
            Vector3 direction,
            float radius,
            float arcDegrees,
            float alpha)
        {
            origin.y = AltitudeLayer.MoteOverheadLow.AltitudeFor();
            direction.y = 0f;
            direction.Normalize();
            float halfAngle = arcDegrees * 0.5f;
            Material edge = GetFocusMaterial(alpha);
            Material inner = GetFlameMaterial(alpha * 0.48f);
            Vector3 left = Rotate(direction, -halfAngle);
            Vector3 right = Rotate(direction, halfAngle);

            GenDraw.DrawLineBetween(origin, origin + left * radius, edge, 0.085f);
            GenDraw.DrawLineBetween(origin, origin + right * radius, edge, 0.085f);
            DrawArc(origin, direction, radius, arcDegrees, 18, edge, 0.09f);
            DrawArc(origin, direction, radius * 0.52f, arcDegrees, 18, inner, 0.055f);
        }

        public static void DrawSectorBlast(
            Vector3 origin,
            Vector3 direction,
            float radius,
            float arcDegrees,
            float progress)
        {
            origin.y = AltitudeLayer.MoteOverheadLow.AltitudeFor();
            direction.y = 0f;
            direction.Normalize();
            if (direction.sqrMagnitude < 0.001f || radius <= 0f || progress >= 1f) return;
            Vector3 emitter = EmitterPosition(origin, direction);
            // Two interleaved volleys: eleven long arrows define the whole fan,
            // eight later arrows fill its gaps and middle distance. No gameplay RNG.
            for (int wave = 0; wave < 2; wave++)
            {
                int count = wave == 0 ? 11 : 8;
                for (int i = 0; i < count; i++)
                {
                    float spread = wave == 0 ? i / 10f * 2f - 1f : (i + 0.5f) / 8f * 2f - 1f;
                    float delay = wave * 0.13f + Mathf.Abs(spread) * 0.055f + (i % 3) * 0.012f;
                    float travel = 0.57f + (i % 4) * 0.022f + wave * 0.035f;
                    float age = (progress - delay) / travel;
                    if (age <= 0f || age >= 1.28f) continue;
                    float fade = 1f - Mathf.SmoothStep(0.88f, 1.28f, age);
                    float angle = spread * Mathf.Clamp(arcDegrees, 2f, 358f) * 0.5f;
                    float reach = wave == 0 ? 0.96f + 0.04f * Mathf.Cos(i * 1.7f) : 0.76f + 0.09f * Mathf.Sin(i * 2.1f);
                    Vector3 end = origin + Rotate(direction, angle) * (radius * reach);
                    // Bezier handles start almost parallel at the bow, then open
                    // outward. The tip follows the tangent, not the final ray.
                    Vector3 handle1 = emitter + Rotate(direction, angle * 0.12f) * (radius * 0.25f);
                    Vector3 handle2 = origin + Rotate(direction, angle * 0.72f) * (radius * reach * 0.72f);
                    float headT = ScatterTravel(age);
                    Vector3 head = ScatterPoint(emitter, handle1, handle2, end, headT);
                    Vector3 tangent = ScatterTangent(emitter, handle1, handle2, end, headT);
                    float size = (wave == 0 ? 1.5f : 1.25f) * (0.92f + 0.08f * Mathf.Sin(i * 2.4f));

                    // Sample the recent trajectory into a tapered ribbon; the tail
                    // continues catching up after the arrow tip has dissolved.
                    const int segments = 5;
                    float tailAge = Mathf.Max(0f, age - 0.26f);
                    Vector3 previous = ScatterPoint(emitter, handle1, handle2, end, ScatterTravel(tailAge));
                    for (int segment = 1; segment <= segments; segment++)
                    {
                        float fraction = segment / (float)segments;
                        float sampleAge = Mathf.Lerp(tailAge, age, fraction);
                        Vector3 point = ScatterPoint(emitter, handle1, handle2, end, ScatterTravel(sampleAge));
                        float width = (0.025f + fraction * 0.105f) * size * fade;
                        float alpha = fade * fraction * (wave == 0 ? 0.9f : 0.7f);
                        DrawScatterTrailSegment(previous, point, width, alpha);
                        previous = point;
                    }

                    float arrowAlpha = 1f - Mathf.SmoothStep(0.94f, 1.07f, age);
                    if (arrowAlpha > 0.01f)
                        DrawScatterArrow(head, tangent, size, arrowAlpha);

                    // A detached ember drifts off each ribbon, instead of ending
                    // every arrow in a rigid spoke or a synchronized blast ring.
                    float emberT = ScatterTravel(Mathf.Max(0f, age - 0.16f));
                    Vector3 ember = ScatterPoint(emitter, handle1, handle2, end, emberT);
                    Vector3 side = new Vector3(tangent.z, 0f, -tangent.x);
                    ember += side * (Mathf.Sin(i * 2.4f + age * 5f) * 0.16f * Mathf.Sin(emberT * Mathf.PI));
                    ember.y += 0.03f;
                    DrawFocusSpark(ember, tangent, 0.09f * size * fade, fade * 0.8f);
                }
            }
            float release = 1f - Mathf.SmoothStep(0f, 0.22f, progress);
            if (release > 0.01f)
                DrawFocusSpark(emitter, direction, 0.45f * release, release);
        }

        public static void DrawScatterCharge(Vector3 origin, Vector3 direction, float arcDegrees, float progress)
        {
            direction = direction.Yto0().normalized;
            if (direction.sqrMagnitude < 0.001f) return;
            Vector3 emitter = EmitterPosition(origin, direction);
            float charge = Mathf.SmoothStep(0f, 1f, progress);
            Vector3 side = new Vector3(direction.z, 0f, -direction.x);
            // Ghost arrows gather close to the drawn bow, then the volley opens
            // into the full fan on release. Range geometry is only a target preview.
            for (int i = -2; i <= 2; i++)
            {
                float spread = i / 2f;
                Vector3 aim = Rotate(direction, spread * arcDegrees * 0.13f);
                Vector3 point = emitter + side * (spread * (0.22f + charge * 0.16f))
                    + direction * (0.38f - charge * 0.28f + Mathf.Abs(spread) * 0.1f);
                DrawScatterArrow(point, aim, 0.65f + charge * 0.25f, 0.2f + charge * 0.65f);
                Vector3 spark = point - aim * (0.65f - charge * 0.4f);
                DrawFocusSpark(spark, aim, 0.08f + charge * 0.06f, 0.3f + charge * 0.6f);
            }
        }

        private static float ScatterTravel(float age)
        {
            return 1f - Mathf.Pow(1f - Mathf.Clamp01(age), 1.35f);
        }

        private static Vector3 ScatterPoint(Vector3 start, Vector3 handle1, Vector3 handle2, Vector3 end, float t)
        {
            float u = 1f - t;
            return u * u * u * start + 3f * u * u * t * handle1 + 3f * u * t * t * handle2 + t * t * t * end;
        }

        private static Vector3 ScatterTangent(Vector3 start, Vector3 handle1, Vector3 handle2, Vector3 end, float t)
        {
            float u = 1f - t;
            return (3f * u * u * (handle1 - start) + 6f * u * t * (handle2 - handle1)
                + 3f * t * t * (end - handle2)).Yto0().normalized;
        }

        private static void DrawScatterArrow(Vector3 point, Vector3 direction, float size, float alpha)
        {
            if (direction.sqrMagnitude < 0.001f || alpha <= 0.01f) return;
            point.y += 0.025f;
            Material material = GetMaterial(ref scatterArrowMaterials, Color.white, ShaderDatabase.Transparent, alpha,
                "MiliraXianMingyuan/Projectile/RainbowArrow");
            Matrix4x4 matrix = Matrix4x4.TRS(point, Quaternion.LookRotation(direction), new Vector3(size, 1f, size * 1.35f));
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0);
        }

        private static void DrawScatterTrailSegment(Vector3 start, Vector3 end, float width, float alpha)
        {
            if ((end - start).sqrMagnitude < 0.0001f || alpha <= 0.01f || width <= 0.001f) return;
            GenDraw.DrawLineBetween(start, end, GetFlameMaterial(alpha * 0.7f), width);
            start.y += 0.012f;
            end.y += 0.012f;
            GenDraw.DrawLineBetween(start, end, GetFocusMaterial(alpha), width * 0.32f);
        }

        private static void DrawArc(
            Vector3 origin,
            Vector3 direction,
            float radius,
            float arcDegrees,
            int segments,
            Material material,
            float width)
        {
            if (radius <= 0.01f || material == null || segments < 2)
            {
                return;
            }

            float startAngle = -arcDegrees * 0.5f;
            Vector3 previous = origin + Rotate(direction, startAngle) * radius;
            for (int i = 1; i <= segments; i++)
            {
                float angle = startAngle + arcDegrees * (i / (float)segments);
                Vector3 current = origin + Rotate(direction, angle) * radius;
                GenDraw.DrawLineBetween(previous, current, material, width);
                previous = current;
            }
        }

        private static Vector3 Rotate(Vector3 direction, float degrees)
        {
            float radians = degrees * Mathf.Deg2Rad;
            float sin = Mathf.Sin(radians);
            float cos = Mathf.Cos(radians);
            return new Vector3(
                direction.x * cos + direction.z * sin,
                0f,
                direction.z * cos - direction.x * sin);
        }

        private static Material GetFocusMaterial(float alpha)
        {
            return GetMaterial(ref focusMaterials, FocusColor, ShaderDatabase.MoteGlow, alpha);
        }

        private static Material GetFlameMaterial(float alpha)
        {
            return GetMaterial(ref flameMaterials, FlameColor, ShaderDatabase.MoteGlow, alpha);
        }

        private static Material GetMaterial(
            ref Material[] cache,
            Color baseColor,
            Shader shader,
            float alpha,
            string texturePath = LineTexturePath)
        {
            if (cache == null)
            {
                cache = new Material[AlphaSteps];
                for (int i = 0; i < cache.Length; i++)
                {
                    float stepAlpha = baseColor.a * ((i + 1f) / cache.Length);
                    Color color = new(baseColor.r, baseColor.g, baseColor.b, stepAlpha);
                    cache[i] = MaterialPool.MatFrom(texturePath, shader, color);
                }
            }

            int index = Mathf.Clamp(Mathf.CeilToInt(Mathf.Clamp01(alpha) * AlphaSteps) - 1, 0, AlphaSteps - 1);
            return cache[index];
        }
    }
}

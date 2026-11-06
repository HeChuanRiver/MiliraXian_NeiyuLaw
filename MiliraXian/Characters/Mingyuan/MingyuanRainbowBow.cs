using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.Mingyuan
{
    public class CompProperties_MingyuanRainbowBow : CompProperties
    {
        public float focusBaseDamage = 40f;
        public float focusDamagePerLayer = 0.6f;
        public float focusMaxDamage = 220f;
        public float burstThresholdFraction = 0.60f;
        public float focusLayerFraction = 0.29f;
        public SoundDef focusChargeSound;
        public SoundDef focusFireSound;
        public ThingDef visualControllerDef;

        public CompProperties_MingyuanRainbowBow() => compClass = typeof(CompEquippable_MingyuanRainbowBow);

        public float FocusDamageFor(float layers) =>
            Mathf.Clamp(focusBaseDamage + Mathf.Max(0f, layers) * focusDamagePerLayer, 0f, focusMaxDamage);
    }

    public class CompEquippable_MingyuanRainbowBow : CompEquippable
    {
        public CompProperties_MingyuanRainbowBow PropsBow => (CompProperties_MingyuanRainbowBow)props;
    }

    public class Verb_MingyuanRainbowBow : Verb
    {
        // Read the previous in-flight mode so a saved scatter order cannot fire as focus.
        private enum LegacyCastMode : byte { Focus, Radiation }
        private LegacyCastMode legacyCastMode;
        private bool automaticFocusTarget;
        private CompProperties_MingyuanRainbowBow PropsBow =>
            (EquipmentCompSource as CompEquippable_MingyuanRainbowBow)?.PropsBow;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref automaticFocusTarget, "mx_mingyuanBowAutomaticFocusTarget");
            if (Scribe.mode == LoadSaveMode.LoadingVars)
                Scribe_Values.Look(ref legacyCastMode, "mx_mingyuanBowCastMode", LegacyCastMode.Focus);
            if (Scribe.mode == LoadSaveMode.PostLoadInit && legacyCastMode == LegacyCastMode.Radiation)
            {
                if (WarmupStance != null) CasterPawn.stances.CancelBusyStanceHard();
                Reset();
            }
        }

        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false,
            bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            automaticFocusTarget = CasterPawn?.jobs?.curJob?.playerForced != true;
            if (automaticFocusTarget)
            {
                Pawn best = TryFindBestFocusTarget();
                if (best == null) return false;
                castTarg = best;
            }

            if (!base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast))
                return false;

            if (WarmupStance != null)
            {
                SpawnVisual(MingyuanBowVisualKind.FocusCharge, DirectionTo(castTarg.Cell), WarmupTicksLeft, this, castTarg.Pawn);
                PlaySound(PropsBow?.focusChargeSound);
            }
            return true;
        }

        public override bool CanHitTargetFrom(IntVec3 root, LocalTargetInfo target)
        {
            Map map = caster?.Map;
            Pawn pawn = target.Pawn;
            return map != null && IsValidFocusTarget(pawn) && pawn.Map == map
                && base.CanHitTargetFrom(root, target)
                && GenSight.LineOfSightToThing(root, pawn, map, skipFirstCell: true);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            if (!base.ValidateTarget(target, showMessages)) return false;
            if (IsValidFocusTarget(target.Pawn)) return CanHitTargetFrom(caster.Position, target);
            if (showMessages)
                Messages.Message("MX_Mingyuan_Bow_InvalidFocusTarget".Translate(), MessageTypeDefOf.RejectInput, false);
            return false;
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            if (caster?.Spawned != true || !target.IsValid) return;
            GenDraw.DrawTargetHighlight(target);
            MingyuanBowVisualDrawer.DrawFocusAim(caster.DrawPos, target.CenterVector3, DirectionTo(target.Cell));
        }

        public override void Reset()
        {
            base.Reset();
            automaticFocusTarget = false;
        }

        protected override bool TryCastShot()
        {
            if (PropsBow is not { } bow || CasterPawn?.Spawned != true) return false;
            Pawn target = currentTarget.Pawn;
            if (!CanResolveFocusTarget(target))
            {
                if (!automaticFocusTarget) return false;
                target = TryFindBestFocusTarget();
                if (target == null) return false;
                currentTarget = target;
            }

            Vector3 targetPosition = target.DrawPos;
            Vector3 direction = DirectionTo(target.Position);
            Pawn shooter = CasterPawn;
            if (MingyuanPowerBalance.Sealed)
            {
                ShotReport report = ShotReport.HitReportFor(shooter, this, target);
                if (Rand.Chance(report.AimOnTargetChance_IgnoringPosture * report.PassCoverChance))
                    MingyuanUtility.ApplyDamageWithoutLifeBurn(target, new DamageInfo(MingyuanPowerBalance.ArrowDamage,
                        bow.focusBaseDamage, 0.1f, -1f, shooter, weapon: EquipmentSource?.def));
            }
            else
            {
                float layers = MingyuanUtility.GetLifeBurnLayers(target);
                float threshold = MingyuanUtility.GetLifeBurnExecuteThreshold(target);
                if (layers >= threshold * bow.burstThresholdFraction)
                {
                    MingyuanUtility.TryTriggerLifeBurnBurst(target, shooter);
                }
                else
                {
                    float factor = EquipmentSource?.GetStatValue(StatDefOf.RangedWeapon_DamageMultiplier) ?? 1f;
                    MingyuanUtility.ApplyTrueDamage(target, MingyuanPowerBalance.ArrowDamage, bow.FocusDamageFor(layers) * factor, shooter);
                    if (!target.Dead && !target.Destroyed)
                    {
                        MingyuanUtility.ConsumeLifeBurn(target, layers);
                        MingyuanUtility.AddLifeBurn(target, shooter, threshold * bow.focusLayerFraction, scaleWithOverburn: true);
                    }
                }
            }

            SpawnVisual(MingyuanBowVisualKind.FocusShot, direction, 24, null, null, targetPosition);
            PlaySound(bow.focusFireSound);
            lastShotTick = Find.TickManager.TicksGame;
            return true;
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
            return target != null
                   && target.Spawned
                   && !target.Destroyed
                   && !target.Dead
                   && !target.IsPsychologicallyInvisible()
                   && MingyuanUtility.IsHostilePawn(target, CasterPawn, out _);
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

        private void SpawnVisual(MingyuanBowVisualKind kind, Vector3 direction, int duration, Verb sourceVerb,
            Pawn focusTarget, Vector3? shotEnd = null)
        {
            ThingDef visualDef = PropsBow?.visualControllerDef;
            Pawn source = CasterPawn;
            if (visualDef == null || source?.Spawned != true) return;

            var visual = (Thing_MingyuanBowVisual)ThingMaker.MakeThing(visualDef);
            visual.Init(source, sourceVerb, kind, direction, duration, focusTarget);
            if (shotEnd is { } end)
                visual.SetShotPath(MingyuanBowVisualDrawer.EmitterPosition(source.DrawPos, direction), end);
            GenSpawn.Spawn(visual, source.Position, source.Map);
        }

        private void PlaySound(SoundDef soundDef)
        {
            if (soundDef != null && CasterPawn?.Map is { } map)
                soundDef.PlayOneShot(new TargetInfo(CasterPawn.Position, map));
        }
    }

    public enum MingyuanBowVisualKind : byte
    {
        FocusCharge,
        // Serialized names and ordinals for retiring visuals from old saves.
        RadiationWarning,
        RadiationBlast,
        FocusShot,
        RadiationHit
    }

    public class Thing_MingyuanBowVisual : Thing
    {
        private Pawn source;
        private Verb sourceVerb;
        private Pawn focusTarget;
        private MingyuanBowVisualKind kind;
        private Vector3 direction;
        private int startTick;
        private int durationTicks;
        private Vector3 shotStart;
        private Vector3 shotEnd;

        private bool WarmupValid => source is { Spawned: true, Dead: false } && source.Map == Map
            && focusTarget is { Spawned: true, Dead: false } && focusTarget.Map == Map
            && sourceVerb?.WarmingUp == true;
        private int Age => Find.TickManager.TicksGame - startTick;

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
            Scribe_Values.Look(ref kind, "kind", MingyuanBowVisualKind.FocusCharge);
            Scribe_Values.Look(ref direction, "direction");
            Scribe_Values.Look(ref startTick, "startTick");
            Scribe_Values.Look(ref durationTicks, "durationTicks", 1);
            Scribe_Values.Look(ref shotStart, "shotStart");
            Scribe_Values.Look(ref shotEnd, "shotEnd");
        }

        internal void Init(Pawn newSource, Verb verb, MingyuanBowVisualKind visualKind, Vector3 aim, int duration, Pawn target)
        {
            source = newSource;
            sourceVerb = verb;
            focusTarget = target;
            kind = visualKind;
            direction = aim;
            startTick = Find.TickManager.TicksGame;
            durationTicks = Mathf.Max(1, duration);
        }

        protected override void Tick()
        {
            base.Tick();
            if (Age >= durationTicks || (kind == MingyuanBowVisualKind.FocusCharge && Age > 1 && !WarmupValid)
                || kind is not (MingyuanBowVisualKind.FocusCharge or MingyuanBowVisualKind.FocusShot))
                Destroy(DestroyMode.Vanish);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            float progress = Mathf.Clamp01(Age / (float)Mathf.Max(1, durationTicks));
            if (kind == MingyuanBowVisualKind.FocusShot)
            {
                MingyuanBowVisualDrawer.DrawFocusArrow(shotStart, shotEnd, progress);
                return;
            }
            if (kind != MingyuanBowVisualKind.FocusCharge || !WarmupValid) return;

            Vector3 aim = (focusTarget.DrawPos - source.DrawPos).Yto0().normalized;
            if (aim.sqrMagnitude < 0.001f) aim = direction;
            MingyuanBowVisualDrawer.DrawFocusCharge(source.DrawPos, aim, progress);
            MingyuanBowVisualDrawer.DrawFocusAim(source.DrawPos, focusTarget.DrawPos, aim, progress, Age / 60f);
        }
    }
}

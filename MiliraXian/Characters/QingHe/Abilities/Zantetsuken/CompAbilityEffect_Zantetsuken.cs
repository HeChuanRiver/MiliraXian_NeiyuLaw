using System.Collections.Generic;
using MiliraXian.Characters.Common.PerkSystem;
using MiliraXian.Characters.Common.SweepDamage;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Common.Vfx.BladeMotion;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Things.Weapons;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Abilities.Zantetsuken
{
    /// <summary>Times are seconds after the arrival at the dash end.</summary>
    public class CompProperties_AbilityZantetsuken : CompProperties_AbilityEffect
    {
        public float dashSpeedCellsPerSecond = 40f;
        public int dashMinTicks = 8;
        public SweepDamageDef dashBurst;
        public float dashCollisionRadius = 1.35f;
        public float dashDamage = 5f;
        public float approachRadius = 3f;

        public float takeoffSeconds = 0.10f;
        public float apexSeconds = 0.60f;
        public float fallSeconds = 0.75f;
        public float landSeconds = 0.88f;
        public float holdSeconds = 1.7f;
        public float firstHitDelay = 0.3f;
        public float hitInterval = 0.08f;
        public int landHits = 5;
        public float plainSheatheSeconds = 0.30f;
        public float plainFadeSeconds = 0.6f;
        public float scoopRiseSeconds = 0.5f;
        public float scoopSwingSeconds = 0.30f;
        public float turnBackSeconds = 0.10f;
        public float turnSouthSeconds = 0.10f;
        public float sheatheSeconds = 1.0f;
        public float sheatheAlignShare = 0.25f;
        public float gaeshiHoldSeconds = 0.4f;
        public float dropSeconds = 0.30f;
        public float burstSeconds = 0.6f;

        public float apexHeight = 8f;
        public float gaeshiHeight = 2.2f;
        public float roofedHeight = 1.2f;

        public SweepDamageDef burstDamage;
        public DamageDef landDamageDef;
        public DamageDef gaeshiDamageDef;
        public float lotusRadius = 3f;
        public float landDamage = 20f;
        public float gaeshiDamage = 100f;
        public float armorPenetration = 1.35f;
        public float hitPosture = 10f;
        public float gaeshiExtraPosture = 30f;
        public int stunTicks = 300;
        public int gaeshiStunTicks = 480;
        public float minSwordPressure = 1f;
        public CharacterPerkNodeDef gaeshiNode;

        public float heightToScreen = 0.35f;
        public float depthToScreen = 0.55f;
        public float heightOrigin = -0.63f;
        public BladeProjection Projection => new(heightToScreen, depthToScreen, heightOrigin);

        public SoundDef dashHitSound;
        public SoundDef landSound;
        public SoundDef hitSound;
        public SoundDef clickSound;
        public SoundDef burstSound;

        public float landShake = 0.1f;
        public float hitShake = 0.035f;
        public float gaeshiShake = 0.15f;

        public ZantetsukenVisualProps visuals = new();

        public CompProperties_AbilityZantetsuken()
        {
            compClass = typeof(CompAbilityEffect_Zantetsuken);
        }

        public float HoldEnd => landSeconds + holdSeconds;
        public float HitTime(int i) => landSeconds + firstHitDelay + i * hitInterval;
        public float ScoopEnd => HoldEnd + scoopRiseSeconds;
        public float ClickTime => ScoopEnd + sheatheSeconds;
        public float DropStart => ClickTime + gaeshiHoldSeconds;
        public float GaeshiEnd => DropStart + dropSeconds;
        public float PlainSheatheEnd => HoldEnd + plainSheatheSeconds;
        public float EndSeconds(bool gaeshi) => gaeshi ? GaeshiEnd : PlainSheatheEnd;

        public override IEnumerable<string> ConfigErrors(AbilityDef parentDef)
        {
            foreach (string e in base.ConfigErrors(parentDef))
            {
                yield return e;
            }
            if (dashBurst == null || burstDamage == null || landDamageDef == null || gaeshiDamageDef == null || gaeshiNode == null)
            {
                yield return "dashBurst, burstDamage, landDamageDef, gaeshiDamageDef and gaeshiNode must be set";
            }
            if (visuals.markVfx == null || visuals.gaeshiMarkVfx == null || visuals.splitVfx == null || visuals.bloom == null)
            {
                yield return "visuals need markVfx, gaeshiMarkVfx, splitVfx and bloom";
            }
            if (!(takeoffSeconds < apexSeconds && apexSeconds <= fallSeconds && fallSeconds < landSeconds))
            {
                yield return "timeline must be takeoff < apex <= fall < land";
            }
            if (HitTime(landHits - 1) > HoldEnd)
            {
                yield return "the landing hits must end within the hold";
            }
        }
    }

    public class ZantetsukenVisualProps
    {
        public Color pink = new(1f, 0.62f, 0.80f, 1f);
        public Color white = new(1f, 0.95f, 0.98f, 1f);
        public Color red = new(1f, 0.36f, 0.52f, 1f);
        public Color blue = new(0.62f, 0.55f, 1f, 1f);
        public Color blueWhite = new(0.92f, 0.89f, 1f, 1f);
        public Color blueDeep = new(0.45f, 0.32f, 1f, 1f);
        public float blueRamp = 0.5f;
        public float lotusBase = 0.45f;
        public float landFlareSeconds = 0.5f;
        public float curtainHeight = 2.6f;
        public float curtainLift = 0.5f;
        public float rimWidth = 0.07f;
        public float rimStrength = 1.2f;
        public QingheImpactVfxProps markVfx;
        public QingheImpactVfxProps gaeshiMarkVfx;
        public float markLengthShare = 0.8f;
        public float markTilt = 0.9f;
        public float markLift = 0.2f;
        public QingheImpactVfxProps splitVfx;
        public float splitDelay = 0.12f;
        public float splitForward = 0.85f;
        public string glowTexPath = "MiliraXianQinghe/Effect/SakuraDance/Glow";
        public Color afterimageTint = new(1f, 0.69f, 0.84f, 1f);
        public ScreenBloomProperties bloom;
    }

    public class CompAbilityEffect_Zantetsuken : CompAbilityEffect
    {
        private static readonly Color PreviewColor = new(1f, 0.45f, 0.65f, 0.55f);

        public new CompProperties_AbilityZantetsuken Props => (CompProperties_AbilityZantetsuken)props;

        private bool LacksPressure =>
            Hediffs.MX_QH_HediffUtility.GetSwordPressure(parent.pawn).CurrentValue < Props.minSwordPressure - 1e-4f;

        public override bool GizmoDisabled(out string reason)
        {
            if (LacksPressure)
            {
                reason = "MX_QH_SwordPressureNotEnough".Translate(Props.minSwordPressure.ToString("0.#"));
                return true;
            }
            return base.GizmoDisabled(out reason);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent.pawn;
            return target.Cell != caster.Position && !LacksPressure
                && (target.HasThing || target.Cell.Standable(caster.Map))
                && base.Valid(target, throwMessages);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent.pawn;
            if (target.Cell == caster.Position)
            {
                return;
            }
            if (TryFindApproach(caster, target, out IntVec3 stand))
            {
                GenDraw.DrawLineBetween(caster.Position.ToVector3Shifted(), stand.ToVector3Shifted());
            }
            GenDraw.DrawRadiusRing(target.Cell, Props.lotusRadius, PreviewColor);
        }

        /// <summary>The free cell within <see cref="CompProperties_AbilityZantetsuken.approachRadius"/> of the target nearest to it that she can see.</summary>
        private bool TryFindApproach(Pawn caster, LocalTargetInfo target, out IntVec3 stand)
        {
            Map map = caster.Map;
            CellRect rect = target.HasThing ? target.Thing.OccupiedRect() : CellRect.SingleCell(target.Cell);
            Vector3 at = target.HasThing ? GenThing.TrueCenter(target.Thing) : target.Cell.ToVector3Shifted();
            float radius = Props.approachRadius;
            float best = float.MaxValue;
            stand = IntVec3.Invalid;
            foreach (IntVec3 c in rect.ExpandedBy(Mathf.CeilToInt(radius)).Cells)
            {
                if (!c.InBounds(map) || rect.ClosestCellTo(c).DistanceTo(c) > radius)
                {
                    continue;
                }
                float d = (c.ToVector3Shifted() - at).sqrMagnitude;
                if (d >= best || !c.Standable(map) || Occupied(caster, map, c) || !GenSight.LineOfSight(caster.Position, c, map))
                {
                    continue;
                }
                best = d;
                stand = c;
            }
            return stand.IsValid;
        }

        private static bool Occupied(Pawn caster, Map map, IntVec3 c)
        {
            foreach (Thing t in c.GetThingList(map))
            {
                if (t is Pawn p && p != caster && !p.Dead)
                {
                    return true;
                }
            }
            return false;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent.pawn;
            if (!TryFindApproach(caster, target, out IntVec3 stand))
            {
                Messages.Message("MX_QH_ZantetsukenNoRoom".Translate(), caster, MessageTypeDefOf.RejectInput, historical: false);
                parent.ResetCooldown();
                return;
            }
            caster.rotationTracker.FaceCell(target.Cell);
            Job job = JobMaker.MakeJob(MX_QHDefOf.MX_QH_Zantetsuken_Guren);
            job.ability = parent;
            job.SetTarget(TargetIndex.A, stand);
            job.SetTarget(TargetIndex.B, target.Cell);
            job.playerForced = true;
            caster.jobs.StartJob(job, JobCondition.InterruptForced, cancelBusyStances: false, tag: JobTag.Misc);
        }
    }
}

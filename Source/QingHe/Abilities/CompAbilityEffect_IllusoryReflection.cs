using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Things.Weapons;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Abilities
{
    public class CompProperties_AbilityIllusoryReflection : CompProperties_AbilityEffect
    {
        public int durationTicks = 300;
        public float normalDamage = 32f;
        public float empoweredDamage = 48f;
        public float armorPenetration = 0.45f;
        public float coneRadius = 5.5f;
        public float coneAngleDegrees = 90f;
        public HediffDef invulnerabilityHediff;
        public int invulnerabilityTicks = 120;
        public SoundDef stanceSound;
        public SoundDef slashSound;
        public FleckDef normalSlashFleck;
        public float normalSlashVisualScale = 3.2f;
        public float normalSlashVisualForwardOffset;
        public float normalSlashVisualAngleOffsetDegrees;
        public string mirrorSlashTexPath = "MiliraXianQinghe/Effect/flower_divination_slash_2";
        public float mirrorSlashRevealSeconds = 0.24f;
        public float mirrorSlashHoldSeconds = 0.1f;
        public float mirrorSlashFadeSeconds = 0.46f;
        public float mirrorSlashDrawSizeFactor = 1.45f;
        public float mirrorSlashForwardOffsetFactor = 0.12f;
        public float mirrorSlashAngleOffsetDegrees;
        public float mirrorSlashHeadWidth = 0.05f;
        public float mirrorSlashHeadIntensity = 3f;
        public float mirrorSlashAdditiveIntensity = 1.25f;
        public float mirrorSlashDistortionStrength = 0.025f;
        public float mirrorSlashDistortionScale = 0.4f;
        public float mirrorSlashDistortionScrollX = 0.07f;
        public float mirrorSlashDistortionScrollY;
        public float mirrorSlashDistortionOpacity = 0.65f;

        public CompProperties_AbilityIllusoryReflection()
        {
            compClass = typeof(CompAbilityEffect_IllusoryReflection);
        }
    }

    public class CompAbilityEffect_IllusoryReflection : CompAbilityEffect
    {
        private static readonly Color PreviewColor = new(0.72f, 0.9f, 1f, 0.7f);
        private readonly List<IntVec3> previewCells = new();

        public new CompProperties_AbilityIllusoryReflection Props => (CompProperties_AbilityIllusoryReflection)props;

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            return caster != null
                && target.IsValid
                && target.Cell != caster.Position
                && base.Valid(target, throwMessages);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || !caster.Spawned || !target.IsValid || target.Cell == caster.Position)
            {
                return;
            }

            QingheSwordCombatUtility.FillConeCells(
                caster,
                caster.Position,
                target.Cell,
                Props.coneRadius,
                Props.coneAngleDegrees,
                previewCells);
            GenDraw.DrawFieldEdges(previewCells, PreviewColor);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent?.pawn;
            if (caster?.jobs == null
                || MX_QHDefOf.MX_QH_IllusoryReflection == null
                || !target.IsValid
                || target.Cell == caster.Position)
            {
                return;
            }

            AddInvulnerability(caster);
            caster.rotationTracker?.FaceCell(target.Cell);
            Job stanceJob = JobMaker.MakeJob(MX_QHDefOf.MX_QH_IllusoryReflection);
            stanceJob.ability = parent;
            stanceJob.SetTarget(TargetIndex.A, target.Cell);
            stanceJob.playerForced = true;
            caster.jobs.StartJob(
                stanceJob,
                JobCondition.InterruptForced,
                cancelBusyStances: false,
                tag: JobTag.Misc);
        }

        private void AddInvulnerability(Pawn caster)
        {
            HediffDef hediffDef = Props.invulnerabilityHediff ?? MX_QHDefOf.MX_QH_IllusoryReflectionInvulnerable;
            if (caster?.health?.hediffSet == null || hediffDef == null)
            {
                return;
            }

            Hediff hediff = caster.health.hediffSet.GetFirstHediffOfDef(hediffDef) ?? caster.health.AddHediff(hediffDef);
            hediff?.TryGetComp<HediffComp_Disappears>()?.SetDuration(Mathf.Max(1, Props.invulnerabilityTicks));
        }
    }
}

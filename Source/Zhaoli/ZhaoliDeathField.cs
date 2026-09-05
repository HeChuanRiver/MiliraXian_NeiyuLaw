using System.Collections.Generic;
using HarmonyLib;
using MiliraXian.Characters;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Zhaoli
{
    public class CompProperties_AbilityZhaoliDeathField : CompProperties_AbilityEffect
    {
        public HediffDef fieldHediff;
        public float radius = 9f;

        public CompProperties_AbilityZhaoliDeathField()
        {
            compClass = typeof(CompAbilityEffect_ZhaoliDeathField);
        }
    }

    public class CompAbilityEffect_ZhaoliDeathField : CompAbilityEffect_ZhaoliPowerLimited
    {
        private static readonly Color PreviewColor = new(0.48f, 0.08f, 0.1f);

        private new CompProperties_AbilityZhaoliDeathField Props => (CompProperties_AbilityZhaoliDeathField)props;
        public float PropsRadius => Props.radius;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (ZhaoliPowerBalance.Sealed) return;
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            if (caster == null || caster.health == null || !target.IsValid)
            {
                return;
            }

            HediffWithComps field = caster.health.hediffSet.GetFirstHediffOfDef(Props.fieldHediff) as HediffWithComps;
            if (field == null)
            {
                field = HediffMaker.MakeHediff(Props.fieldHediff, caster) as HediffWithComps;
                if (field == null)
                {
                    return;
                }

                caster.health.AddHediff(field);
            }

            HediffComp_ZhaoliDeathField comp = field.GetComp<HediffComp_ZhaoliDeathField>();
            comp?.ActivateAt(target.Cell);
            if (ZhaoliScenarioUtility.IsRaidState(caster))
            {
                ZhaoliKarmaUtility.AddKarma(caster, ZhaoliScenarioUtility.DeathFieldRaidBonusKarma);
            }

            if (caster.Spawned)
            {
                FleckMaker.Static(target.Cell, caster.Map, FleckDefOf.PsycastAreaEffect, Mathf.Max(1.5f, Props.radius * 0.65f));
                FleckMaker.Static(target.Cell, caster.Map, FleckDefOf.ExplosionFlash, 2.4f);
            }
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                return;
            }

            GenDraw.DrawRadiusRing(target.Cell, Props.radius, PreviewColor);
        }
    }

    public class HediffCompProperties_ZhaoliDeathField : HediffCompProperties
    {
        public float radius = 9f;
        public int fieldDurationTicks = 540;
        public int applyIntervalTicks = 60;
        public HediffDef_Abnormal abnormal;
        public float accumulationPerApplication = 1f;

        public HediffCompProperties_ZhaoliDeathField()
        {
            compClass = typeof(HediffComp_ZhaoliDeathField);
        }
    }

    [HarmonyPatch(typeof(Stance_Warmup), nameof(Stance_Warmup.StanceDraw))]
    internal static class Patch_ZhaoliDeathFieldWarmup_StanceDraw
    {
        private const float WarningPulseCyclesPerSecond = 2f;
        private static readonly Color WarningColorDim = new(0.25f, 0f, 0.1f);
        private static readonly Color WarningColorBright = new(0.46f, 0.04f, 0.04f);

        private static void Postfix(Stance_Warmup __instance)
        {
            Verb_CastAbility verb = __instance?.verb as Verb_CastAbility;
            Ability ability = verb?.Ability;
            Pawn caster = verb?.CasterPawn;
            if (ability?.def != MXZL_ZhaoliDefOf.MX_Zhaoli_DeathField || caster == null || caster.Faction == Faction.OfPlayer)
            {
                return;
            }

            LocalTargetInfo target = __instance.focusTarg;
            if (!target.IsValid)
            {
                return;
            }

            float radius = ability.CompOfType<CompAbilityEffect_ZhaoliDeathField>()?.PropsRadius ?? ZhaoliScenarioUtility.DeathFieldEvaluationRadius;
            float phase = Find.TickManager.TicksGame / (float)GenTicks.TicksPerRealSecond * WarningPulseCyclesPerSecond * Mathf.PI * 2f;
            float pulse = (Mathf.Sin(phase) + 1f) * 0.5f;
            GenDraw.DrawRadiusRing(target.Cell, radius, Color.Lerp(WarningColorDim, WarningColorBright, pulse));
        }
    }

    public class HediffComp_ZhaoliDeathField : HediffComp
    {
        private const int FieldParticleIntervalTicks = 9;
        private const float FieldAreaRotationRate = 360f;

        private Mote fieldAreaMote;
        private int fieldCenterX;
        private int fieldCenterZ;
        private float activeRadius;
        private bool active;

        private HediffCompProperties_ZhaoliDeathField PropsField => (HediffCompProperties_ZhaoliDeathField)props;

        private IntVec3 FieldCenter => new(fieldCenterX, 0, fieldCenterZ);
        public float DefaultRadius => PropsField.radius;
        private float CurrentRadius => ZhaoliPowerBalance.IsOriginal ? (activeRadius > 0f ? activeRadius : PropsField.radius) : PropsField.radius;

        public void ActivateAt(IntVec3 center, float radiusOverride = -1f)
        {
            if (ZhaoliPowerBalance.Sealed) return;
            fieldCenterX = center.x;
            fieldCenterZ = center.z;
            activeRadius = radiusOverride > 0f ? radiusOverride : PropsField.radius;
            active = true;
            fieldAreaMote = null;
            parent.TryGetComp<HediffComp_Disappears>()?.SetDuration(PropsField.fieldDurationTicks);
        }

        public override bool CompDisallowVisible()
        {
            return true;
        }

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref fieldCenterX, "fieldCenterX", 0);
            Scribe_Values.Look(ref fieldCenterZ, "fieldCenterZ", 0);
            Scribe_Values.Look(ref activeRadius, "activeRadius", 0f);
            Scribe_Values.Look(ref active, "active", false);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (ZhaoliPowerBalance.Sealed) { active = false; Pawn?.health?.RemoveHediff(parent); return; }
            if (!active || Pawn == null || Pawn.Dead)
            {
                return;
            }

            Map map = Pawn.MapHeld;
            IntVec3 center = FieldCenter;
            if (map == null || !center.IsValid || !center.InBounds(map))
            {
                return;
            }

            bool usingUnityVfx = MiliraXian.Characters.CharacterUnityVfxRuntime.TryMaintainWorld(
                MiliraXian.Characters.CharacterUnityVfxKind.ZhaoliDeathField,
                Pawn,
                map,
                center,
                CurrentRadius / 9f,
                360);
            if (!usingUnityVfx)
            {
                MaintainFieldArea(map, center);
                MaintainFieldParticles(map, center);
                if (Find.TickManager != null && Find.TickManager.TicksGame % 60 == 0)
                {
                    FleckDef deathPulse = ZhaoliEffectUtility.DeathRefusalPulseFleckDef;
                    if (deathPulse != null)
                    {
                        FleckMaker.Static(center, map, deathPulse, Mathf.Max(2f, CurrentRadius * 0.55f));
                    }
                }
            }

            if (PropsField.applyIntervalTicks <= 0 || Find.TickManager == null || Find.TickManager.TicksGame % PropsField.applyIntervalTicks != 0)
            {
                return;
            }

            IReadOnlyList<Pawn> allPawnsSpawned = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < allPawnsSpawned.Count; i++)
            {
                Pawn pawn = allPawnsSpawned[i];
                if (pawn == null || pawn == Pawn || pawn.Destroyed || pawn.Dead)
                {
                    continue;
                }

                if (!ZhaoliScenarioUtility.ShouldDeathFieldAffectTarget(Pawn, pawn))
                {
                    continue;
                }

                if (pawn.Position.InHorDistOf(center, CurrentRadius))
                {
                    AbnormalSystem.ApplyAccumulation(Pawn, pawn, PropsField.abnormal, PropsField.accumulationPerApplication);
                }
            }
        }

        private void MaintainFieldArea(Map map, IntVec3 center)
        {
            ThingDef areaDef = ZhaoliEffectUtility.DeathFieldAreaMoteDef;
            if (areaDef == null)
            {
                return;
            }

            if (fieldAreaMote == null || fieldAreaMote.Destroyed)
            {
                fieldAreaMote = MoteMaker.MakeStaticMote(center, map, areaDef, 1f);
                if (fieldAreaMote != null)
                {
                    fieldAreaMote.exactRotation = Rand.Range(0f, 360f);
                    fieldAreaMote.rotationRate = FieldAreaRotationRate;
                }
            }

            fieldAreaMote?.Maintain();
        }

        private void MaintainFieldParticles(Map map, IntVec3 center)
        {
            if (Find.TickManager == null || Find.TickManager.TicksGame % FieldParticleIntervalTicks != 0)
            {
                return;
            }

            ThingDef particleDef = ZhaoliEffectUtility.RandomDeathFieldParticleMoteDef;
            if (particleDef == null)
            {
                return;
            }

            Vector3 loc = center.ToVector3Shifted() + Rand.InsideUnitCircleVec3 * Mathf.Max(0.1f, CurrentRadius * 0.92f);
            Mote mote = MoteMaker.MakeStaticMote(loc, map, particleDef, Rand.Range(0.85f, 1.25f), false, Rand.Range(0f, 360f));
            if (mote != null)
            {
                mote.rotationRate = Rand.Range(-45f, 45f);
            }
        }
    }
}

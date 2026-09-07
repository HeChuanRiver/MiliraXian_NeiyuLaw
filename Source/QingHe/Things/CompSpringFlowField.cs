using Verse;

using System.Collections.Generic;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Vfx;
using RimWorld;
using UnityEngine;

namespace MiliraXian.Characters.QingHe.Things
{
    public class CompProperties_SpringFlowField : CompProperties
    {
        public float radius = 4.0f;
        public int pulseIntervalTicks = 10;
        public int fadeInTicks = 30;
        public int fadeOutTicks = 45;
        public int ambientVisualIntervalTicks = 45;
        public int ambientVisualFlecksPerBurst = 2;
        public List<HediffDef_Abnormal> enhancedBleedAbnormals = new();
        public float enhancedBleedAccumulationAmount = 8f;
        public List<HediffDef_Abnormal> enhancedToxinAbnormals = new();
        public float enhancedToxinAccumulationAmount = 8f;
        public ThingDef fieldMoteDef;
        public FleckDef ambientSplashFleckDef;

        public CompProperties_SpringFlowField()
        {
            compClass = typeof(CompSpringFlowField);
        }
    }
    
    public class CompSpringFlowField : ThingComp
    {
        private Pawn caster;
        private int ticksToNextEffect;
        private int lifetimeTicks;
        private int ageTicks;
        private int ticksToNextAmbientVisual;
        private Verse.Mote fieldMote;
        
        public CompProperties_SpringFlowField Props => (CompProperties_SpringFlowField)props;
        public float CurrentRadius => Props.radius;
        public float VisualAlpha
        {
            get
            {
                float fadeIn = Props.fadeInTicks > 0 ? Mathf.Clamp01(ageTicks / (float)Props.fadeInTicks) : 1f;
                float fadeOut = Props.fadeOutTicks > 0 ? Mathf.Clamp01(lifetimeTicks / (float)Props.fadeOutTicks) : 1f;
                return Mathf.Min(fadeIn, fadeOut);
            }
        }

        public override void PostDraw()
        {
            base.PostDraw();
            if (parent?.Map == null)
            {
                return;
            }

            float pulse = 0.28f + Mathf.Sin(Find.TickManager.TicksGame * 0.06666667f) * 0.08f;
            var color = new Color(1f, 0.68f, 0.82f, pulse * VisualAlpha);
            var material = MX_QHGraphicsUtility.FieldEdgeMaterial();
            MX_QHGraphicsUtility.DrawRadiusRingWithMaterial(parent.Position, CurrentRadius, material, color, parent.Map);
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent.Destroyed || parent.Map == null)
            {
                return;
            }
            ageTicks++;
            MaintainFieldMote();
            lifetimeTicks--;
            if (lifetimeTicks <= 0)
            {
                parent.Destroy();
                return;
            }
            if (ticksToNextEffect == 0)
            {
                ticksToNextEffect = Props.pulseIntervalTicks;
                AttachEffect();
            }

            ticksToNextEffect--;
            if (ticksToNextAmbientVisual <= 0)
            {
                ticksToNextAmbientVisual = Props.ambientVisualIntervalTicks + Rand.RangeInclusive(-12, 18);
                PlayAmbientVisual();
            }

            ticksToNextAmbientVisual--;
        }
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look<Pawn>(ref caster, "caster", false);
            Scribe_Values.Look<int>(ref ticksToNextEffect, "ticksToNextEffect", 0, false);
            Scribe_Values.Look<int>(ref lifetimeTicks, "lifetimeTicks", 0, false);
            Scribe_Values.Look<int>(ref ageTicks, "ageTicks", 0, false);
            Scribe_Values.Look<int>(ref ticksToNextAmbientVisual, "ticksToNextAmbientVisual", 0, false);
            Scribe_References.Look<Verse.Mote>(ref fieldMote, "fieldMote", false);
        }

        public void Init(Pawn newCaster)
        {
            caster = newCaster;
            ticksToNextEffect = 1;
            ageTicks = 0;
            ticksToNextAmbientVisual = Rand.RangeInclusive(10, 30);
            SpawnFieldMote();
        }

        public void Init(Pawn newCaster, int durationTicks)
        {
            caster = newCaster;
            lifetimeTicks = durationTicks;
            ticksToNextEffect = 1;
            ageTicks = 0;
            ticksToNextAmbientVisual = Rand.RangeInclusive(10, 30);
            SpawnFieldMote();
        }

        public override bool DontDrawParent()
        {
            return Props.fieldMoteDef != null;
        }
        
        private void AttachEffect()
        {
            Map map = parent?.Map;
            if (map == null || caster == null)
            {
                return;
            }

            float radius = Mathf.Max(0f, CurrentRadius);
            float radiusSquared = radius * radius;
            float effectFactor = ResolveSpecialEffectFactor();
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead)
                {
                    continue;
                }

                int dx = pawn.Position.x - parent.Position.x;
                int dz = pawn.Position.z - parent.Position.z;
                if (dx * dx + dz * dz > radiusSquared)
                {
                    continue;
                }

                if (pawn.Faction == caster.Faction)
                {
                    ApplySpringFlow(pawn, effectFactor);
                    continue;
                }

                if (!GenHostility.HostileTo(caster, pawn))
                {
                    continue;
                }

                ApplyEnhancedAccumulation(pawn, Props.enhancedBleedAbnormals, Props.enhancedBleedAccumulationAmount * effectFactor);
                ApplyEnhancedAccumulation(pawn, Props.enhancedToxinAbnormals, Props.enhancedToxinAccumulationAmount * effectFactor);
            }
        }

        private static void ApplySpringFlow(Pawn pawn, float effectFactor)
        {
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_SpringFlow);
            if (hediff != null)
            {
                hediff.Severity = Mathf.Max(hediff.Severity, effectFactor);
                hediff.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
                return;
            }

            hediff = HediffMaker.MakeHediff(MX_QHDefOf.MX_QH_SpringFlow, pawn);
            hediff.Severity = effectFactor;
            pawn.health.AddHediff(hediff);
        }

        private float ResolveSpecialEffectFactor()
        {
            return MX_QHSkillUtility.GetSpecialAbilityEffectFactor(caster);
        }

        private void ApplyEnhancedAccumulation(Pawn pawn, List<HediffDef_Abnormal> abnormals, float amount)
        {
            if (pawn == null || abnormals == null || amount <= 0f)
            {
                return;
            }

            for (int i = 0; i < abnormals.Count; i++)
            {
                HediffDef_Abnormal abnormal = abnormals[i];
                if (abnormal != null)
                {
                    AbnormalSystem.ApplyAccumulation(caster, pawn, abnormal, amount);
                }
            }
        }

        private void PlayAmbientVisual()
        {
            if (parent?.Map == null || Props.ambientVisualFlecksPerBurst <= 0 || VisualAlpha <= 0.05f)
            {
                return;
            }

            FleckDef splash = Props.ambientSplashFleckDef ?? MX_QHDefOf.GroundWaterSplash;
            if (splash == null)
            {
                return;
            }

            int count = Mathf.Max(1, Props.ambientVisualFlecksPerBurst);
            int maxCells = GenRadial.NumCellsInRadius(CurrentRadius);
            for (int i = 0; i < count; i++)
            {
                IntVec3 cell = parent.Position + GenRadial.RadialPattern[Rand.Range(0, maxCells)];
                if (!cell.InBounds(parent.Map) || !cell.Walkable(parent.Map))
                {
                    continue;
                }

                if (splash != null)
                {
                    FleckMaker.Static(cell, parent.Map, splash, Rand.Range(0.22f, 0.5f) * VisualAlpha);
                }
            }
        }

        private void MaintainFieldMote()
        {
            if (Props.fieldMoteDef == null || parent?.Map == null)
            {
                return;
            }

            if (fieldMote == null || fieldMote.Destroyed)
            {
                SpawnFieldMote();
            }

            if (fieldMote != null && !fieldMote.Destroyed)
            {
                fieldMote.Maintain();
            }
        }

        private void SpawnFieldMote()
        {
            if (Props.fieldMoteDef == null || parent?.Map == null)
            {
                return;
            }

            fieldMote = MoteMaker.MakeAttachedOverlay(parent, Props.fieldMoteDef, Vector3.zero, 1f, -1f);
            fieldMote?.Maintain();
        }
    }
}

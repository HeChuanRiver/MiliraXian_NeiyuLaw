using Verse;

using MiliraXian.Characters.QingHe;
using MiliraXian.Characters.QingHe.Hediffs;
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
        private Mote fieldMote;
        
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
            var material = GraphicsUtility.FieldEdgeMaterial(color);
            GraphicsUtility.DrawRadiusRingWithMaterial(parent.Position, CurrentRadius, material, parent.Map);
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
            Scribe_References.Look<Mote>(ref fieldMote, "fieldMote", false);
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
            var applied = false;
            foreach (var thing in GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, CurrentRadius, true))
            {
                if (!(thing is Pawn pawn) || pawn.Dead || pawn.Faction != caster.Faction)
                {
                    continue;
                }

                if (pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_SpringFlow) is Hediff_SpringFlow h)
                {
                    h.GetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
                }
                else
                {
                    var hediff = (Hediff_SpringFlow)HediffMaker.MakeHediff(MX_QHDefOf.MX_SpringFlow, pawn);
                    pawn.health.AddHediff(hediff);
                }

                applied = true;
            }

            if (!applied)
            {
                return;
            }
        }

        private void PlayAmbientVisual()
        {
            if (parent?.Map == null || Props.ambientVisualFlecksPerBurst <= 0 || VisualAlpha <= 0.05f)
            {
                return;
            }

            FleckDef splash = Props.ambientSplashFleckDef ?? DefDatabase<FleckDef>.GetNamedSilentFail("GroundWaterSplash");
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

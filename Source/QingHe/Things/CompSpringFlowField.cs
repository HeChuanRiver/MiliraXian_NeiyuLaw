using Verse;

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
        public DamageDef enhancedBleedDamageDef = MX_StatusEffectsDefOf.MX_StatusEffectBleedAccumulation;
        public float enhancedBleedDamageAmount = 0.08f;
        public float enhancedBleedArmorPenetration = 2.1f;
        public DamageDef enhancedToxinDamageDef = MX_StatusEffectsDefOf.MX_StatusEffectToxinAccumulation;
        public float enhancedToxinDamageAmount = 0.08f;
        public float enhancedToxinArmorPenetration = 2.1f;
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
        private bool enhanced;
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
            var material = MX_QHGraphicsUtility.FieldEdgeMaterial(color);
            MX_QHGraphicsUtility.DrawRadiusRingWithMaterial(parent.Position, CurrentRadius, material, parent.Map);
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
            Scribe_Values.Look(ref enhanced, "enhanced", false);
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

        public void SetEnhanced(bool value)
        {
            enhanced = value;
        }

        public override bool DontDrawParent()
        {
            return Props.fieldMoteDef != null;
        }
        
        private void AttachEffect()
        {
            foreach (var thing in GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, CurrentRadius, true))
            {
                if (!(thing is Pawn pawn) || pawn.Dead || pawn.Faction != caster.Faction)
                {
                    continue;
                }

                Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_SpringFlow);
                if (hediff != null)
                {
                    hediff.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
                }
                else
                {
                    pawn.health.AddHediff(HediffMaker.MakeHediff(MX_QHDefOf.MX_QH_SpringFlow, pawn));
                }
            }

            ApplyEnhancedHostileEffects();
        }

        private void ApplyEnhancedHostileEffects()
        {
            if (!enhanced || caster == null || parent?.Map == null)
            {
                return;
            }

            foreach (Thing thing in GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, CurrentRadius, true))
            {
                Pawn pawn = thing as Pawn;
                if (pawn == null || pawn.Dead || !GenHostility.HostileTo(caster, pawn))
                {
                    continue;
                }

                ApplyEnhancedDamage(pawn, Props.enhancedBleedDamageDef, Props.enhancedBleedDamageAmount, Props.enhancedBleedArmorPenetration);
                if (pawn.RaceProps?.IsMechanoid != true)
                {
                    ApplyEnhancedDamage(pawn, Props.enhancedToxinDamageDef, Props.enhancedToxinDamageAmount, Props.enhancedToxinArmorPenetration);
                }
            }
        }

        private void ApplyEnhancedDamage(Pawn pawn, DamageDef damageDef, float amount, float armorPenetration)
        {
            if (pawn == null || damageDef == null || amount <= 0f)
            {
                return;
            }

            DamageInfo dinfo = new DamageInfo(damageDef, amount, armorPenetration, -1f, caster);
            pawn.TakeDamage(dinfo);
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

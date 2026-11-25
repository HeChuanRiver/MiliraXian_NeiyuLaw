using Verse;

using System.Collections.Generic;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Vfx;
using RimWorld;
using UnityEngine;
using MiliraXian.Characters.Common.AbnormalSystem;
using MiliraXian.Characters.Common.AbnormalSystem.Effects;
using MiliraXian.Characters.Common.Vfx.Emitter;
using MiliraXian.Characters.QingHe;

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
        public float bleedAccumulationPerSecond = 12f;
        public float electricLeakAccumulationPerSecond = 12f;
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

        /// <summary>
        /// The boundary's emitter. It is maintained every tick while the field is alive and dies with
        /// it: this comp never holds a map-wide renderer, and there is no per-map lookup table.
        /// </summary>
        private VfxEmitterHandle visual;
        
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
            // The boundary is drawn by the emitter system, not here.
        }

        /// <summary>
        /// Keeps the boundary alive: one <c>Maintain</c> per tick is the only lifetime signal the
        /// emitter needs, and the two numbers it reads are written as channels.
        /// </summary>
        private void MaintainVisual()
        {
            Map map = parent?.Map;
            MapComponent_VfxEmitterHost host = map?.GetComponent<MapComponent_VfxEmitterHost>();
            if (host == null)
            {
                return;
            }
            if (visual.IsNone)
            {
                visual = host.Spawn(
                    VfxDefOf.MX_Vfx_SpringFlow,
                    VfxAnchor.Attached(parent));
            }
            if (visual.IsNone)
            {
                return;
            }

            host.Maintain(visual);
            host.SetChannel(visual, VfxChannels.Radius, CurrentRadius);
            host.SetChannel(visual, VfxChannels.Strength, VisualAlpha);
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
            MaintainVisual();
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
            if (ageTicks % 60 == 0 && MX_QH_PerkUtility.HasSeasonalResonance(caster))
            {
                ApplyAbnormals();
            }

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

        public override void PostDeSpawn(Map map, DestroyMode mode)
        {
            base.PostDeSpawn(map, mode);
            if (!visual.IsNone)
            {
                visual.Host.Destroy(visual);
                visual = default;
            }
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
            // The emitter is created on the first tick by MaintainVisual, so nothing has to be
            // registered here and no map-wide table is touched.
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
            float effectFactor = MX_QH_PerkUtility.GetSpellEffectFactor(caster);
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
                }
            }
        }

        private static void ApplySpringFlow(Pawn pawn, float effectFactor)
        {
            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_SpringFlowHealing);
            if (hediff != null)
            {
                hediff.Severity = Mathf.Max(hediff.Severity, effectFactor);
                hediff.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
                return;
            }

            hediff = HediffMaker.MakeHediff(MX_QHDefOf.MX_QH_SpringFlowHealing, pawn);
            hediff.Severity = effectFactor;
            pawn.health.AddHediff(hediff);
        }

        private void ApplyAbnormals()
        {
            float effectFactor = MX_QH_PerkUtility.GetSpellEffectFactor(caster);
            // Short circuits can kill and despawn nearby pawns during this pulse.
            foreach (Pawn pawn in new List<Pawn>(parent.Map.mapPawns.AllPawnsSpawned))
            {
                if (pawn.Dead || !GenHostility.HostileTo(caster, pawn)
                    || pawn.Position.DistanceToSquared(parent.Position) > CurrentRadius * CurrentRadius)
                {
                    continue;
                }

                // Spring owns one condition per kind (2.1): bleed on flesh, electric leak on
                // machinery. The summer pair (toxin, corrosion) belongs to the Lotus Rain wall.
                AbnormalSystem.ApplyAccumulation(caster, pawn, MX_AbnormalDefOf.MX_AbnormalBleed, Props.bleedAccumulationPerSecond * effectFactor);
                AbnormalSystem.ApplyAccumulation(caster, pawn, MX_AbnormalDefOf.MX_AbnormalElectricLeak, Props.electricLeakAccumulationPerSecond * effectFactor);
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

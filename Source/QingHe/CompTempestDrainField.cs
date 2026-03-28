using MiliraXian.Characters;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_TempestDrainField : CompProperties
    {
        public float radius = 12.0f;
        public int pulseIntervalTicks = 10;
        public int damageEveryPulses = 3;
        public float severityPerPulse = 0.01f;
        public float edgeEffect = 0.5f;
        public DamageDef damageDef;
        public float damageAmount = 1f;
        public float armorPenetration = 0.15f;
        public float damageFactorMax = 0.5f;

        public CompProperties_TempestDrainField()
        {
            compClass = typeof(CompTempestDrainField);
        }
    }
    
    public class CompTempestDrainField : ThingComp
    {
        private Pawn caster;
        private int ticksToNextEffect;
        private int damagePulse;
        
        public CompProperties_TempestDrainField Props => (CompProperties_TempestDrainField)props;

        public override void CompTick()
        {
            base.CompTick();
            if (parent.Destroyed || parent.Map == null)
            {
                return;
            }
            if (caster == null || caster.Dead || !caster.Spawned || caster.Downed)
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
        }
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref caster, "caster", false);
            Scribe_Values.Look(ref ticksToNextEffect, "ticksToNextEffect", 0, false);
            Scribe_Values.Look(ref damagePulse, "damagePulse", 0, false);
        }

        public void Init(Pawn newCaster)
        {
            caster = newCaster;
            ticksToNextEffect = 1;
            damagePulse = 0;
        }
        
        private void AttachEffect()
        {
            damagePulse++;
            var dealDamage = damagePulse >= Mathf.Max(1, Props.damageEveryPulses);
            if (dealDamage)
            {
                damagePulse = 0;
            }

            var combat = false;
            var damageDef = Props.damageDef ?? MX_QHDefOf.MX_Dehydrate ?? DamageDefOf.Blunt;
            var damageFactor = EleganceUtility.FactorLinear(Props.damageFactorMax, caster);
            foreach (var thing in GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, Props.radius, true))
            {
                if (!(thing is Pawn pawn) || pawn.Dead || !pawn.HostileTo(caster))
                {
                    continue;
                }

                var d = pawn.Position.DistanceTo(parent.Position);
                var edge = Mathf.Clamp01(Props.edgeEffect);
                var distanceFactor = 1 - d / Props.radius * (1 - edge);
                if (dealDamage && Props.damageAmount > 0f)
                {
                    var result = pawn.TakeDamage(new DamageInfo(damageDef, Props.damageAmount * damageFactor * distanceFactor, Props.armorPenetration, -1f, caster));
                    if (result != null && result.totalDamageDealt > 0f)
                    {
                        combat = true;
                    }
                }

                var h = HediffMaker.MakeHediff(MX_QHDefOf.MX_Draining, pawn);
                h.Severity = Props.severityPerPulse * EleganceUtility.FactorLinear(1.0f, caster) * distanceFactor;
                pawn.health.AddHediff(h);
                var comp = pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_Draining)?.TryGetComp<HediffComp_SeverityPerSecondPausable>();
                comp?.ResetTimer();
            }

            if (combat)
            {
                EleganceUtility.NotifyCombatEvent(caster);
            }
        }
    }
}

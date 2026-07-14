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
            if (ticksToNextEffect <= 0)
            {
                ticksToNextEffect = Mathf.Max(1, Props.pulseIntervalTicks);
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

            bool combat = false;
            DamageDef damageDef = Props.damageDef ?? MX_QHDefOf.MX_Dehydrate ?? DamageDefOf.Blunt;
            float damageFactor = EleganceUtility.FactorLinear(Props.damageFactorMax, caster);
            float radius = Mathf.Max(0.01f, Props.radius);
            float radiusSquared = radius * radius;
            var pawns = parent.Map.mapPawns.AllPawnsSpawned;
            for (int index = 0; index < pawns.Count; index++)
            {
                Pawn pawn = pawns[index];
                if (pawn == null || pawn.Dead || !pawn.HostileTo(caster))
                {
                    continue;
                }

                int dx = pawn.Position.x - parent.Position.x;
                int dz = pawn.Position.z - parent.Position.z;
                float distanceSquared = dx * dx + dz * dz;
                if (distanceSquared > radiusSquared)
                {
                    continue;
                }

                float distance = Mathf.Sqrt(distanceSquared);
                float edge = Mathf.Clamp01(Props.edgeEffect);
                float distanceFactor = 1f - distance / radius * (1f - edge);
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

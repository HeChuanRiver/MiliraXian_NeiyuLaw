using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_SpringFlowField : CompProperties
    {
        public float radius = 6.0f;
        public int pulseIntervalTicks = 10;

        public CompProperties_SpringFlowField()
        {
            compClass = typeof(CompSpringFlowField);
        }
    }
    
    public class CompSpringFlowField : ThingComp
    {
        private Pawn caster;
        private int ticksToNextEffect;
        
        public CompProperties_SpringFlowField Props => (CompProperties_SpringFlowField)props;

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
                ticksToNextEffect = UnityEngine.Mathf.Max(1, Props.pulseIntervalTicks);
                AttachEffect();
            }

            ticksToNextEffect--;
        }
        
        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look<Pawn>(ref caster, "caster", false);
            Scribe_Values.Look<int>(ref ticksToNextEffect, "ticksToNextEffect", 0, false);
        }

        public void Init(Pawn newCaster)
        {
            caster = newCaster;
            ticksToNextEffect = 1;
        }
        
        private void AttachEffect()
        {
            bool applied = false;
            float radius = UnityEngine.Mathf.Max(0f, Props.radius);
            float radiusSquared = radius * radius;
            float elegance = EleganceUtility.GetCurrent(caster);
            var pawns = parent.Map.mapPawns.AllPawnsSpawned;
            for (int index = 0; index < pawns.Count; index++)
            {
                Pawn pawn = pawns[index];
                if (pawn == null || pawn.Dead || pawn.Faction != caster.Faction)
                {
                    continue;
                }

                int dx = pawn.Position.x - parent.Position.x;
                int dz = pawn.Position.z - parent.Position.z;
                if (dx * dx + dz * dz > radiusSquared)
                {
                    continue;
                }

                if (pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_SpringFlow) is Hediff_SpringFlow h)
                {
                    h.Severity = 1.0f + elegance / 100.0f;
                    h.GetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
                }
                else
                {
                    var hediff = (Hediff_SpringFlow)HediffMaker.MakeHediff(MX_QHDefOf.MX_SpringFlow, pawn);
                    hediff.Severity = 1.0f + elegance / 100.0f;
                    pawn.health.AddHediff(hediff);
                }

                applied = true;
            }

            if (!applied)
            {
                return;
            }

            TempestUtility.NotifyRecoverEvent(caster);
            EleganceUtility.NotifyDecayEvent(caster);
        }
    }
}

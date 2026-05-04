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
            var applied = false;
            foreach (var thing in GenRadial.RadialDistinctThingsAround(parent.Position, parent.Map, Props.radius, true))
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

            TempestUtility.NotifyRecoverEvent(caster);
            EleganceUtility.NotifyDecayEvent(caster);
        }
    }
}

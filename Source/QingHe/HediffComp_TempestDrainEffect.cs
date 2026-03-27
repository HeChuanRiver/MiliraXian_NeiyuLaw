using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class HediffCompProperties_DrainAccumulate : HediffCompProperties
    {
        public int minTicksToFilth = 200;
        public int maxTicksToFilth = 600;
        public int filthRadius = 2;
        
        public HediffCompProperties_DrainAccumulate()
        {
            compClass = typeof(HediffComp_DrainAccumulate);
        }
    }
    
    public class HediffComp_DrainAccumulate : HediffComp
    {
        private int nextTickToFilth = 400;
        private bool dryCorpseApplied;
        
        public HediffCompProperties_DrainAccumulate Props => (HediffCompProperties_DrainAccumulate)props;

        public override void CompPostMake()
        {
            base.CompPostMake();
            nextTickToFilth = Rand.Range(Props.minTicksToFilth, Props.maxTicksToFilth);
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref nextTickToFilth, "mx_qh_drain_nextTickToFilth", 400);
            Scribe_Values.Look(ref dryCorpseApplied, "mx_qh_drain_dryCorpseApplied", false);
        }

        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            TryApplyDryCorpseIfNeeded();
        }
        
        // Randomly create water filth
        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);

            if (Pawn == null)
            {
                return;
            }

            TryApplyDryCorpseIfNeeded();

            if (Pawn.Dead || Pawn.Map == null)
            {
                return;
            }

            if (nextTickToFilth <= 0)
            {
                var pos = CellFinder.RandomClosewalkCellNearNotForbidden(Pawn, Props.filthRadius);
                FilthMaker.TryMakeFilth(pos, Pawn.Map, ThingDefOf.Filth_Water);
                nextTickToFilth = Rand.Range(Props.minTicksToFilth, Props.maxTicksToFilth);
            }
            nextTickToFilth -= delta;
        }

        private bool IsDrainFullyAccumulated()
        {
            return parent != null && parent.Severity >= 0.999f;
        }

        private void TryApplyDryCorpseIfNeeded()
        {
            if (dryCorpseApplied || Pawn == null || !Pawn.Dead || !IsDrainFullyAccumulated() || MX_QHDefOf.MX_DryCorpse == null)
            {
                return;
            }

            if (Pawn.health?.hediffSet?.GetFirstHediffOfDef(MX_QHDefOf.MX_DryCorpse) == null)
            {
                Hediff hediff = HediffMaker.MakeHediff(MX_QHDefOf.MX_DryCorpse, Pawn);
                Pawn.health.AddHediff(hediff);
            }

            dryCorpseApplied = true;
        }
    }

    public class HediffCompProperties_DryCorpseEffect : HediffCompProperties
    {
        public Color corpseColor = Color.white;
        
        public HediffCompProperties_DryCorpseEffect()
        {
            compClass = typeof(HediffComp_DryCorpseEffect);
        }
    }
    
    public class HediffComp_DryCorpseEffect : HediffComp
    {
        
        public HediffCompProperties_DryCorpseEffect Props => (HediffCompProperties_DryCorpseEffect)props;

        public override bool CompShouldRemove => !Pawn.Dead;
        
        public override void CompPostMake()
        {
            base.CompPostMake();
            if (Pawn == null || !Pawn.Dead || Pawn.Corpse == null || Pawn.Corpse.Destroyed)
            {
                Log.Error("[DryCorpseEffect] Pawn is not dead or corpse destroyed: " + Pawn?.Label);
                return;
            }
            
            // 1. Prevent corpse rot
            var rotComp = Pawn.Corpse.GetComp<CompRottable>();
            if (rotComp == null)
            {
                return;
            }
            rotComp.RotProgress = 0f;
            rotComp.disabled = true;
            
            // 2. Change corpse color
           // prevColor = Pawn.Corpse.DrawColor;
            //Pawn.Corpse.DrawColor = Props.corpseColor;
        }
        
        // 3. Remove parent hediff when pawn revived
        public override void CompPostPostRemoved()
        {
            base.CompPostPostRemoved();
            //Pawn.Corpse.DrawColor = prevColor;
        }
    }
}

using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_AbilityAquaMirror : CompProperties_AbilityEffect
    {
        public float previewRadius = 4.0f;

        public CompProperties_AbilityAquaMirror()
        {
            compClass = typeof(CompAbilityEffect_AquaMirror);
        }
    }
    
    public class CompAbilityEffect_AquaMirror : CompAbilityEffect
    {
        public new CompProperties_AbilityAquaMirror Props => (CompProperties_AbilityAquaMirror)props;
        
        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            base.DrawEffectPreview(target);
            GenDraw.DrawRadiusRing(target.Cell, Props.previewRadius, Color.cyan);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Log.Message("AquaMirror: Begin cast");
            foreach (var thing in GenRadial.RadialDistinctThingsAround(target.Cell, parent.pawn.Map, Props.previewRadius, true))
            {
                if (thing is Pawn pawn && !pawn.Dead && pawn.Faction == parent.pawn.Faction)
                {
                    var hediff = HediffMaker.MakeHediff(MX_QHDefOf.MX_AquaMirror, pawn);
                    var comp = hediff.TryGetComp<HediffComp_AquaMirror>();
                    if (comp == null)
                    {
                        Log.Error("AquaMirror: Failed to find HediffComp_AquaMirror on " + pawn.LabelCap);
                        continue;
                    }
                    comp.caster = parent.pawn;
                    pawn.health.AddHediff(hediff);
                }
            }
        }
    }
}
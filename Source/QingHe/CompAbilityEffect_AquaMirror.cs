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

            var map = parent?.pawn?.Map;
            if (map == null)
            {
                return;
            }

            var applySplash = DefDatabase<FleckDef>.GetNamedSilentFail("GroundWaterSplash");
            var applyRing = DefDatabase<FleckDef>.GetNamedSilentFail("PsycastAreaEffect");
            var applied = false;

            foreach (var thing in GenRadial.RadialDistinctThingsAround(target.Cell, map, Props.previewRadius, true))
            {
                if (!(thing is Pawn pawn) || pawn.Dead || pawn.Faction != parent.pawn.Faction)
                {
                    continue;
                }

                var existed = pawn.health.hediffSet.GetFirstHediff<Hediff_AquaMirror>();
                if (existed != null)
                {
                    pawn.health.RemoveHediff(existed);
                }

                var hediff = HediffMaker.MakeHediff(MX_QHDefOf.MX_AquaMirror, pawn);
                hediff.Severity = EleganceUtility.FactorLinear(1.0f, parent.pawn);
                var comp = hediff.TryGetComp<HediffComp_AquaMirror>();
                if (comp == null)
                {
                    Log.Error("AquaMirror: Failed to find HediffComp_AquaMirror on " + pawn.LabelCap);
                    continue;
                }

                comp.caster = parent.pawn;
                pawn.health.AddHediff(hediff);
                PlayApplyVisual(pawn, map, applySplash, applyRing);
                applied = true;
            }

            if (applied)
            {
                EleganceUtility.NotifyDecayEvent(parent.pawn);
            }
        }

        private static void PlayApplyVisual(Pawn pawn, Map map, FleckDef splashDef, FleckDef ringDef)
        {
            if (pawn == null || !pawn.Spawned || pawn.Map != map)
            {
                return;
            }

            if (splashDef != null)
            {
                FleckMaker.Static(pawn.Position, map, splashDef, 0.45f);
            }

            if (ringDef != null)
            {
                FleckMaker.Static(pawn.Position, map, ringDef, 0.35f);
            }
        }
    }
}

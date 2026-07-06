using RimWorld;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Things;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities
{
    public class CompProperties_AbilitySpringFlow : CompProperties_AbilityEffect
    {
        public ThingDef fieldDef = MX_QHDefOf.SpringFlowField;
        public int fieldDurationTicks = 900;
        public HediffDef resourceCostDef;
        public float resourceCost = 0f;
        public string missingResourceMessage = "MX_QH_FlowerDecreeNotEnough";

        public CompProperties_AbilitySpringFlow()
        {
            compClass = typeof(CompAbilityEffect_SpringFlow);
        }
    }

    public class CompAbilityEffect_SpringFlow : CompAbilityEffect
    {
        public new CompProperties_AbilitySpringFlow Props => (CompProperties_AbilitySpringFlow)props;

        public override bool GizmoDisabled(out string reason)
        {
            if (Props.resourceCostDef != null
                && Props.resourceCost > 0f
                && PawnSpecialResourceUtility.GetCurrentResource(parent.pawn, Props.resourceCostDef) < Props.resourceCost)
            {
                reason = Props.missingResourceMessage.Translate();
                return true;
            }

            return base.GizmoDisabled(out reason);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            if (Props.resourceCostDef != null && Props.resourceCost > 0f)
            {
                PawnSpecialResourceUtility.TryConsumeResource(parent.pawn, Props.resourceCostDef, Props.resourceCost);
            }

            if (Props.fieldDef == null || parent?.pawn == null) return;
            var map = parent.pawn.Map;
            var cell = target.Cell;
            if (map == null || !cell.IsValid || !cell.InBounds(map)) return;

            var field = GenSpawn.Spawn(Props.fieldDef, cell, map);
            CompSpringFlowField fieldComp = field.TryGetComp<CompSpringFlowField>();
            fieldComp?.Init(parent.pawn, Props.fieldDurationTicks);
            fieldComp?.SetEnhanced(MX_QHSkillSystem.HasAllFlowerMandates(MX_QH_HediffUtility.GetFlowerResonance(parent.pawn)));
            PlaySummonVisual(map, cell, ResolveRadius());
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            base.DrawEffectPreview(target);
            GenDraw.DrawRadiusRing(target.Cell, ResolveRadius(), Color.magenta);
        }

        private float ResolveRadius()
        {
            if (Props.fieldDef != null && Props.fieldDef.comps != null)
            {
                for (int i = 0; i < Props.fieldDef.comps.Count; i++)
                {
                    if (Props.fieldDef.comps[i] is CompProperties_SpringFlowField fieldProps)
                    {
                        return fieldProps.radius;
                    }
                }
            }

            return 6f;
        }

        private static void PlaySummonVisual(Map map, IntVec3 cell, float radius)
        {
            var splash = DefDatabase<FleckDef>.GetNamedSilentFail("GroundWaterSplash");
            var ring = DefDatabase<FleckDef>.GetNamedSilentFail("PsycastAreaEffect");
            float burstRadius = Mathf.Max(1.5f, radius * 0.5f);

            if (ring != null)
            {
                FleckMaker.Static(cell, map, ring, burstRadius * 0.55f);
            }

            if (splash == null)
            {
                return;
            }

            FleckMaker.Static(cell, map, splash, Mathf.Max(0.9f, burstRadius * 0.35f));
            int splashCount = Mathf.Clamp(Mathf.RoundToInt(burstRadius * 4f), 8, 18);
            int radialCells = GenRadial.NumCellsInRadius(burstRadius);
            for (int i = 0; i < splashCount; i++)
            {
                IntVec3 splashCell = cell + GenRadial.RadialPattern[Rand.Range(1, radialCells)];
                if (splashCell.InBounds(map) && splashCell.Walkable(map))
                {
                    FleckMaker.Static(splashCell, map, splash, Rand.Range(0.35f, 0.7f));
                }
            }
        }
    }
}

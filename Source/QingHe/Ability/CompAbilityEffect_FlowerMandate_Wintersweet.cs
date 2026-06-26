using MiliraXian.Characters.QingHe.Things;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities
{
    public class CompProperties_AbilityFlowerMandate_Wintersweet : CompProperties_AbilityEffect
    {
        public ThingDef shieldDef;
        public HediffDef resourceCostDef;
        public float resourceCost = 1f;
        public int durationTicks = 900;
        public string summonEffecterDefName = "MXNL_ForFeatherCastingCircle";
        public float summonEffectScale = 1f;
        public string fallbackSummonFleckDefName = "PsycastAreaEffect";
        public string missingResourceMessage = "花令不足。";

        public CompProperties_AbilityFlowerMandate_Wintersweet()
        {
            compClass = typeof(CompAbilityEffect_FlowerMandate_Wintersweet);
        }
    }

    public class CompAbilityEffect_FlowerMandate_Wintersweet : CompAbilityEffect
    {
        public new CompProperties_AbilityFlowerMandate_Wintersweet Props => (CompProperties_AbilityFlowerMandate_Wintersweet)props;

        public override bool GizmoDisabled(out string reason)
        {
            if (Props.resourceCostDef != null
                && Props.resourceCost > 0f
                && PawnSpecialResourceUtility.GetCurrentResource(parent.pawn, Props.resourceCostDef) < Props.resourceCost)
            {
                reason = Props.missingResourceMessage;
                return true;
            }

            return base.GizmoDisabled(out reason);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent?.pawn;
            if (caster == null || caster.Map == null || Props.shieldDef == null)
            {
                return;
            }

            IntVec3 cell = target.Cell;
            if (!cell.IsValid || !cell.InBounds(caster.Map) || !cell.Standable(caster.Map))
            {
                return;
            }

            if (Props.resourceCostDef != null && Props.resourceCost > 0f)
            {
                if (!PawnSpecialResourceUtility.TryConsumeResource(caster, Props.resourceCostDef, Props.resourceCost))
                {
                    Messages.Message(Props.missingResourceMessage, caster, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }
            }

            Thing thing = GenSpawn.Spawn(Props.shieldDef, cell, caster.Map, WipeMode.Vanish);
            CompFlowerMandate_WintersweetShield shield = thing.TryGetComp<CompFlowerMandate_WintersweetShield>();
            shield?.Init(caster, Props.durationTicks);
            shield?.SetEnhanced(FlowerMandateEnhanceUtility.ActiveFor(caster, parent.def));
            PlaySummonVisual(caster.Map, cell, Props.summonEffecterDefName, Props.fallbackSummonFleckDefName, Props.summonEffectScale);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            base.DrawEffectPreview(target);
            GenDraw.DrawRadiusRing(target.Cell, ResolveShieldRadius(), new Color(0.62f, 0.88f, 1f, 0.30f));
        }

        private float ResolveShieldRadius()
        {
            if (Props.shieldDef?.comps != null)
            {
                for (int i = 0; i < Props.shieldDef.comps.Count; i++)
                {
                    if (Props.shieldDef.comps[i] is CompProperties_FlowerMandate_WintersweetShield shieldProps)
                    {
                        return shieldProps.radius;
                    }
                }
            }

            return 4f;
        }

        private static void PlaySummonVisual(Map map, IntVec3 cell, string effecterDefName, string fallbackFleckDefName, float scale)
        {
            if (!effecterDefName.NullOrEmpty())
            {
                GraphicsUtility.Fx(map, cell, effecterDefName, scale);
                return;
            }

            if (!fallbackFleckDefName.NullOrEmpty())
            {
                GraphicsUtility.Fleck(map, cell, fallbackFleckDefName, scale);
            }
        }
    }
}

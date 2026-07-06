using MiliraXian.Characters.QingHe.Things;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Vfx;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities
{
    public class CompProperties_AbilityLunarMirror : CompProperties_AbilityEffect
    {
        public ThingDef shieldDef;
        public HediffDef resourceCostDef;
        public float resourceCost = 1f;
        public int durationTicks = 900;
        public string summonEffecterDefName = "MXNL_ForFeatherCastingCircle";
        public float summonEffectScale = 1f;
        public string fallbackSummonFleckDefName = "PsycastAreaEffect";
        public string missingResourceMessage = "MX_QH_FlowerDecreeNotEnough";

        public CompProperties_AbilityLunarMirror()
        {
            compClass = typeof(CompAbilityEffect_LunarMirror);
        }
    }

    public class CompAbilityEffect_LunarMirror : CompAbilityEffect
    {
        public new CompProperties_AbilityLunarMirror Props => (CompProperties_AbilityLunarMirror)props;

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
                    Messages.Message(Props.missingResourceMessage.Translate(), caster, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }
            }

            Thing thing = GenSpawn.Spawn(Props.shieldDef, cell, caster.Map, WipeMode.Vanish);
            CompLunarMirrorShield shield = thing.TryGetComp<CompLunarMirrorShield>();
            shield?.Init(caster, Props.durationTicks);
            shield?.SetEnhanced(MX_QHSkillSystem.HasAllFlowerMandates(MX_QH_HediffUtility.GetFlowerResonance(caster)));
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
                    if (Props.shieldDef.comps[i] is CompProperties_LunarMirrorShield shieldProps)
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
                MX_QHGraphicsUtility.Fx(map, cell, effecterDefName, scale);
                return;
            }

            if (!fallbackFleckDefName.NullOrEmpty())
            {
                MX_QHGraphicsUtility.Fleck(map, cell, fallbackFleckDefName, scale);
            }
        }
    }
}

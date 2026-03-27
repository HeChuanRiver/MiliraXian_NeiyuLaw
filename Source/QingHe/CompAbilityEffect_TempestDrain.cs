using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_AbilityTempestDrain : CompProperties_AbilityEffect
    {
        public ThingDef fieldDef = MX_QHDefOf.TempestDrainField;
        public float previewRadius = 12.0f;

        public float minResourceToCast = 50.0f;
        public float minResourceToMaintain = 10.0f;
        public int channelDurationTicks = 999999;

        public CompProperties_AbilityTempestDrain()
        {
            compClass = typeof(CompAbilityEffect_TempestDrain);
        }
    }

    public class CompAbilityEffect_TempestDrain : CompAbilityEffect
    {
        public new CompProperties_AbilityTempestDrain Props => (CompProperties_AbilityTempestDrain)props;

        public override bool GizmoDisabled(out string reason)
        {
            bool disabled = false;
            StringBuilder r = new StringBuilder();
            if (PawnSpecialResourceUtility.GetCurrentResource(parent.pawn, MX_QHDefOf.MX_QH_Tempest) < Props.minResourceToCast)
            {
                r.AppendLine("MiliraXian.QingHe.Ability_TempestDrain.Disabled".Translate());
                disabled = true;
            }

            reason = r.ToString();
            return disabled;
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            base.DrawEffectPreview(target);
            GenDraw.DrawRadiusRing(target.Cell, ResolvePreviewRadius(), Color.blue);
        }

        private float ResolvePreviewRadius()
        {
            if (Props.fieldDef != null && Props.fieldDef.comps != null)
            {
                for (int i = 0; i < Props.fieldDef.comps.Count; i++)
                {
                    if (Props.fieldDef.comps[i] is CompProperties_TempestDrainField fieldProps)
                    {
                        return fieldProps.radius;
                    }
                }
            }

            return Props.previewRadius;
        }
    }
}

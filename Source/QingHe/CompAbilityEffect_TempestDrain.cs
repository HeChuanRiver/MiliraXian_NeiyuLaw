using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class CompProperties_AbilityTempestDrain : CompProperties_AbilityEffect
    {
        public ThingDef fieldDef = MX_QHDefOf.SpringFlowField;
        public float previewRadius = 12.0f;
        
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
            var res = false;
            var r = new StringBuilder();
            if (PawnSpecialResourceUtility.GetCurrentResource(parent.pawn, MX_QHDefOf.MX_QH_Tempest) < 50.0f)
            {
                r.AppendLine("MiliraXian.QingHe.Ability_TempestDrain.Disabled".Translate());
                res = true;
            }
            reason = r.ToString();
            return res;
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            base.DrawEffectPreview(target);
            GenDraw.DrawRadiusRing(target.Cell, Props.previewRadius, Color.blue);
        }
    }
}
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Ability
{
    public class CompProperties_FlowerGodPlaceholder : CompProperties_AbilityEffect
    {
        public string placeholderMessage = "该花神技能仍是占位框架，具体效果尚未实现。";
        public bool alwaysDisabled;
        public string disabledReason = "尚未调谐四时共鸣。";

        public CompProperties_FlowerGodPlaceholder()
        {
            compClass = typeof(CompAbilityEffect_FlowerGodPlaceholder);
        }
    }

    public class CompAbilityEffect_FlowerGodPlaceholder : CompAbilityEffect
    {
        private new CompProperties_FlowerGodPlaceholder Props => (CompProperties_FlowerGodPlaceholder)props;

        public override bool GizmoDisabled(out string reason)
        {
            if (Props.alwaysDisabled)
            {
                reason = Props.disabledReason;
                return true;
            }

            return base.GizmoDisabled(out reason);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn pawn = parent?.pawn;
            if (pawn != null)
            {
                Messages.Message(Props.placeholderMessage, pawn, MessageTypeDefOf.NeutralEvent, historical: false);
            }
        }
    }
}

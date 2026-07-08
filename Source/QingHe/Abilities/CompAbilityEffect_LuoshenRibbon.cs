using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities
{
    public class CompProperties_AbilityLuoshenRibbon : CompProperties_EffectWithDest
    {
        public ThoughtDef opinionThoughtDef;
        public string invalidTargetMessage = "MX_QH_LuoshenRibbonInvalidTarget";
        public string successMessage = "MX_QH_LuoshenRibbonApplied";

        public CompProperties_AbilityLuoshenRibbon()
        {
            compClass = typeof(CompAbilityEffect_LuoshenRibbon);
        }
    }

    public class CompAbilityEffect_LuoshenRibbon : CompAbilityEffect_WithDest
    {
        public new CompProperties_AbilityLuoshenRibbon Props => (CompProperties_AbilityLuoshenRibbon)props;

        public override bool HideTargetPawnTooltip => true;

        public override TargetingParameters targetParams => new TargetingParameters
        {
            canTargetSelf = true,
            canTargetPawns = true,
            canTargetBuildings = false,
            canTargetAnimals = false,
            canTargetMechs = false
        };

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn first = target.Pawn;
            Pawn second = dest.Pawn;
            if (!ValidPair(first, second))
            {
                return;
            }

            ThoughtDef thoughtDef = Props.opinionThoughtDef;
            if (thoughtDef == null)
            {
                return;
            }

            first.needs?.mood?.thoughts?.memories?.TryGainMemory(thoughtDef, second);
            second.needs?.mood?.thoughts?.memories?.TryGainMemory(thoughtDef, first);
            Messages.Message(Props.successMessage.Translate(first.LabelShort, second.LabelShort), second, MessageTypeDefOf.PositiveEvent, historical: false);
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return Valid(target, false);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn pawn = target.Pawn;
            if (!ValidPawn(pawn))
            {
                if (throwMessages)
                {
                    Messages.Message(Props.invalidTargetMessage.Translate(), pawn, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            return base.Valid(target, throwMessages);
        }

        public override bool ValidateTarget(LocalTargetInfo target, bool showMessages = true)
        {
            Pawn first = selectedTarget.Pawn;
            Pawn second = target.Pawn;
            if (!ValidPair(first, second))
            {
                if (showMessages)
                {
                    Messages.Message(Props.invalidTargetMessage.Translate(), second ?? first, MessageTypeDefOf.RejectInput, historical: false);
                }
                return false;
            }

            return base.ValidateTarget(target, showMessages);
        }

        public override string ExtraLabelMouseAttachment(LocalTargetInfo target)
        {
            return selectedTarget.IsValid
                ? "MX_QH_LuoshenRibbonChooseSecond".Translate()
                : "MX_QH_LuoshenRibbonChooseFirst".Translate();
        }

        private static bool ValidPair(Pawn first, Pawn second)
        {
            return ValidPawn(first) && ValidPawn(second) && first != second;
        }

        private static bool ValidPawn(Pawn pawn)
        {
            return pawn != null
                && !pawn.Dead
                && !pawn.Downed
                && pawn.RaceProps != null
                && pawn.RaceProps.Humanlike;
        }
    }
}

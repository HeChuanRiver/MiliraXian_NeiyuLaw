using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Hediffs;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    public class CompProperties_QingheResonantWeaponLabel : CompProperties
    {
        public List<string> resonanceLabels = new();
        public List<string> resonanceDescriptions = new();

        public CompProperties_QingheResonantWeaponLabel()
        {
            compClass = typeof(CompQingheResonantWeaponLabel);
        }
    }

    public class CompQingheResonantWeaponLabel : ThingComp
    {
        private Pawn cachedHolder;
        private HediffComp_QingheCombatState cachedCombatState;

        public CompProperties_QingheResonantWeaponLabel Props => (CompProperties_QingheResonantWeaponLabel)props;

        public override string TransformLabel(string label)
        {
            int index = CurrentResonanceIndex();
            if (Props.resonanceLabels == null || index < 0 || index >= Props.resonanceLabels.Count)
            {
                return label;
            }

            string suffix = Props.resonanceLabels[index];
            if (suffix.NullOrEmpty())
            {
                return label;
            }

            if (Translator.CanTranslate(suffix))
            {
                suffix = suffix.Translate().ToString();
            }
            return label + "【" + suffix + "】";
        }

        public override string GetDescriptionPart()
        {
            int index = CurrentResonanceIndex();
            if (Props.resonanceDescriptions == null || index < 0 || index >= Props.resonanceDescriptions.Count)
            {
                return null;
            }

            string description = Props.resonanceDescriptions[index];
            if (description.NullOrEmpty())
            {
                return null;
            }

            return Translator.CanTranslate(description)
                ? description.Translate().ToString()
                : description;
        }

        private int CurrentResonanceIndex()
        {
            Pawn pawn = ResolveHolderPawn();
            HediffComp_QingheCombatState state = GetCombatState(pawn);
            return (int)(state?.Resonance ?? FlowerBellResonance.Spring);
        }

        private HediffComp_QingheCombatState GetCombatState(Pawn pawn)
        {
            if (cachedHolder != pawn)
            {
                cachedHolder = pawn;
                cachedCombatState = null;
            }

            if (cachedCombatState == null || cachedCombatState.Pawn != pawn)
            {
                cachedCombatState = MX_QH_HediffUtility.GetCombatState(pawn);
            }

            return cachedCombatState;
        }

        private Pawn ResolveHolderPawn()
        {
            IThingHolder holder = parent?.ParentHolder;
            if (holder is Pawn pawn)
            {
                return pawn;
            }
            return holder?.ParentHolder as Pawn;
        }
    }
}

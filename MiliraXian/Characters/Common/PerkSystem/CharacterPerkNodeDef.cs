using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.PerkSystem
{
    public class CharacterPerkNodeDef : Def
    {
        public CharacterPerkCategoryDef category;
        public int displayOrder;
        public int requiredAuraMasteryLevel;
        public string iconPath;
        public bool displayOnly;
        public List<StatModifier> statOffsets;
        public List<StatModifier> statFactors;
        public List<AbilityDef> grantedAbilities;
        public List<HediffDef> grantedHediffs;
        public CharacterPerkUnlockLetter unlockLetter;

        public virtual void Notify_Unlocked(Pawn pawn)
        {
            if (unlockLetter != null)
            {
                Find.LetterStack.ReceiveLetter(
                    unlockLetter.title.Formatted(pawn.Named("PAWN")),
                    unlockLetter.text.Formatted(pawn.Named("PAWN")),
                    LetterDefOf.PositiveEvent,
                    pawn);
            }
        }

        public Texture2D ResolveIcon()
        {
            if (!iconPath.NullOrEmpty())
            {
                Texture2D tex = ContentFinder<Texture2D>.Get(iconPath, false);
                if (tex != null)
                {
                    return tex;
                }
            }

            return BaseContent.BadTex;
        }
    }

    public class CharacterPerkUnlockLetter
    {
        [MustTranslate]
        public string title;

        [MustTranslate]
        public string text;
    }
}

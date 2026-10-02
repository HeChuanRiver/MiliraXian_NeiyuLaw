using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using MiliraXian.Characters.Common.Conditions;

namespace MiliraXian.Characters.Common.PerkSystem
{
    public class CharacterPerkNodeDef : Def
    {
        public CharacterPerkCategoryDef category;
        public int displayOrder;
        public string iconActive;
        public string iconInactive;
        public bool displayOnly;
        public List<CharacterPerkNodeRef> prerequisites;
        public List<UnlockCondition> requiresAll;
        public List<UnlockCondition> requiresAny;
        public List<ThingDefCountClass> costs;
        public int maxLevel = 1;
        public List<StatModifier> statOffsets;
        public List<StatModifier> statFactors;
        public List<AbilityDef> grantedAbilities;
        public List<HediffDef> grantedHediffs;
        public CharacterPerkUnlockLetter unlockLetter;

        /// <summary>
        /// Whether this node unlocks itself when the event source changes (mastery level reaching a
        /// threshold, a biography condition becoming true). Purchase-style nodes answer false and
        /// wait for an explicit unlock request instead.
        /// </summary>
        public virtual bool ShouldAutoUnlock(Pawn pawn)
        {
            return false;
        }

        /// <summary>
        /// The stored level after this node's seal applies. Sealing only narrows what takes effect;
        /// it never rewrites the stored level.
        /// </summary>
        public virtual int EffectiveLevel(Pawn pawn, int storedLevel)
        {
            return storedLevel;
        }

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

        /// <summary>
        /// iconActive is the primary art; iconInactive is an optional locked-state variant. When the
        /// variant exists it replaces the default grey tint, so the art alone expresses the state.
        /// </summary>
        public Texture2D ResolveIcon(bool active, out bool skipLockedTint)
        {
            skipLockedTint = !active && !iconInactive.NullOrEmpty();
            string path = skipLockedTint ? iconInactive : iconActive;
            if (!path.NullOrEmpty())
            {
                Texture2D tex = ContentFinder<Texture2D>.Get(path, false);
                if (tex != null)
                {
                    return tex;
                }
            }

            return BaseContent.BadTex;
        }
    }

    /// <summary>A prerequisite pointing at a node at a minimum level; omitting minLevel means 1.</summary>
    public class CharacterPerkNodeRef
    {
        public CharacterPerkNodeDef node;
        public int minLevel = 1;
    }

    public class CharacterPerkUnlockLetter
    {
        [MustTranslate]
        public string title;

        [MustTranslate]
        public string text;
    }
}

using System.Collections.Generic;
using Verse;
using MiliraXian.Characters.Common.Biography;

namespace MiliraXian.Characters.Common.Conditions
{
    /// <summary>
    /// A stateless unlock check. Conditions ask a pawn (and, when relevant, its biography tracker)
    /// whether a requirement currently holds; they never tick, cache or push events.
    /// </summary>
    public abstract class UnlockCondition
    {
        public abstract bool IsSatisfied(Pawn pawn, Hediff_BiographyTracker tracker);

        public abstract string GetProgressText(Pawn pawn, Hediff_BiographyTracker tracker);

        public virtual IEnumerable<string> ConfigErrors(BiographyDef biography, BiographyStory story, string path)
        {
            yield break;
        }

        public virtual void CollectReferencedStoryNames(List<string> storyNames)
        {
        }
    }
}

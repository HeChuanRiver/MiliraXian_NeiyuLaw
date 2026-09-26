using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters.FacialAnimationCompat
{
    /// <summary>
    /// Redirects a race-wide Facial Animation entry to a character-specific one.
    /// Pawn kinds are matched by defName so this layer needs no reference to the
    /// character assemblies.
    /// </summary>
    public class FaceAnimationOverrideDef : Def
    {
        public List<string> pawnKinds = new();

        /// <summary>Jobs to patch. Empty means every job bucket, including the constant one.</summary>
        public List<string> targetJobs = new();

        /// <summary>Animation to remove. Empty only adds <see cref="with"/>.</summary>
        public string replace;

        public string with;

        public bool AppliesTo(Pawn pawn)
        {
            string kindDefName = pawn?.kindDef?.defName;
            return kindDefName != null && pawnKinds.Contains(kindDefName);
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }

            if (pawnKinds.Count == 0)
            {
                yield return "pawnKinds is empty, so this override can never match a pawn.";
            }

            if (with.NullOrEmpty() && replace.NullOrEmpty())
            {
                yield return "both replace and with are empty, so this override does nothing.";
            }
        }
    }
}

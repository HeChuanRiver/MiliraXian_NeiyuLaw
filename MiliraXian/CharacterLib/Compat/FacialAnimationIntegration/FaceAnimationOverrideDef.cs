using System.Collections.Generic;
using FacialAnimation;
using Verse;

namespace MiliraXian.CharacterLib.Compat.FacialAnimationIntegration
{
    /// <summary>
    /// Redirects a race-wide Facial Animation entry to a character-specific one.
    /// Def references here cost no assembly dependency: PawnKindDef and FaceAnimationDef
    /// resolve against the global Def database after every mod's XML is merged, so a typo
    /// is reported at load time instead of silently failing to match.
    /// </summary>
    public class FaceAnimationOverrideDef : Def
    {
        public List<PawnKindDef> pawnKinds = new();

        /// <summary>
        /// Jobs to patch. Empty means every job bucket, including the constant one.
        /// These stay strings: they are keys of Facial Animation's own dictionary, whose
        /// key set includes an empty string that no JobDef can express.
        /// </summary>
        public List<string> targetJobs = new();

        /// <summary>Animation to remove. Null only adds <see cref="with"/>.</summary>
        public FaceAnimationDef replace;

        public FaceAnimationDef with;

        public bool AppliesTo(Pawn pawn) => pawn?.kindDef != null && pawnKinds.Contains(pawn.kindDef);

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

            if (with == null && replace == null)
            {
                yield return "both replace and with are empty, so this override does nothing.";
            }
        }
    }
}

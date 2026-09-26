using System.Collections.Generic;
using System.Reflection;
using FacialAnimation;
using HarmonyLib;
using Verse;

namespace MiliraXian.Characters.FacialAnimationCompat
{
    [StaticConstructorOnStartup]
    public static class FACompatBootstrap
    {
        static FACompatBootstrap()
        {
            // The patch target is resolved by name, so report a broken lookup instead of
            // silently losing every animation override.
            if (Patch_CreateAnimationDict.TargetMethod() == null)
            {
                Log.Error(
                    "[MiliraXian] Could not find FacialAnimation.FAHelper:CreateAnimationDict. "
                    + "Facial Animation overrides are disabled; the mod's API likely changed.");
                return;
            }

            new Harmony("MiliraXian.Characters.FACompat").PatchAll();
        }
    }

    /// <summary>
    /// Swaps race-wide animations for character-specific ones. The dictionary and its
    /// FaceAnimation wrappers are rebuilt per pawn on every call, so editing them here
    /// cannot leak to other pawns.
    ///
    /// Never mutate AnimationFrame data on a FaceAnimationDef: GetSequentialAnimationFrames
    /// caches one expanded list per Def and shares it across every pawn using that Def.
    /// Overrides must therefore supply a complete replacement Def.
    /// </summary>
    [HarmonyPatch]
    public static class Patch_CreateAnimationDict
    {
        // FAHelper is an internal type, so the target is resolved at runtime.
        [HarmonyTargetMethod]
        public static MethodBase TargetMethod()
        {
            return AccessTools.TypeByName("FacialAnimation.FAHelper")
                ?.GetMethod("CreateAnimationDict", AccessTools.all);
        }

        [HarmonyPostfix]
        public static void Postfix(Pawn pawn, int initialTick, ref Dictionary<string, List<FaceAnimation>> animationDict)
        {
            if (pawn == null || animationDict == null)
            {
                return;
            }

            bool changed = false;
            foreach (FaceAnimationOverrideDef overrideDef in DefDatabase<FaceAnimationOverrideDef>.AllDefsListForReading)
            {
                if (overrideDef.AppliesTo(pawn))
                {
                    changed |= Apply(overrideDef, initialTick, animationDict);
                }
            }

            if (!changed)
            {
                return;
            }

            foreach (List<FaceAnimation> bucket in animationDict.Values)
            {
                bucket.Sort((a, b) => a.animationDef.priority - b.animationDef.priority);
            }
        }

        private static bool Apply(
            FaceAnimationOverrideDef overrideDef,
            int initialTick,
            Dictionary<string, List<FaceAnimation>> animationDict)
        {
            FaceAnimationDef replacement = null;
            if (!overrideDef.with.NullOrEmpty())
            {
                replacement = DefDatabase<FaceAnimationDef>.GetNamedSilentFail(overrideDef.with);
                if (replacement == null)
                {
                    Log.ErrorOnce(
                        $"[MiliraXian] FaceAnimationOverrideDef {overrideDef.defName} references missing animation '{overrideDef.with}'.",
                        overrideDef.defName.GetHashCode());
                    return false;
                }
            }

            bool changed = false;
            foreach (string jobKey in ResolveJobKeys(overrideDef, animationDict))
            {
                if (!animationDict.TryGetValue(jobKey, out List<FaceAnimation> bucket))
                {
                    continue;
                }

                if (!overrideDef.replace.NullOrEmpty())
                {
                    changed |= bucket.RemoveAll(x => x.animationDef?.defName == overrideDef.replace) > 0;
                }

                if (replacement != null && !bucket.Exists(x => x.animationDef == replacement))
                {
                    bucket.Add(new FaceAnimation(replacement, initialTick));
                    changed = true;
                }
            }

            return changed;
        }

        private static IEnumerable<string> ResolveJobKeys(
            FaceAnimationOverrideDef overrideDef,
            Dictionary<string, List<FaceAnimation>> animationDict)
        {
            if (overrideDef.targetJobs.Count > 0)
            {
                return overrideDef.targetJobs;
            }

            // An empty job list means every bucket, so the constant-job entries are covered too.
            return new List<string>(animationDict.Keys);
        }
    }
}

using System;
using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace MiliraXian.Characters.Common
{
    /// <summary>
    /// Resolves type names written by builds from before the namespaces were aligned with the
    /// directory layout. Saves and mod settings store types as plain strings, and
    /// <see cref="BackCompatibility.GetBackCompatibleType"/> short-circuits straight to
    /// <see cref="GenTypes.GetTypeInAnyAssembly"/> whenever the mod list and game build are
    /// unchanged, so a BackCompatibilityConverter would never run for the "player updated only
    /// this mod" case. Patching the lookup itself covers every path.
    ///
    /// Delete this together with the 1.1 save line; it is not meant to outlive that.
    /// </summary>
    internal static class SaveTypeMigration
    {
        // Old namespace prefix => namespaces its types may have moved into. The first
        // candidate that actually declares the type wins.
        private static readonly (string Old, string[] New)[] PrefixRewrites =
        {
            ("MiliraXian.Characters.Biography.", new[] { "MiliraXian.Characters.Common.Biography." }),
            ("MiliraXian.Characters.UI.", new[] { "MiliraXian.Characters.Common.UI." }),
            ("MiliraXian.Characters.Vfx.", new[] { "MiliraXian.Characters.Common.Vfx." }),
            ("MiliraXian.NeiyuLaw.", new[] { "MiliraXian.Characters.", "MiliraXian.Characters.Neiyu." }),
            ("MiliraXian.Characters.Neiyu.", new[] { "MiliraXian.Characters." }),
            ("MiliraXian.Characters.QingHe.", new[] { "MiliraXian.Characters.QingHe.Quests." }),
            ("MiliraXian.Characters.", new[]
            {
                "MiliraXian.Characters.Common.",
                "MiliraXian.Characters.Common.AbnormalSystem.",
                "MiliraXian.Characters.Common.AbnormalSystem.Effects.",
                "MiliraXian.Characters.Common.AbnormalSystem.Effects.MentalStates.",
                "MiliraXian.Characters.Common.SkillTrees.",
                "MiliraXian.Characters.Common.Stats.",
            }),
        };

        private static readonly Dictionary<string, Type> Resolved = new();

        public static bool TryResolve(string typeName, out Type type)
        {
            if (typeName == null || !typeName.StartsWith("MiliraXian."))
            {
                type = null;
                return false;
            }

            if (Resolved.TryGetValue(typeName, out type))
            {
                return type != null;
            }

            type = Resolve(typeName);
            Resolved[typeName] = type;
            return type != null;
        }

        private static Type Resolve(string typeName)
        {
            foreach ((string oldPrefix, string[] newPrefixes) in PrefixRewrites)
            {
                if (!typeName.StartsWith(oldPrefix))
                {
                    continue;
                }

                string suffix = typeName.Substring(oldPrefix.Length);
                foreach (string newPrefix in newPrefixes)
                {
                    Type candidate = Lookup(newPrefix + suffix);
                    if (candidate != null)
                    {
                        return candidate;
                    }
                }
            }

            return null;
        }

        // Resolved against this assembly directly: going back through GenTypes would re-enter
        // the very postfix that calls this.
        private static Type Lookup(string fullName) =>
            typeof(SaveTypeMigration).Assembly.GetType(fullName, throwOnError: false);
    }

    [HarmonyPatch(typeof(GenTypes), nameof(GenTypes.GetTypeInAnyAssembly))]
    internal static class Patch_GenTypes_GetTypeInAnyAssembly_Migration
    {
        [HarmonyPostfix]
        private static void Postfix(string typeName, ref Type __result)
        {
            if (__result == null && SaveTypeMigration.TryResolve(typeName, out Type migrated))
            {
                __result = migrated;
            }
        }
    }
}

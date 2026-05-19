using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Verse;

namespace MiliraXian.Characters.Zhaoli
{
    internal static class ZhaoliBladeVerbLoadFixUtility
    {
        private const string WeaponDefName = "MX_Zhaoli_DuanzhanBlade";
        private const string VerbOwnerIdMarker = "CompEquippable_" + WeaponDefName;

        private static readonly FieldInfo VerbTrackerVerbsField = AccessTools.Field(typeof(VerbTracker), "verbs");

        public static bool IsZhaoliBlade(Thing thing)
        {
            ThingDef weaponDef = MXZL_ZhaoliDefOf.MX_Zhaoli_DuanzhanBlade;
            return thing?.def != null && (thing.def == weaponDef || thing.def.defName == WeaponDefName);
        }

        public static bool IsZhaoliBladeVerb(Verb verb)
        {
            if (verb == null)
            {
                return false;
            }

            if (verb.verbTracker?.directOwner is CompEquippable comp && IsZhaoliBlade(comp.parent))
            {
                return true;
            }

            return IsZhaoliBladeVerbLoadId(verb.loadID);
        }

        public static bool IsZhaoliBladeVerbLoadId(string loadId)
        {
            return !string.IsNullOrEmpty(loadId) && loadId.Contains(VerbOwnerIdMarker);
        }

        public static bool IsZhaoliBladeVerbUniqueLoadId(string uniqueLoadId)
        {
            return !string.IsNullOrEmpty(uniqueLoadId) && uniqueLoadId.StartsWith("Verb_") && uniqueLoadId.Contains(VerbOwnerIdMarker);
        }

        public static void EnsureCleanVerbTracker(ThingWithComps weapon, bool force = false)
        {
            EnsureCleanVerbTracker(weapon?.TryGetComp<CompEquippable>(), force);
        }

        public static void EnsureCleanVerbTracker(CompEquippable comp, bool force = false)
        {
            if (comp == null || !IsZhaoliBlade(comp.parent))
            {
                return;
            }

            if (comp.verbTracker == null)
            {
                comp.verbTracker = new VerbTracker(comp);
            }

            if (force || NeedsReinit(comp))
            {
                comp.verbTracker.VerbsNeedReinitOnLoad();
                _ = comp.AllVerbs;
            }

            AssignCasterFromHolder(comp);
        }

        private static bool NeedsReinit(CompEquippable comp)
        {
            List<Verb> verbs = VerbTrackerVerbsField?.GetValue(comp.verbTracker) as List<Verb>;
            if (verbs == null)
            {
                return false;
            }

            List<string> expectedLoadIds = BuildExpectedLoadIds(comp);
            if (verbs.Count != expectedLoadIds.Count)
            {
                return true;
            }

            HashSet<string> expected = new HashSet<string>(expectedLoadIds);
            HashSet<string> seen = new HashSet<string>();
            for (int i = 0; i < verbs.Count; i++)
            {
                Verb verb = verbs[i];
                if (verb == null || string.IsNullOrEmpty(verb.loadID) || verb.verbTracker != comp.verbTracker)
                {
                    return true;
                }

                if (!seen.Add(verb.loadID) || !expected.Contains(verb.loadID))
                {
                    return true;
                }
            }

            return false;
        }

        private static List<string> BuildExpectedLoadIds(CompEquippable comp)
        {
            List<string> expectedLoadIds = new List<string>();
            IVerbOwner owner = comp;
            List<VerbProperties> verbProperties = comp.VerbProperties;
            if (verbProperties != null)
            {
                for (int i = 0; i < verbProperties.Count; i++)
                {
                    expectedLoadIds.Add(Verb.CalculateUniqueLoadID(owner, i));
                }
            }

            List<Tool> tools = comp.Tools;
            if (tools != null)
            {
                for (int i = 0; i < tools.Count; i++)
                {
                    Tool tool = tools[i];
                    if (tool?.Maneuvers == null)
                    {
                        continue;
                    }

                    foreach (ManeuverDef maneuver in tool.Maneuvers)
                    {
                        expectedLoadIds.Add(Verb.CalculateUniqueLoadID(owner, tool, maneuver));
                    }
                }
            }

            return expectedLoadIds;
        }

        private static void AssignCasterFromHolder(CompEquippable comp)
        {
            Pawn pawn = (comp.parent?.ParentHolder as Pawn_EquipmentTracker)?.pawn;
            if (pawn == null)
            {
                return;
            }

            List<Verb> verbs = comp.AllVerbs;
            for (int i = 0; i < verbs.Count; i++)
            {
                if (verbs[i] != null)
                {
                    verbs[i].caster = pawn;
                }
            }
        }
    }

    [HarmonyPatch(typeof(LoadedObjectDirectory), nameof(LoadedObjectDirectory.RegisterLoaded))]
    internal static class Patch_LoadedObjectDirectory_RegisterLoaded_ZhaoliBladeVerb
    {
        private static readonly FieldInfo AllObjectsByLoadIDField = AccessTools.Field(typeof(LoadedObjectDirectory), "allObjectsByLoadID");

        public static bool Prefix(LoadedObjectDirectory __instance, ILoadReferenceable reffable)
        {
            if (!(reffable is Verb verb))
            {
                return true;
            }

            string uniqueLoadId;
            try
            {
                if (!ZhaoliBladeVerbLoadFixUtility.IsZhaoliBladeVerb(verb))
                {
                    return true;
                }

                uniqueLoadId = reffable.GetUniqueLoadID();
            }
            catch
            {
                return true;
            }

            if (!ZhaoliBladeVerbLoadFixUtility.IsZhaoliBladeVerbUniqueLoadId(uniqueLoadId))
            {
                return true;
            }

            Dictionary<string, ILoadReferenceable> loadedObjects = AllObjectsByLoadIDField?.GetValue(__instance) as Dictionary<string, ILoadReferenceable>;
            return loadedObjects == null || !loadedObjects.ContainsKey(uniqueLoadId);
        }
    }

    [HarmonyPatch(typeof(CompEquippable), nameof(CompEquippable.PostExposeData))]
    internal static class Patch_CompEquippable_PostExposeData_ZhaoliBladeVerbTracker
    {
        public static void Prefix(CompEquippable __instance)
        {
            if (Scribe.mode == LoadSaveMode.Saving)
            {
                ZhaoliBladeVerbLoadFixUtility.EnsureCleanVerbTracker(__instance);
            }
        }

        public static void Postfix(CompEquippable __instance)
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ZhaoliBladeVerbLoadFixUtility.EnsureCleanVerbTracker(__instance, force: true);
            }
        }
    }
}

using System;
using HarmonyLib;
using Verse;

namespace MiliraXian.Characters.Neiyu.MeleeAnimationCompat
{
    [StaticConstructorOnStartup]
    public static class MABootstrap
    {
        static MABootstrap()
        {
            if (!IsMeleeAnimationLoaded())
            {
                return;
            }

            new Harmony("MiliraXian.Characters.Neiyu.MeleeAnimationCompat").PatchAll();
            Log.Message("[MiliraXian_NeiyuLaw] Melee Animation compat loaded.");
        }

        private static bool IsMeleeAnimationLoaded()
        {
            for (int i = 0; i < LoadedModManager.RunningModsListForReading.Count; i++)
            {
                ModContentPack mod = LoadedModManager.RunningModsListForReading[i];
                if (mod?.PackageId != null &&
                    mod.PackageId.Equals("co.uk.epicguru.meleeanimation", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return AccessTools.TypeByName("AM.Idle.IdleControllerComp") != null;
        }
    }
}

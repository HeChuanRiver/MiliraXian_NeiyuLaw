using HarmonyLib;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    [StaticConstructorOnStartup]
    public static class MX_QHPatches
    {
        private static Harmony patcher = new Harmony("MiliraXian.Characters.QingHe");
        
        static MX_QHPatches()
        {
            patcher.Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.SpawnSetup)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Pawn_SpawnSetup_Postfix)));
        }

        public static void Patch_Pawn_SpawnSetup_Postfix(Pawn __instance)
        {
            if (MX_QHUtility.IsQinghe(__instance))
            {
                PawnSpecialResourceUtility.EnsureSpecialResourceComp(__instance, MX_QHDefOf.MXQH_Tempest);
                PawnSpecialResourceUtility.EnsureSpecialResourceComp(__instance, MX_QHDefOf.MXQH_Elegance);
            }
        }
    }
}
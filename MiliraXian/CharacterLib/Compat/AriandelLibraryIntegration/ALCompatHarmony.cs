using HarmonyLib;
using Verse;

namespace MiliraXian.CharacterLib.Compat.AriandelLibraryIntegration
{
    /// <summary>Applies every Harmony patch in this compatibility assembly.</summary>
    [StaticConstructorOnStartup]
    internal static class ALCompatHarmony
    {
        static ALCompatHarmony()
        {
            new Harmony("MiliraXian.CharacterLib.ALCompat").PatchAll(typeof(ALCompatHarmony).Assembly);
        }
    }
}

using System.Reflection;
using HarmonyLib;
using Verse;

namespace MiliraXian.Characters
{
    /// <summary>Applies every Harmony patch in this assembly.</summary>
    [StaticConstructorOnStartup]
    public static class NeiyuLawHarmony
    {
        static NeiyuLawHarmony()
        {
            new Harmony("HeChuanRiver.MiliraXian.Characters").PatchAll(Assembly.GetExecutingAssembly());
        }
    }
}

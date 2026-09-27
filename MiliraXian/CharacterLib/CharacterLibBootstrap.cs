using HarmonyLib;
using Verse;

namespace MiliraXian.CharacterLib
{
    /// <summary>
    /// Registers this assembly's Harmony patches. The library ships inside the Characters mod
    /// package but must not depend on it, so it carries its own patch entry point.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class CharacterLibBootstrap
    {
        static CharacterLibBootstrap()
        {
            new Harmony("MiliraXian.CharacterLib").PatchAll(typeof(CharacterLibBootstrap).Assembly);
        }
    }
}

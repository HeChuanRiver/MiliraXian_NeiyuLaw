using Verse;
using HarmonyLib;

namespace MiliraXian.CharacterLib
{
    [StaticConstructorOnStartup]
    public static class CharacterLibMod
    {
        public const string AssemblyName = "MiliraXian_CharacterLib";
        static CharacterLibMod() { new Harmony("MiliraXian.CharacterLib").PatchAll(typeof(CharacterLibMod).Assembly); }
    }
}

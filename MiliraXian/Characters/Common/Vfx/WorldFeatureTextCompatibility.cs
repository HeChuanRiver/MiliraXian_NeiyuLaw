using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld.Planet;
using UnityEngine;

namespace MiliraXian.Characters.Common.Vfx
{
    internal static class WorldFeatureTextCompatibility
    {
        internal static bool IsWaylith => typeof(GameObject).Assembly.GetName().Name == "UnityStubs";
        internal const string FontBackend = "RimWorldWaylith.Imgui.ImGuiTextMeshProBackend";
    }

    [HarmonyPatch(typeof(WorldFeatures), "CreateOrDestroyTexts")]
    internal static class Patch_WorldFeatures_WaylithText
    {
        private static bool Prepare() => WorldFeatureTextCompatibility.IsWaylith;

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            ConstructorInfo legacy = AccessTools.Constructor(typeof(WorldFeatureTextMesh_Legacy));
            ConstructorInfo native = AccessTools.Constructor(typeof(WorldFeatureTextMesh_TextMeshPro));
            foreach (CodeInstruction instruction in instructions)
            {
                // 1.7.4976's TextMesh has no renderer. Its failed Init leaves textsCreated
                // true and the native text list incomplete, causing UpdateFeatures to overrun it.
                // Keep the native list/position/visibility lifecycle and replace only its fallback.
                if (instruction.opcode == OpCodes.Newobj && Equals(instruction.operand, legacy))
                    instruction.operand = native;
                yield return instruction;
            }
        }
    }

    [HarmonyPatch]
    internal static class Patch_WaylithWorldText_MissingGlyph
    {
        private static bool Prepare() => WorldFeatureTextCompatibility.IsWaylith;

        private static MethodBase TargetMethod() => AccessTools.Method(
            AccessTools.TypeByName(WorldFeatureTextCompatibility.FontBackend), "TryGetGlyph");

        [HarmonyTranspiler]
        private static IEnumerable<CodeInstruction> Transpiler(
            IEnumerable<CodeInstruction> instructions, ILGenerator generator, MethodBase __originalMethod)
        {
            MethodInfo query = AccessTools.Method(__originalMethod.DeclaringType, "TryGetGlyphAtSize");
            MethodInfo bakeSize = AccessTools.PropertyGetter(__originalMethod.DeclaringType, "BakePixelSize");
            LocalBuilder ascent = generator.DeclareLocal(typeof(float));
            List<CodeInstruction> code = new(instructions);
            for (int i = 0; i < code.Count; i++)
            {
                // The backend's cached glyphs cover only U+0020-007E and U+00A0-00FF.
                // On a miss use its existing dynamic font/fallback query at the same bake size.
                // Emit the call directly: no per-character reflection or second glyph cache.
                if (code[i].opcode == OpCodes.Ldc_I4_0 && i + 1 < code.Count
                    && code[i + 1].opcode == OpCodes.Ret)
                {
                    CodeInstruction first = new(code[i]) { opcode = OpCodes.Ldarg_0, operand = null };
                    yield return first;
                    yield return new CodeInstruction(OpCodes.Ldarg_1);
                    yield return new CodeInstruction(OpCodes.Ldarg_2);
                    yield return new CodeInstruction(OpCodes.Ldarg_0);
                    yield return new CodeInstruction(OpCodes.Call, bakeSize);
                    yield return new CodeInstruction(OpCodes.Ldarg_3);
                    yield return new CodeInstruction(OpCodes.Ldloca, ascent);
                    yield return new CodeInstruction(OpCodes.Call, query);
                }
                else yield return code[i];
            }
        }
    }
}

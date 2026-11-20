using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// One material per (shader, texture) for the whole mod.
    ///
    /// Materials come from <see cref="MaterialPool"/>, which owns them: the cache never destroys
    /// anything, it only avoids asking the pool for the same combination twice. Call
    /// <see cref="Clear"/> only when the game itself is tearing down.
    /// </summary>
    internal static class VfxMaterialCache
    {
        private static readonly Dictionary<string, Material> Cache = new();

        /// <summary>The material for <paramref name="texPath"/> drawn with the shader named by a ShaderDef.</summary>
        internal static Material FromTexture(string texPath, string shaderDefName)
        {
            string key = shaderDefName + "|" + texPath;
            if (Cache.TryGetValue(key, out Material cached))
            {
                return cached;
            }

            Material material = null;
            Shader shader = ShaderOf(shaderDefName);
            Texture2D texture = TextureOf(texPath);
            if (shader != null && texture != null)
            {
                material = MaterialPool.MatFrom(texture, shader, Color.white);
            }
            Cache[key] = material;
            return material;
        }

        /// <summary>The material for an already resolved texture and shader.</summary>
        internal static Material FromShader(Texture2D texture, Shader shader)
        {
            if (texture == null || shader == null)
            {
                return null;
            }
            string key = shader.name + "|" + texture.name;
            if (Cache.TryGetValue(key, out Material cached))
            {
                return cached;
            }
            Material material = MaterialPool.MatFrom(texture, shader, Color.white);
            Cache[key] = material;
            return material;
        }

        /// <summary>The material for an engine-owned texture drawn with a ShaderDef.</summary>
        internal static Material FromShader(Texture2D texture, string shaderDefName)
        {
            if (texture == null)
            {
                return null;
            }
            Shader shader = ShaderOf(shaderDefName);
            return shader == null ? null : FromShader(texture, shader);
        }

        /// <summary>A material an XML MaterialDef already owns; shared, never destroyed here.</summary>
        internal static Material FromMaterialDef(string materialDefName)
        {
            if (materialDefName.NullOrEmpty())
            {
                return null;
            }
            string key = "mat|" + materialDefName;
            if (Cache.TryGetValue(key, out Material cached))
            {
                return cached;
            }
            MaterialDef def = DefDatabase<MaterialDef>.GetNamedSilentFail(materialDefName);
            Material material = def?.Material;
            if (material == null)
            {
                Log.ErrorOnce("[MiliraXian] VFX material def is missing: " + materialDefName,
                    GenText.StableStringHash("MX_Vfx_Material_" + materialDefName));
            }
            Cache[key] = material;
            return material;
        }

        internal static Shader ShaderOf(string shaderDefName)
        {
            Shader shader = DefDatabase<ShaderDef>.GetNamedSilentFail(shaderDefName)?.Shader;
            if (shader == null)
            {
                Log.ErrorOnce("[MiliraXian] VFX shader is missing: " + shaderDefName,
                    GenText.StableStringHash("MX_Vfx_Shader_" + shaderDefName));
            }
            return shader;
        }

        internal static Texture2D TextureOf(string texPath)
        {
            if (texPath.NullOrEmpty())
            {
                return null;
            }
            Texture2D texture = ContentFinder<Texture2D>.Get(texPath, reportFailure: false);
            if (texture == null)
            {
                Log.ErrorOnce("[MiliraXian] VFX texture is missing: " + texPath,
                    GenText.StableStringHash("MX_Vfx_Texture_" + texPath));
            }
            return texture;
        }

        /// <summary>Drops the lookup table. The pooled materials themselves are not destroyed.</summary>
        internal static void Clear()
        {
            Cache.Clear();
        }
    }
}

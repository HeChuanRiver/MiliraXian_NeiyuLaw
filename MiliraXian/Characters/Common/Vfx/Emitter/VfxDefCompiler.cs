using System.Collections.Generic;
using HarmonyLib;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// The renderer kinds an authored pass may name, and the loader that turns a <see cref="VfxDef"/>
    /// into the runtime <see cref="VfxSpec"/> the host works with.
    ///
    /// Specs are cached per def: a pass list is immutable in practice, and a hot-reloaded def is a
    /// new object, so the cache follows the reload by itself.
    /// </summary>
    internal static class VfxRenderers
    {
        internal const string Field = "Field";
        internal const string Sprite = "Sprite";
        internal const string Streak = "Streak";

        internal static bool TryGet(string name, out IVfxRenderer renderer)
        {
            renderer = null;
            if (name.NullOrEmpty())
            {
                return false;
            }
            switch (name.Trim())
            {
                case Field:
                    renderer = FieldRenderer.Instance;
                    return true;
                case Sprite:
                    renderer = SpriteRenderer.Instance;
                    return true;
                case Streak:
                    renderer = StreakRenderer.Instance;
                    return true;
                default:
                    return false;
            }
        }
    }

    internal static class VfxDefCompiler
    {
        private static readonly Dictionary<VfxDef, VfxEffect> Cache = new();

        /// <summary>The runtime effect for a def, built on first use.</summary>
        internal static VfxEffect EffectOf(VfxDef def)
        {
            if (def == null)
            {
                return null;
            }
            if (Cache.TryGetValue(def, out VfxEffect cached))
            {
                return cached;
            }
            VfxEffect effect = Create(def);
            Cache[def] = effect;
            return effect;
        }

        private static VfxEffect Create(VfxDef def)
        {
            VfxEffect effect = NewEffect(def);
            effect.EmitterType = ResolveType(def.emitterClass, typeof(VfxEmitter));
            return effect;
        }

        /// <summary>
        /// Throws every cached effect away and lets each release what it built for itself.
        ///
        /// Defs are process-scoped objects, so the cache would otherwise live as long as the process
        /// and keep a recompiled def's meshes reachable forever. Call it when the game is (re)entered
        /// and after a def reload.
        /// </summary>
        internal static void Reset()
        {
            foreach (VfxEffect effect in Cache.Values)
            {
                try
                {
                    effect?.Release();
                }
                catch (System.Exception exception)
                {
                    Log.ErrorOnce("[MiliraXian] A VFX effect failed to release: " + exception,
                        GenText.StableStringHash("MX_Vfx_Release_" + effect?.Name));
                }
            }
            Cache.Clear();
            VfxFrameUniforms.ResetClock();
            PrewarmPending = true;
        }

        private static VfxEffect NewEffect(VfxDef def)
        {
            System.Type type = ResolveType(def.vfxClass, typeof(VfxEffect));
            try
            {
                // With arguments, Activator only looks at public constructors by default; ours are
                // internal, so ask for non-public ones explicitly.
                return (VfxEffect)System.Activator.CreateInstance(
                    type,
                    System.Reflection.BindingFlags.Instance
                        | System.Reflection.BindingFlags.Public
                        | System.Reflection.BindingFlags.NonPublic,
                    null,
                    new object[] { def },
                    null);
            }
            catch (System.Exception exception)
            {
                string what = def.vfxClass.NullOrEmpty() ? type.Name : def.vfxClass;
                Log.ErrorOnce("[MiliraXian] VFX class '" + what + "' could not be built for '"
                    + def.defName + "': " + exception, GenText.StableStringHash("MX_Vfx_Effect_" + def.defName));
                return new VfxEffect(def);
            }
        }

        /// <summary>
        /// Set when a game is entered; the first main-thread frame consumes it. The warm-up is not run
        /// where the game is loaded because that can happen on a background thread, and building
        /// materials there is forbidden.
        /// </summary>
        internal static bool PrewarmPending;

        /// <summary>
        /// Builds every VFX effect ahead of time so the first cast of each does not pay for material
        /// creation and shader resolution. Must run on the main thread; a failure here must not stop
        /// the game, so each def is guarded on its own.
        /// </summary>
        internal static void Prewarm()
        {
            PrewarmPending = false;
            List<VfxDef> defs = DefDatabase<VfxDef>.AllDefsListForReading;
            for (int i = 0; i < defs.Count; i++)
            {
                VfxDef def = defs[i];
                try
                {
                    EffectOf(def);
                }
                catch (System.Exception exception)
                {
                    Log.ErrorOnce("[MiliraXian] VFX '" + def.defName + "' failed to prewarm: " + exception,
                        GenText.StableStringHash("MX_Vfx_Prewarm_" + def.defName));
                }
            }
        }

        /// <summary>A type named by a def, or the base type when the name is empty or unusable.</summary>
        private static System.Type ResolveType(string name, System.Type baseType)
        {
            if (name.NullOrEmpty())
            {
                return baseType;
            }
            System.Type type = AccessTools.TypeByName(name);
            return type != null && baseType.IsAssignableFrom(type) ? type : baseType;
        }

        internal static bool IsValidVfxClass(string name)
        {
            return IsValidDerived(name, typeof(VfxEffect));
        }

        internal static bool IsValidEmitterClass(string name)
        {
            return IsValidDerived(name, typeof(VfxEmitter));
        }

        private static bool IsValidDerived(string name, System.Type baseType)
        {
            if (name.NullOrEmpty())
            {
                return false;
            }
            System.Type type = AccessTools.TypeByName(name);
            return type != null && baseType.IsAssignableFrom(type);
        }
    }
}

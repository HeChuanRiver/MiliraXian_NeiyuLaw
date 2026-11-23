using MiliraXian.Characters.Common.Vfx.Emitter;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    /// <summary>Timing and placement parameters carried by <c>MX_Vfx_Luohua</c>.</summary>
    public class LuohuaVfxExtension : DefModExtension
    {
        public Vector2 offset;
        public float fadeInSeconds = 0.7f;
        public float holdSeconds = 1.2f;
        public float dissolveSeconds = 0.8f;
        public float darkenAlpha = 0.65f;
        public SoundDef sound;
    }

    /// <summary>
    /// Drives the execution kill glyph ("落华") and its surrounding darkening disc: both fade in
    /// together, hold, then the glyph dissolves through a noise map while the darkening fades out.
    /// </summary>
    internal sealed class LuohuaEmitter : VfxEmitter
    {
        private static readonly LuohuaVfxExtension DefaultProps = new();

        public LuohuaEmitter()
        {
        }

        internal static void Spawn(Map map, VfxDef def, Vector3 at)
        {
            if (map == null || def == null)
            {
                return;
            }
            def.GetModExtension<LuohuaVfxExtension>()?.sound?.PlayOneShot(new TargetInfo(at.ToIntVec3(), map));
            map.GetComponent<MapComponent_VfxEmitterHost>()?.Spawn(def, VfxAnchor.At(at));
        }

        internal override bool Advance(float deltaSeconds, int nowTick, Map map)
        {
            if (!base.Advance(deltaSeconds, nowTick, map))
            {
                return false;
            }

            LuohuaVfxExtension ext = Effect?.Def?.GetModExtension<LuohuaVfxExtension>() ?? DefaultProps;
            float t = AgeSeconds;
            float fadeIn = ext.fadeInSeconds > 0f ? Mathf.Clamp01(t / ext.fadeInSeconds) : 1f;
            float dissolve = ext.dissolveSeconds > 0f
                ? Mathf.Clamp01((t - ext.fadeInSeconds - ext.holdSeconds) / ext.dissolveSeconds)
                : 0f;

            SetEmitterChannel(VfxChannels.Alpha, ext.darkenAlpha * fadeIn * (1f - dissolve));
            SetEmitterChannel(VfxChannels.Progress, dissolve);

            if (Count == 0)
            {
                Vector3 textPos = new(Position.x + ext.offset.x, Position.y, Position.z + ext.offset.y);
                Emit(textPos, 0f, 1f, new Color(1f, 1f, 1f, fadeIn), particleGroup: 0);
            }

            if (Count > 0)
            {
                VfxParticle p = Particles[0];
                p.Alive = dissolve < 1f;
                p.Color = new Color(1f, 1f, 1f, fadeIn);
                Particles[0] = p;
            }

            return true;
        }
    }
}

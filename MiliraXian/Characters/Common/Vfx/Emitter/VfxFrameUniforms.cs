using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.Emitter
{
    /// <summary>
    /// Values that are the same for every stroke this frame because they come from the camera, not
    /// from the effect. They change with zoom, so they are refreshed once per draw frame.
    ///
    /// This is the third kind of uniform data, next to per-pass constants and per-instance rows:
    /// <see cref="ScreenPerCell"/> feeds the refraction strokes of the rain wall.
    /// </summary>
    internal sealed class VfxFrameUniforms
    {
        private static int clockOriginTick = -1;

        /// <summary>How far one map cell moves in screen space: (1/(2·size·aspect), -1/(2·size), 0.25, 0.05).</summary>
        internal Vector4 ScreenPerCell = new(0f, 0f, 0.25f, 0.05f);

        /// <summary>
        /// The clock shaders animate against, on the same scale as <see cref="VfxParticle.SpawnSeconds"/>:
        /// VisualClock game time, so a sped-up game animates faster and a paused one stands still.
        /// </summary>
        internal float Time;

        internal static void ResetClock()
        {
            clockOriginTick = -1;
        }

        internal static float CurrentTimeSeconds()
        {
            TickManager tm = Find.TickManager;
            if (tm == null)
            {
                return 0f;
            }
            if (clockOriginTick < 0)
            {
                clockOriginTick = tm.TicksGame;
            }
            return VisualClock.TicksSince(clockOriginTick) / 60f;
        }

        /// <summary>Recomputes the camera-derived values. Called once per draw frame, on the main thread.</summary>
        internal void Refresh()
        {
            Time = CurrentTimeSeconds();

            Camera camera = Find.Camera;
            if (camera != null && camera.orthographicSize > 0f)
            {
                float height = 2f * camera.orthographicSize;
                ScreenPerCell = new Vector4(
                    1f / (height * Mathf.Max(0.1f, camera.aspect)),
                    -1f / height,
                    0.25f,
                    0.05f);
            }
        }
    }
}

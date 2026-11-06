using System;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx
{
    /// <summary>
    /// Game time for visuals: ticks plus the fraction of the tick in progress, so effects move
    /// smoothly at high frame rates, freeze when paused and follow the game speed.
    /// </summary>
    public static class VisualClock
    {
        private static readonly AccessTools.FieldRef<TickManager, float> RealTimeToTickThrough =
            SafeFieldRef();

        private static AccessTools.FieldRef<TickManager, float> SafeFieldRef()
        {
            try
            {
                return AccessTools.FieldRefAccess<TickManager, float>("realTimeToTickThrough");
            }
            catch (Exception ex)
            {
                Log.Warning("[MiliraXian] VisualClock: TickManager.realTimeToTickThrough not found, visuals step per tick. " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Interpolated tick. The tick loop leaves a residue in (-curTimePerTick, 0]: the game is that
        /// much real time ahead of the frame, so the frame shows TicksGame minus that share of a tick.
        /// </summary>
        public static float Ticks => TicksSince(0);

        // Subtract the origin as integers to retain sub-tick precision in older colonies.
        internal static float TicksSince(int origin)
        {
            TickManager tm = Find.TickManager;
            if (tm == null) return 0f;
            int elapsed = tm.TicksGame - origin;
            if (RealTimeToTickThrough == null || tm.Paused) return elapsed;
            float rate = tm.TickRateMultiplier;
            if (rate <= 0f) return elapsed;
            float perTick = 1f / (60f * rate);
            if (perTick <= 0f) return elapsed;
            return elapsed + Mathf.Clamp(RealTimeToTickThrough(tm) / perTick, -1f, 0f);
        }
    }
}

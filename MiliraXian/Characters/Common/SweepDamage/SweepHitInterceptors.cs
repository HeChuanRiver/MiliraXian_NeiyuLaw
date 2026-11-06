using System;
using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters.Common.SweepDamage
{
    /// <summary>
    /// Reactions that take a sweep contact instead of its damage (a defender turning the cut into its
    /// own skill). Asked in order when a contact lands; the first that returns true consumes it, and
    /// the contact deals no damage, posture or hit effect. Characters register at startup.
    /// </summary>
    public static class SweepHitInterceptors
    {
        public delegate bool Interceptor(SweepDamageSource source, Thing target);

        private static readonly List<Interceptor> Interceptors = new();

        public static void Register(Interceptor interceptor)
        {
            if (interceptor != null && !Interceptors.Contains(interceptor))
            {
                Interceptors.Add(interceptor);
            }
        }

        public static bool TryIntercept(SweepDamageSource source, Thing target)
        {
            for (int i = 0; i < Interceptors.Count; i++)
            {
                try
                {
                    if (Interceptors[i](source, target))
                    {
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    Log.ErrorOnce("[MiliraXian] Sweep hit interceptor failed: " + ex, 0x51c7a8);
                }
            }
            return false;
        }
    }
}

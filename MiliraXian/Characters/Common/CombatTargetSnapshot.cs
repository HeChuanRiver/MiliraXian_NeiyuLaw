using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters
{
    // Damage callbacks may despawn targets or start another area effect. Each
    // caller owns its snapshot until Return, including during nested callbacks.
    internal static class CombatTargetSnapshot
    {
        public static List<T> Rent<T>(IEnumerable<T> source)
        {
            List<T> snapshot = SimplePool<List<T>>.Get();
            snapshot.Clear();
            try
            {
                snapshot.AddRange(source);
                return snapshot;
            }
            catch
            {
                Return(snapshot);
                throw;
            }
        }

        public static void Return<T>(List<T> snapshot)
        {
            snapshot.Clear();
            SimplePool<List<T>>.Return(snapshot);
        }
    }
}

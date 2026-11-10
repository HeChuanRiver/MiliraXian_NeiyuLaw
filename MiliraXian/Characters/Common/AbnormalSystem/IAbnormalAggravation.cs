using Verse;

namespace MiliraXian.Characters.Common.AbnormalSystem
{
    /// <summary>
    /// An abnormal's effect (the hediff or one of its comps) that answers an aggravation: what is left
    /// of it is settled at once (RangedRework.md 3.4). The effect then ends, so the target can build the
    /// abnormal up anew, unless an implementer keeps it (fear and overload refresh instead).
    /// </summary>
    public interface IAbnormalAggravation
    {
        /// <param name="source">Who filled the aggravation bar.</param>
        /// <param name="remainingTicks">Ticks the effect still had to run.</param>
        /// <param name="keepEffect">Set true to keep the effect after this (it refreshed itself).</param>
        void Aggravate(Pawn source, int remainingTicks, ref bool keepEffect);
    }
}

using Verse;

namespace MiliraXian.Characters.Common.SweepDamage
{
    /// <summary>A melee verb that resolves each contact of its own sweeps (hit, dodge, block, log, damage).</summary>
    public interface ISweepHitVerb
    {
        void ResolveSweepHit(SweepDamageSource source, Thing target);
    }

    /// <summary>
    /// Hands every contact to the sweep's <see cref="SweepDamageSource.verb"/> when it resolves its
    /// own hits, so the cut keeps the verb's miss, dodge, block and combat-log rules; otherwise
    /// applies plain damage.
    /// </summary>
    public class SweepDamageWorker_Verb : SweepDamageWorker
    {
        public override void ApplyHit(SweepDamageSource source, Thing target)
        {
            if (source.verb is ISweepHitVerb verb)
            {
                verb.ResolveSweepHit(source, target);
            }
            // Without its verb (lost on load) the amount is only a share: deal nothing rather than a wrong value.
        }
    }
}

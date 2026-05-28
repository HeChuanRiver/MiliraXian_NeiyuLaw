using Verse;

namespace MiliraXian.Characters
{
    public interface IAccumulationHediff
    {
        Pawn Caster { get; }
        HediffDef Def { get; }
        bool CanAccumulate { get; }
        void AddAccumulation(Pawn caster, float severityOffset);
    }
}

using System.Collections;
using Verse;

namespace MiliraXian.Characters
{
    public static class PawnSpecialResourceUtility
    {
        public static HediffComp_PawnSpecialResource GetSpecialResourceComp(Pawn pawn, HediffDef specialResourceDef)
        {
            var hediff = (HediffWithComps)pawn?.health.hediffSet?.GetFirstHediffOfDef(specialResourceDef);
            return hediff?.GetComp<HediffComp_PawnSpecialResource>();
        }

        public static HediffComp_PawnSpecialResource EnsureSpecialResourceComp(Pawn pawn, HediffDef specialResourceDef)
        {
            if (specialResourceDef == null)
            {
                return null;
            }
            var comp = GetSpecialResourceComp(pawn, specialResourceDef);
            if (comp != null || pawn?.health == null)
            {
                return comp;
            }
            
            var hediff = HediffMaker.MakeHediff(specialResourceDef, pawn);
            pawn.health.AddHediff(hediff);
            return (hediff as HediffWithComps)?.GetComp<HediffComp_PawnSpecialResource>();
        }
        
        public static float GetCurrentResource(Pawn pawn, HediffDef specialResourceDef)
        {
            return GetSpecialResourceComp(pawn, specialResourceDef)?.CurrentValue ?? 0f;
        }

        public static float GetMaxResource(Pawn pawn, HediffDef specialResourceDef)
        {
            return GetSpecialResourceComp(pawn, specialResourceDef)?.MaxValue ?? 0f;
        }

        public static void AddResource(Pawn pawn, HediffDef specialResourceDef, float value)
        {
            EnsureSpecialResourceComp(pawn, specialResourceDef)?.AddValue(value);
        }

        public static bool TryConsumeResource(Pawn pawn, HediffDef specialResourceDef, float value)
        {
            HediffComp_PawnSpecialResource comp = EnsureSpecialResourceComp(pawn, specialResourceDef);
            return comp != null && comp.TryConsume(value);
        }
    }
}
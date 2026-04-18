using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters
{
    public class ResourceTransactionEntry : IExposable
    {
        public HediffDef resourceDef;
        public float amount;
        public float maxAmount = -1f;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref resourceDef, "resourceDef");
            Scribe_Values.Look(ref amount, "amount", 0f);
            Scribe_Values.Look(ref maxAmount, "maxAmount", -1f);
        }
    }

    public class ResourceRequirementEntry : IExposable
    {
        public HediffDef resourceDef;
        public float minAmount;
        public string disabledReasonKey;

        public void ExposeData()
        {
            Scribe_Defs.Look(ref resourceDef, "resourceDef");
            Scribe_Values.Look(ref minAmount, "minAmount", 0f);
            Scribe_Values.Look(ref disabledReasonKey, "disabledReasonKey");
        }
    }

    public static class ResourceTransactionUtility
    {
        public static float GetCurrent(Pawn pawn, HediffDef resourceDef)
        {
            if (pawn == null || resourceDef == null)
            {
                return 0f;
            }

            return PawnSpecialResourceUtility.GetCurrentResource(pawn, resourceDef);
        }

        public static void ApplyTransaction(Pawn pawn, ResourceTransactionEntry entry)
        {
            if (pawn == null || entry?.resourceDef == null)
            {
                return;
            }

            if (entry.amount > 0f)
            {
                PawnSpecialResourceUtility.AddResource(pawn, entry.resourceDef, entry.amount);
            }
            else if (entry.amount < 0f)
            {
                PawnSpecialResourceUtility.TryConsumeResource(pawn, entry.resourceDef, -entry.amount);
            }
        }

        public static bool HasEnough(Pawn pawn, HediffDef resourceDef, float minAmount)
        {
            return GetCurrent(pawn, resourceDef) >= minAmount;
        }
    }
}

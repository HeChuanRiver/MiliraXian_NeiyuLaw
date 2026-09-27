using System;
using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    public class StatWorker_Mutable : StatWorker
    {
    }

    public class StatWorker_TicksToSeconds : StatWorker
    {
        public override string ValueToString(float val, bool finalized, ToStringNumberSense numberSense = ToStringNumberSense.Absolute)
        {
            int ticks = Math.Abs((int)val);
            string text = ticks.ToStringTicksToPeriod(allowSeconds: true, shortForm: true, canUseDecimals: true, allowYears: false);
            if (val < 0f)
            {
                return "-" + text;
            }
            return numberSense == ToStringNumberSense.Offset ? "+" + text : text;
        }
    }
}

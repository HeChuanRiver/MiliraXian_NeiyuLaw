using System;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    public abstract class AbnormalLimitFactor : StatPart
    {
        protected abstract float FactorFor(Pawn pawn);

        public sealed override void TransformValue(StatRequest req, ref float val)
        {
            Pawn pawn = req.Thing as Pawn;
            if (pawn == null)
            {
                return;
            }

            val *= Mathf.Max(0f, FactorFor(pawn));
        }

        public override string ExplanationPart(StatRequest req)
        {
            return null;
        }

        protected static float FromAccumulationMultiplier(float multiplier)
        {
            return multiplier > 0f ? 1f / multiplier : 0f;
        }

        protected static float FromResistance(float resistance)
        {
            return FromAccumulationMultiplier(1f - Mathf.Clamp01(resistance));
        }
    }

    public class FactorIfMechanoid : AbnormalLimitFactor
    {
        public float factor = 1f;

        protected override float FactorFor(Pawn pawn)
        {
            return pawn.RaceProps?.IsMechanoid == true ? factor : 1f;
        }
    }

    public class FactorIfNotMechanoid : AbnormalLimitFactor
    {
        public float factor = 1f;

        protected override float FactorFor(Pawn pawn)
        {
            return pawn.RaceProps?.IsMechanoid != true ? factor : 1f;
        }
    }

    public class FactorIfNotFlesh : AbnormalLimitFactor
    {
        public float factor = 1f;

        protected override float FactorFor(Pawn pawn)
        {
            return pawn.RaceProps?.IsFlesh != true ? factor : 1f;
        }
    }

    public class FactorIfCannotBleed : AbnormalLimitFactor
    {
        public float factor = 1f;

        protected override float FactorFor(Pawn pawn)
        {
            return pawn.health?.CanBleed != true ? factor : 1f;
        }
    }

    public class FactorIfHasApparel : AbnormalLimitFactor
    {
        public float factor = 1f;

        protected override float FactorFor(Pawn pawn)
        {
            return pawn.apparel?.WornApparelCount > 0 ? factor : 1f;
        }
    }

    public class FactorIfMilian : AbnormalLimitFactor
    {
        public float factor = 1f;

        protected override float FactorFor(Pawn pawn)
        {
            return MilianCompatibility.IsMilian(pawn) ? factor : 1f;
        }
    }

    public class BodySizeFactor : AbnormalLimitFactor
    {
        public SimpleCurve multiplierCurve;
        public bool skipMilian;

        protected override float FactorFor(Pawn pawn)
        {
            if (skipMilian && MilianCompatibility.IsMilian(pawn))
            {
                return 1f;
            }

            float multiplier = multiplierCurve?.Evaluate(Mathf.Max(0f, pawn.BodySize)) ?? 1f;
            return FromAccumulationMultiplier(multiplier);
        }
    }

    public class MechanoidBodySizeFactor : AbnormalLimitFactor
    {
        public SimpleCurve resistanceCurve;
        public float maxResistance = 0.9f;

        protected override float FactorFor(Pawn pawn)
        {
            if (pawn.RaceProps?.IsMechanoid != true)
            {
                return 1f;
            }

            float resistance = resistanceCurve?.Evaluate(Mathf.Max(0f, pawn.BodySize)) ?? 0f;
            return FromResistance(Mathf.Min(resistance, Mathf.Clamp01(maxResistance)));
        }
    }

    public class PawnStatFactor : AbnormalLimitFactor
    {
        public StatDef stat;
        public SimpleCurve multiplierCurve;

        protected override float FactorFor(Pawn pawn)
        {
            if (stat == null)
            {
                return 1f;
            }

            float value = pawn.GetStatValue(stat, true, -1);
            float multiplier = multiplierCurve?.Evaluate(value) ?? value;
            return FromAccumulationMultiplier(multiplier);
        }
    }

    public class ResistanceStatFactor : AbnormalLimitFactor
    {
        public StatDef stat;

        protected override float FactorFor(Pawn pawn)
        {
            return stat != null ? FromResistance(pawn.GetStatValue(stat, true, -1)) : 1f;
        }
    }

    public class NaturalSharpArmorFactor : AbnormalLimitFactor
    {
        public SimpleCurve resistanceCurve;

        protected override float FactorFor(Pawn pawn)
        {
            float armor = Mathf.Max(0f, SharpArmorWithoutApparel(pawn));
            float resistance = resistanceCurve?.Evaluate(armor) ?? 0f;
            return FromResistance(resistance);
        }

        private static float SharpArmorWithoutApparel(Pawn pawn)
        {
            float armor = pawn.GetStatValue(StatDefOf.ArmorRating_Sharp, true, -1);
            if (pawn.apparel == null)
            {
                return armor;
            }

            var wornApparel = pawn.apparel.WornApparel;
            for (int i = 0; i < wornApparel.Count; i++)
            {
                armor -= StatWorker.StatOffsetFromGear(wornApparel[i], StatDefOf.ArmorRating_Sharp);
            }

            return armor;
        }
    }

    public class ComfyTemperatureFactor : AbnormalLimitFactor
    {
        public float scaleStart;
        public float scaleZero = -100f;

        protected override float FactorFor(Pawn pawn)
        {
            if (pawn.RaceProps?.IsMechanoid == true)
            {
                return 1f;
            }

            float comfyTemperatureMin = pawn.GetStatValue(StatDefOf.ComfyTemperatureMin, true, -1);
            return FromAccumulationMultiplier(TemperatureMultiplier(comfyTemperatureMin));
        }

        private float TemperatureMultiplier(float value)
        {
            if (Mathf.Approximately(scaleStart, scaleZero))
            {
                return value <= scaleZero ? 0f : 1f;
            }

            if (scaleStart > scaleZero)
            {
                if (value >= scaleStart)
                {
                    return 1f;
                }

                if (value <= scaleZero)
                {
                    return 0f;
                }
            }
            else
            {
                if (value <= scaleStart)
                {
                    return 1f;
                }

                if (value >= scaleZero)
                {
                    return 0f;
                }
            }

            return Mathf.Clamp01(Mathf.InverseLerp(scaleZero, scaleStart, value));
        }
    }

    internal static class MilianCompatibility
    {
        private static readonly Func<Pawn, bool> isMilian = CreateIsMilianDelegate();

        public static bool IsMilian(Pawn pawn)
        {
            return pawn != null && isMilian != null && isMilian(pawn);
        }

        private static Func<Pawn, bool> CreateIsMilianDelegate()
        {
            try
            {
                Type utilityType = AccessTools.TypeByName("Milira.MilianUtility");
                var method = utilityType != null
                    ? AccessTools.Method(utilityType, "IsMilian", new[] { typeof(Pawn) })
                    : null;
                return method != null
                    ? (Func<Pawn, bool>)Delegate.CreateDelegate(typeof(Func<Pawn, bool>), method)
                    : null;
            }
            catch (Exception ex)
            {
                Log.Warning("[MiliraXian.Characters] Failed to bind optional Milira.MilianUtility.IsMilian: " + ex.Message);
                return null;
            }
        }
    }
}

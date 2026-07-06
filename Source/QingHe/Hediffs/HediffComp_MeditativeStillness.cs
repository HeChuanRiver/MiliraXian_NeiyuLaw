using UnityEngine;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_MeditativeStillness : HediffCompProperties_PawnSpecialResource
    {
        public float baseGainPerDay = 100f;
        public float partialQualityConsumeFactor = 0.3f;
        public float fullQualityConsumeFactor = 0.8f;
        public float qualityBonusChancePerFullStillness = 1f;
        public int maxNormalQualityBonusLevels = 1;
        public int fullQualityBonusLevels = 1;
        public string longNightLabel = "MX_QH_LongNightStillnessLabel";
        public string longNightDescription = "MX_QH_LongNightStillnessDescription";

        public HediffCompProperties_MeditativeStillness()
        {
            compClass = typeof(HediffComp_MeditativeStillness);
        }
    }

    public class Hediff_MeditativeStillness : HediffWithComps
    {
        private HediffComp_MeditativeStillness StillnessComp => GetComp<HediffComp_MeditativeStillness>();

        public override string LabelBase
        {
            get
            {
                HediffComp_MeditativeStillness comp = StillnessComp;
                if (comp?.LongNightReady == true && !comp.PropsStillness.longNightLabel.NullOrEmpty())
                {
                    return MX_QHUtility.TranslateIfKey(comp.PropsStillness.longNightLabel);
                }

                return base.LabelBase;
            }
        }

        public override string LabelInBrackets
        {
            get
            {
                HediffComp_MeditativeStillness comp = StillnessComp;
                if (comp == null || comp.LongNightReady)
                {
                    return base.LabelInBrackets;
                }

                return comp.ValuePercent.ToStringPercent();
            }
        }

        public override string Description
        {
            get
            {
                HediffComp_MeditativeStillness comp = StillnessComp;
                if (comp?.LongNightReady == true && !comp.PropsStillness.longNightDescription.NullOrEmpty())
                {
                    return MX_QHUtility.TranslateIfKey(comp.PropsStillness.longNightDescription);
                }

                return base.Description;
            }
        }
    }

    public class HediffComp_MeditativeStillness : HediffComp_PawnSpecialResource
    {
        public HediffCompProperties_MeditativeStillness PropsStillness => (HediffCompProperties_MeditativeStillness)props;

        public bool LongNightReady => MaxValue > 0f && CurrentValue >= MaxValue - 0.001f;

        public override void CompExposeData()
        {
            base.CompExposeData();
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
        }

        public void AddStillness(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            bool wasReady = LongNightReady;
            AddValue(amount);
            if (!wasReady && LongNightReady && Pawn != null)
            {
                Messages.Message("MX_QH_MeditativeStillnessFullMessage".Translate(), Pawn, MessageTypeDefOf.PositiveEvent, historical: false);
            }
        }

        public int ConsumeForQualityBonus()
        {
            if (CurrentValue <= 0.001f)
            {
                return 0;
            }

            if (LongNightReady)
            {
                float fullConsumed = MaxValue * Mathf.Clamp01(PropsStillness.fullQualityConsumeFactor);
                SetValue(CurrentValue - fullConsumed);
                return Mathf.Max(0, PropsStillness.fullQualityBonusLevels);
            }

            float consumed = CurrentValue * Mathf.Clamp01(PropsStillness.partialQualityConsumeFactor);
            if (consumed <= 0.001f)
            {
                return 0;
            }

            SetValue(CurrentValue - consumed);
            return RollQualityBonusLevels(consumed / Mathf.Max(1f, MaxValue) * PropsStillness.qualityBonusChancePerFullStillness);
        }

        private int RollQualityBonusLevels(float expectedLevels)
        {
            int maxLevels = Mathf.Max(0, PropsStillness.maxNormalQualityBonusLevels);
            if (maxLevels <= 0 || expectedLevels <= 0f)
            {
                return 0;
            }

            int levels = Mathf.FloorToInt(expectedLevels);
            float fractional = expectedLevels - levels;
            if (fractional > 0f && Rand.Value < fractional)
            {
                levels++;
            }

            return Mathf.Clamp(levels, 0, maxLevels);
        }
    }
}

using UnityEngine;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_MeditativeStillness : HediffCompProperties_PawnSpecialResource
    {
        public float meditationGainPerDay = 100f;
        public float readingGainPerDay = 100f;
        public float sleepGainPerDay = 100f;
        public float partialQualityBonusChancePerFull = 0.5f;
        public int fullQualityBonusLevels = 2;
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
                    return MX_QHCharacterUtility.TranslateIfKey(comp.PropsStillness.longNightLabel);
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
                    return MX_QHCharacterUtility.TranslateIfKey(comp.PropsStillness.longNightDescription);
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
                SetValue(0f);
                return Mathf.Max(0, PropsStillness.fullQualityBonusLevels);
            }

            float chance = Mathf.Clamp01(PropsStillness.partialQualityBonusChancePerFull * ValuePercent);
            return chance > 0f && Rand.Value < chance ? 1 : 0;
        }
    }
}

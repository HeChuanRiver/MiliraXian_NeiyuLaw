using UnityEngine;
using RimWorld;
using Verse;
using MiliraXian.Characters.Common;
using MiliraXian.Characters.QingHe;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_MeditativeStillness : HediffCompProperties_PawnSpecialResource
    {
        public float meditationGainPerSecond = 0.6f;
        public float readingGainPerSecond = 0.48f;
        public float sleepGainPerSecond = 0.12f;
        public float lotusPondFactor = 1.1f;
        public float environmentFactorBase = 1f;
        public float environmentFactorPerBeauty = 0.1f;
        public float environmentFactorPerCleanliness = 0.25f;
        public float environmentFactorMin = 0.75f;
        public float environmentFactorMax = 1.5f;
        public float partialQualityBonusChancePerFull = 0.5f;
        public int fullQualityBonusLevels = 2;
        public string longNightLabel = "MX_QH_LongNightStillnessLabel";
        public string longNightDescription = "MX_QH_LongNightStillnessDescription";

        public HediffCompProperties_MeditativeStillness()
        {
            compClass = typeof(HediffComp_MeditativeStillness);
        }
    }

    public class Hediff_MeditativeStillness : Hediff_PawnSpecialResource
    {
        private HediffComp_MeditativeStillness StillnessComp => GetComp<HediffComp_MeditativeStillness>();

        public override string LabelBase
        {
            get
            {
                HediffComp_MeditativeStillness comp = StillnessComp;
                if (comp?.LongNightReady == true && !comp.PropsStillness.longNightLabel.NullOrEmpty())
                {
                    return comp.PropsStillness.longNightLabel.Translate();
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
                    return comp.PropsStillness.longNightDescription.Translate();
                }

                return base.Description;
            }
        }
    }

    public class HediffComp_MeditativeStillness : HediffComp_PawnSpecialResource
    {
        private const int EnvironmentSampleIntervalTicks = 300;
        private const int MoteIntervalTicks = 1800;

        private float cachedEnvironmentFactor = 1f;
        private int environmentFactorCachedTick = -1;
        private bool wasLongNightReady;
        private float valueAtLastMote = -1f;

        public HediffCompProperties_MeditativeStillness PropsStillness => (HediffCompProperties_MeditativeStillness)props;

        public bool LongNightReady => MaxValue > 0f && CurrentValue >= MaxValue - 0.001f;

        public override bool CompDisallowVisible()
        {
            return base.CompDisallowVisible() || !QinghePowerBalance.ZeroLevelPassivesEnabled;
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);

            bool ready = LongNightReady;
            if (ready && !wasLongNightReady && Pawn != null)
            {
                Messages.Message("MX_QH_MeditativeStillnessFullMessage".Translate(), Pawn, MessageTypeDefOf.PositiveEvent, historical: false);
            }

            wasLongNightReady = ready;

            if (valueAtLastMote < 0f)
            {
                valueAtLastMote = CurrentValue;
            }
            else if (Pawn.IsHashIntervalTick(MoteIntervalTicks) && Pawn.Spawned)
            {
                float gained = CurrentValue - valueAtLastMote;
                valueAtLastMote = CurrentValue;
                if (gained > 0f && MaxValue > 0f)
                {
                    Color textColor = Color.Lerp(parent.def.defaultLabelColor, Color.black, 0.3f);
                    string percent = (gained / MaxValue * 100f).ToString("0.##");
                    MoteMaker.ThrowText(Pawn.DrawPos, Pawn.Map, "MX_QH_MeditativeStillnessGainMote".Translate(percent), textColor, 1.1f);
                }
            }
        }

        // 冥想的环境倍率：美观用原版 BeautyUtility.AverageBeautyPerceptible（半径 8.9 ∩ 可见房间），
        // 清洁度用所在房间的原版房间统计（RoomStatWorker_Cleanliness）。采样贵，缓存一个采样间隔（300t）。
        public float GetEnvironmentFactor()
        {
            int ticksGame = Find.TickManager.TicksGame;
            if (ticksGame - environmentFactorCachedTick < EnvironmentSampleIntervalTicks)
            {
                return cachedEnvironmentFactor;
            }

            environmentFactorCachedTick = ticksGame;
            cachedEnvironmentFactor = SampleEnvironmentFactor();
            return cachedEnvironmentFactor;
        }

        private float SampleEnvironmentFactor()
        {
            HediffCompProperties_MeditativeStillness props = PropsStillness;
            Pawn pawn = Pawn;
            Map map = pawn?.Map;
            if (map == null || !pawn.Position.IsValid || !pawn.Position.InBounds(map))
            {
                return props.environmentFactorBase;
            }

            float beauty = BeautyUtility.AverageBeautyPerceptible(pawn.Position, map);
            float cleanliness = pawn.GetRoom().GetStat(RoomStatDefOf.Cleanliness);
            return Mathf.Clamp(
                props.environmentFactorBase + beauty * props.environmentFactorPerBeauty + cleanliness * props.environmentFactorPerCleanliness,
                props.environmentFactorMin,
                props.environmentFactorMax);
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

    public class HediffCompProperties_StillnessGathering : HediffCompProperties
    {
        public HediffCompProperties_StillnessGathering()
        {
            compClass = typeof(HediffComp_StillnessGathering);
        }
    }

    /// <summary>
    /// The hediff is permanent and its severity is the current accrual rate per second. Severity
    /// stays inside one stage, so refreshing or idling it never raises vanilla's situational
    /// thought invalidation, which adding or removing a hediff would do on every activity edge.
    /// </summary>
    public class HediffComp_StillnessGathering : HediffComp
    {
        private const int LapseTicks = 30;

        private int lastRefreshTick = -LapseTicks - 1;

        public override bool CompDisallowVisible()
        {
            return true;
        }

        public void Refresh(float ratePerSecond)
        {
            lastRefreshTick = Find.TickManager.TicksGame;
            if (Mathf.Abs(parent.Severity - ratePerSecond) > 0.0001f)
            {
                parent.Severity = ratePerSecond;
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (parent.Severity > parent.def.minSeverity && Find.TickManager.TicksGame - lastRefreshTick > LapseTicks)
            {
                parent.Severity = parent.def.minSeverity;
            }
        }
    }
}

using RimWorld;
using UnityEngine;
using Verse;
using MiliraXian.Characters.QingHe;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_AuraMastery : HediffCompProperties
    {
        public HediffCompProperties_AuraMastery()
        {
            compClass = typeof(HediffComp_AuraMastery);
        }
    }

    /// <summary>
    /// Carries aura mastery: the earned level and its crafting experience. The comp lives on the
    /// level's own hediff, so <c>parent.Severity</c> is the gated level and <c>level</c> is the
    /// stored one. The skill tree reads the gated level as an unlock condition; it never stores it.
    /// </summary>
    public class HediffComp_AuraMastery : HediffComp
    {
        public const int MaxAuraMasteryLevel = 24;

        // Each row covers six destination levels: early, established, advanced, endgame.
        private static readonly float[] ProgressRequirements =
        {
            60f, 80f, 110f, 140f, 180f, 230f,
            400f, 500f, 600f, 750f, 900f, 1050f,
            2000f, 2800f, 3600f, 4400f, 5500f, 6700f,
            15000f, 22000f, 30000f, 38000f, 45000f, 60000f
        };

        private float progress;
        private int level;

        public int CurrentLevel => Mathf.Clamp(level, 0, MaxAuraMasteryLevel);

        public int EffectiveLevel => Mathf.Clamp(Mathf.RoundToInt(parent.Severity), 0, MaxAuraMasteryLevel);

        public bool IsMaxLevel => CurrentLevel >= MaxAuraMasteryLevel;

        public float Progress => progress;

        public float RequiredProgressForCurrentLevel => GetRequiredProgress(CurrentLevel);

        public float ProgressPercent
        {
            get
            {
                if (IsMaxLevel)
                {
                    return 1f;
                }

                float required = RequiredProgressForCurrentLevel;
                return required <= 0f ? 0f : Mathf.Clamp01(progress / required);
            }
        }

        public static float GetRequiredProgress(int auraMasteryLevel)
        {
            if (auraMasteryLevel >= MaxAuraMasteryLevel)
            {
                return 0f;
            }

            int level = Mathf.Clamp(auraMasteryLevel, 0, MaxAuraMasteryLevel - 1);
            return ProgressRequirements[level];
        }

        /// <summary>
        /// The only writer of the gated level. Called on level-up, on settings change and on game
        /// start, never per tick: the stored level changes only at those three points.
        /// </summary>
        public void SetEffectiveLevel()
        {
            parent.Severity = Mathf.Min(CurrentLevel, QinghePowerBalance.MaxEffectiveLevel);
            MX_QHSkillUtility.SyncChoices(Pawn);
            QinghePowerBalance.SyncSealHediff(Pawn);
        }

        public void AddProgress(float amount)
        {
            if (amount <= 0f || IsMaxLevel)
            {
                return;
            }

            int oldLevel = CurrentLevel;
            progress += amount;
            while (!IsMaxLevel)
            {
                float required = RequiredProgressForCurrentLevel;
                if (required <= 0f || progress < required)
                {
                    break;
                }

                progress -= required;
                level = Mathf.Min(MaxAuraMasteryLevel, level + 1);
            }

            if (IsMaxLevel)
            {
                progress = 0f;
            }

            if (Pawn.Spawned && Pawn.Map != null)
            {
                MoteMaker.ThrowText(
                    Pawn.DrawPos,
                    Pawn.Map,
                    "MX_QH_AuraMasteryGainMote".Translate(amount.ToString("0.##")),
                    new Color(1f, 0.35f, 0.8f),
                    1.1f);
            }

            if (CurrentLevel != oldLevel)
            {
                Find.LetterStack.ReceiveLetter(
                    "MX_QH_AuraMasteryGainedLetterLabel".Translate(),
                    "MX_QH_AuraMasteryGainedMessage".Translate(CurrentLevel),
                    LetterDefOf.PositiveEvent,
                    Pawn);
                SetEffectiveLevel();
            }
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            SetEffectiveLevel();
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref progress, "mx_qh_auraMasteryProgress", 0f);
            Scribe_Values.Look(ref level, "mx_qh_auraMasteryLevel", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                SetEffectiveLevel();
            }
        }
    }
}

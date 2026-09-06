using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_QingheGraceSync : HediffCompProperties
    {
        public HediffCompProperties_QingheGraceSync()
        {
            compClass = typeof(HediffComp_QingheGraceSync);
        }
    }

    /// <summary>
    /// Watches the divine grace severity and re-syncs grace-level skill nodes whenever it changes.
    /// Also stores crafting progress toward the next grace level.
    /// </summary>
    public class HediffComp_QingheGraceSync : HediffComp
    {
        public const int MaxGraceLevel = 24;

        private const float BaseProgressRequirement = 120f;
        private const float LinearProgressRequirement = 55f;
        private const float QuadraticProgressRequirement = 10f;

        private float progress;
        private float lastSyncedSeverity = float.NaN;

        public int CurrentLevel => parent == null
            ? 0
            : Mathf.Clamp(Mathf.RoundToInt(parent.Severity), 0, MaxGraceLevel);

        public bool IsMaxLevel => CurrentLevel >= MaxGraceLevel;

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

        public static float GetRequiredProgress(int graceLevel)
        {
            if (graceLevel >= MaxGraceLevel)
            {
                return 0f;
            }

            int level = Mathf.Clamp(graceLevel, 0, MaxGraceLevel - 1);
            return BaseProgressRequirement
                + LinearProgressRequirement * level
                + QuadraticProgressRequirement * level * level;
        }

        public void AddProgress(float amount)
        {
            if (parent == null || amount <= 0f || IsMaxLevel)
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
                parent.Severity = Mathf.Min(parent.def?.maxSeverity ?? MaxGraceLevel, CurrentLevel + 1f);
            }

            if (IsMaxLevel)
            {
                progress = 0f;
            }

            if (CurrentLevel != oldLevel)
            {
                Messages.Message(
                    "MX_QH_DivineGraceGainedMessage".Translate(CurrentLevel),
                    Pawn,
                    MessageTypeDefOf.PositiveEvent,
                    historical: false);
                TrySync(force: true);
            }
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            TrySync(force: true);
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            base.CompPostTick(ref severityAdjustment);
            TrySync();
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref progress, "mx_qh_graceProgress", 0f);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                progress = Mathf.Max(0f, progress);
                lastSyncedSeverity = float.NaN;
                TrySync(force: true);
            }
        }

        private void TrySync(bool force = false)
        {
            if (parent == null || (!force && parent.Severity == lastSyncedSeverity))
            {
                return;
            }

            lastSyncedSeverity = parent.Severity;
            if (MX_QHCharacterUtility.IsQinghe(Pawn))
            {
                MX_QHSkillUtility.SyncChoices(Pawn);
            }
        }
    }
}
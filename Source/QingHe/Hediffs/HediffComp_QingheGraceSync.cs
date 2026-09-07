using RimWorld;
using MiliraXian.Characters.QingHe.Defs;
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
        private int level;
        private float lastSyncedSeverity = float.NaN;

        public int CurrentLevel => Mathf.Clamp(level, 0, MaxGraceLevel);

        public int EffectiveLevel
        {
            get
            {
                Hediff effect = Pawn?.health?.hediffSet?.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_AuraMastery);
                return effect == null ? 0 : Mathf.Clamp(Mathf.RoundToInt(effect.Severity), 0, QinghePowerBalance.MaxEffectiveLevel);
            }
        }

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
                level = Mathf.Min(MaxGraceLevel, level + 1);
            }

            if (IsMaxLevel)
            {
                progress = 0f;
            }

            if (Pawn?.Spawned == true && Pawn.Map != null)
            {
                MoteMaker.ThrowText(
                    Pawn.DrawPos,
                    Pawn.Map,
                    $"灵气精通 +{amount:0.##}经验",
                    new Color(1f, 0.35f, 0.8f),
                    1.1f);
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

        public void SyncForPowerLevel()
        {
            if (parent == null || !MX_QHCharacterUtility.IsQinghe(Pawn))
            {
                return;
            }

            EnsureEffectiveHediff();
            MX_QHSkillUtility.SyncChoices(Pawn);
        }

        public override void CompPostPostRemoved()
        {
            Hediff effect = Pawn?.health?.hediffSet?.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_AuraMastery);
            if (effect != null)
            {
                Pawn.health.RemoveHediff(effect);
            }

            base.CompPostPostRemoved();
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
            Scribe_Values.Look(ref level, "mx_qh_auraMasteryLevel", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                progress = Mathf.Max(0f, progress);
                lastSyncedSeverity = float.NaN;
                TrySync(force: true);
            }
        }

        private void TrySync(bool force = false)
        {
            if (parent == null || (!force && level == lastSyncedSeverity))
            {
                return;
            }

            lastSyncedSeverity = level;
            if (MX_QHCharacterUtility.IsQinghe(Pawn))
            {
                EnsureEffectiveHediff();
                MX_QHSkillUtility.SyncChoices(Pawn);
            }
        }

        private void EnsureEffectiveHediff()
        {
            Hediff effect = Pawn?.health?.hediffSet?.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_AuraMastery);
            if (effect == null)
            {
                effect = HediffMaker.MakeHediff(MX_QHDefOf.MX_QH_AuraMastery, Pawn);
                Pawn.health.AddHediff(effect);
            }

            effect.Severity = Mathf.Min(CurrentLevel, QinghePowerBalance.MaxEffectiveLevel);
        }
    }
}

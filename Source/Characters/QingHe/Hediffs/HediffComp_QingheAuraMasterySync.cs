using RimWorld;
using MiliraXian.Characters.QingHe.Defs;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_QingheAuraMasterySync : HediffCompProperties
    {
        public HediffCompProperties_QingheAuraMasterySync()
        {
            compClass = typeof(HediffComp_QingheAuraMasterySync);
        }
    }

    /// <summary>
    /// Tracks aura mastery level and crafting experience, and synchronizes skill nodes and effects.
    /// </summary>
    public class HediffComp_QingheAuraMasterySync : HediffComp
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
        private float lastSyncedSeverity = float.NaN;

        public int CurrentLevel => Mathf.Clamp(level, 0, MaxAuraMasteryLevel);

        public int EffectiveLevel
        {
            get
            {
                Hediff effect = Pawn?.health?.hediffSet?.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_AuraMastery);
                return effect == null ? 0 : Mathf.Clamp(Mathf.RoundToInt(effect.Severity), 0, QinghePowerBalance.MaxEffectiveLevel);
            }
        }

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
                level = Mathf.Min(MaxAuraMasteryLevel, level + 1);
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
                Find.LetterStack.ReceiveLetter(
                    "MX_QH_AuraMasteryGainedLetterLabel".Translate(),
                    "MX_QH_AuraMasteryGainedMessage".Translate(CurrentLevel),
                    LetterDefOf.PositiveEvent,
                    Pawn);
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
            Scribe_Values.Look(ref progress, "mx_qh_auraMasteryProgress", 0f);
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

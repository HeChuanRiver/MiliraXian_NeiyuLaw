using System.Collections.Generic;
using System.Linq;
using MiliraXian.Characters.QingHe.Defs;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerResonance : HediffCompProperties
    {
        public int maxMusicMasteryLevel = 24;

        public HediffCompProperties_FlowerResonance()
        {
            compClass = typeof(HediffComp_FlowerResonance);
        }
    }

    public class HediffComp_FlowerResonance : HediffComp
    {
        private bool initialized;
        private List<MX_QHSkillNodeDef> learnedNodes;
        private Dictionary<QingheMusicScoreDef, float> musicScoreReadingProgress;

        // Legacy fields kept only so old saves can load and discard removed v2 growth data.
        private int skillPoints;
        private float experience;
        private int musicMasteryLevel;

        public HediffCompProperties_FlowerResonance Props => (HediffCompProperties_FlowerResonance)props;

        public IEnumerable<MX_QHSkillNodeDef> LearnedNodes => learnedNodes ?? Enumerable.Empty<MX_QHSkillNodeDef>();

        public int LearnedNodeCount => learnedNodes?.Count ?? 0;

        public int UnlockedTreeCount => DefDatabase<QingheSkillTreeDef>.AllDefsListForReading.Count(IsTreeUnlocked);

        public int MusicMasteryLevel => musicMasteryLevel;

        public int MaxMusicMasteryLevel => Props.maxMusicMasteryLevel;

        public float LotusShieldCapacityMultiplierFromMastery => 1f + musicMasteryLevel * 0.03f;

        public override bool CompDisallowVisible()
        {
            return true;
        }

        public override void CompPostMake()
        {
            InitializeNewState();
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            InitializeNewState();
            SyncDependentComps();
        }

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref initialized, "mx_qh_skillTree_initialized", false);
            Scribe_Values.Look(ref skillPoints, "mx_qh_skillTree_skillPoints", 0);
            Scribe_Values.Look(ref experience, "mx_qh_skillTree_experience", 0f);
            Scribe_Collections.Look(ref learnedNodes, "mx_qh_skillTree_learnedNodes", LookMode.Def);
            Scribe_Collections.Look(ref musicScoreReadingProgress, "mx_qh_musicScoreReadingProgress", LookMode.Def, LookMode.Value);
            Scribe_Values.Look(ref musicMasteryLevel, "mx_qh_skillTree_musicMasteryLevel", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                InitializeNewState();
                NormalizeCollections();
                EnsureInitiallyLearnedNodes();
                SyncDependentComps();
            }
        }

        public bool IsTreeUnlocked(QingheSkillTreeDef treeDef)
        {
            NormalizeCollections();
            return treeDef != null && learnedNodes.Any(node => node?.tree == treeDef);
        }

        public bool HasNode(MX_QHSkillNodeDef node)
        {
            NormalizeCollections();
            return node != null && learnedNodes.Contains(node);
        }

        public bool CanLearn(MX_QHSkillNodeDef node, out string reason)
        {
            NormalizeCollections();
            if (node == null)
            {
                reason = "MX_QH_UnknownNode".Translate();
                return false;
            }

            if (HasNode(node))
            {
                reason = "MX_QH_SkillTreeStateAlreadyLearned".Translate();
                return false;
            }

            reason = null;
            return true;
        }

        public bool TryLearn(MX_QHSkillNodeDef node, out string reason)
        {
            if (!CanLearn(node, out reason))
            {
                return false;
            }

            learnedNodes.Add(node);
            SyncDependentComps();
            reason = null;
            return true;
        }

        public int LearnNodes(IEnumerable<MX_QHSkillNodeDef> nodes)
        {
            NormalizeCollections();
            if (nodes == null)
            {
                return 0;
            }

            int learnedCount = 0;
            foreach (MX_QHSkillNodeDef node in nodes)
            {
                if (node != null && !learnedNodes.Contains(node))
                {
                    learnedNodes.Add(node);
                    learnedCount++;
                }
            }

            if (learnedCount > 0)
            {
                SyncDependentComps();
            }

            return learnedCount;
        }

        public float GetMusicScoreReadingProgressTicks(QingheMusicScoreDef score)
        {
            NormalizeCollections();
            if (score == null)
            {
                return 0f;
            }

            float progress;
            return musicScoreReadingProgress.TryGetValue(score, out progress) ? Mathf.Max(0f, progress) : 0f;
        }

        public float GetMusicScoreReadingProgressPercent(QingheMusicScoreDef score)
        {
            if (score == null)
            {
                return 0f;
            }

            return Mathf.Clamp01(GetMusicScoreReadingProgressTicks(score) / Mathf.Max(1, score.requiredReadingTicks));
        }

        public float GetNodeReadingProgressPercent(MX_QHSkillNodeDef node)
        {
            NormalizeCollections();
            if (node == null)
            {
                return 0f;
            }

            if (HasNode(node))
            {
                return 1f;
            }

            float bestProgress = 0f;
            foreach (QingheMusicScoreDef score in DefDatabase<QingheMusicScoreDef>.AllDefsListForReading)
            {
                if (score.unlocksNodes != null && score.unlocksNodes.Contains(node))
                {
                    bestProgress = Mathf.Max(bestProgress, GetMusicScoreReadingProgressPercent(score));
                }
            }

            return bestProgress;
        }

        public bool AddMusicScoreReadingProgress(QingheMusicScoreDef score, float progressTicks)
        {
            NormalizeCollections();
            if (score == null)
            {
                return false;
            }

            float progress = GetMusicScoreReadingProgressTicks(score) + Mathf.Max(0f, progressTicks);
            int requiredTicks = Mathf.Max(1, score.requiredReadingTicks);
            if (progress >= requiredTicks)
            {
                musicScoreReadingProgress[score] = requiredTicks;
                return true;
            }

            musicScoreReadingProgress[score] = progress;
            return false;
        }

        public void ClearMusicScoreReadingProgress(QingheMusicScoreDef score)
        {
            NormalizeCollections();
            if (score != null)
            {
                musicScoreReadingProgress.Remove(score);
            }
        }

        public bool TryGainMusicMastery(int levels, out string reason)
        {
            NormalizeCollections();
            int gain = levels <= 0 ? 1 : levels;
            int maxLevel = MaxMusicMasteryLevel;
            if (musicMasteryLevel >= maxLevel)
            {
                reason = "MX_QH_MusicMasteryMaxed".Translate();
                return false;
            }

            musicMasteryLevel = System.Math.Min(maxLevel, musicMasteryLevel + gain);
            SyncDependentComps();
            reason = null;
            return true;
        }

        public void LearnAllNodesInTree(QingheSkillTreeDef treeDef)
        {
            NormalizeCollections();
            if (treeDef == null)
            {
                return;
            }

            foreach (MX_QHSkillNodeDef node in DefDatabase<MX_QHSkillNodeDef>.AllDefsListForReading)
            {
                if (node.tree == treeDef && !learnedNodes.Contains(node))
                {
                    learnedNodes.Add(node);
                }
            }

            SyncDependentComps();
        }

        private void InitializeNewState()
        {
            NormalizeCollections();
            if (initialized)
            {
                return;
            }

            initialized = true;
            EnsureInitiallyLearnedNodes();

            skillPoints = 0;
            experience = 0f;
            musicMasteryLevel = 0;
        }

        private void EnsureInitiallyLearnedNodes()
        {
            LearnNodes(DefDatabase<MX_QHSkillNodeDef>.AllDefsListForReading.Where(node => node.initiallyLearned));
        }

        private void SyncDependentComps()
        {
            MX_QH_HediffUtility.GetDivineFortune(Pawn)?.Recalculate();
            MX_QHSkillSystem.SyncChoices(Pawn, this);
        }

        private void NormalizeCollections()
        {
            if (learnedNodes == null)
            {
                learnedNodes = new List<MX_QHSkillNodeDef>();
            }

            if (musicScoreReadingProgress == null)
            {
                musicScoreReadingProgress = new Dictionary<QingheMusicScoreDef, float>();
            }

        }
    }
}

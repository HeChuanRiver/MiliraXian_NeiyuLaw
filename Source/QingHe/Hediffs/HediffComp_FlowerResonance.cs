using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerResonance : HediffCompProperties
    {
        public int initialSkillPoints = 1;

        public HediffCompProperties_FlowerResonance()
        {
            compClass = typeof(HediffComp_FlowerResonance);
        }
    }

    public class HediffComp_FlowerResonance : HediffComp
    {
        public const int MaxMusicMasteryLevel = 12;

        private bool initialized;
        private int skillPoints;
        private float experience;
        private List<QingheSkillNodeDef> learnedNodes;
        private List<QingheSkillTreeDef> unlockedTrees;
        private int musicMasteryLevel;

        public HediffCompProperties_FlowerResonance Props => (HediffCompProperties_FlowerResonance)props;

        public int SkillPoints => skillPoints;

        public float Experience => experience;

        public IEnumerable<QingheSkillNodeDef> LearnedNodes => learnedNodes;

        public int MusicMasteryLevel => musicMasteryLevel;

        public bool MusicMasteryComplete => musicMasteryLevel >= MaxMusicMasteryLevel;

        public bool IsMusicMasteryActive => TotalEarnedSkillPoints >= TotalSkillPointCost();

        public float LotusShieldCapacityMultiplierFromMastery => MusicMasteryLevel > 0 ? 1f + 0.04f * musicMasteryLevel : 1f;

        public float ExperienceToNextPoint => MusicMasteryComplete ? 0f : ExperienceRequiredForPoint(TotalEarnedSkillPoints + musicMasteryLevel);

        public float ExperienceProgressPercent => ExperienceToNextPoint <= 0f ? 0f : Mathf.Clamp01(experience / ExperienceToNextPoint);

        private int SpentSkillPoints => CalculateSpentSkillPoints();

        private int TotalEarnedSkillPoints => System.Math.Min(skillPoints + SpentSkillPoints, TotalSkillPointCost());

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

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            NormalizeMusicMasteryState();
            SyncMusicMasteryHediff();
        }

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref initialized, "mx_qh_skillTree_initialized", false);
            Scribe_Values.Look(ref skillPoints, "mx_qh_skillTree_skillPoints", 0);
            Scribe_Values.Look(ref experience, "mx_qh_skillTree_experience", 0f);
            Scribe_Collections.Look(ref learnedNodes, "mx_qh_skillTree_learnedNodes", LookMode.Def);
            Scribe_Collections.Look(ref unlockedTrees, "mx_qh_skillTree_unlockedTrees", LookMode.Def);
            Scribe_Values.Look(ref musicMasteryLevel, "mx_qh_skillTree_musicMasteryLevel", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                InitializeNewState();
                NormalizeMusicMasteryState();
                SyncDependentComps();
            }
        }

        public bool IsTreeUnlocked(QingheSkillTreeDef treeDef)
        {
            return treeDef != null && unlockedTrees.Contains(treeDef);
        }

        public void UnlockTree(QingheSkillTreeDef treeDef)
        {
            if (treeDef != null && !unlockedTrees.Contains(treeDef))
            {
                unlockedTrees.Add(treeDef);
            }
        }

        public bool HasNode(QingheSkillNodeDef node)
        {
            return node != null && learnedNodes.Contains(node);
        }

        public bool CanLearn(QingheSkillNodeDef node, out string reason)
        {
            if (node == null)
            {
                reason = "未知节点。";
                return false;
            }

            if (HasNode(node))
            {
                reason = "已经习得。";
                return false;
            }

            if (skillPoints < node.cost)
            {
                reason = "技能点不足。";
                return false;
            }

            if (node.tree != null && !IsTreeUnlocked(node.tree))
            {
                reason = "尚未获得对应曲谱。";
                return false;
            }

            List<QingheSkillNodeDef> prerequisites = node.prerequisites;
            for (int i = 0; i < prerequisites.Count; i++)
            {
                if (!HasNode(prerequisites[i]))
                {
                    reason = "前置节点未习得。";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        public bool TryLearn(QingheSkillNodeDef node, out string reason)
        {
            if (!CanLearn(node, out reason))
            {
                return false;
            }

            skillPoints -= node.cost;
            learnedNodes.Add(node);
            NormalizeMusicMasteryState();
            SyncDependentComps();
            reason = null;
            return true;
        }

        public void AddSkillPoints(int amount)
        {
            skillPoints = System.Math.Max(0, skillPoints + amount);
            NormalizeMusicMasteryState();
            SyncDependentComps();
        }

        public void AddExperience(float amount)
        {
            if (amount <= 0f)
            {
                return;
            }

            experience = System.Math.Max(0f, experience + amount);
            NormalizeMusicMasteryState();
            while (!MusicMasteryComplete && ExperienceToNextPoint > 0f && experience >= ExperienceToNextPoint)
            {
                experience -= ExperienceToNextPoint;
                if (IsMusicMasteryActive)
                {
                    musicMasteryLevel = System.Math.Min(MaxMusicMasteryLevel, musicMasteryLevel + 1);
                    if (MusicMasteryComplete)
                    {
                        experience = 0f;
                    }
                }
                else
                {
                    skillPoints++;
                }

                SyncMusicMasteryHediff();
            }
        }

        private void InitializeNewState()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            learnedNodes = new List<QingheSkillNodeDef>();
            unlockedTrees = new List<QingheSkillTreeDef>();
            skillPoints = System.Math.Max(0, Props?.initialSkillPoints ?? 1);
            foreach (QingheSkillTreeDef treeDef in DefDatabase<QingheSkillTreeDef>.AllDefsListForReading)
            {
                if (treeDef.initiallyUnlocked)
                {
                    unlockedTrees.Add(treeDef);
                }
            }

            NormalizeMusicMasteryState();
        }

        private void NormalizeMusicMasteryState()
        {
            int completeCost = TotalSkillPointCost();
            int spent = SpentSkillPoints;
            int totalNormalPoints = skillPoints + spent;
            if (completeCost > 0 && totalNormalPoints > completeCost)
            {
                int overflowLevels = totalNormalPoints - completeCost;
                skillPoints = System.Math.Max(0, completeCost - spent);
                musicMasteryLevel += overflowLevels;
            }

            musicMasteryLevel = Mathf.Clamp(musicMasteryLevel, 0, MaxMusicMasteryLevel);
            if (!IsMusicMasteryActive)
            {
                musicMasteryLevel = 0;
            }

            if (MusicMasteryComplete)
            {
                experience = 0f;
            }
        }

        private void SyncDependentComps()
        {
            parent?.GetComp<HediffComp_FlowerChoices>()?.ApplyChoicesToPawn();
            SyncMusicMasteryHediff();
        }

        private int CalculateSpentSkillPoints()
        {
            if (learnedNodes.NullOrEmpty())
            {
                return 0;
            }

            int spent = 0;
            for (int i = 0; i < learnedNodes.Count; i++)
            {
                QingheSkillNodeDef node = learnedNodes[i];
                if (node != null)
                {
                    spent += System.Math.Max(0, node.cost);
                }
            }

            return spent;
        }

        private static int TotalSkillPointCost()
        {
            int total = 0;
            foreach (QingheSkillNodeDef node in DefDatabase<QingheSkillNodeDef>.AllDefsListForReading)
            {
                total += System.Math.Max(0, node.cost);
            }

            return total;
        }

        private static float ExperienceRequiredForPoint(int earnedSkillPoints)
        {
            int completeCost = TotalSkillPointCost();
            float preComplete = 75f + 4f * earnedSkillPoints + 0.08f * earnedSkillPoints * earnedSkillPoints;
            if (earnedSkillPoints < completeCost)
            {
                return Mathf.Min(495f, preComplete);
            }

            int overflow = earnedSkillPoints - completeCost + 1;
            return 500f + 100f * overflow * overflow;
        }

        private void SyncMusicMasteryHediff()
        {
            if (Pawn == null || Pawn.Dead || MX_QHDefOf.MX_QH_MusicMastery == null)
            {
                return;
            }

            Hediff hediff = Pawn.health?.hediffSet?.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_MusicMastery);
            if (musicMasteryLevel <= 0)
            {
                if (hediff != null)
                {
                    Pawn.health.RemoveHediff(hediff);
                }
                return;
            }

            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(MX_QHDefOf.MX_QH_MusicMastery, Pawn);
                Pawn.health.AddHediff(hediff);
            }

            hediff.Severity = musicMasteryLevel;
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class Hediff_FlowerResonance : HediffWithComps
    {
    }

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
        private bool initialized;
        private int skillPoints;
        private float experience;
        private List<string> learnedNodes;
        private List<string> unlockedTreeKeys;
        private string selectedFlowerMandateDefName;
        private string selectedTimedFlowerMandateDefName;
        private string selectedFlowerSigilDefName;
        private string selectedFlowerWordDefName;
        private int timedFlowerMandateCooldownTicksLeft;
        private bool flowerBellEnhanced;
        private int musicMasteryLevel;

        public const int MaxMusicMasteryLevel = 12;

        public HediffCompProperties_FlowerResonance Props => (HediffCompProperties_FlowerResonance)props;

        public int SkillPoints => skillPoints;

        public float Experience => experience;

        public string SelectedFlowerMandateDefName => selectedFlowerMandateDefName;

        public string SelectedTimedFlowerMandateDefName => selectedTimedFlowerMandateDefName;

        public string SelectedFlowerSigilDefName => selectedFlowerSigilDefName;

        public string SelectedFlowerWordDefName => selectedFlowerWordDefName;

        public bool FlowerBellEnhanced => flowerBellEnhanced;

        public IEnumerable<string> LearnedNodes => learnedNodes;

        public int MusicMasteryLevel => musicMasteryLevel;

        public bool MusicMasteryComplete => musicMasteryLevel >= MaxMusicMasteryLevel;

        public bool IsMusicMasteryActive => TotalEarnedSkillPoints >= TotalSkillPointCost();

        public float LotusShieldCapacityMultiplierFromMastery => MusicMasteryLevel > 0 ? 1f + 0.04f * musicMasteryLevel : 1f;

        public float ExperienceToNextPoint => MusicMasteryComplete ? 0f : ExperienceRequiredForPoint(TotalEarnedSkillPoints + musicMasteryLevel);

        public float ExperienceProgressPercent => ExperienceToNextPoint <= 0f ? 0f : Mathf.Clamp01(experience / ExperienceToNextPoint);

        private int SpentSkillPoints => CalculateSpentSkillPoints();

        private int TotalEarnedSkillPoints => System.Math.Min(skillPoints + SpentSkillPoints, TotalSkillPointCost());

        public int TimedFlowerMandateCooldownTicksTotal => 60000;

        public int TimedFlowerMandateCooldownTicksLeft => System.Math.Max(0, timedFlowerMandateCooldownTicksLeft);

        public bool TimedFlowerMandateOnCooldown => TimedFlowerMandateCooldownTicksLeft > 0;

        public float TimedFlowerMandateCooldownRemainingPercent => TimedFlowerMandateCooldownTicksTotal <= 0 ? 0f : UnityEngine.Mathf.Clamp01(TimedFlowerMandateCooldownTicksLeft / (float)TimedFlowerMandateCooldownTicksTotal);

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
            ApplyChoicesToPawn();
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            NormalizeMusicMasteryState();
            SyncMusicMasteryHediff();

            if (timedFlowerMandateCooldownTicksLeft > 0)
            {
                timedFlowerMandateCooldownTicksLeft = System.Math.Max(0, timedFlowerMandateCooldownTicksLeft - delta);
            }
        }

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref initialized, "mx_qh_skillTree_initialized", false);
            Scribe_Values.Look(ref skillPoints, "mx_qh_skillTree_skillPoints", 0);
            Scribe_Values.Look(ref experience, "mx_qh_skillTree_experience", 0f);
            Scribe_Collections.Look(ref learnedNodes, "mx_qh_skillTree_learnedNodes", LookMode.Value);
            Scribe_Collections.Look(ref unlockedTreeKeys, "mx_qh_skillTree_unlockedTreeKeys", LookMode.Value);
            Scribe_Values.Look(ref selectedFlowerMandateDefName, "mx_qh_skillTree_selectedFlowerMandateDefName");
            Scribe_Values.Look(ref selectedTimedFlowerMandateDefName, "mx_qh_skillTree_selectedTimedFlowerMandateDefName");
            Scribe_Values.Look(ref selectedFlowerSigilDefName, "mx_qh_skillTree_selectedFlowerSigilDefName");
            Scribe_Values.Look(ref selectedFlowerWordDefName, "mx_qh_skillTree_selectedFlowerWordDefName");
            Scribe_Values.Look(ref timedFlowerMandateCooldownTicksLeft, "mx_qh_skillTree_timedFlowerMandateCooldownTicksLeft", 0);
            Scribe_Values.Look(ref flowerBellEnhanced, "mx_qh_skillTree_flowerBellEnhanced", false);
            Scribe_Values.Look(ref musicMasteryLevel, "mx_qh_skillTree_musicMasteryLevel", 0);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                InitializeNewState();
                NormalizeMusicMasteryState();
                ApplyChoicesToPawn();
            }
        }

        public bool IsTreeUnlocked(string treeKey)
        {
            return !treeKey.NullOrEmpty() && unlockedTreeKeys.Contains(treeKey);
        }

        public bool IsTreeUnlocked(QingheSkillTreeDef treeDef)
        {
            return treeDef != null && IsTreeUnlocked(treeDef.defName);
        }

        public void UnlockTree(string treeKey)
        {
            if (!treeKey.NullOrEmpty() && !unlockedTreeKeys.Contains(treeKey))
            {
                unlockedTreeKeys.Add(treeKey);
            }
        }

        public void UnlockTree(QingheSkillTreeDef treeDef)
        {
            if (treeDef != null)
            {
                UnlockTree(treeDef.defName);
            }
        }

        public bool HasNode(string nodeDefName)
        {
            return !nodeDefName.NullOrEmpty() && learnedNodes.Contains(nodeDefName);
        }

        public bool CanLearn(string nodeDefName, out string reason)
        {
            QingheSkillNodeDef node = DefDatabase<QingheSkillNodeDef>.GetNamedSilentFail(nodeDefName);
            if (node == null)
            {
                reason = "未知节点。";
                return false;
            }

            if (HasNode(nodeDefName))
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
                if (!HasNode(prerequisites[i].defName))
                {
                    reason = "前置节点未习得。";
                    return false;
                }
            }

            reason = null;
            return true;
        }

        public bool TryLearn(string nodeDefName, out string reason)
        {
            if (!CanLearn(nodeDefName, out reason))
            {
                return false;
            }

            QingheSkillNodeDef node = DefDatabase<QingheSkillNodeDef>.GetNamedSilentFail(nodeDefName);
            skillPoints -= node.cost;
            learnedNodes.Add(nodeDefName);
            NormalizeMusicMasteryState();
            ApplyChoicesToPawn();
            reason = null;
            return true;
        }

        public void AddSkillPoints(int amount)
        {
            skillPoints = System.Math.Max(0, skillPoints + amount);
            NormalizeMusicMasteryState();
            ApplyChoicesToPawn();
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

        public void SetFlowerMandate(string abilityDefName)
        {
            selectedFlowerMandateDefName = abilityDefName;
            if (selectedTimedFlowerMandateDefName == selectedFlowerMandateDefName)
            {
                selectedTimedFlowerMandateDefName = null;
            }
            ApplyChoicesToPawn();
        }

        public bool TrySetTimedFlowerMandate(string abilityDefName, out string reason)
        {
            if (!HasNode(QingheSkillTreeSystem.NodeSishiLiuzhuan))
            {
                reason = "尚未习得四时流转。";
                return false;
            }

            if (TimedFlowerMandateOnCooldown)
            {
                reason = "飞花令·寄时仍在冷却中。\n剩余时间: " + TimedFlowerMandateCooldownTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
                return false;
            }

            if (!QingheFlowerChoiceUtility.FlowerMandates.Contains(abilityDefName))
            {
                reason = "未知的飞花令。";
                return false;
            }

            if (selectedFlowerMandateDefName == abilityDefName)
            {
                reason = "飞花令·寄时不能与当前主飞花令相同。";
                return false;
            }

            if (selectedTimedFlowerMandateDefName == abilityDefName)
            {
                reason = "飞花令·寄时已经是“" + QingheFlowerChoiceUtility.LabelForDefName(abilityDefName) + "”。";
                return false;
            }

            selectedTimedFlowerMandateDefName = abilityDefName;
            timedFlowerMandateCooldownTicksLeft = TimedFlowerMandateCooldownTicksTotal;
            ApplyChoicesToPawn();
            QingheFlowerChoiceUtility.StartFlowerMandateCooldown(Pawn, abilityDefName);
            reason = null;
            return true;
        }

        public void SetFlowerSigil(string hediffDefName)
        {
            selectedFlowerSigilDefName = hediffDefName;
            ApplyChoicesToPawn();
        }

        public void SetFlowerWord(string traitDefName)
        {
            selectedFlowerWordDefName = traitDefName;
            ApplyChoicesToPawn();
        }

        public void SetFlowerBellEnhanced(bool enabled)
        {
            flowerBellEnhanced = enabled && HasNode(QingheSkillTreeSystem.NodeQingjue);
        }

        public void ApplyChoicesToPawn()
        {
            if (Pawn == null || Pawn.Dead)
            {
                return;
            }

            if (!HasNode(QingheSkillTreeSystem.NodeQingjue))
            {
                flowerBellEnhanced = false;
            }

            if (!HasNode(QingheSkillTreeSystem.NodeSishiLiuzhuan))
            {
                selectedTimedFlowerMandateDefName = null;
                timedFlowerMandateCooldownTicksLeft = 0;
            }
            else if (selectedTimedFlowerMandateDefName == selectedFlowerMandateDefName)
            {
                selectedTimedFlowerMandateDefName = null;
            }

            QingheSkillTreeSystem.SyncChoices(Pawn, this);
            SyncMusicMasteryHediff();
        }

        private void InitializeNewState()
        {
            if (initialized)
            {
                return;
            }

            initialized = true;
            learnedNodes = new List<string>();
            unlockedTreeKeys = new List<string>();
            skillPoints = System.Math.Max(0, Props?.initialSkillPoints ?? 1);
            foreach (QingheSkillTreeDef treeDef in DefDatabase<QingheSkillTreeDef>.AllDefsListForReading)
            {
                if (treeDef.initiallyUnlocked)
                {
                    unlockedTreeKeys.Add(treeDef.defName);
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

        private int CalculateSpentSkillPoints()
        {
            if (learnedNodes.NullOrEmpty())
            {
                return 0;
            }

            int spent = 0;
            for (int i = 0; i < learnedNodes.Count; i++)
            {
                QingheSkillNodeDef node = DefDatabase<QingheSkillNodeDef>.GetNamedSilentFail(learnedNodes[i]);
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
                return UnityEngine.Mathf.Min(495f, preComplete);
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

using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class Hediff_QingheSkillTreeState : HediffWithComps
    {
    }

    public class HediffCompProperties_QingheSkillTreeState : HediffCompProperties
    {
        public int initialSkillPoints = 1;

        public HediffCompProperties_QingheSkillTreeState()
        {
            compClass = typeof(HediffComp_QingheSkillTreeState);
        }
    }

    public class HediffComp_QingheSkillTreeState : HediffComp
    {
        private bool initialized;
        private int skillPoints;
        private float experience;
        private List<string> learnedNodes;
        private List<string> unlockedTreeKeys;
        private string selectedFlowerMandateDefName;
        private string selectedFlowerSigilDefName;
        private string selectedFlowerWordDefName;

        public HediffCompProperties_QingheSkillTreeState Props => (HediffCompProperties_QingheSkillTreeState)props;

        public int SkillPoints => skillPoints;

        public float Experience => experience;

        public string SelectedFlowerMandateDefName => selectedFlowerMandateDefName;

        public string SelectedFlowerSigilDefName => selectedFlowerSigilDefName;

        public string SelectedFlowerWordDefName => selectedFlowerWordDefName;

        public IEnumerable<string> LearnedNodes => learnedNodes;

        public float ExperienceToNextPoint => 100f;

        public float ExperienceProgressPercent => UnityEngine.Mathf.Clamp01(experience / ExperienceToNextPoint);

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
        }

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref initialized, "mx_qh_skillTree_initialized", false);
            Scribe_Values.Look(ref skillPoints, "mx_qh_skillTree_skillPoints", 0);
            Scribe_Values.Look(ref experience, "mx_qh_skillTree_experience", 0f);
            Scribe_Collections.Look(ref learnedNodes, "mx_qh_skillTree_learnedNodes", LookMode.Value);
            Scribe_Collections.Look(ref unlockedTreeKeys, "mx_qh_skillTree_unlockedTreeKeys", LookMode.Value);
            Scribe_Values.Look(ref selectedFlowerMandateDefName, "mx_qh_skillTree_selectedFlowerMandateDefName");
            Scribe_Values.Look(ref selectedFlowerSigilDefName, "mx_qh_skillTree_selectedFlowerSigilDefName");
            Scribe_Values.Look(ref selectedFlowerWordDefName, "mx_qh_skillTree_selectedFlowerWordDefName");

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
            reason = null;
            return true;
        }

        public void AddSkillPoints(int amount)
        {
            skillPoints = System.Math.Max(0, skillPoints + amount);
        }

        public void AddExperience(float amount)
        {
            experience = System.Math.Max(0f, experience + amount);
        }

        public void SetFlowerMandate(string abilityDefName)
        {
            selectedFlowerMandateDefName = abilityDefName;
        }

        public void SetFlowerSigil(string hediffDefName)
        {
            selectedFlowerSigilDefName = hediffDefName;
        }

        public void SetFlowerWord(string traitDefName)
        {
            selectedFlowerWordDefName = traitDefName;
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
        }
    }
}

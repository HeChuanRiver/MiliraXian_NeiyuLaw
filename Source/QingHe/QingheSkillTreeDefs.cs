using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class QingheSkillTreeDef : Def
    {
        public int displayOrder;
        public bool initiallyUnlocked;
    }

    public class QingheSkillNodeDef : Def
    {
        public QingheSkillTreeDef tree;
        public int column;
        public float y;
        public int cost;
        public bool important;
        public List<QingheSkillNodeDef> prerequisites = new List<QingheSkillNodeDef>();
    }

    public class QingheMusicScoreDef : Def
    {
        public QingheSkillTreeDef unlocksTree;
        public float experienceGain;
    }
}

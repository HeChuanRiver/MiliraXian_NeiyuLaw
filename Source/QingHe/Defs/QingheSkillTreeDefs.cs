using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters.QingHe.Defs
{
    public class QingheSkillTreeDef : Def
    {
        public int displayOrder;
    }

    public class MX_QHSkillNodeDef : Def
    {
        public QingheSkillTreeDef tree;
        public int displayOrder;
        public int column;
        public float y = -1f;
        public bool initiallyLearned;
        public bool important;
    }

    public class QingheMusicScoreDef : Def
    {
        public List<MX_QHSkillNodeDef> unlocksNodes = new List<MX_QHSkillNodeDef>();
        public int masteryGain;
        public int requiredReadingTicks = 5000;
    }
}

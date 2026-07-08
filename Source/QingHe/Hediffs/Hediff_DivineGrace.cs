using System.Text;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_DivineGrace : HediffCompProperties
    {
        public HediffCompProperties_DivineGrace()
        {
            compClass = typeof(HediffComp_DivineGrace);
        }
    }

    public class Hediff_DivineGrace : HediffWithComps
    {
        private HediffComp_DivineGrace GraceComp => GetComp<HediffComp_DivineGrace>();

        public override string LabelInBrackets
        {
            get
            {
                HediffComp_DivineGrace comp = GraceComp;
                return comp == null ? base.LabelInBrackets : "MX_QH_DivineGraceLevel".Translate(comp.Level, comp.MaxLevel).ToString();
            }
        }
    }

    public class HediffComp_DivineGrace : HediffComp
    {
        private int level;
        private int maxLevel = 24;

        public HediffCompProperties_DivineGrace Props => (HediffCompProperties_DivineGrace)props;

        public int Level => Mathf.Max(0, level);

        public int MaxLevel => Mathf.Max(1, maxLevel);

        public override string CompTipStringExtra
        {
            get
            {
                if (Level <= 0)
                {
                    return null;
                }

                StringBuilder builder = new StringBuilder();
                AppendMasteryLines(builder);
                return builder.ToString().TrimEndNewlines();
            }
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            SyncFromSkillTree();
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            if (Pawn != null && Pawn.IsHashIntervalTick(250, delta))
            {
                SyncFromSkillTree();
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref level, "mx_qh_divineGrace_level", 0);
            Scribe_Values.Look(ref maxLevel, "mx_qh_divineGrace_maxLevel", 24);
        }

        public void SetLevel(int value, int max)
        {
            level = Mathf.Max(0, value);
            maxLevel = Mathf.Max(1, max);
            if (parent != null)
            {
                parent.Severity = Mathf.Clamp(level, 1, MaxLevel);
            }
        }

        private void SyncFromSkillTree()
        {
            HediffComp_SkillTreeState state = MX_QH_HediffUtility.GetFlowerResonance(Pawn);
            SkillNodeDef node = MX_QHSkillNodeDefOf.MX_QH_Node_DivineGrace;
            SetLevel(state?.GetNodeLevel(node) ?? 0, node?.MaxLevel ?? MaxLevel);
        }

        private void AppendMasteryLines(StringBuilder builder)
        {
            HediffComp_DivineFortune fortune = MX_QH_HediffUtility.GetDivineFortune(Pawn);
            if (fortune == null)
            {
                return;
            }

            if (builder.Length > 0)
            {
                builder.AppendLine();
            }

            builder.AppendLine("MX_QH_DivineGraceFortuneHeader".Translate());
            builder.AppendLine("MX_QH_DivineGraceShieldCapacityLine".Translate((1f + Mathf.Max(0f, fortune.Props.masteryShieldCapacityMultiplierPerLevel) * Level).ToStringPercent()));
            builder.AppendLine("MX_QH_DivineGraceShieldRegenLine".Translate((1f + Mathf.Max(0f, fortune.Props.masteryShieldRegenMultiplierPerLevel) * Level).ToStringPercent()));
            builder.AppendLine("MX_QH_DivineGraceFlowerDecreeRegenLine".Translate((1f + Mathf.Max(0f, fortune.Props.masteryFlowerDecreeRegenMultiplierPerLevel) * Level).ToStringPercent()));

            float reduction = Mathf.Max(0f, fortune.Props.masteryDivineBlessingRechargeFactorPerLevel) * Level;
            float rechargeFactor = Mathf.Max(Mathf.Clamp01(fortune.Props.minDivineBlessingRechargeFactor), 1f - reduction);
            builder.AppendLine("MX_QH_DivineGraceBlessingRechargeLine".Translate(rechargeFactor.ToStringPercent()));
        }
    }
}

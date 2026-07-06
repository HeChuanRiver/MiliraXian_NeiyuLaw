using RimWorld;
using MiliraXian.Characters.QingHe.Defs;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_DivineFortune : HediffCompProperties
    {
        public float shieldCapacityMultiplier = 2.5f;
        public float shieldRegenMultiplier = 3f;
        public int shieldRegenDelayTicks = 180;
        public int shieldBreakDelayTicks = 600;
        public int acceleratedShieldRegenDelayTicks = 180;
        public int acceleratedShieldBreakDelayTicks = 180;
        public int immediateShieldRegenDelayTicks = 0;
        public float shieldDamageCap = 20f;
        public int flowerDecreeMaxBonusPerMandateNode = 1;
        public float flowerDecreeRegenMultiplier = 1f;
        public float activeStateFlowerDecreeRegenMultiplier = 2f;
        public float masteryShieldCapacityMultiplierPerLevel = 0.03f;
        public float masteryShieldRegenMultiplierPerLevel = 0.03f;
        public float masteryFlowerDecreeRegenMultiplierPerLevel = 0.01f;
        public float masteryDivineBlessingRechargeFactorPerLevel = 0.02f;
        public float minDivineBlessingRechargeFactor = 0.25f;

        public HediffCompProperties_DivineFortune()
        {
            compClass = typeof(HediffComp_DivineFortune);
        }
    }

    /// <summary>
    /// DivineFortune is the central passive numeric proxy for QingHe.
    /// Skill nodes and flower words register their numeric effects here,
    /// and systems like the Lotus Shield or Flower Decree read the aggregated values.
    /// </summary>
    public class HediffComp_DivineFortune : HediffComp
    {
        private float shieldCapacityMultiplier = 1f;
        private float shieldRegenMultiplier = 1f;
        private float shieldDamageCap;
        private int shieldRegenDelayTicks;
        private int shieldBreakDelayTicks;
        private int flowerDecreeMaxBonus;
        private float flowerDecreeRegenMultiplier = 1f;
        private float divineBlessingRechargeFactor = 1f;

        public HediffCompProperties_DivineFortune Props => (HediffCompProperties_DivineFortune)props;

        public float ShieldCapacityMultiplier => shieldCapacityMultiplier;

        public float ShieldRegenMultiplier => shieldRegenMultiplier;

        public float ShieldDamageCap => shieldDamageCap;

        public int ShieldRegenDelayTicks => shieldRegenDelayTicks;

        public int ShieldBreakDelayTicks => shieldBreakDelayTicks;

        public int FlowerDecreeMaxBonus => flowerDecreeMaxBonus;

        public float FlowerDecreeRegenMultiplier => flowerDecreeRegenMultiplier;

        public float DivineBlessingRechargeFactor => divineBlessingRechargeFactor;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            Recalculate();
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            base.CompPostTickInterval(ref severityAdjustment, delta);
            Recalculate();
        }

        public void Recalculate()
        {
            RecalculateFromSkillState(MX_QH_HediffUtility.GetFlowerResonance(Pawn));
        }

        private void RecalculateFromSkillState(HediffComp_FlowerResonance skillState)
        {
            shieldCapacityMultiplier = 1f;
            shieldRegenMultiplier = 1f;
            shieldDamageCap = 0f;
            shieldRegenDelayTicks = Mathf.Max(0, Props.shieldRegenDelayTicks);
            shieldBreakDelayTicks = Mathf.Max(0, Props.shieldBreakDelayTicks);
            flowerDecreeMaxBonus = 0;
            flowerDecreeRegenMultiplier = 1f;
            divineBlessingRechargeFactor = 1f;

            if (skillState == null)
            {
                return;
            }

            if (skillState.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Gaoshan))
            {
                shieldCapacityMultiplier *= Mathf.Max(0.01f, Props.shieldCapacityMultiplier);
            }

            if (skillState.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Liushui))
            {
                shieldRegenMultiplier *= Mathf.Max(0f, Props.shieldRegenMultiplier);
            }

            if (skillState.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Chunjiang))
            {
                shieldRegenDelayTicks = Mathf.Min(shieldRegenDelayTicks, Mathf.Max(0, Props.acceleratedShieldRegenDelayTicks));
                shieldBreakDelayTicks = Mathf.Min(shieldBreakDelayTicks, Mathf.Max(0, Props.acceleratedShieldBreakDelayTicks));
            }

            if (HasAllDivineFortuneNodes(skillState))
            {
                shieldDamageCap = Mathf.Max(shieldDamageCap, Props.shieldDamageCap);
            }

            flowerDecreeMaxBonus += CountNode(skillState, MX_QHSkillNodeDefOf.MX_QH_Node_Chuanhun) * Props.flowerDecreeMaxBonusPerMandateNode;
            flowerDecreeMaxBonus += CountNode(skillState, MX_QHSkillNodeDefOf.MX_QH_Node_Shuangyuejing) * Props.flowerDecreeMaxBonusPerMandateNode;
            flowerDecreeRegenMultiplier *= Mathf.Max(0f, Props.flowerDecreeRegenMultiplier);

            ApplyMusicMastery(skillState.MusicMasteryLevel);

            if (IsActiveStateEnabled())
            {
                flowerDecreeRegenMultiplier *= Mathf.Max(0f, Props.activeStateFlowerDecreeRegenMultiplier);
                shieldRegenDelayTicks = Mathf.Min(shieldRegenDelayTicks, Mathf.Max(0, Props.immediateShieldRegenDelayTicks));
            }
        }

        private void ApplyMusicMastery(int level)
        {
            if (level <= 0)
            {
                return;
            }

            shieldCapacityMultiplier *= 1f + Mathf.Max(0f, Props.masteryShieldCapacityMultiplierPerLevel) * level;
            shieldRegenMultiplier *= 1f + Mathf.Max(0f, Props.masteryShieldRegenMultiplierPerLevel) * level;
            flowerDecreeRegenMultiplier *= 1f + Mathf.Max(0f, Props.masteryFlowerDecreeRegenMultiplierPerLevel) * level;

            float reduction = Mathf.Max(0f, Props.masteryDivineBlessingRechargeFactorPerLevel) * level;
            divineBlessingRechargeFactor = Mathf.Max(Mathf.Clamp01(Props.minDivineBlessingRechargeFactor), 1f - reduction);
        }

        private static int CountNode(HediffComp_FlowerResonance skillState, MX_QHSkillNodeDef node)
        {
            return skillState != null && node != null && skillState.HasNode(node) ? 1 : 0;
        }

        private static bool HasAllDivineFortuneNodes(HediffComp_FlowerResonance skillState)
        {
            return skillState != null
                && skillState.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Gaoshan)
                && skillState.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Liushui)
                && skillState.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Chunjiang);
        }

        private bool IsActiveStateEnabled()
        {
            return Pawn?.health?.hediffSet != null
                && MX_QHDefOf.MX_QH_FlowerDance != null
                && Pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_FlowerDance) != null;
        }
    }
}

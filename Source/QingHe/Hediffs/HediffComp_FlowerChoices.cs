using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_FlowerChoices : HediffCompProperties
    {
        public int flowerMandateCooldownTicksTotal = 60000;
        public int timedFlowerMandateCooldownTicksTotal = 60000;
        public int flowerSigilCooldownTicksTotal = 60000;
        public int flowerWordCooldownTicksTotal = 60000;

        public HediffCompProperties_FlowerChoices()
        {
            compClass = typeof(HediffComp_FlowerChoices);
        }
    }

    public class HediffComp_FlowerChoices : HediffComp
    {
        private AbilityDef selectedFlowerMandate;
        private AbilityDef selectedTimedFlowerMandate;
        private HediffDef selectedFlowerSigil;
        private TraitDef selectedFlowerWord;
        private int flowerMandateCooldownTicksLeft;
        private int timedFlowerMandateCooldownTicksLeft;
        private int flowerSigilCooldownTicksLeft;
        private int flowerWordCooldownTicksLeft;
        private bool flowerBellEnhanced;

        public HediffCompProperties_FlowerChoices Props => (HediffCompProperties_FlowerChoices)props;

        public AbilityDef SelectedFlowerMandate => selectedFlowerMandate;

        public AbilityDef SelectedTimedFlowerMandate => selectedTimedFlowerMandate;

        public HediffDef SelectedFlowerSigil => selectedFlowerSigil;

        public TraitDef SelectedFlowerWord => selectedFlowerWord;

        public bool FlowerBellEnhanced => flowerBellEnhanced;

        public int FlowerMandateCooldownTicksTotal => Mathf.Max(1, Props.flowerMandateCooldownTicksTotal);

        public int FlowerMandateCooldownTicksLeft => System.Math.Max(0, flowerMandateCooldownTicksLeft);

        public bool FlowerMandateOnCooldown => FlowerMandateCooldownTicksLeft > 0;

        public float FlowerMandateCooldownRemainingPercent => Mathf.Clamp01(FlowerMandateCooldownTicksLeft / (float)FlowerMandateCooldownTicksTotal);

        public int TimedFlowerMandateCooldownTicksTotal => Mathf.Max(1, Props.timedFlowerMandateCooldownTicksTotal);

        public int TimedFlowerMandateCooldownTicksLeft => System.Math.Max(0, timedFlowerMandateCooldownTicksLeft);

        public bool TimedFlowerMandateOnCooldown => TimedFlowerMandateCooldownTicksLeft > 0;

        public float TimedFlowerMandateCooldownRemainingPercent => Mathf.Clamp01(TimedFlowerMandateCooldownTicksLeft / (float)TimedFlowerMandateCooldownTicksTotal);

        public int FlowerSigilCooldownTicksTotal => Mathf.Max(1, Props.flowerSigilCooldownTicksTotal);

        public int FlowerSigilCooldownTicksLeft => System.Math.Max(0, flowerSigilCooldownTicksLeft);

        public bool FlowerSigilOnCooldown => FlowerSigilCooldownTicksLeft > 0;

        public float FlowerSigilCooldownRemainingPercent => Mathf.Clamp01(FlowerSigilCooldownTicksLeft / (float)FlowerSigilCooldownTicksTotal);

        public int FlowerWordCooldownTicksTotal => Mathf.Max(1, Props.flowerWordCooldownTicksTotal);

        public int FlowerWordCooldownTicksLeft => System.Math.Max(0, flowerWordCooldownTicksLeft);

        public bool FlowerWordOnCooldown => FlowerWordCooldownTicksLeft > 0;

        public float FlowerWordCooldownRemainingPercent => Mathf.Clamp01(FlowerWordCooldownTicksLeft / (float)FlowerWordCooldownTicksTotal);

        private HediffComp_FlowerResonance SkillState => parent?.GetComp<HediffComp_FlowerResonance>();

        public override bool CompDisallowVisible()
        {
            return true;
        }

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            ApplyChoicesToPawn();
        }

        public override void CompPostTickInterval(ref float severityAdjustment, int delta)
        {
            if (flowerMandateCooldownTicksLeft > 0)
            {
                flowerMandateCooldownTicksLeft = System.Math.Max(0, flowerMandateCooldownTicksLeft - delta);
            }

            if (timedFlowerMandateCooldownTicksLeft > 0)
            {
                timedFlowerMandateCooldownTicksLeft = System.Math.Max(0, timedFlowerMandateCooldownTicksLeft - delta);
            }

            if (flowerSigilCooldownTicksLeft > 0)
            {
                flowerSigilCooldownTicksLeft = System.Math.Max(0, flowerSigilCooldownTicksLeft - delta);
            }

            if (flowerWordCooldownTicksLeft > 0)
            {
                flowerWordCooldownTicksLeft = System.Math.Max(0, flowerWordCooldownTicksLeft - delta);
            }
        }

        public override void CompExposeData()
        {
            Scribe_Defs.Look(ref selectedFlowerMandate, "mx_qh_flowerChoices_selectedFlowerMandate");
            Scribe_Defs.Look(ref selectedTimedFlowerMandate, "mx_qh_flowerChoices_selectedTimedFlowerMandate");
            Scribe_Defs.Look(ref selectedFlowerSigil, "mx_qh_flowerChoices_selectedFlowerSigil");
            Scribe_Defs.Look(ref selectedFlowerWord, "mx_qh_flowerChoices_selectedFlowerWord");
            Scribe_Values.Look(ref flowerMandateCooldownTicksLeft, "mx_qh_flowerChoices_flowerMandateCooldownTicksLeft", 0);
            Scribe_Values.Look(ref timedFlowerMandateCooldownTicksLeft, "mx_qh_flowerChoices_timedFlowerMandateCooldownTicksLeft", 0);
            Scribe_Values.Look(ref flowerSigilCooldownTicksLeft, "mx_qh_flowerChoices_flowerSigilCooldownTicksLeft", 0);
            Scribe_Values.Look(ref flowerWordCooldownTicksLeft, "mx_qh_flowerChoices_flowerWordCooldownTicksLeft", 0);
            Scribe_Values.Look(ref flowerBellEnhanced, "mx_qh_flowerChoices_flowerBellEnhanced", false);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                ApplyChoicesToPawn();
            }
        }

        public bool TrySetFlowerMandate(AbilityDef abilityDef, out string reason)
        {
            HediffComp_FlowerResonance skillState = SkillState;
            if (skillState == null || !skillState.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerMandate))
            {
                reason = "尚未习得飞花令。";
                return false;
            }

            if (FlowerMandateOnCooldown)
            {
                reason = BuildCooldownReason("飞花令", FlowerMandateCooldownTicksLeft);
                return false;
            }

            if (selectedFlowerMandate == abilityDef)
            {
                reason = "飞花令已经是\"" + QingheFlowerChoiceUtility.LabelForDef(abilityDef) + "\"。";
                return false;
            }

            selectedFlowerMandate = abilityDef;
            selectedTimedFlowerMandate = null;
            timedFlowerMandateCooldownTicksLeft = 0;
            flowerMandateCooldownTicksLeft = FlowerMandateCooldownTicksTotal;
            ApplyChoicesToPawn();
            reason = null;
            return true;
        }

        public bool TrySetTimedFlowerMandate(AbilityDef abilityDef, out string reason)
        {
            HediffComp_FlowerResonance skillState = SkillState;
            if (skillState == null || !skillState.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_SishiLiuzhuan))
            {
                reason = "尚未习得四时流转。";
                return false;
            }

            if (TimedFlowerMandateOnCooldown)
            {
                reason = "飞花令·寄时仍在冷却中。\n剩余时间: " + TimedFlowerMandateCooldownTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
                return false;
            }

            if (selectedFlowerMandate == abilityDef)
            {
                reason = "飞花令·寄时不能与当前主飞花令相同。";
                return false;
            }

            if (selectedTimedFlowerMandate == abilityDef)
            {
                reason = "飞花令·寄时已经是\"" + QingheFlowerChoiceUtility.LabelForDef(abilityDef) + "\"。";
                return false;
            }

            selectedTimedFlowerMandate = abilityDef;
            timedFlowerMandateCooldownTicksLeft = TimedFlowerMandateCooldownTicksTotal;
            ApplyChoicesToPawn();
            QingheFlowerChoiceUtility.StartFlowerMandateCooldown(Pawn, abilityDef);
            reason = null;
            return true;
        }

        public bool TrySetFlowerSigil(HediffDef hediffDef, out string reason)
        {
            HediffComp_FlowerResonance skillState = SkillState;
            if (skillState == null || !skillState.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerSigil))
            {
                reason = "尚未习得花神签节点。";
                return false;
            }

            if (FlowerSigilOnCooldown)
            {
                reason = BuildCooldownReason("花神签", FlowerSigilCooldownTicksLeft);
                return false;
            }

            if (selectedFlowerSigil == hediffDef)
            {
                reason = "花神签已经是\"" + QingheFlowerChoiceUtility.LabelForDef(hediffDef) + "\"。";
                return false;
            }

            selectedFlowerSigil = hediffDef;
            flowerSigilCooldownTicksLeft = FlowerSigilCooldownTicksTotal;
            ApplyChoicesToPawn();
            reason = null;
            return true;
        }

        public bool TrySetFlowerWord(TraitDef traitDef, out string reason)
        {
            HediffComp_FlowerResonance skillState = SkillState;
            if (skillState == null || !skillState.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerWord))
            {
                reason = "尚未习得花语节点。";
                return false;
            }

            if (FlowerWordOnCooldown)
            {
                reason = BuildCooldownReason("花语", FlowerWordCooldownTicksLeft);
                return false;
            }

            if (selectedFlowerWord == traitDef)
            {
                reason = "花语已经是\"" + QingheFlowerChoiceUtility.LabelForDef(traitDef) + "\"。";
                return false;
            }

            selectedFlowerWord = traitDef;
            flowerWordCooldownTicksLeft = FlowerWordCooldownTicksTotal;
            ApplyChoicesToPawn();
            reason = null;
            return true;
        }

        public void SetFlowerBellEnhanced(bool enabled)
        {
            HediffComp_FlowerResonance skillState = SkillState;
            flowerBellEnhanced = enabled && skillState != null && skillState.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Qingjue);
        }

        public void ApplyChoicesToPawn()
        {
            HediffComp_FlowerResonance skillState = SkillState;
            if (Pawn == null || Pawn.Dead || skillState == null)
            {
                return;
            }

            if (!skillState.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Qingjue))
            {
                flowerBellEnhanced = false;
            }

            if (!skillState.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_SishiLiuzhuan))
            {
                selectedTimedFlowerMandate = null;
                timedFlowerMandateCooldownTicksLeft = 0;
            }
            else if (selectedTimedFlowerMandate == selectedFlowerMandate)
            {
                selectedTimedFlowerMandate = null;
            }

            QingheSkillTreeSystem.SyncChoices(Pawn, skillState, this);
        }

        private static string BuildCooldownReason(string label, int ticksLeft)
        {
            return label + "仍在切换冷却中。\n剩余时间: " + ticksLeft.ToStringTicksToPeriod(true, false, true, true, false);
        }
    }
}

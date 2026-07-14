using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Grammar;

namespace MiliraXian.Characters
{
    public class BookOutcomeDoer_SkillTreeUnlock : BookOutcomeDoer
    {
        private SkillNodeDef selectedNode;
        private bool active = true;
        private Pawn cachedReader;
        private HediffComp_SkillTreeState cachedState;
        private SkillNodeDef cachedNode;
        private bool cachedNodeLearned;

        public new BookOutcomeProperties_SkillTreeUnlock Props => (BookOutcomeProperties_SkillTreeUnlock)props;

        public SkillNodeDef Node => selectedNode ?? Props.node;

        public string BookName => Node?.bookName;

        public string BookDescription => Node?.bookDescription;

        public string CombinedBookName => Props.combinedBookName;

        public int AcquirePriority => Node?.bookAcquirePriority ?? 0;

        public float AcquireWeight => Node?.BookAcquireWeight ?? 1f;

        public float ExtractedNodeAcquireWeightFactor => Mathf.Max(0.01f, Props.extractedNodeAcquireWeightFactor);

        public float LearningSpeed => Mathf.Max(0.01f, Props.learningSpeed);

        public float ExtraNodeChance => Mathf.Clamp01(Props.extraNodeChance);

        public bool Active => active && Node != null;

        public override bool DoesProvidesOutcome(Pawn reader)
        {
            return CanStudy(reader, out _);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_Defs.Look(ref selectedNode, "mx_skillBook_node");
            Scribe_Values.Look(ref active, "mx_skillBook_active", true);
            if (selectedNode != null)
            {
                GameComponent_MX_SkillTreeKnowledge.NotifyNodeExtracted(selectedNode);
            }
        }

        public override void OnReadingTick(Pawn reader, float factor)
        {
            AddReadingProgress(reader, factor);
        }

        public override bool BenefitDetailsCanChange(Pawn reader = null)
        {
            return reader != null;
        }

        public override IEnumerable<Rule_String> GetTopicRuleStrings()
        {
            if (!(Book is Thing_MX_SkillBook skillBook) || GetDoers(Book).FirstOrDefault() != this)
            {
                return null;
            }

            string paragraphs = skillBook.GenerateSkillBookParagraphs();
            string qualitySummary = skillBook.GenerateSkillBookQualitySummary(Props.qualitySummaryKey);
            return paragraphs.NullOrEmpty() && qualitySummary.NullOrEmpty()
                ? null
                : new[]
                {
                    new Rule_String("mx_skillbook_paragraphs", paragraphs ?? string.Empty),
                    new Rule_String("mx_skillbook_quality_summary", qualitySummary ?? string.Empty)
                };
        }

        public void SetActive(bool value)
        {
            active = value;
        }

        public void SelectNode(SkillNodeDef node)
        {
            selectedNode = node;
            active = node != null;
            GameComponent_MX_SkillTreeKnowledge.NotifyNodeExtracted(node);
            ClearCachedStudyTarget();
        }

        public bool CacheStudyTarget(Pawn pawn)
        {
            ClearCachedStudyTarget();
            if (!active || pawn == null || Node == null)
            {
                return false;
            }

            HediffComp_SkillTreeState state = GetSkillState(pawn);
            if (state == null || !state.CanLearn(Node, out _))
            {
                return false;
            }

            cachedReader = pawn;
            cachedState = state;
            cachedNode = Node;
            cachedNodeLearned = false;
            return true;
        }

        public void ClearCachedStudyTarget(Pawn pawn = null)
        {
            if (pawn != null && cachedReader != pawn)
            {
                return;
            }

            cachedReader = null;
            cachedState = null;
            cachedNode = null;
            cachedNodeLearned = false;
        }

        public bool CachedStudyTargetLearned(Pawn pawn)
        {
            return pawn != null && cachedReader == pawn && cachedNodeLearned;
        }

        public bool TryGetStudyProgress(Pawn pawn, out float progress)
        {
            progress = 0f;
            if (pawn == null)
            {
                return false;
            }

            if (cachedReader == pawn && cachedState != null && cachedNode != null)
            {
                progress = cachedNodeLearned ? 1f : cachedState.GetNodeReadingProgressPercent(cachedNode);
                return true;
            }

            if (!CanStudy(pawn, out _))
            {
                return false;
            }

            HediffComp_SkillTreeState state = GetSkillState(pawn);
            if (state == null || Node == null)
            {
                return false;
            }

            progress = state.GetNodeReadingProgressPercent(Node);
            return true;
        }

        public bool CanSelectFor(Pawn pawn)
        {
            if (Node == null)
            {
                return false;
            }

            return CanSelectNode(pawn, Node);
        }

        public bool CanSelectNode(Pawn pawn, SkillNodeDef node)
        {
            if (node == null || node.bookName.NullOrEmpty() || node.bookDescription.NullOrEmpty())
            {
                return false;
            }

            if (!node.CanSpawnFor(pawn))
            {
                return false;
            }

            if (Props.skillCategory != null && node.category != Props.skillCategory)
            {
                return false;
            }

            HediffComp_SkillTreeState state = GetSkillState(pawn);
            if (state == null)
            {
                return pawn == null;
            }

            return state.CanLearn(node, out _);
        }

        public bool CanStudy(Pawn pawn, out string disabledReason)
        {
            if (!active)
            {
                disabledReason = TranslateReason(Props.dataMissingReasonKey);
                return false;
            }

            HediffComp_SkillTreeState state = GetSkillState(pawn);
            if (state == null)
            {
                disabledReason = TranslateReason(Props.missingStateReasonKey);
                return false;
            }

            if (Node == null)
            {
                disabledReason = TranslateReason(Props.dataMissingReasonKey);
                return false;
            }

            if (!state.CanLearn(Node, out disabledReason))
            {
                return false;
            }

            disabledReason = null;
            return true;
        }

        private void AddReadingProgress(Pawn pawn, float factor)
        {
            if (cachedReader == pawn && cachedState != null && cachedNode != null)
            {
                AddReadingProgress(pawn, cachedState, cachedNode, factor);
                return;
            }

            if (!CanStudy(pawn, out _))
            {
                return;
            }

            HediffComp_SkillTreeState state = GetSkillState(pawn);
            if (state == null || Node == null)
            {
                return;
            }

            AddReadingProgress(pawn, state, Node, factor);
        }

        private void AddReadingProgress(Pawn pawn, HediffComp_SkillTreeState state, SkillNodeDef node, float factor)
        {
            float progressTicks = Mathf.Max(0f, factor);
            if (state.CanLearn(node, out _)
                && state.AddNodeReadingProgress(node, progressTicks * LearningSpeed)
                && state.TryLearn(node, out _))
            {
                state.ClearNodeReadingProgress(node);
                cachedNodeLearned = cachedReader == pawn && cachedNode == node;
                SendSkillNodeLearnedLetter(pawn, node, state);
            }
        }

        public override string GetBenefitsString(Pawn reader = null)
        {
            if (Node == null || !active)
            {
                return null;
            }

            string effect = TranslateKey(Props.benefitKey, Node.LabelCap, GetSkillPointsPerHour(reader).ToStringDecimalIfSmall());

            if (reader != null && !CanStudy(reader, out string reason))
            {
                effect += TranslateKey(Props.parenthesesKey, reason);
            }

            return effect;
        }

        private float GetSkillPointsPerHour(Pawn reader)
        {
            float factor = LearningSpeed;
            if (reader != null)
            {
                factor *= reader.GetStatValue(StatDefOf.ReadingSpeed);
                factor *= BookUtility.GetReadingBonus(reader);
            }

            float ticksPerSkillPoint = Mathf.Max(1f, (float)Node.RequiredReadingTicks / Node.SkillPoints);
            return factor * GenDate.TicksPerHour / ticksPerSkillPoint;
        }

        private HediffComp_SkillTreeState GetSkillState(Pawn pawn)
        {
            if (pawn?.health?.hediffSet?.hediffs == null || Props.skillCategory == null)
            {
                return null;
            }

            foreach (Hediff hediff in pawn.health.hediffSet.hediffs)
            {
                HediffComp_SkillTreeState state = (hediff as HediffWithComps)?.GetComp<HediffComp_SkillTreeState>();
                if (state != null && state.AllowsCategory(Props.skillCategory))
                {
                    return state;
                }
            }

            return null;
        }

        private void SendSkillNodeLearnedLetter(Pawn reader, SkillNodeDef node, HediffComp_SkillTreeState state)
        {
            if (Find.LetterStack == null)
            {
                return;
            }

            string nodeLabel = node.LabelCap.ToString() + " " + TranslateKey(Props.levelTextKey, state.GetNodeLevel(node), node.MaxLevel);
            string title = Book is Thing_MX_SkillBook skillBook ? skillBook.BookTitle : Book.LabelCap;
            Find.LetterStack.ReceiveLetter(
                TranslateReason(Props.learnedLetterLabelKey),
                TranslateKey(Props.learnedLetterTextKey, title, nodeLabel),
                LetterDefOf.PositiveEvent,
                reader == null ? null : new LookTargets(reader));
        }

        public string TranslateReason(string key)
        {
            return key.NullOrEmpty() ? string.Empty : TranslateIfKey(key);
        }

        private static string TranslateKey(string key, params NamedArgument[] args)
        {
            return key.NullOrEmpty()
                ? string.Empty
                : Translator.CanTranslate(key) ? key.Translate(args).ToString() : string.Format(key, args.Select(arg => arg.arg).ToArray());
        }

        public static bool TryGetDoer(Book book, out BookOutcomeDoer_SkillTreeUnlock doer)
        {
            doer = null;
            return book?.BookComp != null && book.BookComp.TryGetDoer(out doer);
        }

        public static IEnumerable<BookOutcomeDoer_SkillTreeUnlock> GetDoers(Book book)
        {
            return book?.BookComp?.GetDoers<BookOutcomeDoer_SkillTreeUnlock>() ?? Enumerable.Empty<BookOutcomeDoer_SkillTreeUnlock>();
        }

        public static string TranslateIfKey(string text)
        {
            if (text.NullOrEmpty())
            {
                return text;
            }

            return Translator.CanTranslate(text) ? text.Translate().ToString() : text;
        }
    }

}

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
    public class SkillNodeCategoryDef : Def
    {
        public int displayOrder;
    }

    public class SkillNodeCollectionDef : Def
    {
        public SkillNodeCategoryDef category;
        public int displayOrder;
    }

    public class SkillNodeDef : Def
    {
        private const int TicksPerSkillPoint = 5000;

        public SkillNodeCategoryDef category;
        public SkillNodeCollectionDef collection;
        public int displayOrder;
        public int column;
        public float y = -1f;
        public bool initiallyLearned;
        public bool important;
        public int maxLevel = 1;
        public int skillPoints = 1;
        public string bookName;
        public string bookDescription;
        public int bookAcquirePriority;
        public float bookAcquireWeight = 1f;

        public int MaxLevel => maxLevel < 1 ? 1 : maxLevel;

        public int SkillPoints => Mathf.Max(1, skillPoints);

        public int RequiredReadingTicks => SkillPoints * TicksPerSkillPoint;

        public float BookAcquireWeight => Mathf.Max(0.01f, bookAcquireWeight);
    }

    public interface ISkillTreeStateListener
    {
        void Notify_SkillTreeStateChanged(Pawn pawn, HediffComp_SkillTreeState state);
    }

    public class HediffCompProperties_SkillTreeState : HediffCompProperties
    {
        public List<SkillNodeCategoryDef> categories;
        public string alreadyLearnedReasonKey = "MX_Common_SkillTreeStateAlreadyLearned";

        public HediffCompProperties_SkillTreeState()
        {
            compClass = typeof(HediffComp_SkillTreeState);
        }
    }

    public class HediffComp_SkillTreeState : HediffComp
    {
        private bool initialized;
        private Dictionary<SkillNodeDef, int> nodeLevels;
        private Dictionary<SkillNodeDef, float> nodeReadingProgress;

        public HediffCompProperties_SkillTreeState Props => (HediffCompProperties_SkillTreeState)props;

        public IEnumerable<SkillNodeDef> LearnedNodes
        {
            get
            {
                NormalizeCollections();
                return nodeLevels.Where(pair => pair.Value > 0).Select(pair => pair.Key);
            }
        }

        public int LearnedNodeCount
        {
            get
            {
                NormalizeCollections();
                return nodeLevels.Count(pair => pair.Value > 0);
            }
        }

        public int UnlockedCollectionCount => RelevantCollections().Count(IsCollectionUnlocked);

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
            NotifyStateChanged();
        }

        public override void CompExposeData()
        {
            Scribe_Values.Look(ref initialized, "mx_skillTree_initialized", false);
            Scribe_Collections.Look(ref nodeLevels, "mx_skillTree_nodeLevels", LookMode.Def, LookMode.Value);
            Scribe_Collections.Look(ref nodeReadingProgress, "mx_skillTree_nodeReadingProgress", LookMode.Def, LookMode.Value);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                NormalizeCollections();
                InitializeNewState();
                EnsureInitiallyLearnedNodes();
                NotifyStateChanged();
            }
        }

        public bool IsCollectionUnlocked(SkillNodeCollectionDef collectionDef)
        {
            NormalizeCollections();
            return collectionDef != null && nodeLevels.Any(pair => pair.Value > 0 && pair.Key?.collection == collectionDef);
        }

        public bool HasNode(SkillNodeDef node)
        {
            return GetNodeLevel(node) > 0;
        }

        public int GetNodeLevel(SkillNodeDef node)
        {
            NormalizeCollections();
            if (node == null)
            {
                return 0;
            }

            int level;
            return nodeLevels.TryGetValue(node, out level) ? Mathf.Clamp(level, 0, node.MaxLevel) : 0;
        }

        public bool CanLearn(SkillNodeDef node, out string reason)
        {
            NormalizeCollections();
            if (node == null)
            {
                reason = "MX_Common_Unknown".Translate();
                return false;
            }

            if (!IsRelevantNode(node))
            {
                reason = "MX_Common_Unknown".Translate();
                return false;
            }

            if (GetNodeLevel(node) >= node.MaxLevel)
            {
                reason = Props.alreadyLearnedReasonKey.Translate();
                return false;
            }

            reason = null;
            return true;
        }

        public bool TryLearn(SkillNodeDef node, out string reason)
        {
            if (!CanLearn(node, out reason))
            {
                return false;
            }

            nodeLevels[node] = GetNodeLevel(node) + 1;
            NotifyStateChanged();
            reason = null;
            return true;
        }

        public int LearnNodes(IEnumerable<SkillNodeDef> nodes)
        {
            NormalizeCollections();
            if (nodes == null)
            {
                return 0;
            }

            int learnedCount = 0;
            foreach (SkillNodeDef node in nodes)
            {
                if (node != null && IsRelevantNode(node) && GetNodeLevel(node) <= 0)
                {
                    nodeLevels[node] = 1;
                    learnedCount++;
                }
            }

            if (learnedCount > 0)
            {
                NotifyStateChanged();
            }

            return learnedCount;
        }

        public float GetNodeReadingProgressTicks(SkillNodeDef node)
        {
            NormalizeCollections();
            if (node == null)
            {
                return 0f;
            }

            float progress;
            return nodeReadingProgress.TryGetValue(node, out progress) ? Mathf.Max(0f, progress) : 0f;
        }

        public float GetNodeReadingProgressPercent(SkillNodeDef node)
        {
            if (node == null)
            {
                return 0f;
            }

            if (GetNodeLevel(node) >= node.MaxLevel)
            {
                return 1f;
            }

            return Mathf.Clamp01(GetNodeReadingProgressTicks(node) / Mathf.Max(1, node.RequiredReadingTicks));
        }

        public bool AddNodeReadingProgress(SkillNodeDef node, float progressTicks)
        {
            NormalizeCollections();
            if (node == null || !IsRelevantNode(node))
            {
                return false;
            }

            float progress = GetNodeReadingProgressTicks(node) + Mathf.Max(0f, progressTicks);
            int requiredTicks = Mathf.Max(1, node.RequiredReadingTicks);
            if (progress >= requiredTicks)
            {
                nodeReadingProgress[node] = requiredTicks;
                return true;
            }

            nodeReadingProgress[node] = progress;
            return false;
        }

        public void ClearNodeReadingProgress(SkillNodeDef node)
        {
            NormalizeCollections();
            if (node != null)
            {
                nodeReadingProgress.Remove(node);
            }
        }

        public void LearnAllNodesInCollection(SkillNodeCollectionDef collectionDef)
        {
            NormalizeCollections();
            if (collectionDef == null)
            {
                return;
            }

            foreach (SkillNodeDef node in DefDatabase<SkillNodeDef>.AllDefsListForReading)
            {
                if (node.collection == collectionDef && IsRelevantNode(node) && GetNodeLevel(node) <= 0)
                {
                    nodeLevels[node] = 1;
                }
            }

            NotifyStateChanged();
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
        }

        private void EnsureInitiallyLearnedNodes()
        {
            LearnNodes(RelevantNodes().Where(node => node.initiallyLearned));
        }

        private IEnumerable<SkillNodeDef> RelevantNodes()
        {
            return DefDatabase<SkillNodeDef>.AllDefsListForReading.Where(IsRelevantNode);
        }

        private List<SkillNodeCollectionDef> RelevantCollections()
        {
            return DefDatabase<SkillNodeCollectionDef>.AllDefsListForReading
                .Where(collection => collection != null && IsRelevantCategory(collection.category))
                .ToList();
        }

        public bool IsRelevantNode(SkillNodeDef node)
        {
            if (node == null)
            {
                return false;
            }

            return IsRelevantCategory(node.category);
        }

        public bool AllowsCategory(SkillNodeCategoryDef category)
        {
            return IsRelevantCategory(category);
        }

        private bool IsRelevantCategory(SkillNodeCategoryDef category)
        {
            return Props.categories == null || Props.categories.Count == 0 || Props.categories.Contains(category);
        }

        private void NotifyStateChanged()
        {
            if (parent?.comps == null)
            {
                return;
            }

            foreach (HediffComp comp in parent.comps)
            {
                if (comp is ISkillTreeStateListener listener)
                {
                    listener.Notify_SkillTreeStateChanged(Pawn, this);
                }
            }
        }

        private void NormalizeCollections()
        {
            if (nodeLevels == null)
            {
                nodeLevels = new Dictionary<SkillNodeDef, int>();
            }

            if (nodeReadingProgress == null)
            {
                nodeReadingProgress = new Dictionary<SkillNodeDef, float>();
            }
        }
    }

    public class BookOutcomeProperties_SkillTreeUnlock : BookOutcomeProperties
    {
        public SkillNodeCategoryDef skillCategory;
        public SkillNodeDef node;
        public float learningSpeed = 1f;
        public float extraNodeChance;
        public string combinedBookName;
        public string missingStateReasonKey = "MX_Common_SkillBookMissingState";
        public string dataMissingReasonKey = "MX_Common_SkillBookDataMissing";
        public string benefitKey = "MX_Common_SkillBookBenefitNodeLine";
        public string qualitySummaryKey = "MX_Common_SkillBookQualitySummary";
        public string parenthesesKey = "MX_Common_Parentheses";
        public string learnedLetterLabelKey = "MX_Common_SkillNodesLearnedLetterLabel";
        public string learnedLetterTextKey = "MX_Common_SkillNodesLearnedLetterText";
        public string levelTextKey = "MX_Common_SkillTreeStateLevel";
        public string statCategoryKey = "MX_Common_SkillBookStatCategory";

        public override Type DoerClass => typeof(BookOutcomeDoer_SkillTreeUnlock);
    }

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

    public class Thing_MX_CustomBook : Book
    {
        private string customTitle;
        private string customContent;

        public override string LabelNoCount => customTitle.NullOrEmpty()
            ? base.LabelNoCount
            : customTitle + GenLabel.LabelExtras(this, includeHp: true, includeQuality: true);

        public override string LabelNoParenthesis => customTitle.NullOrEmpty()
            ? base.LabelNoParenthesis
            : customTitle;

        public override string DescriptionFlavor => DescriptionDetailed;

        public override string DescriptionDetailed
        {
            get
            {
                if (customTitle.NullOrEmpty() && customContent.NullOrEmpty())
                {
                    return base.DescriptionDetailed;
                }

                StringBuilder builder = new StringBuilder();
                builder.AppendLine((customTitle.NullOrEmpty() ? base.LabelNoParenthesis : customTitle).Colorize(ColoredText.TipSectionTitleColor)
                    + GenLabel.LabelExtras(this, includeHp: false, includeQuality: true)
                    + "\n");
                builder.AppendLine(GenericDescription + "\n");
                string benefits = GetBookBenefitsString();
                if (!benefits.NullOrEmpty())
                {
                    builder.AppendLine(benefits);
                }
                return builder.ToString().TrimEndNewlines();
            }
        }

        protected string GenericDescription => def.description.NullOrEmpty()
            ? base.FlavorUI
            : def.description;

        protected string GetBookBenefitsString()
        {
            return BookComp?.Doers?
                .Select(doer => doer.GetBenefitsString())
                .Where(text => !text.NullOrEmpty())
                .ToLineList();
        }

        public void SetCustomText(string title, string content)
        {
            customTitle = title;
            customContent = content;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref customTitle, "mx_customBook_title");
            Scribe_Values.Look(ref customContent, "mx_customBook_content");
        }
    }

    public class Thing_MX_SkillBook : Thing_MX_CustomBook
    {
        private string generatedTitle;
        private string generatedDescription;

        public string BookTitle => generatedTitle.NullOrEmpty() ? def.LabelCap.ToString() : generatedTitle;

        public string BookContent => generatedDescription.NullOrEmpty() ? def.description : generatedDescription;

        public override string LabelNoCount => BookTitle
            + GenLabel.LabelExtras(this, includeHp: true, includeQuality: true);

        public override string LabelNoParenthesis => BookTitle;

        public override string DescriptionFlavor => DescriptionDetailed;

        public override string DescriptionDetailed
        {
            get
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine(BookTitle.Colorize(ColoredText.TipSectionTitleColor)
                    + GenLabel.LabelExtras(this, includeHp: false, includeQuality: true)
                    + "\n");
                builder.AppendLine(GenericDescription + "\n");

                string benefits = GetBenefitsString();
                if (!benefits.NullOrEmpty())
                {
                    builder.AppendLine(benefits);
                }

                return builder.ToString().TrimEndNewlines();
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref generatedTitle, "mx_skillBook_title");
            Scribe_Values.Look(ref generatedDescription, "mx_skillBook_description");
        }

        public bool InitializeFor(Pawn pawn)
        {
            List<BookOutcomeDoer_SkillTreeUnlock> doers = SkillDoers().ToList();
            if (doers.Count == 0)
            {
                return false;
            }

            foreach (BookOutcomeDoer_SkillTreeUnlock doer in doers)
            {
                doer.SelectNode(null);
            }

            BookOutcomeDoer_SkillTreeUnlock generator = doers[0];
            SkillNodeDef selected = SelectSkillNode(generator, pawn, Enumerable.Empty<SkillNodeDef>());
            if (selected == null)
            {
                return false;
            }

            generator.SelectNode(selected);
            if (doers.Count > 1 && Rand.Chance(generator.ExtraNodeChance))
            {
                SkillNodeDef extraNode = SelectSkillNode(doers[1], pawn, new[] { selected });
                if (extraNode != null)
                {
                    doers[1].SelectNode(extraNode);
                }
            }

            GenerateBook(pawn);
            GenerateSkillBookText();
            return true;
        }

        public bool CanStudy(Pawn pawn, out string disabledReason)
        {
            disabledReason = null;
            foreach (BookOutcomeDoer_SkillTreeUnlock doer in SkillDoers())
            {
                if (doer.CanStudy(pawn, out disabledReason))
                {
                    disabledReason = null;
                    return true;
                }
            }

            if (disabledReason == null)
            {
                disabledReason = "MX_Common_SkillBookNoLearnableContent".Translate();
            }
            return false;
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats())
            {
                yield return entry;
            }

            BookOutcomeDoer_SkillTreeUnlock firstDoer = SkillDoers().FirstOrDefault();
            string benefits = GetBenefitsString();
            if (!benefits.NullOrEmpty())
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    TranslateKey(firstDoer?.Props.statCategoryKey),
                    benefits,
                    benefits,
                    1000);
            }
        }

        private IEnumerable<BookOutcomeDoer_SkillTreeUnlock> SkillDoers()
        {
            return BookOutcomeDoer_SkillTreeUnlock.GetDoers(this);
        }

        public bool CacheSkillStudyTargetsForJob(Pawn pawn)
        {
            bool cachedAny = false;
            foreach (BookOutcomeDoer_SkillTreeUnlock doer in SkillDoers())
            {
                cachedAny |= doer.CacheStudyTarget(pawn);
            }

            return cachedAny;
        }

        public bool CachedSkillStudyTargetLearned(Pawn pawn)
        {
            return SkillDoers().Any(doer => doer.CachedStudyTargetLearned(pawn));
        }

        public void ClearCachedSkillStudyTargets(Pawn pawn)
        {
            foreach (BookOutcomeDoer_SkillTreeUnlock doer in SkillDoers())
            {
                doer.ClearCachedStudyTarget(pawn);
            }
        }

        private static SkillNodeDef SelectSkillNode(
            BookOutcomeDoer_SkillTreeUnlock doer,
            Pawn pawn,
            IEnumerable<SkillNodeDef> excludedNodes)
        {
            List<SkillNodeDef> excluded = excludedNodes?.Where(node => node != null).ToList() ?? new List<SkillNodeDef>();
            List<SkillNodeDef> candidates = DefDatabase<SkillNodeDef>.AllDefsListForReading
                .Where(node => !excluded.Contains(node) && doer.CanSelectNode(pawn, node))
                .ToList();
            if (candidates.Count == 0)
            {
                return null;
            }

            int bestPriority = candidates.Max(node => node.bookAcquirePriority);
            return candidates
                .Where(node => node.bookAcquirePriority == bestPriority)
                .RandomElementByWeightWithFallback(node => node.BookAcquireWeight);
        }

        private void GenerateSkillBookText()
        {
            List<BookOutcomeDoer_SkillTreeUnlock> activeDoers = SkillDoers()
                .Where(doer => doer.Active)
                .ToList();
            if (activeDoers.Count == 1
                && !activeDoers[0].BookName.NullOrEmpty()
                && !activeDoers[0].BookDescription.NullOrEmpty())
            {
                generatedTitle = TranslateIfKey(activeDoers[0].BookName);
                generatedDescription = GenerateSkillBookDetailText(activeDoers);
                return;
            }

            generatedDescription = GenerateSkillBookDetailText(activeDoers);
            BookOutcomeDoer_SkillTreeUnlock templateDoer = activeDoers.FirstOrDefault();
            generatedTitle = TranslateIfKey(templateDoer?.CombinedBookName);
            if (generatedTitle.NullOrEmpty())
            {
                generatedTitle = def.LabelCap.ToString();
            }
        }

        public string GenerateSkillBookParagraphs()
        {
            return GenerateSkillBookParagraphs(SkillDoers().Where(doer => doer.Active).ToList());
        }

        private string GenerateSkillBookDetailText(List<BookOutcomeDoer_SkillTreeUnlock> activeDoers)
        {
            return JoinParagraphs(
                GenerateSkillBookParagraphs(activeDoers),
                GenerateSkillBookQualitySummary(activeDoers.FirstOrDefault()?.Props.qualitySummaryKey));
        }

        private string GenerateSkillBookParagraphs(List<BookOutcomeDoer_SkillTreeUnlock> activeDoers)
        {
            return activeDoers?
                .Select(doer => TranslateIfKey(doer.BookDescription))
                .Where(text => !text.NullOrEmpty())
                .Aggregate((string)null, JoinParagraphs);
        }

        public string GenerateSkillBookQualitySummary(string key = null)
        {
            QualityCategory quality = this.TryGetComp<CompQuality>()?.Quality ?? QualityCategory.Normal;
            return TranslateKey(key ?? "MX_Common_SkillBookQualitySummary", quality.GetLabel());
        }

        private string GetBenefitsString()
        {
            return BookComp.Doers
                .Select(doer => doer.GetBenefitsString())
                .Where(text => !text.NullOrEmpty())
                .ToLineList();
        }

        private static string JoinParagraphs(string first, string second)
        {
            if (first.NullOrEmpty())
            {
                return second;
            }

            if (second.NullOrEmpty())
            {
                return first;
            }

            return first + "\n\n" + second;
        }

        private static string TranslateIfKey(string text)
        {
            return BookOutcomeDoer_SkillTreeUnlock.TranslateIfKey(text);
        }

        private static string TranslateKey(string key, params NamedArgument[] args)
        {
            return key.NullOrEmpty()
                ? string.Empty
                : Translator.CanTranslate(key) ? key.Translate(args).ToString() : string.Format(key, args.Select(arg => arg.arg).ToArray());
        }
    }
}

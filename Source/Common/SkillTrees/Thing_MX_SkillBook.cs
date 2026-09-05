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
                StringBuilder builder = new();
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

        public float GetSkillStudyProgressPercent(Pawn pawn)
        {
            float progress = 0f;
            foreach (BookOutcomeDoer_SkillTreeUnlock doer in SkillDoers())
            {
                if (doer.TryGetStudyProgress(pawn, out float doerProgress))
                {
                    progress = Mathf.Max(progress, doerProgress);
                }
            }

            return progress;
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
                .RandomElementByWeightWithFallback(node => GetAdjustedAcquireWeight(doer, node));
        }

        private static float GetAdjustedAcquireWeight(BookOutcomeDoer_SkillTreeUnlock doer, SkillNodeDef node)
        {
            float weight = node.BookAcquireWeight;
            if (GameComponent_MX_SkillTreeKnowledge.IsNodeExtracted(node))
            {
                weight *= doer.ExtractedNodeAcquireWeightFactor;
            }

            return Mathf.Max(0.01f, weight);
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
            return TranslateKey((key ?? "MX_Common_SkillBookQualitySummary") + "_" + quality);
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

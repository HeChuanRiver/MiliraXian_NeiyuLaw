using System.Collections.Generic;
using System.Text;
using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe.Things
{
    public class Thing_QingheMusicScoreBook : Book
    {
        private Comp_QingheMusicScore cachedScoreComp;

        public Comp_QingheMusicScore ScoreComp => cachedScoreComp ?? (cachedScoreComp = GetComp<Comp_QingheMusicScore>());

        public override bool IsReadable => false;

        public override string LabelNoCount => (ScoreComp?.BookTitle ?? def.LabelCap)
            + GenLabel.LabelExtras(this, includeHp: true, includeQuality: true);

        public override string LabelNoParenthesis => ScoreComp?.BookTitle ?? def.LabelCap;

        public override string DescriptionFlavor => DescriptionDetailed;

        public override string DescriptionDetailed
        {
            get
            {
                StringBuilder builder = new StringBuilder();
                builder.AppendLine((ScoreComp?.BookTitle ?? LabelCap).Colorize(ColoredText.TipSectionTitleColor)
                    + GenLabel.LabelExtras(this, includeHp: false, includeQuality: true)
                    + "\n");
                builder.AppendLine((ScoreComp?.BookContent ?? def.description) + "\n");

                string benefits = ScoreComp?.GetBenefitsString();
                if (!benefits.NullOrEmpty())
                {
                    builder.AppendLine(" - " + benefits);
                }

                return builder.ToString().TrimEndNewlines();
            }
        }

        public override void PostPostMake()
        {
            base.PostPostMake();
            ScoreComp?.EnsureInitialized();
        }

        public override void GenerateBook(Pawn author = null, long? fixedDate = null)
        {
            ScoreComp?.EnsureInitialized();
        }

        public override IEnumerable<FloatMenuOption> GetFloatMenuOptions(Pawn selPawn)
        {
            FloatMenuOption option = new FloatMenuOption(
                "MX_QH_ReadSkillBookFloatMenu".Translate(Label),
                delegate
                {
                    Job job = JobMaker.MakeJob(MX_QHDefOf.MX_QH_ReadSkillBook, this);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                })
            {
                iconThing = this
            };

            string reason = null;
            if (ScoreComp == null || !ScoreComp.CanStudy(selPawn, out reason))
            {
                option.Label = string.Format("{0}: {1}", "AssignCannotReadNow".Translate(Label), reason);
                option.Disabled = true;
            }

            Pawn reserver = selPawn.Map.reservationManager.FirstRespectedReserver(this, selPawn)
                ?? selPawn.Map.physicalInteractionReservationManager.FirstReserverOf(this);
            if (reserver != null)
            {
                option.Label += " (" + "ReservedBy".Translate(reserver.LabelShort, reserver) + ")";
            }

            yield return option;
        }

        public void Notify_ReadTick(Pawn pawn, int delta)
        {
            ScoreComp?.AddReadingProgress(pawn, delta);
        }

        public override IEnumerable<StatDrawEntry> SpecialDisplayStats()
        {
            foreach (StatDrawEntry entry in base.SpecialDisplayStats())
            {
                yield return entry;
            }

            string benefits = ScoreComp?.GetBenefitsString();
            if (!benefits.NullOrEmpty())
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    "MX_QH_MusicScoreStatCategory".Translate(),
                    benefits,
                    benefits,
                    1000);
            }

            if (ScoreComp != null)
            {
                yield return new StatDrawEntry(
                    StatCategoryDefOf.Basics,
                    "MX_QH_ReadingProgressStat".Translate(),
                    ScoreComp.ReadingProgressPercent.ToStringPercent("F0"),
                    "MX_QH_ReadingProgressStatDesc".Translate(),
                    999);
            }
        }
    }
}

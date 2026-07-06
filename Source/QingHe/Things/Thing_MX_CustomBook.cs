using System.Text;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Things
{
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
                builder.AppendLine(customContent.NullOrEmpty() ? base.DescriptionDetailed : customContent);
                return builder.ToString().TrimEndNewlines();
            }
        }

        public void SetCustomText(string title, string content)
        {
            customTitle = title;
            customContent = content;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref customTitle, "mx_qh_customTitle");
            Scribe_Values.Look(ref customContent, "mx_qh_customContent");
        }
    }
}

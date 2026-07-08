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
                builder.AppendLine((customContent.NullOrEmpty() ? GenericDescription : customContent) + "\n");
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

}

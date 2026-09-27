using System;
using UnityEngine;
using Verse;
using Widgets = Verse.Widgets;

namespace MiliraXian.Characters.UI
{
    public class TextWidget : Widget_Base
    {
        private readonly Func<string> textGetter;

        public TextWidget(string text, Rect localRect, TextAnchor alignment)
            : this(text, localRect, alignment, GameFont.Small)
        {
        }

        public TextWidget(string text, Rect localRect, TextAnchor alignment, GameFont font)
            : this(() => text, localRect, alignment, font)
        {
        }

        public TextWidget(Func<string> textGetter, Rect localRect, TextAnchor alignment)
            : this(textGetter, localRect, alignment, GameFont.Small)
        {
        }

        public TextWidget(Func<string> textGetter, Rect localRect, TextAnchor alignment, GameFont font)
            : base(localRect, alignment)
        {
            this.textGetter = textGetter ?? (() => string.Empty);
            Font = font;
        }

        public GameFont Font { get; set; } = GameFont.Small;

        public Color TextColor { get; set; } = Color.white;

        public bool WordWrap { get; set; } = true;

        protected override void DrawContents(Rect rect)
        {
            GameFont oldFont = Text.Font;
            bool oldWordWrap = Text.WordWrap;
            Color oldColor = GUI.color;

            Text.Font = Font;
            Text.WordWrap = WordWrap;
            GUI.color = TextColor;
            Widgets.Label(rect, textGetter() ?? string.Empty);

            GUI.color = oldColor;
            Text.WordWrap = oldWordWrap;
            Text.Font = oldFont;
        }
    }
}

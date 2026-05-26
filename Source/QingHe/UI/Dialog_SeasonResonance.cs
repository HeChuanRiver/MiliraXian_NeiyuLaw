using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.UI
{
    public class Dialog_SeasonResonance : Window
    {
        private const float WindowWidth = 520f;
        private const float WindowHeight = 460f;
        private const float SeasonCellSize = 156f;
        private const float CenterIconSize = 92f;

        private readonly Pawn pawn;
        private readonly HediffComp_SeasonResonance resonance;
        private static readonly Color SpringColor = new Color(0.92f, 0.58f, 0.66f);
        private static readonly Color SummerColor = new Color(0.90f, 0.28f, 0.22f);
        private static readonly Color AutumnColor = new Color(0.82f, 0.62f, 0.28f);
        private static readonly Color WinterColor = new Color(0.42f, 0.66f, 0.86f);
        private static readonly Color EmptyBarColor = new Color(0.06f, 0.065f, 0.075f, 1f);
        private static readonly Color CenterIconColor = new Color(0.22f, 0.26f, 0.30f, 1f);

        public override Vector2 InitialSize => new Vector2(WindowWidth, WindowHeight);

        public Dialog_SeasonResonance(Pawn pawn, HediffComp_SeasonResonance resonance)
        {
            this.pawn = pawn;
            this.resonance = resonance;
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseButton = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "花神庭");

            Text.Font = GameFont.Small;
            Rect headerRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, 64f);
            Widgets.Label(headerRect, "当前四时共鸣：" + GetSeasonLabel(resonance.CurrentAttunedSeason)
                                      + "\n总调谐度：" + resonance.Attunement.ToString("F0") + " / " + resonance.MaxAttunement.ToString("F0")
                                      + "\n" + BuildResourceSummary());

            Rect courtRect = new Rect(inRect.x, inRect.y + 108f, inRect.width, inRect.height - 118f);
            float left = courtRect.x + 18f;
            float right = courtRect.xMax - 18f - SeasonCellSize;
            float top = courtRect.y + 8f;
            float bottom = courtRect.yMax - SeasonCellSize - 8f;

            DrawSeasonCell(new Rect(left, top, SeasonCellSize, SeasonCellSize), AttunedSeason.Spring, "春", SpringColor);
            DrawSeasonCell(new Rect(right, top, SeasonCellSize, SeasonCellSize), AttunedSeason.Summer, "夏", SummerColor);
            DrawSeasonCell(new Rect(left, bottom, SeasonCellSize, SeasonCellSize), AttunedSeason.Autumn, "秋", AutumnColor);
            DrawSeasonCell(new Rect(right, bottom, SeasonCellSize, SeasonCellSize), AttunedSeason.Winter, "冬", WinterColor);

            Rect centerRect = new Rect(courtRect.center.x - CenterIconSize * 0.5f, courtRect.center.y - CenterIconSize * 0.5f, CenterIconSize, CenterIconSize);
            DrawCenterPlaceholder(centerRect);
        }

        private void DrawSeasonCell(Rect rect, AttunedSeason season, string label, Color color)
        {
            bool selected = resonance.CurrentAttunedSeason == season;
            Widgets.DrawMenuSection(rect);
            if (Mouse.IsOver(rect))
            {
                Widgets.DrawHighlight(rect);
            }

            if (selected)
            {
                Widgets.DrawHighlight(rect);
            }

            Rect labelRect = new Rect(rect.x + 10f, rect.y + 10f, rect.width - 20f, 30f);
            GUI.color = selected ? color : Color.white;
            Text.Font = GameFont.Medium;
            Widgets.Label(labelRect, label + "季");
            GUI.color = Color.white;

            Rect buttonRect = new Rect(rect.x + 10f, rect.yMax - 38f, rect.width - 20f, 30f);
            if (Widgets.ButtonText(buttonRect, selected ? "当前共鸣" : "切换共鸣"))
            {
                resonance.SetAttunedSeason(season);
            }
        }

        private void DrawCenterPlaceholder(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, CenterIconColor);
            Widgets.DrawBox(rect);
            float percent = Mathf.Clamp01(resonance.Attunement / Mathf.Max(1f, resonance.MaxAttunement));
            Rect fillRect = new Rect(rect.x, rect.yMax - 10f, rect.width * percent, 10f);
            Widgets.DrawBoxSolid(fillRect, ResolveAttunementColor(resonance.CurrentAttunedSeason));
            Text.Anchor = TextAnchor.MiddleCenter;
            Text.Font = GameFont.Small;
            Widgets.Label(rect, "花神庭");
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private string BuildResourceSummary()
        {
            return BuildResourceLine(MX_QHDefOf.MX_QH_FlowerDecree, "花令");
        }

        private string BuildResourceLine(HediffDef resourceDef, string fallbackLabel)
        {
            HediffComp_PawnSpecialResource comp = PawnSpecialResourceUtility.GetSpecialResourceComp(pawn, resourceDef);
            string label = comp?.ResourceLabel ?? fallbackLabel;
            float current = comp?.CurrentValue ?? 0f;
            float max = Mathf.Max(1f, comp?.MaxValue ?? 100f);
            return label + "：" + current.ToString("F0") + " / " + max.ToString("F0");
        }

        private static Color ResolveAttunementColor(AttunedSeason season)
        {
            switch (season)
            {
                case AttunedSeason.Spring:
                    return SpringColor;
                case AttunedSeason.Summer:
                    return SummerColor;
                case AttunedSeason.Autumn:
                    return AutumnColor;
                case AttunedSeason.Winter:
                    return WinterColor;
                default:
                    return new Color(0.72f, 0.90f, 0.80f, 1f);
            }
        }

        private static string GetSeasonLabel(AttunedSeason season)
        {
            switch (season)
            {
                case AttunedSeason.Spring:
                    return "春";
                case AttunedSeason.Summer:
                    return "夏";
                case AttunedSeason.Autumn:
                    return "秋";
                case AttunedSeason.Winter:
                    return "冬";
                default:
                    return "未调谐";
            }
        }
    }
}

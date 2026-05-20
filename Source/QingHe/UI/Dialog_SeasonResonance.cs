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
        private const float AttunementBarThickness = 8f;

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
            Rect headerRect = new Rect(inRect.x, inRect.y + 36f, inRect.width, 48f);
            Widgets.Label(headerRect, "当前四时共鸣：" + GetSeasonLabel(resonance.CurrentAttunedSeason) + "\n" + BuildResourceSummary());

            Rect courtRect = new Rect(inRect.x, inRect.y + 92f, inRect.width, inRect.height - 102f);
            float left = courtRect.x + 18f;
            float right = courtRect.xMax - 18f - SeasonCellSize;
            float top = courtRect.y + 8f;
            float bottom = courtRect.yMax - SeasonCellSize - 8f;

            DrawSeasonCell(new Rect(left, top, SeasonCellSize, SeasonCellSize), AttunedSeason.Spring, "春", SpringColor, Corner.TopLeft);
            DrawSeasonCell(new Rect(right, top, SeasonCellSize, SeasonCellSize), AttunedSeason.Summer, "夏", SummerColor, Corner.TopRight);
            DrawSeasonCell(new Rect(left, bottom, SeasonCellSize, SeasonCellSize), AttunedSeason.Autumn, "秋", AutumnColor, Corner.BottomLeft);
            DrawSeasonCell(new Rect(right, bottom, SeasonCellSize, SeasonCellSize), AttunedSeason.Winter, "冬", WinterColor, Corner.BottomRight);

            Rect centerRect = new Rect(courtRect.center.x - CenterIconSize * 0.5f, courtRect.center.y - CenterIconSize * 0.5f, CenterIconSize, CenterIconSize);
            DrawCenterPlaceholder(centerRect);
        }

        private void DrawSeasonCell(Rect rect, AttunedSeason season, string label, Color color, Corner corner)
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
            Text.Font = GameFont.Medium;
            Widgets.Label(labelRect, label + "季");

            Text.Font = GameFont.Small;
            Rect valueRect = new Rect(rect.x + 10f, rect.y + 42f, rect.width - 20f, 24f);
            float value = resonance.GetAttunement(season);
            float max = Mathf.Max(1f, resonance.MaxAttunement);
            Widgets.Label(valueRect, value.ToString("F0") + " / " + max.ToString("F0"));

            DrawCornerAttunementBar(rect.ContractedBy(10f), Mathf.Clamp01(value / max), color, corner);

            Rect buttonRect = new Rect(rect.x + 10f, rect.yMax - 38f, rect.width - 20f, 30f);
            if (Widgets.ButtonText(buttonRect, selected ? "当前共鸣" : "切换共鸣"))
            {
                resonance.SetAttunedSeason(season);
            }
        }

        private static void DrawCornerAttunementBar(Rect rect, float percent, Color color, Corner corner)
        {
            Rect horizontal;
            Rect vertical;
            switch (corner)
            {
                case Corner.TopRight:
                    horizontal = new Rect(rect.xMax - 72f, rect.y, 72f, AttunementBarThickness);
                    vertical = new Rect(rect.xMax - AttunementBarThickness, rect.y, AttunementBarThickness, 72f);
                    break;
                case Corner.BottomLeft:
                    horizontal = new Rect(rect.x, rect.yMax - AttunementBarThickness, 72f, AttunementBarThickness);
                    vertical = new Rect(rect.x, rect.yMax - 72f, AttunementBarThickness, 72f);
                    break;
                case Corner.BottomRight:
                    horizontal = new Rect(rect.xMax - 72f, rect.yMax - AttunementBarThickness, 72f, AttunementBarThickness);
                    vertical = new Rect(rect.xMax - AttunementBarThickness, rect.yMax - 72f, AttunementBarThickness, 72f);
                    break;
                default:
                    horizontal = new Rect(rect.x, rect.y, 72f, AttunementBarThickness);
                    vertical = new Rect(rect.x, rect.y, AttunementBarThickness, 72f);
                    break;
            }

            DrawBentBar(horizontal, vertical, percent, color);
        }

        private static void DrawBentBar(Rect horizontal, Rect vertical, float percent, Color color)
        {
            Widgets.DrawBoxSolid(horizontal, EmptyBarColor);
            Widgets.DrawBoxSolid(vertical, EmptyBarColor);

            float totalLength = horizontal.width + vertical.height;
            float filledLength = totalLength * percent;
            float horizontalFill = Mathf.Min(horizontal.width, filledLength);
            float verticalFill = Mathf.Clamp(filledLength - horizontal.width, 0f, vertical.height);
            if (horizontalFill > 0.5f)
            {
                Widgets.DrawBoxSolid(new Rect(horizontal.x, horizontal.y, horizontalFill, horizontal.height), color);
            }

            if (verticalFill > 0.5f)
            {
                Widgets.DrawBoxSolid(new Rect(vertical.x, vertical.y, vertical.width, verticalFill), color);
            }
        }

        private static void DrawCenterPlaceholder(Rect rect)
        {
            Widgets.DrawBoxSolid(rect, CenterIconColor);
            Widgets.DrawBox(rect);
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

        private enum Corner
        {
            TopLeft,
            TopRight,
            BottomLeft,
            BottomRight
        }
    }
}

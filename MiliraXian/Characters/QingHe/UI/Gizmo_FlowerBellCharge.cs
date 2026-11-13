using MiliraXian.Characters.QingHe.Things.Projectiles;
using MiliraXian.Characters.QingHe.Things.Weapons;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.UI
{
    /// <summary>
    /// The Flower Bell's charge: a peach blossom on the
    /// left (one petal a tier, display only), on the right a five-segment charge bar that also sets
    /// the auto-release threshold, and the release button.
    /// </summary>
    [StaticConstructorOnStartup]
    public class Gizmo_FlowerBellCharge : Gizmo
    {
        private const float Width = 140f;
        private const float GizmoHeight = 75f;
        private const float FlowerSide = 54f;
        private const float ColumnX0 = 64f;
        private const float ColumnX1 = 134f;
        private const float BarHeight = 10f;
        private const float BarButtonGap = 8f;
        private const float ButtonHeight = 22f;
        private const float SegmentGap = 2f;

        private static readonly Texture2D PetalTex = ContentFinder<Texture2D>.Get("MiliraXianQinghe/Effect/FlowerBell/PeachPetalSolid");
        private static readonly Texture2D CenterTex = ContentFinder<Texture2D>.Get("MiliraXianQinghe/Effect/FlowerBell/PeachCenter");

        private static readonly Color Dark = new(0.30f, 0.31f, 0.33f, 1f);
        private static readonly Color Stamen = new(1f, 0.85f, 0.45f, 1f);
        private static readonly Color SegmentBorder = new(0.42f, 0.44f, 0.44f, 1f);
        private static readonly Color SegmentInside = new(0.16f, 0.17f, 0.18f, 1f);
        private static readonly Color Gold = new(1f, 0.86f, 0.45f, 1f);
        private static readonly Color GoldLocked = new(0.75f, 0.65f, 0.37f, 1f);
        private static readonly Color DefaultSeason = new(0.95f, 0.92f, 0.95f, 1f);

        private readonly CompFlowerBellCharge comp;

        public Gizmo_FlowerBellCharge(CompFlowerBellCharge comp)
        {
            this.comp = comp;
            Order = -98f;
        }

        public override float GetWidth(float maxWidth) => Width;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new(topLeft.x, topLeft.y, Width, GizmoHeight);
            Widgets.DrawWindowBackground(rect);

            float charge = comp.Charge;
            int tier = comp.Tier;
            float progress = comp.TierProgress;
            bool decaying = comp.Decaying;
            Color season = SeasonColor();

            DrawFlower(new Rect(rect.x + 5f, rect.y + (GizmoHeight - FlowerSide) / 2f, FlowerSide, FlowerSide), tier, progress, decaying, season);

            float blockTop = rect.y + (GizmoHeight - (BarHeight + BarButtonGap + ButtonHeight)) / 2f;
            Rect bar = new(rect.x + ColumnX0, blockTop, ColumnX1 - ColumnX0, BarHeight);
            DrawBar(bar, tier, progress, decaying, season);

            Rect button = new(bar.x, bar.yMax + BarButtonGap, bar.width, ButtonHeight);
            bool can = comp.CanRelease;
            Text.Font = GameFont.Tiny;
            if (Widgets.ButtonText(button, "MX_QH_BellReleaseNow".Translate(), active: can) && can)
            {
                comp.releaseRequested = true;
            }
            Text.Font = GameFont.Small;
            TooltipHandler.TipRegion(button, () => can
                ? "MX_QH_BellReleaseTip".Translate(tier).ToString()
                : "MX_QH_BellReleaseDisabled".Translate().ToString(), 9712311);

            TooltipHandler.TipRegion(rect, () => "MX_QH_BellChargeTip".Translate(charge.ToString("F1"), tier, comp.Threshold).ToString()
                + (decaying ? "\n" + "MX_QH_BellDecaying".Translate() : string.Empty), 9712310);
            return new GizmoResult(Mouse.IsOver(rect) ? GizmoState.Mouseover : GizmoState.Clear);
        }

        private Color SeasonColor()
        {
            ThingDef proj = comp.parent.TryGetComp<CompFlowerBellResonance>()?.CurrentProjectileFor(comp.Wielder);
            Color c = proj?.GetModExtension<FlowerBellNoteExtension>()?.color ?? DefaultSeason;
            c.a = 1f;
            return c;
        }

        private static Color Grey(Color c, float k)
        {
            float g = (c.r + c.g + c.b) / 3f;
            return new Color(Mathf.Lerp(c.r, g, k), Mathf.Lerp(c.g, g, k), Mathf.Lerp(c.b, g, k), 1f);
        }

        // --- the flower: petal i at 72 i degrees clockwise from up, the base at the centre
        private void DrawFlower(Rect r, int tier, float progress, bool decaying, Color season)
        {
            if (Event.current.type != EventType.Repaint)
            {
                return;
            }
            Vector2 c = r.center;
            float s = r.width / 2f;
            Rect petal = new(c.x - s / 2f, c.y - s, s, s);
            for (int i = 0; i < comp.Tiers; i++)
            {
                Color pc;
                if (i < tier)
                {
                    pc = decaying ? Grey(season, 0.75f) * 0.8f : season;
                }
                else if (i == tier && !decaying)
                {
                    pc = Color.Lerp(Dark, season, 0.75f * progress);
                }
                else
                {
                    pc = Dark;
                }
                pc.a = 1f;
                Matrix4x4 m = GUI.matrix;
                Verse.UI.RotateAroundPivot(72f * i, c);
                GUI.color = pc;
                GUI.DrawTexture(petal, PetalTex);
                GUI.matrix = m;
            }
            float k = tier / (float)comp.Tiers;
            Color cc = Color.Lerp(Dark, decaying ? new Color(0.6f, 0.58f, 0.55f, 1f) : Stamen, k);
            cc.a = 1f;
            GUI.color = cc;
            float cs = s * 0.42f;
            GUI.DrawTexture(new Rect(c.x - cs / 2f, c.y - cs / 2f, cs, cs), CenterTex);
            GUI.color = Color.white;
        }

        // --- the bar: reached tiers full, the one being charged partly; the threshold in gold
        private void DrawBar(Rect bar, int tier, float progress, bool decaying, Color season)
        {
            int n = comp.Tiers;
            float w = (bar.width - SegmentGap * (n - 1)) / n;
            bool locked = comp.ThresholdLocked;
            int hover = -1;
            for (int i = 0; i < n; i++)
            {
                Rect seg = new(bar.x + i * (w + SegmentGap), bar.y, w, bar.height);
                if (Mouse.IsOver(seg))
                {
                    hover = i;
                }
            }
            int shown = hover >= 0 && !locked ? hover + 1 : comp.Threshold;
            Color gold = locked ? GoldLocked : Gold;
            Color fill = decaying ? Grey(season, 0.75f) * 0.8f : season * 0.9f;
            fill.a = 1f;
            for (int i = 0; i < n; i++)
            {
                Rect seg = new(bar.x + i * (w + SegmentGap), bar.y, w, bar.height);
                Widgets.DrawBoxSolid(seg, i < shown ? gold : SegmentBorder);
                Rect inside = seg.ContractedBy(1f);
                Widgets.DrawBoxSolid(inside, SegmentInside);
                if (i < tier)
                {
                    Widgets.DrawBoxSolid(inside, fill);
                }
                else if (i == tier && progress > 0f && !decaying)
                {
                    Widgets.DrawBoxSolid(new Rect(inside.x, inside.y, inside.width * progress, inside.height), Color.Lerp(SegmentInside, fill, 0.65f));
                }
                if (i == hover && !locked)
                {
                    Widgets.DrawBox(seg, 1);
                }
                if (Widgets.ButtonInvisible(seg) && !locked)
                {
                    comp.Threshold = i + 1;
                }
                int index = i;
                TooltipHandler.TipRegion(seg, () => locked
                    ? "MX_QH_BellThresholdLocked".Translate().ToString()
                    : "MX_QH_BellThresholdSet".Translate(index + 1).ToString(), 9712320 + i);
            }
            // The arrow over the middle of the threshold segment, in the margin above the bar.
            float tx = bar.x + (shown - 1) * (w + SegmentGap) + w / 2f;
            for (int row = 0; row < 4; row++)
            {
                float half = 3.5f - row;
                Widgets.DrawBoxSolid(new Rect(tx - half, bar.y - 6f + row, half * 2f, 1f), gold);
            }
        }
    }
}

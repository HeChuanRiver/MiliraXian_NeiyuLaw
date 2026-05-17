using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public class Hediff_QH_StatusGizmo : HediffWithComps
    {
    }

    public class HediffCompProperties_QH_StatusGizmo : HediffCompProperties
    {
        public HediffDef firstResourceDef;
        public HediffDef secondResourceDef;
        public bool onlyWhenSelected = true;

        public HediffCompProperties_QH_StatusGizmo()
        {
            compClass = typeof(HediffComp_QH_StatusGizmo);
        }
    }

    public class HediffComp_QH_StatusGizmo : HediffComp
    {
        private const float CompareEpsilon = 0.0001f;

        private int lastSampleTick = -1;
        private float lastFirstValue;
        private float lastSecondValue;
        private int firstTickDeltaDirection;
        private int secondTickDeltaDirection;
        private bool trendInitialized;

        public HediffCompProperties_QH_StatusGizmo Props => (HediffCompProperties_QH_StatusGizmo)props;

        public int FirstTickDeltaDirection => firstTickDeltaDirection;

        public int SecondTickDeltaDirection => secondTickDeltaDirection;

        public override bool CompDisallowVisible()
        {
            return true;
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (Pawn == null || Pawn.Dead)
            {
                yield break;
            }

            if (Props.onlyWhenSelected && Find.Selector.SingleSelectedThing != Pawn)
            {
                yield break;
            }

            UpdateTickDeltaIfNeeded();
            yield return new Gizmo_QH_StatusGizmo(this);
        }

        public HediffComp_PawnSpecialResource FirstComp
        {
            get
            {
                var def = Props.firstResourceDef ?? MX_QHDefOf.MX_QH_Tempest;
                return def == MX_QHDefOf.MX_QH_Tempest ? TempestUtility.GetResourceComp(Pawn) : PawnSpecialResourceUtility.GetSpecialResourceComp(Pawn, def);
            }
        }

        public HediffComp_PawnSpecialResource SecondComp
        {
            get
            {
                var def = Props.secondResourceDef ?? MX_QHDefOf.MX_QH_Elegance;
                return def == MX_QHDefOf.MX_QH_Elegance ? EleganceUtility.GetResourceComp(Pawn) : PawnSpecialResourceUtility.GetSpecialResourceComp(Pawn, def);
            }
        }

        public CompLotusShield LotusShieldComp => Pawn?.GetComp<CompLotusShield>();

        private void UpdateTickDeltaIfNeeded()
        {
            var currentTick = Find.TickManager?.TicksGame ?? -1;
            if (currentTick == lastSampleTick)
            {
                return;
            }

            var currentFirst = FirstComp?.CurrentValue ?? 0f;
            var currentSecond = SecondComp?.CurrentValue ?? 0f;

            if (!trendInitialized)
            {
                firstTickDeltaDirection = 0;
                secondTickDeltaDirection = 0;
                trendInitialized = true;
            }
            else
            {
                firstTickDeltaDirection = CompareDelta(currentFirst - lastFirstValue);
                secondTickDeltaDirection = CompareDelta(currentSecond - lastSecondValue);
            }

            lastFirstValue = currentFirst;
            lastSecondValue = currentSecond;
            lastSampleTick = currentTick;
        }

        private static int CompareDelta(float delta)
        {
            if (delta > CompareEpsilon)
            {
                return 1;
            }

            if (delta < -CompareEpsilon)
            {
                return -1;
            }

            return 0;
        }
    }

    [StaticConstructorOnStartup]
    public class Gizmo_QH_StatusGizmo : Gizmo
    {
        private const int FirstBarTipSalt = 910101;
        private const int SecondBarTipSalt = 910102;
        private const int ShieldBarTipSalt = 910103;
        private const float DimFactor = 0.55f;
        private const float WhitenFactor = 0.45f;
        private const float ShieldDarkFactor = 0.45f;
        private const float TrendCycleTicks = 180f;
        private const float TrendDeadTicks = 60f;
        private const float TrendWidthPct = 0.18f;
        private const float TrendMinWidth = 8f;
        private const float TrendMaxWidth = 20f;
        private const int TrendSlices = 9;
        private const float TrendLightAlpha = 0.18f;

        private readonly HediffComp_QH_StatusGizmo source;

        private static readonly Texture2D EmptyBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.03f, 0.035f, 0.05f));
        private static readonly Color TargetLineColor = new Color(1f, 1f, 1f, 0.85f);
        private static readonly Color FirstBaseColor = new Color(0.34f, 0.42f, 0.80f);
        private static readonly Color SecondBaseColor = new Color(0.72f, 0.28f, 0.75f);
        private static readonly Color ShieldBaseColor = new Color(0.55f, 0.7f, 1f, 1f);
        private static readonly Color ShieldPinkColor = new Color(1f, 0.75f, 1f, 1f);
        private static readonly Color ShieldBackgroundColor = new Color(0.08f, 0.09f, 0.11f, 1f);
        private static readonly Color ShieldBreakDarkColor = new Color(0.22f, 0.05f, 0.06f, 1f);
        private static readonly Color ShieldBreakBrightColor = new Color(1f, 0.95f, 0.95f, 1f);
        private static readonly Dictionary<Color32, Texture2D> FillBarTexCache = new Dictionary<Color32, Texture2D>();

        public Gizmo_QH_StatusGizmo(HediffComp_QH_StatusGizmo source)
        {
            this.source = source;
            Order = -100f;
        }

        public override float GetWidth(float maxWidth)
        {
            return 212f;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            var inner = rect.ContractedBy(6f);
            Widgets.DrawWindowBackground(rect);

            var first = source.FirstComp;
            var second = source.SecondComp;
            var lotusShield = source.LotusShieldComp;
            var eleganceTargetPercent = EleganceUtility.GetTempestRecoverThreshold(source.Pawn);

            var firstColor = ResolveDisplayColor(FirstBaseColor, source.FirstTickDeltaDirection);
            var secondColor = ResolveDisplayColor(SecondBaseColor, source.SecondTickDeltaDirection);
            if (second != null)
            {
                var secondMax = Mathf.Max(1f, second.MaxValue);
                if (second.CurrentValue <= secondMax * eleganceTargetPercent + 0.0001f)
                {
                    secondColor = Color.Lerp(secondColor, Color.white, WhitenFactor);
                }
            }

            var firstBarRect = DrawRow(inner, 6f, first, "MX_QH_TempestFallbackLabel".Translate().ToString(), firstColor, source.FirstTickDeltaDirection);
            var secondBarRect = DrawRow(inner, 38f, second, "MX_QH_EleganceFallbackLabel".Translate().ToString(), secondColor, source.SecondTickDeltaDirection, eleganceTargetPercent);
            var shieldBarRect = DrawShieldBar(inner, lotusShield, first, second);

            TooltipHandler.TipRegion(firstBarRect, () => BuildResourceBarTip(source.FirstComp, "MX_QH_TempestFallbackLabel".Translate().ToString()), GetStableTipId(FirstBarTipSalt));
            TooltipHandler.TipRegion(secondBarRect, () => BuildResourceBarTip(source.SecondComp, "MX_QH_EleganceFallbackLabel".Translate().ToString(), eleganceTargetPercent), GetStableTipId(SecondBarTipSalt));
            TooltipHandler.TipRegion(shieldBarRect, () => BuildShieldBarTip(source.LotusShieldComp), GetStableTipId(ShieldBarTipSalt));
            return new GizmoResult(GizmoState.Clear);
        }

        private static Rect DrawRow(
            Rect inner,
            float offsetY,
            HediffComp_PawnSpecialResource comp,
            string fallbackLabel,
            Color fillColor,
            int trendDirection,
            float? targetPercent = null)
        {
            Text.Font = GameFont.Small;
            var labelRect = new Rect(inner.x, inner.y + offsetY - 1f, 60f, Text.LineHeight + 2f);
            Widgets.Label(labelRect, comp?.ResourceLabel ?? fallbackLabel);

            var barRect = new Rect(inner.x + 63f, inner.y + offsetY, 100f, 22f);
            var current = comp?.CurrentValue ?? 0f;
            var max = Mathf.Max(1f, comp?.MaxValue ?? 100f);
            var percent = Mathf.Clamp01(current / max);
            var fillableRect = Widgets.FillableBar(barRect, percent, GetFillBarTexture(fillColor), EmptyBarTex, true);
            DrawTrendOverlay(fillableRect, percent, trendDirection);
            if (targetPercent.HasValue)
            {
                DrawTargetValueLine(fillableRect, targetPercent.Value);
            }
            DrawBarHoverHighlight(barRect);

            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(barRect, current.ToString("F0") + " / " + max.ToString("F0"));
            Text.Anchor = TextAnchor.UpperLeft;

            return barRect;
        }

        private static Color ResolveDisplayColor(Color baseColor, int tickDeltaDirection)
        {
            if (tickDeltaDirection < 0)
            {
                return new Color(
                    baseColor.r * DimFactor,
                    baseColor.g * DimFactor,
                    baseColor.b * DimFactor,
                    baseColor.a);
            }

            return baseColor;
        }

        private static Texture2D GetFillBarTexture(Color color)
        {
            var key = (Color32)color;
            if (!FillBarTexCache.TryGetValue(key, out var texture))
            {
                texture = SolidColorMaterials.NewSolidColorTexture(color);
                FillBarTexCache[key] = texture;
            }

            return texture;
        }

        private static void DrawTrendOverlay(Rect contentRect, float percent, int trendDirection)
        {
            if (trendDirection <= 0 || percent <= 0.0001f)
            {
                return;
            }

            var bandWidth = Mathf.Clamp(contentRect.width * TrendWidthPct, TrendMinWidth, TrendMaxWidth);
            if (bandWidth <= 1f)
            {
                return;
            }

            var totalTicks = TrendCycleTicks + TrendDeadTicks;
            var cycleTick = Mathf.Repeat(Find.TickManager?.TicksGame ?? 0, totalTicks);
            if (cycleTick >= TrendCycleTicks)
            {
                return;
            }

            var t = cycleTick / TrendCycleTicks;
            var centerX = contentRect.x + contentRect.width * t;
            var color = new Color(1f, 1f, 1f, TrendLightAlpha);
            DrawGradientBand(contentRect, centerX, bandWidth * 0.5f, color);
        }

        private static void DrawGradientBand(Rect limitRect, float centerX, float halfWidth, Color color)
        {
            if (halfWidth <= 0.5f)
            {
                return;
            }

            var left = Mathf.Max(limitRect.x, centerX - halfWidth);
            var right = Mathf.Min(limitRect.xMax, centerX + halfWidth);
            var width = right - left;
            if (width <= 0.5f)
            {
                return;
            }

            var sliceWidth = width / TrendSlices;
            for (var i = 0; i < TrendSlices; i++)
            {
                var x = left + sliceWidth * i;
                var sliceCenter = x + sliceWidth * 0.5f;
                var dist = Mathf.Abs(sliceCenter - centerX) / halfWidth;
                var alpha = Mathf.Clamp01(1f - dist);
                if (alpha <= 0.001f)
                {
                    continue;
                }

                var c = color;
                c.a *= alpha * alpha;
                Widgets.DrawBoxSolid(new Rect(x, limitRect.y, sliceWidth + 0.75f, limitRect.height), c);
            }
        }

        private static void DrawTargetValueLine(Rect barRect, float targetPercent)
        {
            var clampedTarget = Mathf.Clamp01(targetPercent);
            var lineX = barRect.x + barRect.width * clampedTarget - 1f;
            var lineRect = new Rect(Mathf.Clamp(lineX, barRect.x, barRect.xMax - 2f), barRect.y + 1f, 2f, barRect.height - 2f);
            Widgets.DrawBoxSolid(lineRect, TargetLineColor);
        }

        private static Rect DrawShieldBar(
            Rect inner,
            CompLotusShield shield,
            HediffComp_PawnSpecialResource tempestComp,
            HediffComp_PawnSpecialResource eleganceComp)
        {
            var outerRect = new Rect(inner.x + 170f, inner.y + 6f, 12f, 54f);
            Widgets.DrawBoxSolid(outerRect, Color.black);

            var barRect = outerRect.ContractedBy(1f);
            if (shield != null && shield.InBreak)
            {
                DrawBreakBackground(barRect);
            }
            else
            {
                Widgets.DrawBoxSolid(barRect, ShieldBackgroundColor);
            }

            var fillPercent = 0f;
            var fillColor = ResolveShieldBarColor(tempestComp, eleganceComp);
            if (shield != null)
            {
                fillPercent = shield.MaxEnergy > 0f ? Mathf.Clamp01(shield.Energy / shield.MaxEnergy) : 0f;
            }

            if (fillPercent > 0.0001f)
            {
                var fillHeight = barRect.height * fillPercent;
                var fillRect = new Rect(barRect.x, barRect.yMax - fillHeight, barRect.width, fillHeight);
                Widgets.DrawBoxSolid(fillRect, fillColor);
            }

            DrawBarHoverHighlight(outerRect);
            return outerRect;
        }

        private static void DrawBreakBackground(Rect barRect)
        {
            var tick = Find.TickManager?.TicksGame ?? 0;
            var pulse = 0.5f + 0.5f * Mathf.Sin(tick / 8f);
            var highlight = Mathf.Clamp01(0.32f + pulse * 0.6f);
            var bright = Color.Lerp(ShieldBreakDarkColor, ShieldBreakBrightColor, highlight);
            Widgets.DrawBoxSolid(barRect, bright);
        }

        private static Color ResolveShieldBarColor(
            HediffComp_PawnSpecialResource tempestComp,
            HediffComp_PawnSpecialResource eleganceComp)
        {
            var tempestPercent = GetResourcePercent(tempestComp);
            var elegancePercent = GetResourcePercent(eleganceComp);

            var darkBase = ScaleColorRgb(ShieldBaseColor, ShieldDarkFactor);
            var darkPink = ScaleColorRgb(ShieldPinkColor, ShieldDarkFactor);

            var baseByTempest = Color.Lerp(darkBase, ShieldBaseColor, tempestPercent);
            var pinkByTempest = Color.Lerp(darkPink, ShieldPinkColor, tempestPercent);

            return Color.Lerp(baseByTempest, pinkByTempest, elegancePercent);
        }

        private static float GetResourcePercent(HediffComp_PawnSpecialResource comp)
        {
            if (comp == null || comp.MaxValue <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(comp.CurrentValue / comp.MaxValue);
        }

        private static Color ScaleColorRgb(Color color, float factor)
        {
            return new Color(color.r * factor, color.g * factor, color.b * factor, color.a);
        }

        private static void DrawBarHoverHighlight(Rect rect)
        {
            if (!Mouse.IsOver(rect))
            {
                return;
            }

            Widgets.DrawHighlight(rect, 0.45f);
        }

        private int GetStableTipId(int salt)
        {
            var pawnId = source?.Pawn?.thingIDNumber ?? 0;
            return Gen.HashCombineInt(pawnId, salt);
        }

        private static string BuildResourceBarTip(HediffComp_PawnSpecialResource comp, string fallbackLabel, float? targetPercent = null)
        {
            var label = comp?.ResourceLabel ?? fallbackLabel;
            var current = comp?.CurrentValue ?? 0f;
            var max = Mathf.Max(1f, comp?.MaxValue ?? 100f);
            var tip = label + ": " + current.ToString("F0") + " / " + max.ToString("F0");
            if (targetPercent.HasValue)
            {
                tip += "\n" + "MX_QH_TargetLine".Translate(targetPercent.Value.ToStringPercent("F0"));
            }

            if (comp != null && !comp.ResourceDescription.NullOrEmpty())
            {
                tip += "\n\n" + comp.ResourceDescription;
            }

            return tip;
        }

        private static string BuildShieldBarTip(CompLotusShield shield)
        {
            if (shield == null)
            {
                return "MX_QH_ShieldInactive".Translate().ToString();
            }

            return shield.BuildShieldTooltip();
        }
    }
}







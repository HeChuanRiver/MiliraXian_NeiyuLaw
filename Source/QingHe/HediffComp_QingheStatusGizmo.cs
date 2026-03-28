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
            int currentTick = Find.TickManager?.TicksGame ?? -1;
            if (currentTick == lastSampleTick)
            {
                return;
            }

            float currentFirst = FirstComp?.CurrentValue ?? 0f;
            float currentSecond = SecondComp?.CurrentValue ?? 0f;

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

        private readonly HediffComp_QH_StatusGizmo source;
        private const float DimFactor = 0.55f;
        private const float WhitenFactor = 0.45f;
        private const float ShieldDarkFactor = 0.45f;

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
            Rect rect = new Rect(topLeft.x, topLeft.y, GetWidth(maxWidth), 75f);
            Rect inner = rect.ContractedBy(6f);
            Widgets.DrawWindowBackground(rect);

            HediffComp_PawnSpecialResource first = source.FirstComp;
            HediffComp_PawnSpecialResource second = source.SecondComp;
            CompLotusShield lotusShield = source.LotusShieldComp;
            float eleganceTargetPercent = EleganceUtility.GetTempestRecoverThreshold(source.Pawn);

            Color firstColor = ResolveDisplayColor(FirstBaseColor, source.FirstTickDeltaDirection);
            Color secondColor = ResolveDisplayColor(SecondBaseColor, source.SecondTickDeltaDirection);
            if (second != null)
            {
                float secondMax = Mathf.Max(1f, second.MaxValue);
                if (second.CurrentValue <= secondMax * eleganceTargetPercent + 0.0001f)
                {
                    secondColor = Color.Lerp(secondColor, Color.white, WhitenFactor);
                }
            }

            Rect firstBarRect = DrawRow(inner, 6f, first, "\u6fc0\u6d41", firstColor);
            Rect secondBarRect = DrawRow(inner, 38f, second, "\u96c5\u4e50", secondColor, eleganceTargetPercent);
            Rect shieldBarRect = DrawShieldBar(inner, lotusShield, first, second);

            TooltipHandler.TipRegion(firstBarRect, () => BuildResourceBarTip(source.FirstComp, "\u6fc0\u6d41"), GetStableTipId(FirstBarTipSalt));
            TooltipHandler.TipRegion(secondBarRect, () => BuildResourceBarTip(source.SecondComp, "\u96c5\u4e50", eleganceTargetPercent), GetStableTipId(SecondBarTipSalt));
            TooltipHandler.TipRegion(shieldBarRect, () => BuildShieldBarTip(source.LotusShieldComp), GetStableTipId(ShieldBarTipSalt));
            return new GizmoResult(GizmoState.Clear);
        }

        private static Rect DrawRow(
            Rect inner,
            float offsetY,
            HediffComp_PawnSpecialResource comp,
            string fallbackLabel,
            Color fillColor,
            float? targetPercent = null)
        {
            Text.Font = GameFont.Small;
            Rect labelRect = new Rect(inner.x, inner.y + offsetY - 1f, 60f, Text.LineHeight + 2f);
            Widgets.Label(labelRect, comp?.ResourceLabel ?? fallbackLabel);

            Rect barRect = new Rect(inner.x + 63f, inner.y + offsetY, 100f, 22f);
            float current = comp?.CurrentValue ?? 0f;
            float max = Mathf.Max(1f, comp?.MaxValue ?? 100f);
            float percent = Mathf.Clamp01(current / max);
            Widgets.FillableBar(barRect, percent, GetFillBarTexture(fillColor), EmptyBarTex, true);
            if (targetPercent.HasValue)
            {
                DrawTargetValueLine(barRect, targetPercent.Value);
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
            Color32 key = color;
            if (!FillBarTexCache.TryGetValue(key, out Texture2D texture))
            {
                texture = SolidColorMaterials.NewSolidColorTexture(color);
                FillBarTexCache[key] = texture;
            }

            return texture;
        }

        private static void DrawTargetValueLine(Rect barRect, float targetPercent)
        {
            float clampedTarget = Mathf.Clamp01(targetPercent);
            float lineX = barRect.x + barRect.width * clampedTarget - 1f;
            Rect lineRect = new Rect(Mathf.Clamp(lineX, barRect.x, barRect.xMax - 2f), barRect.y + 1f, 2f, barRect.height - 2f);
            Widgets.DrawBoxSolid(lineRect, TargetLineColor);
        }

        private static Rect DrawShieldBar(
            Rect inner,
            CompLotusShield shield,
            HediffComp_PawnSpecialResource tempestComp,
            HediffComp_PawnSpecialResource eleganceComp)
        {
            Rect outerRect = new Rect(inner.x + 170f, inner.y + 6f, 12f, 54f);
            Widgets.DrawBoxSolid(outerRect, Color.black);

            Rect barRect = outerRect.ContractedBy(1f);
            if (shield != null && shield.InBreak)
            {
                DrawBreakBackground(barRect);
            }
            else
            {
                Widgets.DrawBoxSolid(barRect, ShieldBackgroundColor);
            }

            float fillPercent = 0f;
            Color fillColor = ResolveShieldBarColor(tempestComp, eleganceComp);
            if (shield != null)
            {
                fillPercent = shield.MaxEnergy > 0f ? Mathf.Clamp01(shield.Energy / shield.MaxEnergy) : 0f;
            }

            if (fillPercent > 0.0001f)
            {
                float fillHeight = barRect.height * fillPercent;
                Rect fillRect = new Rect(barRect.x, barRect.yMax - fillHeight, barRect.width, fillHeight);
                Widgets.DrawBoxSolid(fillRect, fillColor);
            }

            DrawBarHoverHighlight(outerRect);
            return outerRect;
        }

        private static void DrawBreakBackground(Rect barRect)
        {
            int tick = Find.TickManager?.TicksGame ?? 0;
            float pulse = 0.5f + 0.5f * Mathf.Sin(tick / 8f);
            float highlight = Mathf.Clamp01(0.32f + pulse * 0.6f);
            Color bright = Color.Lerp(ShieldBreakDarkColor, ShieldBreakBrightColor, highlight);
            Widgets.DrawBoxSolid(barRect, bright);
        }

        private static Color ResolveShieldBarColor(
            HediffComp_PawnSpecialResource tempestComp,
            HediffComp_PawnSpecialResource eleganceComp)
        {
            float tempestPercent = GetResourcePercent(tempestComp);
            float elegancePercent = GetResourcePercent(eleganceComp);

            Color darkBase = ScaleColorRgb(ShieldBaseColor, ShieldDarkFactor);
            Color darkPink = ScaleColorRgb(ShieldPinkColor, ShieldDarkFactor);

            Color baseByTempest = Color.Lerp(darkBase, ShieldBaseColor, tempestPercent);
            Color pinkByTempest = Color.Lerp(darkPink, ShieldPinkColor, tempestPercent);

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
            int pawnId = source?.Pawn?.thingIDNumber ?? 0;
            return Gen.HashCombineInt(pawnId, salt);
        }

        private static string BuildResourceBarTip(HediffComp_PawnSpecialResource comp, string fallbackLabel, float? targetPercent = null)
        {
            string label = comp?.ResourceLabel ?? fallbackLabel;
            float current = comp?.CurrentValue ?? 0f;
            float max = Mathf.Max(1f, comp?.MaxValue ?? 100f);
            string tip = label + ": " + current.ToString("F0") + " / " + max.ToString("F0");
            if (targetPercent.HasValue)
            {
                tip += "\n\u76ee\u6807\u7ebf: " + targetPercent.Value.ToStringPercent("F0");
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
                return "\u62a4\u76fe\u672a\u6fc0\u6d3b";
            }

            return shield.BuildShieldTooltip();
        }
    }
}



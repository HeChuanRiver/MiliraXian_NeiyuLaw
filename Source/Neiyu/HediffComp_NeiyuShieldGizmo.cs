using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu
{
    public class HediffCompProperties_NeiyuShieldGizmo : HediffCompProperties
    {
        public bool onlyWhenSelected = true;
        public Color stageIColor = new(0.47f, 0.80f, 1.00f, 1f);
        public Color stageIIColor = new(0.36f, 0.61f, 0.84f, 1f);
        public Color stageIIIAbsorbColor = new(0.85f, 0.65f, 0.13f, 1f);
        public Color stageIIIBuffColor = new(1.00f, 0.55f, 0.00f, 1f);
        public Color weakColor = new(0.55f, 0.00f, 0.00f, 1f);
        public float thresholdLineAlpha = 0.6f;
        public string rendererClass;

        public HediffCompProperties_NeiyuShieldGizmo()
        {
            compClass = typeof(HediffComp_NeiyuShieldGizmo);
        }
    }

    public class HediffComp_NeiyuShieldGizmo : HediffComp
    {
        private static INeiyuShieldGizmoRenderer cachedDefaultRenderer;

        public HediffCompProperties_NeiyuShieldGizmo Props => (HediffCompProperties_NeiyuShieldGizmo)props;

        public override bool CompDisallowVisible() => true;

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            if (Pawn == null || Pawn.Dead) yield break;
            if (Props.onlyWhenSelected && Find.Selector.SingleSelectedThing != Pawn) yield break;
            var shield = parent.TryGetComp<HediffComp_MXNeiyuCountShield>();
            if (shield == null) yield break;

            var renderer = ResolveRenderer();
            yield return new Gizmo_NeiyuShieldStatus(shield, renderer, Props);
        }

        private INeiyuShieldGizmoRenderer ResolveRenderer()
        {
            if (!Props.rendererClass.NullOrEmpty())
            {
                try
                {
                    var type = GenTypes.GetTypeInAnyAssembly(Props.rendererClass);
                    if (type != null && typeof(INeiyuShieldGizmoRenderer).IsAssignableFrom(type))
                        return (INeiyuShieldGizmoRenderer)Activator.CreateInstance(type);
                }
                catch { }
            }
            cachedDefaultRenderer ??= new NeiyuShieldGizmoDefaultRenderer();
            return cachedDefaultRenderer;
        }
    }

    [StaticConstructorOnStartup]
    public class Gizmo_NeiyuShieldStatus : Gizmo
    {
        private const float Width = 185f;
        private const float GizmoHeight = 80f;
        private const float InnerMargin = 6f;
        private const float LabelHeight = 20f;
        private const float BarHeight = 20f;
        private const float HintHeight = 18f;
        private const float BadgeWidth = 28f;
        private const float BadgeHeight = 20f;

        private static readonly Color WeakBarColor = new(0.55f, 0.05f, 0.05f, 1f);
        private static readonly Color HintGrayColor = new(0.6f, 0.6f, 0.6f, 1f);
        private static readonly Color AbsorbInvincibleColor = new(1f, 0.85f, 0.2f, 1f);
        private static readonly Color BuffSummaryColor = new(0.9f, 0.9f, 0.9f, 1f);

        private const int FlashDurationTicks = 15;
        private static readonly Color FlashColor = new(1f, 0.25f, 0.25f, 1f);
        private static readonly Color FlashWhiteColor = new(1f, 1f, 1f, 1f);

        private readonly HediffComp_MXNeiyuCountShield shield;
        private readonly INeiyuShieldGizmoRenderer renderer;
        private readonly HediffCompProperties_NeiyuShieldGizmo props;

        public Gizmo_NeiyuShieldStatus(HediffComp_MXNeiyuCountShield shield, INeiyuShieldGizmoRenderer renderer, HediffCompProperties_NeiyuShieldGizmo props)
        {
            this.shield = shield;
            this.renderer = renderer;
            this.props = props;
            Order = -100f;
        }

        public override float GetWidth(float maxWidth) => Width;

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            var rect = new Rect(topLeft.x, topLeft.y, Width, GizmoHeight);
            var inner = rect.ContractedBy(InnerMargin);

            renderer.DrawBackground(rect, shield);

            switch (shield.Stage)
            {
                case 1: DrawStage1(inner); break;
                case 2: DrawStage2(inner); break;
                case 3: DrawStage3(inner); break;
            }

            if (shield.InWeak)
            {
                var badgeRect = new Rect(rect.xMax - BadgeWidth - 2f, rect.y + 2f, BadgeWidth, BadgeHeight);
                renderer.DrawWeakBadge(badgeRect, shield);
                TooltipHandler.TipRegion(badgeRect, new TipSignal(() => BuildWeakBadgeTip(), 910200));
            }

            TooltipHandler.TipRegion(rect, new TipSignal(() => BuildFullTooltip(), 910201));

            return new GizmoResult(GizmoState.Clear);
        }

        private Color GetFlashTintedColor(Color baseColor)
        {
            int now = shield.CurrentTickForDisplay;
            int lastPen = shield.LastPenetrateTick;
            if (lastPen >= 0 && now - lastPen <= FlashDurationTicks)
            {
                float t = 1f - (now - lastPen) / (float)FlashDurationTicks;
                return Color.Lerp(baseColor, FlashColor, t * 0.55f);
            }
            int lastHit = shield.LastAbsorbTick;
            if (lastHit >= 0 && now - lastHit <= FlashDurationTicks)
            {
                float t = 1f - (now - lastHit) / (float)FlashDurationTicks;
                return Color.Lerp(baseColor, FlashWhiteColor, t * 0.40f);
            }
            return baseColor;
        }

        private void DrawStage1(Rect inner)
        {
            var labelRect = new Rect(inner.x, inner.y, inner.width, LabelHeight);
            renderer.DrawStageLabel(labelRect, "MX_NL_ShieldStage1".Translate(), props.stageIColor);

            var barRect = new Rect(inner.x, inner.y + 24f, inner.width, BarHeight);
            renderer.DrawShieldBar(barRect, 1f, GetFlashTintedColor(props.stageIColor), shield);
            renderer.DrawCenterText(barRect, "MX_NL_ShieldReady".Translate());

            var hintRect = new Rect(inner.x, inner.y + 46f, inner.width, HintHeight);
            renderer.DrawStatusHint(hintRect, "MX_NL_ShieldStage1Hint".Translate(), HintGrayColor);
        }

        private void DrawStage2(Rect inner)
        {
            var labelRect = new Rect(inner.x, inner.y, inner.width, LabelHeight);
            var statusColor = shield.InWeak ? props.weakColor : props.stageIIColor;
            renderer.DrawStageLabel(labelRect, "MX_NL_ShieldStage2".Translate(), statusColor);

            var barRect = new Rect(inner.x, inner.y + 24f, inner.width, BarHeight);
            var maxCharges = Mathf.Max(1f, (float)shield.Phase2MaxCharges);
            var fillPercent = Mathf.Clamp01(shield.Phase2Charges / maxCharges);
            var barColor = GetFlashTintedColor(shield.InWeak ? WeakBarColor : props.stageIIColor);
            renderer.DrawShieldBar(barRect, fillPercent, barColor, shield);
            renderer.DrawCenterText(barRect, shield.Phase2Charges + " / " + shield.Phase2MaxCharges);

            var hintRect = new Rect(inner.x, inner.y + 46f, inner.width, HintHeight);
            var threshold = shield.Props.phase2Threshold;
            var hintText = "MX_NL_ShieldStage2Hint".Translate(threshold.ToString("F0")) + (shield.InWeak ? "MX_NL_ShieldWeakSuffix".Translate().ToString() : "");
            renderer.DrawStatusHint(hintRect, hintText, shield.InWeak ? props.weakColor : HintGrayColor);
        }

        private void DrawStage3(Rect inner)
        {
            float absorbProgress = shield.Stage3AbsorbProgress;
            float buffProgress = shield.Stage3BuffProgress;
            bool inAbsorb = absorbProgress < 1f && buffProgress <= 0f;
            bool inBuff = absorbProgress >= 1f && buffProgress < 1f;

            if (inAbsorb)
                DrawStage3Absorb(inner);
            else if (inBuff)
                DrawStage3Buff(inner);
            else
                DrawStage3Expired(inner);
        }

        // 各阶颜色——压暗以便白色文字在条上清晰可读
        private static readonly Color TierAColor = new(0.35f, 0.52f, 0.60f, 1f);
        private static readonly Color TierBColor = new(0.45f, 0.38f, 0.55f, 1f);
        private static readonly Color TierCColor = new(0.65f, 0.35f, 0.45f, 1f);
        // D阶起始暖橙→亮金，保证前几层肉眼可辨
        private static readonly Color TierDBaseColor = new(0.70f, 0.40f, 0.20f, 1f);
        private static readonly Color TierDDeepColor = new(0.95f, 0.85f, 0.05f, 1f);

        private void DrawStage3Absorb(Rect inner)
        {
            var labelRect = new Rect(inner.x, inner.y, inner.width, LabelHeight);
            renderer.DrawStageLabel(labelRect, "MX_NL_ShieldStage3Absorb".Translate(), props.stageIIIAbsorbColor);

            var barRect = new Rect(inner.x, inner.y + 24f, inner.width, BarHeight);
            DrawMultiTierAbsorbBar(barRect);

            int remainTicks = shield.Phase3AbsorbUntilTick > 0 ? Mathf.Max(0, shield.Phase3AbsorbUntilTick - shield.CurrentTickForDisplay) : 0;
            string timeText = FormatTicks(remainTicks);
            renderer.DrawCenterText(barRect, "MX_NL_ShieldAbsorbInfo".Translate(shield.Phase3StoredDamage.ToString("F0"), timeText));

            var hintRect = new Rect(inner.x, inner.y + 46f, inner.width, HintHeight);
            var tick = Find.TickManager?.TicksGame ?? 0;
            var pulse = 0.6f + 0.4f * Mathf.Sin(tick / 30f);
            var invincColor = new Color(AbsorbInvincibleColor.r, AbsorbInvincibleColor.g, AbsorbInvincibleColor.b, pulse);
            renderer.DrawStatusHint(hintRect, "MX_NL_ShieldInvincible".Translate(), invincColor);
        }

        private void DrawMultiTierAbsorbBar(Rect barRect)
        {
            float d = shield.Phase3StoredDamage;
            var p = shield.Props;

            float tierAMax = p.stage3TierA_MaxDamage;
            float tierBMax = p.stage3TierB_MaxDamage;
            float tierCMax = p.stage3TierC_MaxDamage;
            float tierDStep = Mathf.Max(1f, p.stage3TierD_ExtraStepDamage);

            int currentTier = 0;
            float currentFill = 0f;

            if (d <= tierAMax) { currentTier = 0; currentFill = Mathf.Clamp01(d / tierAMax); }
            else if (d <= tierBMax) { currentTier = 1; currentFill = Mathf.Clamp01((d - tierAMax) / Mathf.Max(1f, tierBMax - tierAMax)); }
            else if (d <= tierCMax) { currentTier = 2; currentFill = Mathf.Clamp01((d - tierBMax) / Mathf.Max(1f, tierCMax - tierBMax)); }
            else { float aboveC = d - tierCMax; int fullStacks = Mathf.FloorToInt(aboveC / tierDStep); currentTier = 3 + fullStacks; currentFill = Mathf.Clamp01((aboveC - fullStacks * tierDStep) / tierDStep); }

            Color currentColor;
            if (currentTier >= 3) { int dIdx = currentTier - 3; float t = dIdx / Mathf.Max(1f, dIdx + 2f); currentColor = Color.Lerp(TierDBaseColor, TierDDeepColor, t); }
            else if (currentTier == 2) currentColor = TierCColor;
            else if (currentTier == 1) currentColor = TierBColor;
            else currentColor = TierAColor;

            var emptyTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.03f, 0.035f, 0.05f));
            GUI.DrawTexture(barRect, emptyTex);

            // Match FillableBar's 1px border
            var innerBar = barRect.ContractedBy(1f);

            // Completed base tiers A, B, C
            if (currentTier >= 1) Widgets.DrawBoxSolid(innerBar, TierAColor);
            if (currentTier >= 2) Widgets.DrawBoxSolid(innerBar, TierBColor);
            if (currentTier >= 3) Widgets.DrawBoxSolid(innerBar, TierCColor);

            // Completed D tiers (D1, D2, ...) underneath current D tier
            int completedDCount = currentTier - 3;
            for (int i = 0; i < completedDCount; i++)
            {
                float dt = i / Mathf.Max(1f, i + 2f);
                Color dColor = Color.Lerp(TierDBaseColor, TierDDeepColor, dt);
                Widgets.DrawBoxSolid(innerBar, dColor);
            }

            // Current tier partial fill
            var fillRect = new Rect(innerBar.x, innerBar.y, innerBar.width * currentFill, innerBar.height);
            if (fillRect.width > 0.5f)
                Widgets.DrawBoxSolid(fillRect, currentColor);
        }

        private void DrawStage3Buff(Rect inner)
        {
            var labelRect = new Rect(inner.x, inner.y, inner.width, LabelHeight);
            renderer.DrawStageLabel(labelRect, "MX_NL_ShieldStage3Buff".Translate(), props.stageIIIBuffColor);

            var barRect = new Rect(inner.x, inner.y + 24f, inner.width, BarHeight);
            float fillPercent = Mathf.Clamp01(1f - shield.Stage3BuffProgress);
            renderer.DrawShieldBar(barRect, fillPercent, GetFlashTintedColor(props.stageIIIBuffColor), shield);

            int remainTicks = shield.Phase3EndTick > 0 ? Mathf.Max(0, shield.Phase3EndTick - shield.CurrentTickForDisplay) : 0;
            string timeText = FormatTicks(remainTicks);
            renderer.DrawCenterText(barRect, "MX_NL_ShieldBuffInfo".Translate(shield.GetStage3TierLabel(), timeText));

            var hintRect = new Rect(inner.x, inner.y + 46f, inner.width, HintHeight);
            MXNeiyuStage3Profile profile;
            if (shield.TryGetStage3Profile(out profile))
            {
                string summary = BuildBuffSummary(profile);
                renderer.DrawStatusHint(hintRect, summary, BuffSummaryColor);
            }
            else
            {
                renderer.DrawStatusHint(hintRect, "MX_NL_ShieldBuffExpired".Translate(), HintGrayColor);
            }
        }

        private void DrawStage3Expired(Rect inner)
        {
            var labelRect = new Rect(inner.x, inner.y, inner.width, LabelHeight);
            renderer.DrawStageLabel(labelRect, "MX_NL_ShieldStage3".Translate(), props.stageIIIBuffColor);
            var centerRect = new Rect(inner.x, inner.y + 24f, inner.width, 26f);
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(centerRect, "MX_NL_ShieldStage3Expiring".Translate());
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static string BuildBuffSummary(MXNeiyuStage3Profile profile)
        {
            string primary = "";
            string secondary = "";

            float dmgBonus = (profile.outgoingDamageFactor - 1f) * 100f;
            if (Mathf.Abs(dmgBonus) > 0.5f)
                primary = "MX_NL_BuffDamage".Translate(dmgBonus.ToString("F0"));

            float incomingReduce = (1f - profile.incomingDamageFactor) * 100f;
            if (Mathf.Abs(incomingReduce) > 0.5f)
                secondary = "MX_NL_BuffIncoming".Translate(incomingReduce.ToString("F0"));
            else
            {
                float moveBonus = (profile.moveSpeedFactor - 1f) * 100f;
                if (Mathf.Abs(moveBonus) > 0.5f)
                    secondary = "MX_NL_BuffMoveSpeed".Translate(moveBonus.ToString("F0"));
                else
                {
                    float healBonus = (profile.injuryHealingFactor - 1f) * 100f;
                    if (Mathf.Abs(healBonus) > 0.5f)
                        secondary = "MX_NL_BuffHeal".Translate(healBonus.ToString("F0"));
                }
            }

            return primary + secondary;
        }

        private static string FormatTicks(int ticks)
        {
            if (ticks <= 0) return "0s";
            float hours = ticks / 2500f;
            if (hours >= 1f) return hours.ToString("F1") + "h";
            float minutes = ticks / 60f;
            return Mathf.CeilToInt(minutes) + "m";
        }

        private string BuildFullTooltip()
        {
            string tip = "";
            tip += "MX_NL_TooltipStage".Translate(shield.Stage == 1 ? "MX_NL_ShieldStage1Short".Translate().ToString() : shield.Stage == 2 ? "MX_NL_ShieldStage2Short".Translate().ToString() : "MX_NL_ShieldStage3Short".Translate().ToString());
            if (shield.InWeak) tip += "MX_NL_TooltipWeak".Translate();
            tip += "\n";

            if (shield is HediffComp comp)
                tip += comp.CompTipStringExtra;

            return tip;
        }

        private string BuildWeakBadgeTip()
        {
            int remain = Mathf.Max(0, shield.WeakUntilTick - shield.CurrentTickForDisplay);
            return "MX_NL_WeakBadgeTip".Translate(FormatTicks(remain));
        }
    }
}

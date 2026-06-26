using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.UI;
using RimWorld;
using UnityEngine;
using Verse;
using Widgets = Verse.Widgets;

namespace MiliraXian.Characters.QingHe.UI.WidgetControls
{
    public class Widget_LongBreathChargeBlocks : Widget_Base
    {
        private const int TipSalt = 910205;
        private const int MaxDisplayBlocks = 2;
        private const float BlockGap = 3f;

        private readonly Pawn pawn;

        private static readonly Color BorderColor = new Color(0.42f, 0.44f, 0.44f, 1f);
        private static readonly Color EmptyColor = new Color(0.035f, 0.04f, 0.045f, 1f);
        private static readonly Color ChargedColor = new Color(0.45f, 0.90f, 0.42f, 1f);
        private static readonly Color RechargingColor = new Color(1f, 0.82f, 0.24f, 1f);

        public Widget_LongBreathChargeBlocks(Pawn pawn, Rect localRect, TextAnchor alignment)
            : base(localRect, alignment)
        {
            this.pawn = pawn;
        }

        protected override void DrawContents(Rect rect)
        {
            HediffComp_DivineBlessing comp = GetLongBreathComp();
            int maxCharges = Mathf.Clamp(comp?.MaxCharges ?? 1, 1, MaxDisplayBlocks);
            int currentCharges = Mathf.Clamp(comp?.CurrentCharges ?? maxCharges, 0, maxCharges);
            float rechargeProgress = comp?.RechargeProgressPercent ?? 0f;
            float blockSize = Mathf.Max(0f, rect.height);

            Rect blocksRect = GetAlignedRect(rect, new Vector2(maxCharges * blockSize + (maxCharges - 1) * BlockGap, blockSize), null);
            for (int i = 0; i < maxCharges; i++)
            {
                Rect blockRect = new Rect(blocksRect.x + i * (blockSize + BlockGap), blocksRect.y, blockSize, blockSize);
                DrawBlock(blockRect, ResolveFillPercent(i, currentCharges, maxCharges, rechargeProgress), ResolveFillColor(i, currentCharges));
            }

            TooltipHandler.TipRegion(rect, () => BuildTip(comp), GetStableTipId());
        }

        private static void DrawBlock(Rect blockRect, float fillPercent, Color fillColor)
        {
            Widgets.DrawBoxSolid(blockRect, BorderColor);
            Rect innerRect = blockRect.ContractedBy(1f);
            Widgets.DrawBoxSolid(innerRect, EmptyColor);

            fillPercent = Mathf.Clamp01(fillPercent);
            if (fillPercent <= 0.0001f)
            {
                return;
            }

            Rect fillRect = new Rect(innerRect.x, innerRect.y, innerRect.width * fillPercent, innerRect.height);
            Widgets.DrawBoxSolid(fillRect, fillColor);
        }

        private static float ResolveFillPercent(int index, int currentCharges, int maxCharges, float rechargeProgress)
        {
            if (index < currentCharges)
            {
                return 1f;
            }

            if (index == currentCharges && currentCharges < maxCharges)
            {
                return rechargeProgress;
            }

            return 0f;
        }

        private static Color ResolveFillColor(int index, int currentCharges)
        {
            return index < currentCharges ? ChargedColor : RechargingColor;
        }

        private HediffComp_DivineBlessing GetLongBreathComp()
        {
            Hediff hediff = pawn?.health?.hediffSet?.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_DivineBlessing);
            return (hediff as HediffWithComps)?.GetComp<HediffComp_DivineBlessing>();
        }

        private static string BuildTip(HediffComp_DivineBlessing comp)
        {
            if (comp == null)
            {
                return "长息充能: 0 / 1";
            }

            string tip = "长息充能: " + comp.CurrentCharges.ToString() + " / " + comp.MaxCharges.ToString();
            if (comp.IsRecharging)
            {
                tip += "\n状态: 充能中";
                tip += "\n恢复进度: " + comp.RechargeProgressPercent.ToStringPercent();
                tip += "\n剩余时间: " + comp.RechargeTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
            }
            else if (comp.CurrentCharges > 0)
            {
                tip += "\n状态: 已就绪";
            }
            else
            {
                tip += "\n状态: 充能耗尽";
            }

            return tip;
        }

        private int GetStableTipId()
        {
            int pawnId = pawn?.thingIDNumber ?? 0;
            return Gen.HashCombineInt(pawnId, TipSalt);
        }
    }
}

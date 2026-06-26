using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Widgets = Verse.Widgets;

namespace MiliraXian.Characters.QingHe.UI
{
    public class Gizmo_QH_FlowerDivination : Gizmo
    {
        private const float Width = 170f;
        private const float GizmoHeight = 75f;
        private const float Padding = 6f;
        private const float IconSize = 44f;
        private const int TipSalt = 910210;

        private readonly Pawn pawn;

        private static readonly Texture2D IconTex = ContentFinder<Texture2D>.Get("MiliraXianQinghe/UI/MX_QH_FlowerDivination_Diamond", reportFailure: false)
            ?? ContentFinder<Texture2D>.Get("MiliraXianNeiyu/UI/MX_Neiyu_ThunderMarkedStorm", reportFailure: false);
        private static readonly Color FillReadyColor = new Color(0.95f, 0.74f, 0.42f, 1f);
        private static readonly Color FillActiveColor = new Color(1f, 0.42f, 0.72f, 1f);
        private static readonly Color FillCooldownColor = new Color(0.36f, 0.48f, 0.62f, 1f);
        private static readonly Color EmptyColor = new Color(0.10f, 0.10f, 0.12f, 0.86f);

        public Gizmo_QH_FlowerDivination(Pawn pawn)
        {
            this.pawn = pawn;
            Order = -98.9f;
        }

        public override float GetWidth(float maxWidth)
        {
            return Width;
        }

        public override GizmoResult GizmoOnGUI(Vector2 topLeft, float maxWidth, GizmoRenderParms parms)
        {
            Rect rect = new Rect(topLeft.x, topLeft.y, Width, GizmoHeight);
            Widgets.DrawWindowBackground(rect);

            Rect inner = rect.ContractedBy(Padding);
            Rect iconRect = new Rect(inner.x, inner.y + (inner.height - IconSize) * 0.5f, IconSize, IconSize);
            Rect labelRect = new Rect(iconRect.xMax + 8f, inner.y, inner.width - IconSize - 8f, 20f);
            Rect barRect = new Rect(labelRect.x, inner.y + 25f, labelRect.width, 18f);
            Rect timeRect = new Rect(labelRect.x, inner.y + 46f, labelRect.width, 14f);

            HediffComp_FlowerResonance state = FlowerCourtUtility.EnsureSkillTreeState(pawn);
            HediffComp_FlowerDivination divination = FlowerCourtUtility.EnsureFlowerDivination(pawn);
            bool learned = state?.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_FlowerDance) == true;
            bool canStart = learned && divination != null && divination.CanStartDivination(out _);

            DrawIconButton(iconRect, canStart);
            DrawLabel(labelRect, divination, learned);
            DrawProgressBar(barRect, divination, learned);
            DrawTimeText(timeRect, divination, learned);

            if (Mouse.IsOver(rect))
            {
                TooltipHandler.TipRegion(rect, () => BuildTip(divination, learned), Gen.HashCombineInt(pawn?.thingIDNumber ?? 0, TipSalt));
            }

            if (canStart && Widgets.ButtonInvisible(iconRect))
            {
                TryStartDivination(divination);
            }

            return new GizmoResult(GizmoState.Clear);
        }

        private static void DrawIconButton(Rect rect, bool canStart)
        {
            Widgets.DrawBoxSolid(rect, EmptyColor);
            Widgets.DrawBox(rect);

            Color oldColor = GUI.color;
            GUI.color = canStart && Mouse.IsOver(rect) ? GenUI.MouseoverColor : Color.white;
            if (IconTex != null)
            {
                GUI.DrawTexture(rect.ContractedBy(4f), IconTex, ScaleMode.ScaleToFit, true);
            }
            GUI.color = oldColor;
        }

        private static void DrawLabel(Rect rect, HediffComp_FlowerDivination divination, bool learned)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect, learned ? divination?.Label ?? "花神降临" : "花神降临");
            Text.Anchor = oldAnchor;
            Text.Font = oldFont;
        }

        private static void DrawProgressBar(Rect rect, HediffComp_FlowerDivination divination, bool learned)
        {
            Widgets.DrawBoxSolid(rect, EmptyColor);
            float fillPercent = ResolveFillPercent(divination, learned);
            if (fillPercent > 0f)
            {
                Rect fillRect = new Rect(rect.x, rect.y, rect.width * fillPercent, rect.height);
                Widgets.DrawBoxSolid(fillRect, ResolveFillColor(divination, learned));
            }
            Widgets.DrawBox(rect);
        }

        private static float ResolveFillPercent(HediffComp_FlowerDivination divination, bool learned)
        {
            if (!learned || divination == null)
            {
                return 0f;
            }

            if (divination.Active)
            {
                return divination.ActiveRemainingPercent;
            }

            if (divination.OnCooldown)
            {
                return divination.CooldownReadyPercent;
            }

            return divination.Ready ? 1f : 0f;
        }

        private static Color ResolveFillColor(HediffComp_FlowerDivination divination, bool learned)
        {
            if (!learned || divination == null)
            {
                return FillCooldownColor;
            }

            if (divination.Active)
            {
                return FillActiveColor;
            }

            if (divination.OnCooldown)
            {
                return FillCooldownColor;
            }

            return FillReadyColor;
        }

        private static void DrawTimeText(Rect rect, HediffComp_FlowerDivination divination, bool learned)
        {
            GameFont oldFont = Text.Font;
            TextAnchor oldAnchor = Text.Anchor;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(rect, BuildStateText(divination, learned));
            Text.Anchor = oldAnchor;
            Text.Font = oldFont;
        }

        private static string BuildStateText(HediffComp_FlowerDivination divination, bool learned)
        {
            if (!learned)
            {
                return "未习得";
            }

            if (divination == null)
            {
                return "未准备";
            }

            if (divination.Active)
            {
                return "持续 " + divination.ActiveTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
            }

            if (divination.OnCooldown)
            {
                return "充能 " + divination.CooldownTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
            }

            return "可启动";
        }

        private void TryStartDivination(HediffComp_FlowerDivination divination)
        {
            if (divination == null)
            {
                Messages.Message("清荷尚未建立花神庭。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (!divination.CanStartDivination(out string reason))
            {
                Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (MX_QHDefOf.MX_QH_FlowerDivination == null || pawn?.jobs == null)
            {
                Messages.Message("花神降临尚未准备好。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            Job job = JobMaker.MakeJob(MX_QHDefOf.MX_QH_FlowerDivination);
            pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }

        private static string BuildTip(HediffComp_FlowerDivination divination, bool learned)
        {
            if (!learned)
            {
                return "花神降临\n\n尚未习得花之舞。";
            }

            if (divination == null)
            {
                return "花神降临\n\n清荷尚未建立花神庭。";
            }

            string tip = divination.Label;
            if (divination.Active)
            {
                return tip + "\n\n当前状态: 降临中\n剩余时间: " + divination.ActiveTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
            }

            if (divination.OnCooldown)
            {
                return tip + "\n\n当前状态: 充能中\n剩余时间: " + divination.CooldownTicksLeft.ToStringTicksToPeriod(true, false, true, true, false);
            }

            if (divination.CanStartDivination(out string reason))
            {
                return tip + "\n\n当前状态: 可启动";
            }

            return tip + "\n\n" + reason;
        }
    }
}

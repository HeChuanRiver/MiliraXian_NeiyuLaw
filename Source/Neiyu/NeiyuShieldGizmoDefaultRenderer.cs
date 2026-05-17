using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu
{
    public class NeiyuShieldGizmoDefaultRenderer : INeiyuShieldGizmoRenderer
    {
        private static readonly Texture2D EmptyBarTex = SolidColorMaterials.NewSolidColorTexture(new Color(0.03f, 0.035f, 0.05f));
        private static readonly Dictionary<Color32, Texture2D> FillBarTexCache = new Dictionary<Color32, Texture2D>();

        public void DrawBackground(Rect rect, HediffComp_MXNeiyuCountShield shield)
        {
            Widgets.DrawWindowBackground(rect);
        }

        public void DrawShieldBar(Rect barRect, float fillPercent, Color barColor, HediffComp_MXNeiyuCountShield shield)
        {
            Widgets.FillableBar(barRect, Mathf.Clamp01(fillPercent), GetFillBarTexture(barColor), EmptyBarTex, true);
        }

        public void DrawStageLabel(Rect labelRect, string label, Color stageColor)
        {
            var prev = GUI.color;
            GUI.color = stageColor;
            Text.Font = GameFont.Small;
            Widgets.Label(labelRect, label);
            GUI.color = prev;
        }

        public void DrawCenterText(Rect textRect, string text)
        {
            Text.Font = GameFont.Small;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(textRect, text);
            Text.Anchor = TextAnchor.UpperLeft;
        }

        public void DrawStatusHint(Rect hintRect, string text, Color color)
        {
            var prev = GUI.color;
            GUI.color = color;
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            Widgets.Label(hintRect, text);
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = prev;
        }

        public void DrawWeakBadge(Rect badgeRect, HediffComp_MXNeiyuCountShield shield)
        {
            var tick = Find.TickManager?.TicksGame ?? 0;
            var pulse = 0.5f + 0.5f * Mathf.Sin(tick / 20f);
            var bgColor = new Color(0.55f, 0.05f, 0.05f, 0.75f + pulse * 0.25f);
            Widgets.DrawBoxSolid(badgeRect, bgColor);
            Text.Font = GameFont.Tiny;
            Text.Anchor = TextAnchor.MiddleCenter;
            var prev = GUI.color;
            GUI.color = new Color(1f, 0.6f, 0.6f, 0.9f + pulse * 0.1f);
            Widgets.Label(badgeRect, "MX_NL_WeakBadge".Translate());
            GUI.color = prev;
            Text.Anchor = TextAnchor.UpperLeft;
        }

        private static Texture2D GetFillBarTexture(Color color)
        {
            var key = (Color32)color;
            if (!FillBarTexCache.TryGetValue(key, out var tex))
            {
                tex = SolidColorMaterials.NewSolidColorTexture(color);
                FillBarTexCache[key] = tex;
            }
            return tex;
        }
    }
}

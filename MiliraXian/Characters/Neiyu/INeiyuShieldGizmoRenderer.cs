using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu
{
    /// <summary>
    /// 护盾 Gizmo 渲染器接口 —— 预留自定义材质替换入口。
    /// 默认实现 NeiyuShieldGizmoDefaultRenderer 使用内置 IMGUI；
    /// 后续可替换为自定义材质实现。
    /// </summary>
    public interface INeiyuShieldGizmoRenderer
    {
        void DrawBackground(Rect rect, HediffComp_MXNeiyuCountShield shield);
        void DrawShieldBar(Rect barRect, float fillPercent, Color barColor, HediffComp_MXNeiyuCountShield shield);
        void DrawStageLabel(Rect labelRect, string label, Color stageColor);
        void DrawCenterText(Rect textRect, string text);
        void DrawStatusHint(Rect hintRect, string text, Color color);
        void DrawWeakBadge(Rect badgeRect, HediffComp_MXNeiyuCountShield shield);
    }
}

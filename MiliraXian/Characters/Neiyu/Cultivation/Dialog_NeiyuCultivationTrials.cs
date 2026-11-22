using System;
using MiliraXian.Characters.Common.UI;
using UnityEngine;
using Verse;
using static MiliraXian.Characters.Neiyu.Cultivation.CultivationArt;

namespace MiliraXian.Characters.Neiyu.Cultivation;

internal sealed class Dialog_NeiyuCultivationTrials : Window
{
    private readonly Hediff_NeiyuCultivation state;
    private Vector2 scroll;
    private float height = 640;
    public override Vector2 InitialSize => new(Mathf.Min(620, UI.screenWidth - 48), Mathf.Min(780, UI.screenHeight - 48));
    protected override float Margin => 0;
    internal Dialog_NeiyuCultivationTrials(Hediff_NeiyuCultivation state)
    { this.state = state; forcePause = true; absorbInputAroundWindow = true; doWindowBackground = false; closeOnAccept = false; doCloseX = false; }
    public override void DoWindowContents(Rect rect)
    {
        GUI.DrawTexture(rect, TreeBackdrop, ScaleMode.ScaleAndCrop);
        Widgets.DrawBoxSolid(rect, new Color(.04f, .08f, .14f, .35f));
        Theme.Frame(rect, Color.white, 28, drawCenter: false);
        Theme.Label(new Rect(25, 22, rect.width - 80, 36), "MX_Cultivation_TrialLog".Translate().ToString(), Ink, GameFont.Medium);
        if (Theme.Button(new Rect(rect.width - 49, 22, 25, 28), "×", quiet: true)) Close();
        Rect area = new(26, 80, rect.width - 52, rect.height - 106);
        Rect content = new(0, 0, area.width - 16, Mathf.Max(area.height, height));
        Widgets.BeginScrollView(area, ref scroll, content);
        try
        {
            float y = 0;
            foreach (CultivationTrial trial in Enum.GetValues<CultivationTrial>())
            {
                if (trial == CultivationTrial.None) continue;
                int target = 0;
                foreach (var node in CultivationService.ExtensionNodes.Values)
                    if (node.trial == trial) target = Math.Max(target, node.trialTarget);
                int value = state.Progress(trial);
                Theme.Label(new Rect(0, y, content.width - 100, 26), CultivationTrials.Label(trial), Ink);
                Theme.Label(new Rect(content.width - 100, y, 100, 26), value + " / " + target, Accent, GameFont.Tiny, TextAnchor.MiddleRight);
                if (value >= target) Theme.Symbol(new Rect(content.width - 16, y + 31, 14, 14), PerkNodeVisual.Mastered, Pink);
                Widgets.DrawBoxSolid(new Rect(0, y + 40, content.width - 28, 2), Rule);
                Widgets.DrawBoxSolid(new Rect(0, y + 40, (content.width - 28) * Mathf.Clamp01((float)value / Math.Max(1, target)), 2), Accent);
                y += 73;
            }
            Theme.TextBlock(content.width, ref y, "MX_Cultivation_TrialCombatRules".Translate().ToString(), Muted, GameFont.Tiny);
            Theme.TextBlock(content.width, ref y, "MX_Cultivation_TrialCountingRules".Translate().ToString(), Muted, GameFont.Tiny);
            height = y + 10;
        }
        finally { Widgets.EndScrollView(); }
    }
}

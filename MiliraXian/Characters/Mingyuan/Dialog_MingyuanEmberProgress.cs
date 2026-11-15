using System;
using UnityEngine;
using Verse;
using static MiliraXian.Characters.Mingyuan.MingyuanEmberArt;

namespace MiliraXian.Characters.Mingyuan;

/// <summary>Secondary progress stays outside the node's reading and purchase card.</summary>
internal sealed class Dialog_MingyuanEmberProgress : Window
{
    private readonly MingyuanEmberBook book;
    private Vector2 scroll;
    private float contentHeight = 680;
    public override Vector2 InitialSize => new(Mathf.Min(520, UI.screenWidth - 48), Mathf.Min(740, UI.screenHeight - 48));
    protected override float Margin => 0;

    public Dialog_MingyuanEmberProgress(MingyuanEmberBook book)
    {
        this.book = book;
        doWindowBackground = false;
    }

    public override void DoWindowContents(Rect rect)
    {
        Color previous = GUI.color;
        GUI.color = Color.white;
        GUI.DrawTexture(rect, Backdrop, ScaleMode.ScaleAndCrop);
        GUI.color = previous;
        Frame(rect, Color.white, 28, drawCenter: false);
        rect = rect.ContractedBy(20);
        Label(new Rect(rect.x, rect.y, rect.width - 42, 35), T("Trials", "试炼与资源"), Ink, GameFont.Medium);
        if (Button(new Rect(rect.xMax - 34, rect.y, 32, 32), "×", quiet: true)) Close();
        Widgets.DrawBoxSolid(new Rect(rect.x, rect.y + 42, rect.width, 1), Rule);
        Rect area = new(rect.x, rect.y + 56, rect.width, rect.height - 56);
        Rect view = new(0, 0, area.width - 20, contentHeight);
        Widgets.BeginScrollView(area, ref scroll, view);
        try
        {
            float y = 0;
            var c = MingyuanEmberBook.Config;
            TextBlock(view.width, ref y, T("Experience", "累计战斗经历") + "  " + book.Experience, Gold);
            foreach (EmberTrial trial in Enum.GetValues<EmberTrial>())
            {
                if (trial == EmberTrial.None) continue;
                y += 12;
                TextBlock(view.width, ref y, book.TrialDescription(trial), Ink);
                if (book.TrialComplete(trial)) Symbol(new Rect(view.width - 16, y + 4, 14, 14), EmberNodeVisual.Mastered, Gold);
                TextBlock(view.width - 22, ref y, book.TrialProgress(trial).ToString("0") + " / " + book.TrialTarget(trial).ToString("0"), Gold, GameFont.Tiny);
                Widgets.DrawBoxSolid(new Rect(0, y, view.width, 2), Rule);
                Widgets.DrawBoxSolid(new Rect(0, y, view.width * Mathf.Clamp01(book.TrialProgress(trial) / Mathf.Max(1, book.TrialTarget(trial))), 2), Gold);
                y += 8;
            }
            y += 22;
            TextBlock(view.width, ref y, T("ResourceRules", "实战施法每 {0} 次、生命燃烧实际伤害每 {1} 点、护焰战斗修复每 {2} 点，各获得 1 灰烬。\n每 {3} 点战斗经历获得 1 焰识。完成每项试炼额外获得 {4} 焰识、{5} 灰烬。")
                .Formatted(c.castsPerAsh, c.burnDamagePerAsh, c.shieldRepairPerAsh, c.experiencePerInsight, c.trialInsightReward, c.trialAshReward), Muted, GameFont.Tiny);
            TextBlock(view.width, ref y, T("ExperienceRules", "每次计入奖励的施法、每次由伤害或修复获得灰烬，以及每名带有生命燃烧的敌人死亡，均增加 1 点战斗经历。"), Muted, GameFont.Tiny);
            TextBlock(view.width, ref y, T("CastRewardInterval", "施法奖励每 {0} 秒最多计入一次，试炼次数照常累计。")
                .Formatted((c.resourceEventCooldownTicks / 60f).ToString("0.#")), Muted, GameFont.Tiny);
            contentHeight = y + 8;
        }
        finally { Widgets.EndScrollView(); }
    }
}

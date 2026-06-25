using UnityEngine;
using Verse;
using Widgets = Verse.Widgets;

namespace MiliraXian.Characters.QingHe.UI
{
    public class Dialog_QH_FlowerDecreeHelp : Window
    {
        private const float WindowWidth = 520f;
        private const float WindowHeight = 430f;

        private static readonly Color HeaderColor = new Color(0.72f, 0.86f, 0.76f, 1f);
        private static readonly Color BodyColor = new Color(0.86f, 0.88f, 0.86f, 1f);

        public override Vector2 InitialSize => new Vector2(WindowWidth, WindowHeight);

        public Dialog_QH_FlowerDecreeHelp()
        {
            forcePause = true;
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseButton = true;
        }

        public override void DoWindowContents(Rect inRect)
        {
            Text.Font = GameFont.Medium;
            Text.Anchor = TextAnchor.UpperLeft;
            GUI.color = HeaderColor;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 32f), "清荷面板说明");

            GUI.color = BodyColor;
            Text.Font = GameFont.Small;
            float y = inRect.y + 48f;
            DrawItem(inRect, ref y, "花令", "用于释放飞花令，分段显示当前花令数量和恢复进度。");
            DrawItem(inRect, ref y, "护盾", "显示花神护体当前护盾百分比。");
            DrawItem(inRect, ref y, "长息", "绿色方块表示可用充能，黄色填充表示正在恢复。");
            DrawItem(inRect, ref y, "花信铃强化", "习得清角后可切换强化状态。");
            DrawItem(inRect, ref y, "飞花令·寄时", "习得四时流转后可选择一个不同于主飞花令的额外飞花令。");
            DrawItem(inRect, ref y, "花神降临", "显示降临持续时间或冷却进度，可用时点击启动。");
            DrawItem(inRect, ref y, "花神庭", "显示技能树经验进度；中心数字为可用技能点，点击打开花神庭面板。");

            GUI.color = Color.white;
        }

        private static void DrawItem(Rect inRect, ref float y, string title, string body)
        {
            Text.Font = GameFont.Small;
            GUI.color = HeaderColor;
            Widgets.Label(new Rect(inRect.x + 8f, y, 112f, 24f), title);

            GUI.color = BodyColor;
            Widgets.Label(new Rect(inRect.x + 122f, y, inRect.width - 132f, 42f), body);
            y += 46f;
        }
    }
}

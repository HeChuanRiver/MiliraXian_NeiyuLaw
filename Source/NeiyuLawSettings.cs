using UnityEngine;
using Verse;

namespace MiliraXian.NeiyuLaw
{
    public class NeiyuLawSettings : ModSettings
    {
        public bool EnableAriandelSpecialPawnIntegration = true;

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref EnableAriandelSpecialPawnIntegration, "EnableAriandelSpecialPawnIntegration", true);
        }
    }

    public class NeiyuLawMod : Mod
    {
        public static NeiyuLawMod Instance { get; private set; }

        public NeiyuLawSettings Settings;

        public NeiyuLawMod(ModContentPack content)
            : base(content)
        {
            Settings = GetSettings<NeiyuLawSettings>();
            Instance = this;
        }

        public override string SettingsCategory()
        {
            return "米莉拉角色拓展";
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                "启用霓羽接入特殊角色管理器",
                ref Settings.EnableAriandelSpecialPawnIntegration,
                "启用后，通过霓羽自己的剧本、任务或其他玩家派系生成路径加入殖民地的霓羽，会被注册进 Ariandel 的特殊角色管理器。");
            listing.End();
        }
    }
}
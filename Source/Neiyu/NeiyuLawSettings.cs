using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu
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
                "启用角色接入特殊角色管理器",
                ref Settings.EnableAriandelSpecialPawnIntegration,
                "启用后，霓羽、昭离、清荷在加入玩家派系或读档巡检时，会被注册进 Ariandel 的特殊角色管理器，避免因未注册而被唯一角色限制器清理。");
            listing.End();
        }
    }
}

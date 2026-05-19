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
            return "MX_NL_ModSettingsTitle".Translate().ToString();
        }

        public override void DoSettingsWindowContents(Rect inRect)
        {
            Listing_Standard listing = new Listing_Standard();
            listing.Begin(inRect);
            listing.CheckboxLabeled(
                "MX_NL_EnableSpecialPawnIntegrationLabel".Translate().ToString(),
                ref Settings.EnableAriandelSpecialPawnIntegration,
                "MX_NL_EnableSpecialPawnIntegrationDesc".Translate().ToString());
            listing.End();
        }
    }
}

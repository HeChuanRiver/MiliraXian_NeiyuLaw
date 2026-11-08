using System.Text;
using MiliraXian.Characters.Common.PerkSystem;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu.Cultivation
{
    public sealed class CompProperties_NeiyuCultivatedEquipment : CompProperties
    {
        public CompProperties_NeiyuCultivatedEquipment() => compClass = typeof(CompNeiyuCultivatedEquipment);
    }

    /// <summary>Per-instance projection using the game's public stat hooks. Nothing here is saved as progress.</summary>
    public sealed class CompNeiyuCultivatedEquipment : ThingComp, IPerkEventListener
    {
        private CultivationEquipmentBonus bonus;
        private bool initialized;
        internal CultivationEquipmentBonus Bonus
        {
            get
            {
                // A loaded item's first read is a cold initialization after all pawn references resolve.
                if (!initialized) Refresh(CultivationPower.Owner(parent));
                return NeiyuPowerBalance.PassivesDisabled ? null : bonus;
            }
        }
        public override float GetStatOffset(StatDef stat) => Bonus?.Stats.TryGetValue(stat, out float value) == true ? value : 0f;
        public override void GetStatsExplanation(StatDef stat, StringBuilder builder, string numberFormat)
        {
            float value = GetStatOffset(stat);
            if (Mathf.Abs(value) > 0.00001f)
                builder.AppendLine("修行: " + value.ToStringByStyle(stat.ToStringStyleUnfinalized, ToStringNumberSense.Offset));
        }
        public override void PostExposeData()
        {
            base.PostExposeData();
            if (Scribe.mode == LoadSaveMode.PostLoadInit) initialized = false;
        }
        public override void Notify_Equipped(Pawn pawn)
        {
            base.Notify_Equipped(pawn);
            Refresh(pawn);
            CultivationService.Refresh(pawn);
        }
        public override void Notify_Unequipped(Pawn pawn)
        {
            base.Notify_Unequipped(pawn);
            if (bonus != null)
                foreach (var stat in bonus.Equipped.Keys) stat.Worker.ClearCacheForThing(pawn);
            Refresh(null);
            CultivationService.Refresh(pawn);
        }
        public void Notify_PerkTreeChanged(Pawn pawn, HediffComp_CharacterPerkTree tree) => Refresh(pawn);
        private void Refresh(Pawn pawn)
        {
            var previous = bonus;
            initialized = true;
            bonus = NeiyuEquipmentUtility.IsNeiyu(pawn) ? CultivationService.State(pawn)?.EquipmentBonus(parent.def) : null;
            if (previous != null)
            {
                foreach (var stat in previous.Stats.Keys) stat.Worker.ClearCacheForThing(parent);
                if (pawn != null) foreach (var stat in previous.Equipped.Keys) stat.Worker.ClearCacheForThing(pawn);
            }
            if (bonus != null)
            {
                foreach (var stat in bonus.Stats.Keys) stat.Worker.ClearCacheForThing(parent);
                if (pawn != null) foreach (var stat in bonus.Equipped.Keys) stat.Worker.ClearCacheForThing(pawn);
            }
            foreach (string name in DerivedStats)
                DefDatabase<StatDef>.GetNamedSilentFail(name)?.Worker.ClearCacheForThing(parent);
        }
        private static readonly string[] DerivedStats = { "MeleeWeapon_AverageDPS", "MeleeWeapon_AverageArmorPenetration", "RangedWeapon_Damage", "RangedWeapon_ArmorPenetration" };
    }

    internal static class CultivationPower
    {
        public static Pawn Owner(Thing thing) => thing is Apparel apparel ? apparel.Wearer : MXNeiyuShieldUtility.TryGetEquipmentOwnerPawn(thing);
        private static CompNeiyuCultivatedEquipment Weapon(Pawn pawn) => pawn?.equipment?.Primary?.TryGetComp<CompNeiyuCultivatedEquipment>();
        public static float MeleeFactor(Tool tool, Pawn pawn, bool penetration, Thing equipment = null)
        {
            var comp = equipment?.TryGetComp<CompNeiyuCultivatedEquipment>() ?? Weapon(pawn);
            if (tool == null || comp?.parent.def.tools?.Contains(tool) != true || comp.Bonus is not { } bonus) return 1f;
            float baseline = penetration ? tool.armorPenetration : tool.power;
            return baseline <= 0f ? 1f : (baseline + (penetration ? bonus.MeleePenetration : bonus.MeleeDamage)) / baseline;
        }
        public static int SplitCount(Pawn pawn, int baseline, Thing equipment = null) => Mathf.Max(1, baseline
            + ((equipment?.TryGetComp<CompNeiyuCultivatedEquipment>() ?? Weapon(pawn))?.Bonus?.SplitCount ?? 0));
        public static float ProjectileDamageFactor(Thing equipment, float baseline) => baseline <= 0f ? 1f
            : 1f + (equipment?.TryGetComp<CompNeiyuCultivatedEquipment>()?.Bonus?.ProjectileDamage ?? 0f) / baseline;
        public static float ProjectilePenetrationBonus(Thing equipment) =>
            equipment?.TryGetComp<CompNeiyuCultivatedEquipment>()?.Bonus?.ProjectilePenetration ?? 0f;
        public static bool ShieldEnabled(Pawn pawn) => NeiyuEquipmentUtility.IsNeiyu(pawn) && !NeiyuPowerBalance.PassivesDisabled;
        public static bool StageThreeEnabled(Pawn pawn) => ShieldEnabled(pawn);
        public static int ShieldCapacity(Pawn pawn, int baseline) => NeiyuPowerBalance.PassivesDisabled ? 0
            : Mathf.Max(0, baseline + (CultivationService.State(pawn)?.ShieldBonus ?? 0));
    }
}

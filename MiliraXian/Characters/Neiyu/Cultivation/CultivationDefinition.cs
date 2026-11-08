using System.Collections.Generic;
using MiliraXian.Characters.Common.PerkSystem;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Neiyu.Cultivation
{
    public sealed class NeiyuCultivationNodeDef : CharacterPerkNodeDef
    {
        public int rank = 1;
        public AbilityDef ability;
        public bool initiallyLearned;
        public int shieldCharges;
        public List<CultivationEquipmentEffect> equipmentEffects;
        public CultivationAbilityValues abilityValues;
        [MustTranslate] public string story;

        public override int EffectiveLevel(Pawn pawn, int storedLevel) =>
            NeiyuPowerBalance.PassivesDisabled ? 0 : storedLevel;
        public override string PurchaseBlockedReason(Pawn pawn) => CultivationService.Eligibility(pawn);

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (category == null) yield return defName + ": missing category.";
            if (rank < 1 || maxLevel != 1) yield return defName + ": cultivation tiers must be single-level nodes.";
            if (rank > 1 && (prerequisites == null || prerequisites.Count != 1
                || prerequisites[0].node is not NeiyuCultivationNodeDef previous
                || previous.category != category || previous.ability != ability || previous.rank != rank - 1))
                yield return defName + ": prerequisite must be the preceding tier of the same cultivation.";
            if (initiallyLearned && (rank != 1 || !costs.NullOrEmpty()))
                yield return defName + ": initial skills must be free first tiers.";
            if (ability != null && abilityValues == null) yield return defName + ": missing ability values.";
            if (costs != null)
            {
                HashSet<ThingDef> seen = new();
                foreach (var cost in costs)
                    if (cost?.thingDef == null || cost.count <= 0 || !seen.Add(cost.thingDef))
                        yield return defName + ": invalid or duplicate material cost.";
            }
            if (requiresAll != null)
                foreach (var condition in requiresAll)
                    if (condition != null)
                        foreach (string error in condition.ConfigErrors(null, null, defName)) yield return error;
            if (equipmentEffects != null)
                foreach (var effect in equipmentEffects)
                    if (effect?.equipment == null) yield return defName + ": missing equipment effect target.";
        }
    }

    public sealed class CultivationEquipmentEffect
    {
        public ThingDef equipment;
        public List<StatModifier> statOffsets;
        public List<StatModifier> equippedStatOffsets;
        public float meleeDamage;
        public float meleePenetration;
        public float projectileDamage;
        public float projectilePenetration;
        public int splitCount;
    }

    /// <summary>Authored absolute values of a skill tier; never mutated for a pawn.</summary>
    public sealed class CultivationAbilityValues
    {
        public float radius = -1f;
        public float range = -1f;
        public int damage = -1;
        public int durationTicks = -1;
        public int shotCount = -1;
        public int strikeBudget = -1;
        public int cooldownTicks = -1;
    }

    [DefOf]
    public static class CultivationDefOf
    {
        public static HediffDef MX_Neiyu_Cultivation;
        public static CharacterPerkCategoryDef MX_Neiyu_Cultivation_Weapons;
        public static CharacterPerkCategoryDef MX_Neiyu_Cultivation_Apparel;
        public static CharacterPerkCategoryDef MX_Neiyu_Cultivation_Halo;
        static CultivationDefOf() => DefOfHelper.EnsureInitializedInCtor(typeof(CultivationDefOf));
    }

    internal static class CultivationText
    {
        public static string Get(string id, string source)
        {
            string key = "MX_Cultivation_" + id;
            return key.CanTranslate() ? key.Translate().ToString() : source;
        }
        public static string Title => Get("Title", "修行");
    }
}

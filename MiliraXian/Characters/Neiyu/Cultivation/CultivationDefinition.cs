using System.Collections.Generic;
using MiliraXian.Characters.Common.PerkSystem;
using RimWorld;
using Verse;
using UnityEngine;

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
        public CultivationEffect effect;
        public Vector2 position;
        public CultivationTrial trial;
        public int trialTarget;
        public float value, secondary;
        public int durationTicks, cooldownTicks;
        [MustTranslate] public List<string> levelDescriptions;
        private List<List<ThingDefCountClass>> levelCosts;

        public bool IsExtension => effect != CultivationEffect.None;
        public string EffectText(int level) => levelDescriptions.NullOrEmpty() ? description
            : levelDescriptions[Mathf.Clamp(level - 1, 0, levelDescriptions.Count - 1)];
        public List<ThingDefCountClass> CostsAt(int level)
        {
            if (!IsExtension || costs.NullOrEmpty()) return costs;
            if (levelCosts == null)
            {
                levelCosts = new();
                for (int i = 0; i < maxLevel; i++)
                {
                    List<ThingDefCountClass> list = new();
                    foreach (var cost in costs)
                        list.Add(new(cost.thingDef, (int)System.Math.Floor(cost.count
                            * (1d + (cost.thingDef.defName == "Milira_Feather" ? .65d : .6d) * i) + .5d)));
                    levelCosts.Add(list);
                }
            }
            return levelCosts[Mathf.Clamp(level, 0, maxLevel - 1)];
        }
        public override bool HasCosts(Pawn pawn, int level) => PerkCostUtility.HasCosts(pawn, CostsAt(level));
        public override bool TryStageCosts(Pawn pawn, int level, out IPerkCostTransaction payment)
        {
            bool success = PerkCostUtility.TryStageCosts(pawn, CostsAt(level), out var materials);
            payment = materials;
            return success;
        }

        public override int EffectiveLevel(Pawn pawn, int storedLevel) =>
            NeiyuPowerBalance.PassivesDisabled ? 0 : storedLevel;
        public override string PurchaseBlockedReason(Pawn pawn) => CultivationService.Eligibility(pawn)
            ?? (trial != CultivationTrial.None && (CultivationService.State(pawn)?.Progress(trial) ?? 0) < trialTarget
                ? "MX_Cultivation_TrialProgress".Translate(CultivationTrials.Label(trial), CultivationService.State(pawn)?.Progress(trial) ?? 0, trialTarget).ToString() : null);

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors()) yield return error;
            if (category == null) yield return defName + ": missing category.";
            if (!IsExtension && (rank < 1 || maxLevel != 1)) yield return defName + ": cultivation tiers must be single-level nodes.";
            if (!IsExtension && rank > 1 && (prerequisites == null || prerequisites.Count != 1
                || prerequisites[0].node is not NeiyuCultivationNodeDef previous
                || previous.category != category || previous.ability != ability || previous.rank != rank - 1))
                yield return defName + ": prerequisite must be the preceding tier of the same cultivation.";
            if (initiallyLearned && (rank != 1 || !costs.NullOrEmpty()))
                yield return defName + ": initial skills must be free first tiers.";
            if (ability != null && abilityValues == null) yield return defName + ": missing ability values.";
            if (IsExtension && (levelDescriptions == null || levelDescriptions.Count != maxLevel))
                yield return defName + ": each level needs a cumulative effect description.";
            if (trial != CultivationTrial.None && trialTarget <= 0) yield return defName + ": invalid trial target.";
            if (maxLevel <= 0 || !float.IsFinite(value) || !float.IsFinite(secondary)
                || durationTicks < 0 || cooldownTicks < 0) yield return defName + ": invalid node values.";
            HashSet<CharacterPerkNodeDef> visiting = new(), visited = new();
            bool Cyclic(CharacterPerkNodeDef node)
            {
                if (node == null || visited.Contains(node)) return false;
                if (!visiting.Add(node)) return true;
                if (node.prerequisites != null)
                    foreach (var predecessor in node.prerequisites)
                        if (Cyclic(predecessor?.node)) return true;
                visiting.Remove(node); visited.Add(node);
                return false;
            }
            if (Cyclic(this)) yield return defName + ": cyclic prerequisites.";
            if (prerequisites != null)
                foreach (var prerequisite in prerequisites)
                    if (prerequisite?.node is not NeiyuCultivationNodeDef || prerequisite.minLevel <= 0
                        || prerequisite.minLevel > prerequisite.node.maxLevel)
                        yield return defName + ": invalid cultivation prerequisite.";
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

    public enum CultivationEffect
    {
        None, Origin, SwordDamage, BowDamage, MoveSpeed, SwordPenetration, ApparelArmor, BowCooldown,
        DamageReduction, FormDamage, FinalDamage, SwitchTempo, DodgeStep, QuietShield, DodgeCounter,
        ShieldMemory, FlowerSword, BreakStep, SwitchRhythm, LastFeather, SpellRoot, BlessRadius,
        SkyfallEcho, BarrageExtra, BlessAfterglow, ExecuteFocus, BarrageGuidance, ToxinAfterglow,
        ThunderHaste, BarrageSplash, Finale
    }
    public enum CultivationTrial { None, Attack, Switch, Block, Dodge, Kills, Skill, Heal }

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
        public CultivationAbilityValues Copy() => (CultivationAbilityValues)MemberwiseClone();
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
}

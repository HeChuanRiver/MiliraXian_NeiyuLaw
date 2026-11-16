using System.Collections.Generic;
using MiliraXian.Characters.Common.PerkSystem;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Neiyu.Cultivation
{
    /// <summary>Effects derive from the common tree; this carrier also owns trial progress and trigger clocks.</summary>
    public sealed partial class Hediff_NeiyuCultivation : Hediff_CharacterPerkTree
    {
        private int schemaVersion;
        internal bool SuppressInitialGrant;
        private readonly Dictionary<AbilityDef, NeiyuCultivationNodeDef> abilities = new();
        private readonly Dictionary<AbilityDef, CultivationAbilityValues> abilityProjection = new();
        private readonly Dictionary<ThingDef, CultivationEquipmentBonus> equipment = new();
        public int ShieldBonus { get; private set; }
        public bool Initialized => schemaVersion >= 2;
        public HediffComp_CharacterPerkTree Tree => GetComp<HediffComp_CharacterPerkTree>();
        public HediffComp_PerkEventHandler Events => GetComp<HediffComp_PerkEventHandler>();
        public override bool ShouldRemove => false;

        public override void PostAdd(DamageInfo? dinfo)
        {
            base.PostAdd(dinfo);
            if (!SuppressInitialGrant) InitializeNew();
        }

        internal void InitializeNew()
        {
            if (Initialized) return;
            foreach (var node in CultivationService.Nodes)
                if (node.initiallyLearned) Events.TryGrant(node, broadcast: false);
            schemaVersion = 2;
            Events.RefreshEffects();
        }

        internal void FinishMigration()
        {
            schemaVersion = 2;
            Events.RestoreGrantedContent();
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref schemaVersion, "mx_neiyu_cultivationSchema", 0);
            ExposeRhythm();
            if (Scribe.mode == LoadSaveMode.PostLoadInit) RebuildProjection();
        }

        public override void Notify_PerkTreeChanged(Pawn owner, HediffComp_CharacterPerkTree tree)
        {
            RebuildProjection();
            base.Notify_PerkTreeChanged(owner, tree);
        }

        private void RebuildProjection()
        {
            abilities.Clear();
            abilityProjection.Clear();
            equipment.Clear();
            bonuses.Clear();
            ShieldBonus = 0;
            if (Tree == null) return;
            foreach (var definition in Tree.LearnedNodes)
            {
                if (definition is not NeiyuCultivationNodeDef node || Tree.EffectiveNodeLevel(node) <= 0) continue;
                if (node.IsExtension) { bonuses[node.effect] = node.value * Tree.EffectiveNodeLevel(node); continue; }
                ShieldBonus += node.shieldCharges;
                if (node.ability != null && (!abilities.TryGetValue(node.ability, out var current) || current.rank < node.rank))
                    abilities[node.ability] = node;
                if (node.equipmentEffects == null) continue;
                foreach (var effect in node.equipmentEffects)
                {
                    if (effect?.equipment == null) continue;
                    if (!equipment.TryGetValue(effect.equipment, out var bonus))
                        equipment[effect.equipment] = bonus = new CultivationEquipmentBonus();
                    bonus.Add(effect);
                }
            }
            float armor = Bonus(CultivationEffect.ApparelArmor);
            if (armor > 0f)
                foreach (string name in new[] { "MiliraXian_NeiyuNormal", "MiliraXian_NeiyuInner" })
                {
                    ThingDef def = DefDatabase<ThingDef>.GetNamed(name);
                    if (!equipment.TryGetValue(def, out var bonus)) equipment[def] = bonus = new();
                    bonus.Stats.TryGetValue(StatDefOf.ArmorRating_Sharp, out float current);
                    bonus.Stats[StatDefOf.ArmorRating_Sharp] = current + armor;
                }
            foreach (var pair in abilities)
            {
                var values = pair.Value.abilityValues.Copy();
                if (pair.Key.defName == "MX_Neiyu_Flower_BlessingField") values.radius += Bonus(CultivationEffect.BlessRadius);
                abilityProjection[pair.Key] = values;
            }
        }

        internal CultivationEquipmentBonus EquipmentBonus(ThingDef def) =>
            def != null && equipment.TryGetValue(def, out var bonus) ? bonus : null;
        internal CultivationAbilityValues AbilityValues(AbilityDef def) =>
            def != null && abilityProjection.TryGetValue(def, out var values) ? values : null;

        protected override void AddAdditionalStats(Dictionary<StatDef, float> offsets, Dictionary<StatDef, float> factors)
        {
            AddRhythmStats(offsets, factors);
            if (pawn?.apparel == null || NeiyuPowerBalance.PassivesDisabled) return;
            foreach (var apparel in pawn.apparel.WornApparel)
                if (EquipmentBonus(apparel.def) is { } bonus)
                    foreach (var pair in bonus.Equipped)
                    {
                        offsets.TryGetValue(pair.Key, out float value);
                        offsets[pair.Key] = value + pair.Value;
                    }
        }
    }

    /// <summary>A disposable projection, rebuilt on tree/gear events; it owns no progress.</summary>
    internal sealed class CultivationEquipmentBonus
    {
        public readonly Dictionary<StatDef, float> Stats = new();
        public readonly Dictionary<StatDef, float> Equipped = new();
        public float MeleeDamage, MeleePenetration, ProjectileDamage, ProjectilePenetration;
        public int SplitCount;
        public void Add(CultivationEquipmentEffect effect)
        {
            Add(Stats, effect.statOffsets);
            Add(Equipped, effect.equippedStatOffsets);
            MeleeDamage += effect.meleeDamage;
            MeleePenetration += effect.meleePenetration;
            ProjectileDamage += effect.projectileDamage;
            ProjectilePenetration += effect.projectilePenetration;
            SplitCount += effect.splitCount;
        }
        private static void Add(Dictionary<StatDef, float> target, List<StatModifier> values)
        {
            if (values == null) return;
            foreach (var value in values)
            {
                if (value.stat == null) continue;
                target.TryGetValue(value.stat, out float current);
                target[value.stat] = current + value.value;
            }
        }
    }
}

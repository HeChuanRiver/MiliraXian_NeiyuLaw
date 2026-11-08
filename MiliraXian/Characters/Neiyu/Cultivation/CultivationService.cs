using System;
using System.Collections.Generic;
using System.Linq;
using MiliraXian.Characters.Common.Biography;
using MiliraXian.Characters.Common.PerkSystem;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Neiyu.Cultivation
{
    internal static class CultivationService
    {
        private static List<NeiyuCultivationNodeDef> nodes;
        public static IReadOnlyList<NeiyuCultivationNodeDef> Nodes => nodes ??= DefDatabase<CharacterPerkNodeDef>
            .AllDefsListForReading.OfType<NeiyuCultivationNodeDef>().OrderBy(n => n.rank).ThenBy(n => n.displayOrder).ToList();
        public static Hediff_NeiyuCultivation State(Pawn pawn) => pawn?.health?.hediffSet?
            .GetFirstHediffOfDef(CultivationDefOf.MX_Neiyu_Cultivation) as Hediff_NeiyuCultivation;
        public static CultivationAbilityValues AbilityValues(Pawn pawn, AbilityDef ability) => State(pawn)?.AbilityValues(ability);
        public static void Refresh(Pawn pawn) => State(pawn)?.Events.RefreshEffects();

        public static int Rank(Pawn pawn, CharacterPerkCategoryDef category, AbilityDef ability = null)
        {
            var tree = State(pawn)?.Tree;
            int rank = 0;
            if (tree != null)
                foreach (var node in Nodes)
                    if (node.category == category && node.ability == ability && tree.HasNode(node)) rank = Math.Max(rank, node.rank);
            return rank;
        }

        public static bool Learned(Pawn pawn, NeiyuCultivationNodeDef node) => State(pawn)?.Tree.HasNode(node) == true;

        public static string Eligibility(Pawn pawn)
        {
            if (!NeiyuEquipmentUtility.IsNeiyu(pawn) || pawn.Destroyed || pawn.Dead) return "角色当前无法修行。";
            if (pawn.Faction != Faction.OfPlayer || !pawn.IsColonistPlayerControlled) return "加入殖民地后才能修行。";
            if (!pawn.Spawned || pawn.Map == null) return "返回地图后才能修行。";
            if (pawn.Downed || pawn.InMentalState || pawn.Drafted) return "需要清醒、未征召，且不处于精神崩溃状态。";
            if (NeiyuPowerBalance.PassivesDisabled) return "观赏模式封印了力量，修行进度仍会保留。";
            return null;
        }

        public static string Check(Pawn pawn, NeiyuCultivationNodeDef node)
        {
            var events = State(pawn)?.Events;
            if (events == null) return "修行状态尚未就绪。";
            return events.CanUnlock(node, out string reason, checkCosts: false) ? null : reason;
        }
        public static bool UsableMaterial(Pawn pawn, Thing thing) => PerkCostUtility.UsableMaterial(pawn, thing);
        public static bool TryLearn(Pawn pawn, NeiyuCultivationNodeDef node, out string message)
        {
            var events = State(pawn)?.Events;
            if (events == null) { message = "修行状态尚未就绪。"; return false; }
            if (!events.TryUnlock(node, out message)) return false;
            message = "已领悟 · " + node.LabelCap;
            return true;
        }

        public static bool TryCompleteAllForDev(Pawn pawn, out string message)
        {
            message = "角色当前无法修行。";
            if (!Prefs.DevMode) { message = "仅开发者模式可用。"; return false; }
            if (!NeiyuEquipmentUtility.IsNeiyu(pawn) || pawn.Dead || pawn.Destroyed || pawn.Faction != Faction.OfPlayer) return false;
            var state = State(pawn);
            if (state == null) { message = "修行状态尚未就绪。"; return false; }
            bool changed;
            do
            {
                changed = false;
                foreach (var node in state.Tree.RelevantNodes())
                    if (!node.displayOnly && state.Tree.GetNodeLevel(node) < node.maxLevel)
                        changed |= state.Events.TryGrant(node, broadcast: false);
            } while (changed);
            state.Events.RefreshEffects();
            if (state.Tree.RelevantNodes().Exists(n => !n.displayOnly && state.Tree.GetNodeLevel(n) < n.maxLevel))
            { message = "有修行前置缺失，无法修满。"; return false; }
            message = "已领悟全部修行。";
            return true;
        }

        /// <summary>Only old pawns missing the new carrier enter here, once during LoadedGame.</summary>
        public static void MigrateLegacy(Pawn pawn)
        {
            if (!NeiyuEquipmentUtility.IsNeiyu(pawn) || pawn.health == null) return;
            if (State(pawn) is { } existing)
            {
                if (!existing.Initialized) existing.InitializeNew();
                existing.Events.RestoreGrantedContent();
                return;
            }
            HashSet<AbilityDef> owned = new();
            if (pawn.abilities != null)
                foreach (var ability in pawn.abilities.AllAbilitiesForReading)
                    if (pawn.abilities.GetAbility(ability.def, includeTemporary: false) != null) owned.Add(ability.def);
            var tracker = BiographyFrameworkUtility.GetTracker(pawn);
            int Read(string key)
            {
                float value = tracker?.GetProgress(key) ?? 0f;
                int rank = float.IsFinite(value) ? Math.Clamp((int)value, 0, 3) : 0;
                return rank == 3 ? 5 : rank == 2 ? 3 : rank;
            }
            int weapon = Math.Max(Read("cultivation_wing"), Read("cultivation_arrow"));
            int apparel = Read("cultivation_law"), halo = Read("cultivation_halo");
            var state = (Hediff_NeiyuCultivation)HediffMaker.MakeHediff(CultivationDefOf.MX_Neiyu_Cultivation, pawn);
            state.SuppressInitialGrant = true;
            pawn.health.AddHediff(state);
            foreach (var node in Nodes)
            {
                bool grant = node.initiallyLearned || (node.ability != null && owned.Contains(node.ability))
                    || (node.ability == null && node.rank <= (node.category == CultivationDefOf.MX_Neiyu_Cultivation_Weapons ? weapon
                        : node.category == CultivationDefOf.MX_Neiyu_Cultivation_Apparel ? apparel : halo));
                if (grant) state.Events.TryGrant(node, broadcast: false);
            }
            state.FinishMigration();
        }
    }
}

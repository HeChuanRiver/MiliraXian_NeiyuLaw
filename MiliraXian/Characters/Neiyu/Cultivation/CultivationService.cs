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
        private static Dictionary<CultivationEffect, NeiyuCultivationNodeDef> extensionNodes;
        internal static IReadOnlyDictionary<CultivationEffect, NeiyuCultivationNodeDef> ExtensionNodes => extensionNodes ??=
            Nodes.Where(n => n.IsExtension).ToDictionary(n => n.effect);
        public static IReadOnlyList<NeiyuCultivationNodeDef> Nodes => nodes ??= DefDatabase<CharacterPerkNodeDef>
            .AllDefsListForReading.OfType<NeiyuCultivationNodeDef>().OrderBy(n => n.rank).ThenBy(n => n.displayOrder).ToList();
        public static Hediff_NeiyuCultivation State(Pawn pawn) => NeiyuEquipmentUtility.IsNeiyu(pawn)
            ? pawn.health?.hediffSet?.GetFirstHediffOfDef(CultivationDefOf.MX_Neiyu_Cultivation) as Hediff_NeiyuCultivation : null;
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
            if (!NeiyuEquipmentUtility.IsNeiyu(pawn) || pawn.Destroyed || pawn.Dead) return "MX_Cultivation_Unavailable".Translate().ToString();
            if (pawn.Faction != Faction.OfPlayer || !pawn.IsColonistPlayerControlled) return "MX_Cultivation_ColonyRequired".Translate().ToString();
            if (!pawn.Spawned || pawn.Map == null) return "MX_Cultivation_MapRequired".Translate().ToString();
            if (pawn.Downed || pawn.InMentalState || pawn.Drafted) return "MX_Cultivation_MustBeReady".Translate().ToString();
            if (NeiyuPowerBalance.PassivesDisabled) return "MX_Cultivation_DecorativeBlocked".Translate().ToString();
            return null;
        }

        public static string Check(Pawn pawn, NeiyuCultivationNodeDef node)
        {
            var events = State(pawn)?.Events;
            if (events == null) return "MX_Cultivation_NotReady".Translate().ToString();
            return events.CanUnlock(node, out string reason, checkCosts: false) ? null : reason;
        }
        public static bool UsableMaterial(Pawn pawn, Thing thing) => PerkCostUtility.UsableMaterial(pawn, thing);
        public static bool TryLearn(Pawn pawn, NeiyuCultivationNodeDef node, out string message)
        {
            var events = State(pawn)?.Events;
            if (events == null) { message = "MX_Cultivation_NotReady".Translate().ToString(); return false; }
            if (!events.TryUnlock(node, out message)) return false;
            message = "MX_Cultivation_LearnedNode".Translate(node.LabelCap).ToString();
            return true;
        }

        public static bool TryCompleteAllForDev(Pawn pawn, out string message)
        {
            message = "MX_Cultivation_Unavailable".Translate().ToString();
            if (!Prefs.DevMode) { message = "MX_Cultivation_DevOnly".Translate().ToString(); return false; }
            if (!NeiyuEquipmentUtility.IsNeiyu(pawn) || pawn.Dead || pawn.Destroyed || pawn.Faction != Faction.OfPlayer) return false;
            var state = State(pawn);
            if (state == null) { message = "MX_Cultivation_NotReady".Translate().ToString(); return false; }
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
            { message = "MX_Cultivation_DevPrerequisiteMissing".Translate().ToString(); return false; }
            message = "MX_Cultivation_DevCompleted".Translate().ToString();
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
                int oldRank = node.category == CultivationDefOf.MX_Neiyu_Cultivation_Weapons ? weapon
                    : node.category == CultivationDefOf.MX_Neiyu_Cultivation_Apparel ? apparel : halo;
                bool grant = node.initiallyLearned || (!node.IsExtension
                    && (node.ability != null ? owned.Contains(node.ability) : node.rank <= oldRank));
                if (grant) state.Events.TryGrant(node, broadcast: false);
            }
            state.FinishMigration();
        }
    }
}

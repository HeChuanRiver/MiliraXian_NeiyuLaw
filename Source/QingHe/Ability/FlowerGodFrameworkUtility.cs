using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Ability
{
    public static class FlowerGodFrameworkUtility
    {
        private const string DefaultFlowerMandateAbilityDefName = "MX_QH_FlowerMandate";

        private struct SeasonFlowerGodSet
        {
            public SeasonFlowerGodSet(string abilityDefName, string hediffDefName, string traitDefName)
            {
                AbilityDefName = abilityDefName;
                HediffDefName = hediffDefName;
                TraitDefName = traitDefName;
            }

            public string AbilityDefName { get; }
            public string HediffDefName { get; }
            public string TraitDefName { get; }
        }

        private static readonly Dictionary<AttunedSeason, SeasonFlowerGodSet> SetsBySeason =
            new Dictionary<AttunedSeason, SeasonFlowerGodSet>
            {
                { AttunedSeason.Spring, new SeasonFlowerGodSet("MX_QH_FlowerMandate_Peach", "MX_QH_FlowerGodSigil_RedApricot", "MX_QH_FlowerWord_Peony") },
                { AttunedSeason.Summer, new SeasonFlowerGodSet("MX_QH_FlowerMandate_Pomegranate", "MX_QH_FlowerGodSigil_Lotus", "MX_QH_FlowerWord_JadeHairpin") },
                { AttunedSeason.Autumn, new SeasonFlowerGodSet("MX_QH_FlowerMandate_Chrysanthemum", "MX_QH_FlowerGodSigil_Magnolia", "MX_QH_FlowerWord_Osmanthus") },
                { AttunedSeason.Winter, new SeasonFlowerGodSet("MX_QH_FlowerMandate_Wintersweet", "MX_QH_FlowerGodSigil_RedPlum", "MX_QH_FlowerWord_Narcissus") }
            };

        public static void SyncSeason(Pawn pawn, AttunedSeason season)
        {
            if (pawn == null)
            {
                return;
            }

            RemoveAllSeasonalFramework(pawn);

            if (!SetsBySeason.TryGetValue(season, out SeasonFlowerGodSet set))
            {
                EnsureAbility(pawn, DefaultFlowerMandateAbilityDefName);
                return;
            }

            RemoveAbility(pawn, DefaultFlowerMandateAbilityDefName);
            EnsureAbility(pawn, set.AbilityDefName);
            EnsureHediff(pawn, set.HediffDefName);
            EnsureTrait(pawn, set.TraitDefName);
        }

        private static void RemoveAllSeasonalFramework(Pawn pawn)
        {
            foreach (SeasonFlowerGodSet set in SetsBySeason.Values)
            {
                RemoveAbility(pawn, set.AbilityDefName);
                RemoveHediff(pawn, set.HediffDefName);
                RemoveTrait(pawn, set.TraitDefName);
            }
        }

        private static void EnsureAbility(Pawn pawn, string defName)
        {
            if (pawn.abilities == null)
            {
                return;
            }

            AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail(defName);
            if (def != null && pawn.abilities.GetAbility(def, includeTemporary: false) == null)
            {
                pawn.abilities.GainAbility(def);
            }
        }

        private static void RemoveAbility(Pawn pawn, string defName)
        {
            if (pawn.abilities == null)
            {
                return;
            }

            AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                pawn.abilities.RemoveAbility(def);
            }
        }

        private static void EnsureHediff(Pawn pawn, string defName)
        {
            if (pawn.health?.hediffSet == null)
            {
                return;
            }

            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            if (def != null && pawn.health.hediffSet.GetFirstHediffOfDef(def) == null)
            {
                pawn.health.AddHediff(HediffMaker.MakeHediff(def, pawn));
            }
        }

        private static void RemoveHediff(Pawn pawn, string defName)
        {
            if (pawn.health?.hediffSet == null)
            {
                return;
            }

            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            Hediff hediff = def != null ? pawn.health.hediffSet.GetFirstHediffOfDef(def) : null;
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        private static void EnsureTrait(Pawn pawn, string defName)
        {
            if (pawn.story?.traits == null)
            {
                return;
            }

            TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            if (def != null && !pawn.story.traits.HasTrait(def))
            {
                pawn.story.traits.GainTrait(new Trait(def));
            }
        }

        private static void RemoveTrait(Pawn pawn, string defName)
        {
            if (pawn.story?.traits == null)
            {
                return;
            }

            TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            Trait trait = def != null ? pawn.story.traits.GetTrait(def) : null;
            if (trait != null)
            {
                pawn.story.traits.RemoveTrait(trait);
            }
        }
    }
}

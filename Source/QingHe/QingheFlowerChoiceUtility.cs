using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class QingheFlowerChoiceUtility
    {
        private const string DefaultFlowerMandateAbilityDefName = "MX_QH_FlowerMandate";
        private const string FlowerDivinationSlashAbilityDefName = "MX_QH_FlowerDivinationSlash";

        private static readonly List<string> FlowerMandateAbilityDefNames = new List<string>
        {
            "MX_QH_FlowerMandate_Peach",
            "MX_QH_FlowerMandate_Pomegranate",
            "MX_QH_FlowerMandate_Chrysanthemum",
            "MX_QH_FlowerMandate_Wintersweet"
        };

        private static readonly List<string> FlowerSigilHediffDefNames = new List<string>
        {
            "MX_QH_FlowerSigil_RedApricot",
            "MX_QH_FlowerSigil_Lotus",
            "MX_QH_FlowerSigil_Magnolia",
            "MX_QH_FlowerSigil_RedPlum"
        };

        private static readonly List<string> FlowerWordTraitDefNames = new List<string>
        {
            "MX_QH_FlowerWord_Peony",
            "MX_QH_FlowerWord_JadeHairpin",
            "MX_QH_FlowerWord_Osmanthus",
            "MX_QH_FlowerWord_Narcissus"
        };

        public static IReadOnlyList<string> FlowerMandates => FlowerMandateAbilityDefNames;
        public static IReadOnlyList<string> FlowerSigils => FlowerSigilHediffDefNames;
        public static IReadOnlyList<string> FlowerWords => FlowerWordTraitDefNames;

        public static void SyncFlowerMandate(Pawn pawn, string selectedAbilityDefName)
        {
            if (pawn?.abilities == null)
            {
                return;
            }

            RemoveAbility(pawn, DefaultFlowerMandateAbilityDefName);
            for (int i = 0; i < FlowerMandateAbilityDefNames.Count; i++)
            {
                RemoveAbility(pawn, FlowerMandateAbilityDefNames[i]);
            }

            if (selectedAbilityDefName.NullOrEmpty())
            {
                return;
            }

            EnsureAbility(pawn, selectedAbilityDefName);
        }

        public static void SyncFlowerDivinationSlash(Pawn pawn, bool unlocked)
        {
            if (pawn?.abilities == null)
            {
                return;
            }

            if (unlocked)
            {
                EnsureAbility(pawn, FlowerDivinationSlashAbilityDefName);
            }
            else
            {
                RemoveAbility(pawn, FlowerDivinationSlashAbilityDefName);
            }
        }

        public static void SyncFlowerSigil(Pawn pawn, string selectedHediffDefName)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }

            for (int i = 0; i < FlowerSigilHediffDefNames.Count; i++)
            {
                RemoveHediff(pawn, FlowerSigilHediffDefNames[i]);
            }

            if (!selectedHediffDefName.NullOrEmpty())
            {
                EnsureHediff(pawn, selectedHediffDefName);
            }
        }

        public static void SyncFlowerWord(Pawn pawn, string selectedTraitDefName)
        {
            if (pawn?.story?.traits == null)
            {
                return;
            }

            for (int i = 0; i < FlowerWordTraitDefNames.Count; i++)
            {
                RemoveTrait(pawn, FlowerWordTraitDefNames[i]);
            }

            if (!selectedTraitDefName.NullOrEmpty())
            {
                EnsureTrait(pawn, selectedTraitDefName);
            }
        }

        public static string LabelForDefName(string defName)
        {
            if (defName.NullOrEmpty())
            {
                return "未选择";
            }

            AbilityDef abilityDef = DefDatabase<AbilityDef>.GetNamedSilentFail(defName);
            if (abilityDef != null)
            {
                return abilityDef.LabelCap;
            }

            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            if (hediffDef != null)
            {
                return hediffDef.LabelCap;
            }

            TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            if (traitDef != null)
            {
                return traitDef.LabelCap;
            }

            return defName;
        }

        public static string ShortLabelForDefName(string defName)
        {
            string label = LabelForDefName(defName);
            if (label.NullOrEmpty())
            {
                return "?";
            }

            int separatorIndex = label.IndexOf('·');
            if (separatorIndex >= 0 && separatorIndex < label.Length - 1)
            {
                label = label.Substring(separatorIndex + 1);
            }

            return label.Length > 2 ? label.Substring(0, 2) : label;
        }

        public static Texture2D IconForDefName(string defName)
        {
            if (defName.NullOrEmpty())
            {
                return null;
            }

            AbilityDef abilityDef = DefDatabase<AbilityDef>.GetNamedSilentFail(defName);
            if (abilityDef?.uiIcon != null)
            {
                return abilityDef.uiIcon;
            }

            return null;
        }

        public static bool HasAppliedChoice(Pawn pawn, string defName)
        {
            if (defName.NullOrEmpty() || pawn == null)
            {
                return false;
            }

            AbilityDef abilityDef = DefDatabase<AbilityDef>.GetNamedSilentFail(defName);
            if (abilityDef != null)
            {
                return pawn.abilities?.GetAbility(abilityDef, includeTemporary: false) != null;
            }

            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            if (hediffDef != null)
            {
                return pawn.health?.hediffSet?.GetFirstHediffOfDef(hediffDef) != null;
            }

            TraitDef traitDef = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            if (traitDef != null)
            {
                return pawn.story?.traits?.HasTrait(traitDef) == true;
            }

            return false;
        }

        private static void EnsureAbility(Pawn pawn, string defName)
        {
            AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail(defName);
            if (def != null && pawn.abilities.GetAbility(def, includeTemporary: false) == null)
            {
                pawn.abilities.GainAbility(def);
            }
        }

        private static void RemoveAbility(Pawn pawn, string defName)
        {
            AbilityDef def = DefDatabase<AbilityDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                pawn.abilities.RemoveAbility(def);
            }
        }

        private static void EnsureHediff(Pawn pawn, string defName)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            if (def != null && pawn.health.hediffSet.GetFirstHediffOfDef(def) == null)
            {
                pawn.health.AddHediff(HediffMaker.MakeHediff(def, pawn));
            }
        }

        private static void RemoveHediff(Pawn pawn, string defName)
        {
            HediffDef def = DefDatabase<HediffDef>.GetNamedSilentFail(defName);
            Hediff hediff = def != null ? pawn.health.hediffSet.GetFirstHediffOfDef(def) : null;
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        private static void EnsureTrait(Pawn pawn, string defName)
        {
            TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            if (def != null && !pawn.story.traits.HasTrait(def))
            {
                pawn.story.traits.GainTrait(new Trait(def));
            }
        }

        private static void RemoveTrait(Pawn pawn, string defName)
        {
            TraitDef def = DefDatabase<TraitDef>.GetNamedSilentFail(defName);
            Trait trait = def != null ? pawn.story.traits.GetTrait(def) : null;
            if (trait != null)
            {
                pawn.story.traits.RemoveTrait(trait);
            }
        }
    }
}

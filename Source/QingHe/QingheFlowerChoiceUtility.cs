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

        public static IReadOnlyList<AbilityDef> FlowerMandates => ResolveDefs<AbilityDef>(FlowerMandateAbilityDefNames);
        public static IReadOnlyList<HediffDef> FlowerSigils => ResolveDefs<HediffDef>(FlowerSigilHediffDefNames);
        public static IReadOnlyList<TraitDef> FlowerWords => ResolveDefs<TraitDef>(FlowerWordTraitDefNames);

        public static void SyncFlowerMandate(Pawn pawn, AbilityDef selectedAbilityDef)
        {
            SyncFlowerMandates(pawn, selectedAbilityDef, null);
        }

        public static void SyncFlowerMandates(Pawn pawn, AbilityDef primaryAbilityDef, AbilityDef timedAbilityDef)
        {
            if (pawn?.abilities == null)
            {
                return;
            }

            RemoveAbility(pawn, DefDatabase<AbilityDef>.GetNamedSilentFail(DefaultFlowerMandateAbilityDefName));
            IReadOnlyList<AbilityDef> mandates = FlowerMandates;
            for (int i = 0; i < mandates.Count; i++)
            {
                AbilityDef def = mandates[i];
                if (def != primaryAbilityDef && def != timedAbilityDef)
                {
                    RemoveAbility(pawn, def);
                }
            }

            if (primaryAbilityDef != null)
            {
                EnsureAbility(pawn, primaryAbilityDef);
            }

            if (timedAbilityDef != null && timedAbilityDef != primaryAbilityDef)
            {
                EnsureAbility(pawn, timedAbilityDef);
            }
        }

        public static void StartFlowerMandateCooldown(Pawn pawn, AbilityDef abilityDef)
        {
            Ability ability = GetFlowerMandateAbility(pawn, abilityDef);
            if (ability == null)
            {
                return;
            }

            int ticks = ability.def.cooldownTicksRange.RandomInRange;
            if (ticks > 0)
            {
                ability.StartCooldown(ticks);
            }
        }

        public static Ability GetFlowerMandateAbility(Pawn pawn, AbilityDef abilityDef)
        {
            if (pawn?.abilities == null || abilityDef == null)
            {
                return null;
            }

            return pawn.abilities.GetAbility(abilityDef, includeTemporary: false);
        }

        public static void SyncFlowerDivinationSlash(Pawn pawn, bool unlocked)
        {
            if (pawn?.abilities == null)
            {
                return;
            }

            if (unlocked)
            {
                EnsureAbility(pawn, DefDatabase<AbilityDef>.GetNamedSilentFail(FlowerDivinationSlashAbilityDefName));
            }
            else
            {
                RemoveAbility(pawn, DefDatabase<AbilityDef>.GetNamedSilentFail(FlowerDivinationSlashAbilityDefName));
            }
        }

        public static void SyncFlowerSigil(Pawn pawn, HediffDef selectedHediffDef)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }

            IReadOnlyList<HediffDef> sigils = FlowerSigils;
            for (int i = 0; i < sigils.Count; i++)
            {
                RemoveHediff(pawn, sigils[i]);
            }

            if (selectedHediffDef != null)
            {
                EnsureHediff(pawn, selectedHediffDef);
            }
        }

        public static void SyncFlowerWord(Pawn pawn, TraitDef selectedTraitDef)
        {
            if (pawn?.story?.traits == null)
            {
                return;
            }

            IReadOnlyList<TraitDef> words = FlowerWords;
            for (int i = 0; i < words.Count; i++)
            {
                RemoveTrait(pawn, words[i]);
            }

            if (selectedTraitDef != null)
            {
                EnsureTrait(pawn, selectedTraitDef);
            }
        }

        public static bool IsFlowerWordTraitDef(TraitDef traitDef)
        {
            if (traitDef == null)
            {
                return false;
            }

            IReadOnlyList<TraitDef> words = FlowerWords;
            for (int i = 0; i < words.Count; i++)
            {
                if (words[i] == traitDef)
                {
                    return true;
                }
            }

            return false;
        }

        public static string LabelForDef(Def def)
        {
            if (def == null)
            {
                return "未选择";
            }

            TraitDef traitDef = def as TraitDef;
            if (traitDef != null)
            {
                return LabelForTraitDef(traitDef);
            }

            return def.LabelCap;
        }

        private static string LabelForTraitDef(TraitDef traitDef)
        {
            if (traitDef?.degreeDatas != null && traitDef.degreeDatas.Count > 0)
            {
                for (int i = 0; i < traitDef.degreeDatas.Count; i++)
                {
                    if (traitDef.degreeDatas[i]?.degree == 0)
                    {
                        return traitDef.degreeDatas[i].LabelCap;
                    }
                }

                return traitDef.degreeDatas[0].LabelCap;
            }

            return traitDef?.LabelCap ?? string.Empty;
        }

        public static string ShortLabelForDef(Def def)
        {
            string label = LabelForDef(def);
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

        public static Texture2D IconForDef(Def def)
        {
            AbilityDef abilityDef = def as AbilityDef;
            if (abilityDef?.uiIcon != null)
            {
                return abilityDef.uiIcon;
            }

            return null;
        }

        public static bool HasAppliedChoice(Pawn pawn, Def def)
        {
            if (def == null || pawn == null)
            {
                return false;
            }

            AbilityDef abilityDef = def as AbilityDef;
            if (abilityDef != null)
            {
                return pawn.abilities?.GetAbility(abilityDef, includeTemporary: false) != null;
            }

            HediffDef hediffDef = def as HediffDef;
            if (hediffDef != null)
            {
                return pawn.health?.hediffSet?.GetFirstHediffOfDef(hediffDef) != null;
            }

            TraitDef traitDef = def as TraitDef;
            if (traitDef != null)
            {
                return pawn.story?.traits?.HasTrait(traitDef) == true;
            }

            return false;
        }

        private static void EnsureAbility(Pawn pawn, AbilityDef def)
        {
            if (def != null && pawn.abilities.GetAbility(def, includeTemporary: false) == null)
            {
                pawn.abilities.GainAbility(def);
            }
        }

        private static void RemoveAbility(Pawn pawn, AbilityDef def)
        {
            if (def != null)
            {
                pawn.abilities.RemoveAbility(def);
            }
        }

        private static void EnsureHediff(Pawn pawn, HediffDef def)
        {
            if (def != null && pawn.health.hediffSet.GetFirstHediffOfDef(def) == null)
            {
                pawn.health.AddHediff(HediffMaker.MakeHediff(def, pawn));
            }
        }

        private static void RemoveHediff(Pawn pawn, HediffDef def)
        {
            Hediff hediff = def != null ? pawn.health.hediffSet.GetFirstHediffOfDef(def) : null;
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }

        private static void EnsureTrait(Pawn pawn, TraitDef def)
        {
            if (def != null && !pawn.story.traits.HasTrait(def))
            {
                pawn.story.traits.GainTrait(new Trait(def));
            }
        }

        private static void RemoveTrait(Pawn pawn, TraitDef def)
        {
            Trait trait = def != null ? pawn.story.traits.GetTrait(def) : null;
            if (trait != null)
            {
                pawn.story.traits.RemoveTrait(trait);
            }
        }

        private static List<T> ResolveDefs<T>(List<string> defNames) where T : Def
        {
            List<T> defs = new List<T>();
            for (int i = 0; i < defNames.Count; i++)
            {
                T def = DefDatabase<T>.GetNamedSilentFail(defNames[i]);
                if (def != null)
                {
                    defs.Add(def);
                }
            }

            return defs;
        }
    }
}

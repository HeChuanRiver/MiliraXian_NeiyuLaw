using System.Collections.Generic;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.UI;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe
{
    public static class MX_QHSkillUtility
    {
        public static void SyncChoices(Pawn pawn)
        {
            HediffComp_SkillTreeState state = MX_QH_HediffUtility.EnsureFlowerResonance(pawn);
            SyncChoices(pawn, state);
        }

        public static void SyncChoices(Pawn pawn, HediffComp_SkillTreeState state)
        {
            if (pawn == null || state == null)
            {
                return;
            }

            int effectiveLevel = Mathf.Min(MX_QH_HediffUtility.GetAuraMasteryLevel(pawn), QinghePowerBalance.MaxEffectiveLevel);
            state.SyncNodesByAuraMasteryLevel(effectiveLevel);
            state.SyncGrantedDefs();
            HediffComp_LuoshenContract.SyncForQinghe(pawn, state);
        }

        public static bool HasSeasonalResonance(Pawn pawn)
        {
            SkillNodeDef node = MX_QHSkillNodeDefOf.MX_QH_Node_SeasonalResonance;
            return node != null
                && Mathf.Min(MX_QH_HediffUtility.GetAuraMasteryLevel(pawn), QinghePowerBalance.MaxEffectiveLevel) >= node.requiredAuraMasteryLevel
                && MX_QH_HediffUtility.GetFlowerResonance(pawn)?.HasNode(node) == true;
        }

        public static float GetSpellEffectFactor(Pawn pawn)
        {
            if (pawn == null || MX_QHDefOf.MX_QH_SpellEffectFactor == null)
            {
                return 1f;
            }

            return Mathf.Max(0f, pawn.GetStatValue(MX_QHDefOf.MX_QH_SpellEffectFactor));
        }

        public static IEnumerable<Gizmo> GetGizmos(Pawn pawn, HediffComp_SkillTreeState state)
        {
            if (pawn == null || pawn.Dead || state == null || Find.Selector.SingleSelectedThing != pawn)
            {
                yield break;
            }

            if (QinghePowerBalance.Sealed)
            {
                yield break;
            }

            yield return new Gizmo_QH_FlowerDecree(pawn);

            if (!Prefs.DevMode || !DebugSettings.ShowDevGizmos)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "MX_QH_DevAddAuraMasteryLevelLabel".Translate(),
                defaultDesc = "MX_QH_DevAddAuraMasteryLevelDesc".Translate(),
                action = delegate
                {
                    MX_QH_HediffUtility.AddAuraMasteryLevel(pawn);
                }
            };

        }

    }
}

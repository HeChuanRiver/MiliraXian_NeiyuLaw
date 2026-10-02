using System.Collections.Generic;
using MiliraXian.Characters;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.UI;
using RimWorld;
using UnityEngine;
using Verse;
using MiliraXian.Characters.Common.PerkSystem;

namespace MiliraXian.Characters.QingHe
{
    public static class MX_QH_PerkUtility
    {
        public static void SyncChoices(Pawn pawn)
        {
            HediffComp_CharacterPerkTree state = MX_QH_HediffUtility.GetFlowerResonance(pawn);
            SyncChoices(pawn, state);
        }

        public static void SyncChoices(Pawn pawn, HediffComp_CharacterPerkTree state)
        {
            if (pawn == null || state == null)
            {
                return;
            }

            // Unlocking writes stored state, so it must see the earned level, not the gated one.
            state.SyncNodesByAuraMasteryLevel(MX_QH_HediffUtility.GetAuraMasteryComp(pawn)?.CurrentLevel ?? 0);
            state.SyncGrantedDefs();
        }

        public static bool HasSeasonalResonance(Pawn pawn)
        {
            CharacterPerkNodeDef node = MX_QHCharacterPerkNodeDefOf.MX_QH_Node_SeasonalResonance;
            // HasNode answers "is it unlocked", GetAuraMasteryLevel is already gated: one asks
            // stored, the other asks whether it currently takes effect.
            return node != null
                && MX_QH_HediffUtility.GetAuraMasteryLevel(pawn) >= node.requiredAuraMasteryLevel
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

        public static IEnumerable<Gizmo> GetGizmos(Pawn pawn, HediffComp_CharacterPerkTree state)
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

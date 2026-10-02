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
            HediffComp_CharacterPerkTree tree = MX_QH_HediffUtility.GetFlowerResonance(pawn);
            tree?.parent?.TryGetComp<HediffComp_PerkEventHandler>()?.TryAutoUnlock();
        }

        public static bool HasSeasonalResonance(Pawn pawn)
        {
            CharacterPerkNodeDef node = MX_QH_PerkNodeDefOf.MX_QH_Node_SeasonalResonance;
            return node != null && MX_QH_HediffUtility.GetFlowerResonance(pawn)?.EffectiveNodeLevel(node) > 0;
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

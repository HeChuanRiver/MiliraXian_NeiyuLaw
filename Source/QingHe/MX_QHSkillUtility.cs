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

            state.SyncNodesByGraceLevel(MX_QH_HediffUtility.GetDivineGraceLevel(pawn));
            state.SyncGrantedDefs();
            HediffComp_LuoshenContract.SyncForQinghe(pawn, state);
        }

        public static float GetSpecialAbilityEffectFactor(Pawn pawn)
        {
            if (pawn == null || MX_QHDefOf.MX_QH_SpecialAbilityEffectFactor == null)
            {
                return 1f;
            }

            return Mathf.Max(0f, pawn.GetStatValue(MX_QHDefOf.MX_QH_SpecialAbilityEffectFactor));
        }

        public static IEnumerable<Gizmo> GetGizmos(Pawn pawn, HediffComp_SkillTreeState state)
        {
            if (pawn == null || pawn.Dead || state == null || Find.Selector.SingleSelectedThing != pawn)
            {
                yield break;
            }

            yield return new Gizmo_QH_FlowerDecree(pawn);

            if (!Prefs.DevMode)
            {
                yield break;
            }

            yield return new Command_Action
            {
                defaultLabel = "MX_QH_DevAddDivineGraceLevelLabel".Translate(),
                defaultDesc = "MX_QH_DevAddDivineGraceLevelDesc".Translate(),
                action = delegate
                {
                    MX_QH_HediffUtility.AddDivineGraceLevel(pawn);
                }
            };

        }

    }
}

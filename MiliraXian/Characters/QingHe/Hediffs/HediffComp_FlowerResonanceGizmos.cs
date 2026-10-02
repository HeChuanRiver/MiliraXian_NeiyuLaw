using System.Collections.Generic;
using MiliraXian.Characters;
using RimWorld;
using Verse;
using MiliraXian.Characters.Common.PerkSystem;
using MiliraXian.Characters.QingHe;

namespace MiliraXian.Characters.QingHe.Hediffs
{
    public class HediffCompProperties_PerkTreeGizmos : HediffCompProperties
    {
        public HediffCompProperties_PerkTreeGizmos()
        {
            compClass = typeof(HediffComp_PerkTreeGizmos);
        }
    }

    public class HediffComp_PerkTreeGizmos : HediffComp, IPerkEventListener
    {
        public override bool CompDisallowVisible()
        {
            return true;
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            HediffComp_CharacterPerkTree state = parent?.GetComp<HediffComp_CharacterPerkTree>();
            foreach (Gizmo gizmo in MX_QH_PerkUtility.GetGizmos(Pawn, state))
            {
                yield return gizmo;
            }
        }

        public void Notify_PerkTreeChanged(Pawn pawn, HediffComp_CharacterPerkTree state)
        {
            MX_QH_PerkUtility.SyncChoices(pawn, state);
        }
    }
}



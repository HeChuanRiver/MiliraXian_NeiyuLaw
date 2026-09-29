using System.Collections.Generic;
using Verse;

namespace MiliraXian.Characters.QingHe.CombatStance
{
    public class HediffCompProperties_CombatStance : HediffCompProperties
    {
        public HediffCompProperties_CombatStance()
        {
            compClass = typeof(HediffComp_CombatStance);
        }
    }

    public class HediffComp_CombatStance : HediffComp
    {
        private CombatStanceDef currentStance;

        public CombatStanceDef CurrentStance => currentStance;

        public override void CompPostPostAdd(DamageInfo? dinfo)
        {
            base.CompPostPostAdd(dinfo);
            RefreshStance();
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Defs.Look(ref currentStance, "currentStance");
        }

        public override bool CompDisallowVisible()
        {
            return true;
        }

        public void RefreshStance()
        {
            ThingDef weaponDef = Pawn?.equipment?.Primary?.def;
            StanceWeaponClass? weaponClass = weaponDef == null ? null
                : weaponDef.IsMeleeWeapon ? StanceWeaponClass.Melee
                : weaponDef.IsRangedWeapon ? StanceWeaponClass.Ranged
                : null;
            currentStance = null;
            if (weaponClass == null)
            {
                return;
            }

            List<CombatStanceDef> stances = DefDatabase<CombatStanceDef>.AllDefsListForReading;
            for (int i = 0; i < stances.Count; i++)
            {
                if (stances[i].weaponClass == weaponClass.Value)
                {
                    currentStance = stances[i];
                    return;
                }
            }
        }
    }
}

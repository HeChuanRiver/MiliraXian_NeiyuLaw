using Verse;

namespace MiliraXian.Characters.QingHe.CombatStance
{
    public enum StanceWeaponClass
    {
        Melee,
        Ranged
    }

    public class CombatStanceDef : Def
    {
        public StanceWeaponClass weaponClass;
        public HediffStage stanceEffect;
    }
}

using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things
{
    public class CompProperties_FlowerBellStatusOnHit : CompProperties
    {
        public DamageDef damageDef;
        public float amount;
        public float armorPenetration = -1f;
        public float chance = 1f;
        public float yangchunMultiplier = 1.5f;
        public bool requireHostileTarget = true;

        public CompProperties_FlowerBellStatusOnHit()
        {
            compClass = typeof(CompFlowerBellStatusOnHit);
        }
    }

    public class CompFlowerBellStatusOnHit : ThingComp
    {
        public CompProperties_FlowerBellStatusOnHit Props => (CompProperties_FlowerBellStatusOnHit)props;

        private Projectile ProjectileParent => parent as Projectile;

        public void NotifyImpact(Thing hitThing, bool blockedByShield)
        {
            Pawn target = hitThing as Pawn;
            Pawn caster = ProjectileParent?.Launcher as Pawn;
            if (blockedByShield || target == null || caster == null || Props?.damageDef == null || Props.amount <= 0f)
            {
                return;
            }

            if (Props.requireHostileTarget && !GenHostility.HostileTo(caster, target))
            {
                return;
            }

            if (!HasBianzhi(caster))
            {
                return;
            }

            if (Props.chance < 1f && !Rand.Chance(Mathf.Clamp01(Props.chance)))
            {
                return;
            }

            float amount = Props.amount * AccumulationMultiplier(caster);
            float armorPenetration = Props.armorPenetration >= 0f ? Props.armorPenetration : Props.damageDef.defaultArmorPenetration;
            DamageInfo dinfo = new DamageInfo(Props.damageDef, amount, armorPenetration, ProjectileParent?.ExactRotation.eulerAngles.y ?? -1f, caster);
            target.TakeDamage(dinfo);
        }

        private static bool HasBianzhi(Pawn caster)
        {
            HediffComp_FlowerResonance state = FlowerCourtUtility.GetSkillTreeState(caster);
            return state?.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Bianzhi) == true;
        }

        private float AccumulationMultiplier(Pawn caster)
        {
            HediffComp_FlowerResonance state = FlowerCourtUtility.GetSkillTreeState(caster);
            return state?.HasNode(MX_QHSkillNodeDefOf.MX_QH_Node_Yangchun) == true ? Mathf.Max(0f, Props.yangchunMultiplier) : 1f;
        }
    }
}

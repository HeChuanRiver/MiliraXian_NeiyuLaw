using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    public class CompProperties_FlowerBellStatusOnHit : CompProperties
    {
        public List<HediffDef_Abnormal> abnormals = new List<HediffDef_Abnormal>();
        public float accumulationAmount;
        public float chance = 1f;
        public float yangchunMultiplier = 1.5f;
        public bool requireHostileTarget = true;
        public bool scaleWithQingheSpecialAbilityEffect;

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
            if (blockedByShield)
            {
                return;
            }

            ApplyAbnormals(caster, target, Props);
        }

        public static void ApplyAbnormals(Pawn caster, Pawn target, CompProperties_FlowerBellStatusOnHit props)
        {
            if (caster == null || target == null || target.Dead || target.Destroyed || props?.abnormals == null || props.abnormals.Count == 0 || props.accumulationAmount <= 0f)
            {
                return;
            }

            if (props.requireHostileTarget && !GenHostility.HostileTo(caster, target))
            {
                return;
            }

            if (props.chance < 1f && !Rand.Chance(Mathf.Clamp01(props.chance)))
            {
                return;
            }

            float amount = props.accumulationAmount * ResolveSpecialAbilityEffectFactor(caster, props);
            for (int i = 0; i < props.abnormals.Count; i++)
            {
                HediffDef_Abnormal abnormal = props.abnormals[i];
                if (abnormal != null)
                {
                    AbnormalSystem.ApplyAccumulation(caster, target, abnormal, amount);
                }
            }
        }

        public static CompProperties_FlowerBellStatusOnHit PropsFor(ThingDef projectileDef)
        {
            if (projectileDef?.comps == null)
            {
                return null;
            }

            for (int i = 0; i < projectileDef.comps.Count; i++)
            {
                if (projectileDef.comps[i] is CompProperties_FlowerBellStatusOnHit props)
                {
                    return props;
                }
            }

            return null;
        }

        public static float ResolveSpecialAbilityEffectFactor(Pawn caster, CompProperties_FlowerBellStatusOnHit props)
        {
            return props?.scaleWithQingheSpecialAbilityEffect == true
                ? MiliraXian.Characters.QingHe.MX_QHSkillUtility.GetSpecialAbilityEffectFactor(caster)
                : 1f;
        }
    }
}

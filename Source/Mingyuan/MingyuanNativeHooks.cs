using MiliraXian.Characters.Neiyu;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan
{
    internal static class MingyuanStatUtility
    {
        public static float LifeBurnPenaltyFactor(float lifeBurn)
        {
            if (lifeBurn <= 0f)
            {
                return 1f;
            }

            return Mathf.Max(0.05f, 1f - lifeBurn * 0.0001f);
        }
    }

    public class CompProperties_MingyuanDamageResponder : CompProperties
    {
        public CompProperties_MingyuanDamageResponder()
        {
            compClass = typeof(CompMingyuanDamageResponder);
        }
    }

    public class CompMingyuanDamageResponder : ThingComp
    {
        public override void PostPreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            absorbed = false;
            Pawn pawn = parent as Pawn;
            if (pawn == null || pawn.Dead || pawn.health?.hediffSet == null)
            {
                return;
            }

            HediffComp_MingyuanBurningBody body =
                (pawn.health.hediffSet.GetFirstHediffOfDef(MingyuanUtility.BurningBodyDef) as HediffWithComps)
                ?.GetComp<HediffComp_MingyuanBurningBody>();
            if (body == null)
            {
                return;
            }

            if (body.Invulnerable)
            {
                absorbed = true;
                return;
            }

            if (MingyuanUtility.IsHeatOrExplosionDamage(dinfo.Def))
            {
                absorbed = true;
                MingyuanUtility.RestorePawnToBestCondition(pawn, true);
            }
        }
    }

    public class Hediff_MingyuanBurningBody : HediffWithComps
    {
        public override void Notify_PawnDamagedThing(Thing thing, DamageInfo dinfo, DamageWorker.DamageResult result)
        {
            base.Notify_PawnDamagedThing(thing, dinfo, result);
            if (MingyuanUtility.SuppressOnHitLifeBurn || result == null || result.totalDamageDealt <= 0f)
            {
                return;
            }

            Pawn target = thing as Pawn;
            if (target == null || target == pawn || target.Dead || !target.HostileTo(pawn))
            {
                return;
            }

            HediffComp_MingyuanBurningBody body = GetComp<HediffComp_MingyuanBurningBody>();
            if (body == null)
            {
                return;
            }

            bool ranged = dinfo.Weapon?.IsRangedWeapon == true;
            float bonusSteps = MingyuanUtility.GetLifeBurnBonusStep(pawn);
            float baseLayers = ranged ? body.PropsBody.rangedLifeBurnLayers : body.PropsBody.meleeLifeBurnLayers;
            float bonusPer100 = ranged ? body.PropsBody.rangedSelfBurnBonusPer100 : body.PropsBody.meleeSelfBurnBonusPer100;
            MingyuanUtility.AddLifeBurn(target, pawn, baseLayers + bonusSteps * bonusPer100, scaleWithOverburn: true);
        }
    }

    public class StatPart_MingyuanRangedDamage : StatPart
    {
        public override void TransformValue(StatRequest req, ref float val)
        {
            val *= FactorFor(req);
        }

        public override string ExplanationPart(StatRequest req)
        {
            float factor = FactorFor(req);
            if (Mathf.Approximately(factor, 1f))
            {
                return null;
            }

            return "MX_Mingyuan_RangedDamage_StatPart".Translate(factor.ToStringPercent()).ToString();
        }

        private static float FactorFor(StatRequest req)
        {
            Pawn owner = MXNeiyuShieldUtility.TryGetEquipmentOwnerPawn(req.Thing);
            if (owner == null)
            {
                return 1f;
            }

            float factor = MingyuanStatUtility.LifeBurnPenaltyFactor(MingyuanUtility.GetLifeBurnLayers(owner));
            factor *= MingyuanUtility.GetSelfBurnRangedWeaponDamageFactor(owner);
            factor *= MingyuanUtility.GetOverburnDamageFactor(owner);
            return factor;
        }
    }
}

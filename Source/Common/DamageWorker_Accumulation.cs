using RimWorld;
using Verse;

namespace MiliraXian.Characters
{
    public class DamageWorker_Accumulation : DamageWorker
    {
        public override DamageResult Apply(DamageInfo dinfo, Thing victim)
        {
            DamageResult result = new DamageResult();
            Pawn pawn = victim as Pawn;
            if (pawn == null || dinfo.Amount <= 0f)
            {
                return result;
            }

            MX_AccumulationDamageProperties props = def.GetModExtension<MX_AccumulationDamageProperties>();
            if (props == null)
            {
                return result;
            }

            HediffDef accumulationHediff = ResolveAccumulationHediff(pawn, props);
            if (accumulationHediff == null)
            {
                return result;
            }

            BodyPartRecord hitPart = ResolveHitPart(dinfo, pawn);
            if (hitPart == null)
            {
                return result;
            }

            dinfo.SetHitPart(hitPart);
            float effectiveAmount = dinfo.Amount;
            bool deflectedByMetalArmor = false;
            bool diminishedByMetalArmor;
            if (!dinfo.IgnoreArmor)
            {
                DamageDef damageDef = dinfo.Def;
                effectiveAmount = ArmorUtility.GetPostArmorDamage(pawn, effectiveAmount, dinfo.ArmorPenetrationInt, hitPart, ref damageDef, out deflectedByMetalArmor, out diminishedByMetalArmor);
                dinfo.Def = damageDef;
                if (effectiveAmount < dinfo.Amount)
                {
                    result.diminished = true;
                    result.diminishedByMetalArmor = diminishedByMetalArmor;
                }
            }

            result.AddPart(pawn, hitPart);
            if (effectiveAmount <= 0f)
            {
                result.deflected = true;
                result.deflectedByMetalArmor = deflectedByMetalArmor;
                return result;
            }

            float severityOffset = effectiveAmount * props.severityMultiplier;
            if (severityOffset <= 0f)
            {
                return result;
            }

            Pawn caster = dinfo.Instigator as Pawn;
            if (AccumulationUtility.TryApplyAccumulation(caster, pawn, accumulationHediff, severityOffset, out float finalSeverityOffset, out Hediff appliedHediff))
            {
                NotifyAccumulationApplied(caster, appliedHediff, finalSeverityOffset);
            }

            return result;
        }

        private static HediffDef ResolveAccumulationHediff(Pawn pawn, MX_AccumulationDamageProperties props)
        {
            if (pawn?.RaceProps?.IsMechanoid == true)
            {
                return props.mechAccumulationHediff ?? props.accumulationHediff;
            }

            return props.accumulationHediff;
        }

        protected virtual void NotifyAccumulationApplied(Pawn caster, Hediff appliedHediff, float finalSeverityOffset)
        {
            HediffWithComps hediffWithComps = appliedHediff as HediffWithComps;
            if (hediffWithComps?.comps == null)
            {
                return;
            }

            for (int i = 0; i < hediffWithComps.comps.Count; i++)
            {
                HediffComp_OnAccumulated comp = hediffWithComps.comps[i] as HediffComp_OnAccumulated;
                comp?.NotifyAccumulationApplied(caster, finalSeverityOffset);
            }
        }

        private static BodyPartRecord ResolveHitPart(DamageInfo dinfo, Pawn pawn)
        {
            if (dinfo.HitPart != null && pawn.health.hediffSet.HasBodyPart(dinfo.HitPart))
            {
                return dinfo.HitPart;
            }

            return pawn.health.hediffSet.GetRandomNotMissingPart(dinfo.Def, dinfo.Height, dinfo.Depth);
        }
    }

}

using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan
{
    public static class MingyuanUtility
    {
        public const string PawnKindDefName = "MiliraXian_Mingyuan";
        public const string NeiyuPawnKindDefName = "MiliraXian_Neiyu";
        public const string QinghePawnKindDefName = "MiliraXian_Qinghe";
        public const string ZhaoliPawnKindDefName = "MiliraXian_Zhaoli";
        public const int TicksPerHour = 2500;
        public const float DefaultSelfBurnEffectiveCap = 300f;

        public static bool SuppressOnHitLifeBurn;

        public static HediffDef LifeBurnDef => MX_MingyuanDefOf.MX_Mingyuan_LifeBurn ?? DefDatabase<HediffDef>.GetNamedSilentFail("MX_Mingyuan_LifeBurn");
        public static HediffDef SelfBurnDef => MX_MingyuanDefOf.MX_Mingyuan_SelfBurn ?? DefDatabase<HediffDef>.GetNamedSilentFail("MX_Mingyuan_SelfBurn");
        public static HediffDef BurningBodyDef => MX_MingyuanDefOf.MX_Mingyuan_BurningBody ?? DefDatabase<HediffDef>.GetNamedSilentFail("MX_Mingyuan_BurningBody");
        public static HediffDef ShieldDef => MX_MingyuanDefOf.MX_Mingyuan_ProtectiveFlameShield ?? DefDatabase<HediffDef>.GetNamedSilentFail("MX_Mingyuan_ProtectiveFlameShield");
        public static HediffDef RebirthDef => MX_MingyuanDefOf.MX_Mingyuan_RebirthFlame ?? DefDatabase<HediffDef>.GetNamedSilentFail("MX_Mingyuan_RebirthFlame");
        public static HediffDef EternalBurningDef => MX_MingyuanDefOf.MX_Mingyuan_EternalBurning ?? DefDatabase<HediffDef>.GetNamedSilentFail("MX_Mingyuan_EternalBurning");
        public static HediffDef TimeBurnFrozenDef => MX_MingyuanDefOf.MX_Mingyuan_TimeBurnFrozen ?? DefDatabase<HediffDef>.GetNamedSilentFail("MX_Mingyuan_TimeBurnFrozen");

        public static bool IsMingyuan(Pawn pawn)
        {
            return pawn != null
                   && !pawn.Destroyed
                   && (pawn.kindDef?.defName == PawnKindDefName || HasHediff(pawn, BurningBodyDef));
        }

        public static bool IsLifeBurnImmunePawn(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed)
            {
                return false;
            }

            string defName = pawn.kindDef?.defName;
            return defName == PawnKindDefName
                   || defName == NeiyuPawnKindDefName
                   || defName == QinghePawnKindDefName
                   || defName == ZhaoliPawnKindDefName
                   || HasHediff(pawn, BurningBodyDef);
        }

        public static bool HasHediff(Pawn pawn, HediffDef def)
        {
            return pawn?.health?.hediffSet != null
                   && def != null
                   && pawn.health.hediffSet.GetFirstHediffOfDef(def) != null;
        }

        public static Hediff EnsureHediff(Pawn pawn, HediffDef def, float severityToAdd = 0f)
        {
            if (pawn?.health == null || def == null || pawn.Dead)
            {
                return null;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (hediff == null)
            {
                hediff = HediffMaker.MakeHediff(def, pawn);
                hediff.Severity = Mathf.Abs(severityToAdd) > 0.0001f ? 0f : Mathf.Max(def.initialSeverity, 0.0001f);
                pawn.health.AddHediff(hediff);
            }

            if (Mathf.Abs(severityToAdd) > 0.0001f)
            {
                hediff.Severity = Mathf.Clamp(hediff.Severity + severityToAdd, 0f, def.maxSeverity);
                pawn.health.Notify_HediffChanged(hediff);
            }

            return hediff;
        }

        public static void AddLifeBurn(Pawn target, Pawn instigator, float layers, bool refreshDecayTimer = true, bool scaleWithOverburn = false)
        {
            if (target == null || layers <= 0f || target.Dead || IsLifeBurnImmunePawn(target))
            {
                return;
            }

            if (scaleWithOverburn)
            {
                layers *= GetOverburnLifeBurnFactor(instigator);
            }

            Hediff hediff = EnsureHediff(target, LifeBurnDef, layers);
            HediffComp_MingyuanLifeBurn comp = (hediff as HediffWithComps)?.GetComp<HediffComp_MingyuanLifeBurn>();
            comp?.NotifyLifeBurnStack(instigator, refreshDecayTimer);
        }

        public static void AddSelfBurn(Pawn pawn, float layers, bool refreshDecayTimer = true, bool showMote = true)
        {
            if (pawn == null || layers <= 0f || pawn.Dead)
            {
                return;
            }

            Hediff hediff = EnsureHediff(pawn, SelfBurnDef, layers);
            HediffComp_MingyuanSelfBurn comp = (hediff as HediffWithComps)?.GetComp<HediffComp_MingyuanSelfBurn>();
            comp?.NotifySelfBurnStack(refreshDecayTimer, showMote);
        }

        public static Hediff EnsureSelfBurnTracker(Pawn pawn)
        {
            if (pawn?.health == null || pawn.Dead || SelfBurnDef == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(SelfBurnDef);
            if (hediff != null)
            {
                return hediff;
            }

            hediff = HediffMaker.MakeHediff(SelfBurnDef, pawn);
            hediff.Severity = 0f;
            pawn.health.AddHediff(hediff);
            return hediff;
        }

        public static float ReduceSelfBurn(Pawn pawn, float layers)
        {
            if (pawn?.health == null || layers <= 0f)
            {
                return 0f;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(SelfBurnDef);
            if (hediff == null || hediff.Severity <= 0f)
            {
                return 0f;
            }

            float reduced = Mathf.Min(layers, hediff.Severity);
            hediff.Severity = Mathf.Max(0f, hediff.Severity - reduced);
            pawn.health.Notify_HediffChanged(hediff);
            return reduced;
        }

        public static float GetLifeBurnLayers(Pawn pawn)
        {
            if (IsLifeBurnImmunePawn(pawn))
            {
                return 0f;
            }

            return pawn?.health?.hediffSet?.GetFirstHediffOfDef(LifeBurnDef)?.Severity ?? 0f;
        }

        public static float GetSelfBurnLayers(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.GetFirstHediffOfDef(SelfBurnDef)?.Severity ?? 0f;
        }

        public static float GetSelfBurnEffectiveLayers(Pawn pawn)
        {
            Hediff hediff = pawn?.health?.hediffSet?.GetFirstHediffOfDef(SelfBurnDef);
            float layers = hediff?.Severity ?? 0f;
            if (layers <= 0f)
            {
                return 0f;
            }

            HediffComp_MingyuanSelfBurn comp = (hediff as HediffWithComps)?.GetComp<HediffComp_MingyuanSelfBurn>();
            float cap = comp?.PropsSelfBurn.effectiveBonusCap ?? DefaultSelfBurnEffectiveCap;
            return Mathf.Min(layers, Mathf.Max(0f, cap));
        }

        public static float GetSelfBurnOverburnThreshold(Pawn pawn)
        {
            Hediff hediff = pawn?.health?.hediffSet?.GetFirstHediffOfDef(SelfBurnDef);
            HediffComp_MingyuanSelfBurn comp = (hediff as HediffWithComps)?.GetComp<HediffComp_MingyuanSelfBurn>();
            return comp?.PropsSelfBurn.overburnThreshold ?? DefaultSelfBurnEffectiveCap;
        }

        public static bool IsOverburning(Pawn pawn)
        {
            return GetSelfBurnLayers(pawn) > GetSelfBurnOverburnThreshold(pawn);
        }

        public static float GetOverburnLayers(Pawn pawn)
        {
            return Mathf.Max(0f, GetSelfBurnLayers(pawn) - GetSelfBurnOverburnThreshold(pawn));
        }

        public static float GetOverburnDamageFactor(Pawn pawn)
        {
            if (!IsOverburning(pawn))
            {
                return 1f;
            }

            Hediff hediff = pawn?.health?.hediffSet?.GetFirstHediffOfDef(SelfBurnDef);
            HediffComp_MingyuanSelfBurn comp = (hediff as HediffWithComps)?.GetComp<HediffComp_MingyuanSelfBurn>();
            return Mathf.Max(1f, comp?.PropsSelfBurn.overburnDamageFactor ?? 2f);
        }

        public static float GetOverburnLifeBurnFactor(Pawn pawn)
        {
            if (!IsOverburning(pawn))
            {
                return 1f;
            }

            Hediff hediff = pawn?.health?.hediffSet?.GetFirstHediffOfDef(SelfBurnDef);
            HediffComp_MingyuanSelfBurn comp = (hediff as HediffWithComps)?.GetComp<HediffComp_MingyuanSelfBurn>();
            return Mathf.Max(1f, comp?.PropsSelfBurn.overburnLifeBurnFactor ?? 2f);
        }

        public static float GetLifeBurnBonusStep(Pawn pawn)
        {
            return Mathf.Floor(GetSelfBurnEffectiveLayers(pawn) / 100f);
        }

        public static float GetSelfBurnSkillDamageFactor(Pawn pawn)
        {
            float selfBurn = GetSelfBurnEffectiveLayers(pawn);
            return selfBurn > 0f ? 1f + selfBurn * 0.01f : 1f;
        }

        public static float GetSelfBurnRangedWeaponDamageFactor(Pawn pawn)
        {
            float selfBurn = GetSelfBurnEffectiveLayers(pawn);
            if (selfBurn <= 0f)
            {
                return 1f;
            }

            Hediff hediff = pawn?.health?.hediffSet?.GetFirstHediffOfDef(SelfBurnDef);
            HediffComp_MingyuanSelfBurn comp = (hediff as HediffWithComps)?.GetComp<HediffComp_MingyuanSelfBurn>();
            float perLayer = Mathf.Max(0f, comp?.PropsSelfBurn.rangedWeaponDamagePerLayer ?? 0.002f);
            float cap = Mathf.Max(0f, comp?.PropsSelfBurn.rangedWeaponDamageBonusCap ?? 0.6f);
            return 1f + Mathf.Min(selfBurn * perLayer, cap);
        }

        public static bool IsHostilePawn(Thing thing, Pawn caster, out Pawn pawn)
        {
            pawn = thing as Pawn;
            return pawn != null
                   && !pawn.Dead
                   && caster != null
                   && pawn != caster
                   && pawn.HostileTo(caster);
        }

        public static DamageWorker.DamageResult ApplyTrueDamage(Thing target, DamageDef damageDef, float amount, Pawn instigator = null, BodyPartRecord hitPart = null, bool scaleWithSelfBurn = false)
        {
            if (target == null || target.Destroyed || amount <= 0f)
            {
                return null;
            }

            if (scaleWithSelfBurn)
            {
                amount *= GetSelfBurnSkillDamageFactor(instigator);
                amount *= GetOverburnDamageFactor(instigator);
            }

            DamageInfo dinfo = new DamageInfo(damageDef ?? DamageDefOf.Burn, amount, 999f, -1f, instigator, hitPart);
            dinfo.SetIgnoreArmor(true);
            dinfo.SetIgnoreInstantKillProtection(true);
            dinfo.SetApplyAllDamage(true);
            bool previousSuppression = SuppressOnHitLifeBurn;
            try
            {
                SuppressOnHitLifeBurn = true;
                return target.TakeDamage(dinfo);
            }
            finally
            {
                SuppressOnHitLifeBurn = previousSuppression;
            }
        }

        public static bool TryMakeAttachedMote(Thing target, ThingDef moteDef, float scale = 1f)
        {
            if (target == null || target.Destroyed || !target.Spawned || target.MapHeld == null || moteDef == null)
            {
                return false;
            }

            Mote mote = MoteMaker.MakeAttachedOverlay(target, moteDef, Vector3.zero, Mathf.Max(0.1f, scale));
            if (mote == null)
            {
                return false;
            }

            mote.exactRotation = Rand.Range(0f, 360f);
            return true;
        }

        public static bool TryMakeStaticMote(IntVec3 cell, Map map, ThingDef moteDef, float scale = 1f)
        {
            if (map == null || moteDef == null || !cell.IsValid || !cell.InBounds(map))
            {
                return false;
            }

            Mote mote = MoteMaker.MakeStaticMote(cell.ToVector3Shifted(), map, moteDef, Mathf.Max(0.1f, scale), false, Rand.Range(0f, 360f));
            if (mote == null)
            {
                return false;
            }

            mote.exactPosition = cell.ToVector3Shifted();
            return true;
        }

        public static void HealInjuriesIncludingScars(Pawn pawn, float amount)
        {
            if (pawn?.health?.hediffSet == null || pawn.Dead || amount <= 0f)
            {
                return;
            }

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = hediffs.Count - 1; i >= 0 && amount > 0f; i--)
            {
                Hediff_Injury injury = hediffs[i] as Hediff_Injury;
                if (injury == null || injury.Severity <= 0f)
                {
                    continue;
                }

                float heal = Mathf.Min(amount, injury.Severity);
                injury.Heal(heal);
                amount -= heal;
            }
        }

        public static bool IsHeatOrExplosionDamage(DamageDef def)
        {
            return def != null
                   && (def.isExplosive || def.armorCategory?.armorRatingStat == StatDefOf.ArmorRating_Heat);
        }

        public static void ClearControlStates(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            pawn.stances?.stunner?.StopStun();
            pawn.mindState?.mentalStateHandler?.Reset();
            Thing fire = pawn.GetAttachment(ThingDefOf.Fire);
            if (fire != null && !fire.Destroyed)
            {
                fire.Destroy(DestroyMode.Vanish);
            }
        }

        public static void RestorePawnToBestCondition(Pawn pawn, bool keepLifeBurn)
        {
            if (pawn?.health?.hediffSet == null || pawn.Dead)
            {
                return;
            }

            List<Hediff_MissingPart> missingParts = new List<Hediff_MissingPart>();
            foreach (Hediff_MissingPart missingPart in pawn.health.hediffSet.GetMissingPartsCommonAncestors())
            {
                if (missingPart.Part != null)
                {
                    missingParts.Add(missingPart);
                }
            }

            for (int i = 0; i < missingParts.Count; i++)
            {
                pawn.health.RestorePart(missingParts[i].Part);
            }

            List<Hediff> toRemove = new List<Hediff>();
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            HediffDef lifeBurn = LifeBurnDef;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (hediff == null || (keepLifeBurn && hediff.def == lifeBurn))
                {
                    continue;
                }

                if (hediff is Hediff_Injury
                    || hediff is Hediff_MissingPart
                    || hediff.def.isBad
                    || hediff.def.defName == "BloodLoss")
                {
                    toRemove.Add(hediff);
                }
            }

            for (int i = 0; i < toRemove.Count; i++)
            {
                pawn.health.RemoveHediff(toRemove[i]);
            }
        }

        public static IntVec3 FindStandableCellNear(IntVec3 center, Map map, int radius)
        {
            if (map == null)
            {
                return center;
            }

            if (center.InBounds(map) && center.Standable(map))
            {
                return center;
            }

            foreach (IntVec3 cell in GenRadial.RadialCellsAround(center, radius, true))
            {
                if (cell.InBounds(map) && cell.Standable(map))
                {
                    return cell;
                }
            }

            return center;
        }

        public static bool IsAlivePawn(Thing thing)
        {
            Pawn pawn = thing as Pawn;
            return pawn != null && !pawn.Dead && !pawn.Destroyed;
        }
    }
}

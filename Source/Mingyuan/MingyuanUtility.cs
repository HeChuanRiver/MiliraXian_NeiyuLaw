using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Mingyuan
{
    public static class MingyuanUtility
    {
        public const string PawnKindDefName = "MiliraXian_Mingyuan";
        public const int TicksPerHour = 2500;

        private static readonly HashSet<DamageDef> HeatOrBlastDefs = new HashSet<DamageDef>();

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

        public static void AddLifeBurn(Pawn target, Pawn instigator, float layers, bool refreshDecayTimer = true)
        {
            if (target == null || layers <= 0f || target.Dead)
            {
                return;
            }

            Hediff hediff = EnsureHediff(target, LifeBurnDef, layers);
            HediffComp_MingyuanLifeBurn comp = (hediff as HediffWithComps)?.GetComp<HediffComp_MingyuanLifeBurn>();
            comp?.NotifyLifeBurnStack(instigator, refreshDecayTimer);
        }

        public static void AddSelfBurn(Pawn pawn, float layers)
        {
            if (pawn == null || layers <= 0f || pawn.Dead)
            {
                return;
            }

            EnsureHediff(pawn, SelfBurnDef, layers);
        }

        public static float GetLifeBurnLayers(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.GetFirstHediffOfDef(LifeBurnDef)?.Severity ?? 0f;
        }

        public static float GetSelfBurnLayers(Pawn pawn)
        {
            return pawn?.health?.hediffSet?.GetFirstHediffOfDef(SelfBurnDef)?.Severity ?? 0f;
        }

        public static float GetLifeBurnBonusStep(Pawn pawn)
        {
            return Mathf.Floor(GetSelfBurnLayers(pawn) / 100f);
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

        public static DamageWorker.DamageResult ApplyTrueDamage(Thing target, DamageDef damageDef, float amount, Pawn instigator = null, BodyPartRecord hitPart = null)
        {
            if (target == null || target.Destroyed || amount <= 0f)
            {
                return null;
            }

            DamageInfo dinfo = new DamageInfo(damageDef ?? DamageDefOf.Burn, amount, 999f, -1f, instigator, hitPart);
            dinfo.SetIgnoreArmor(true);
            dinfo.SetIgnoreInstantKillProtection(true);
            dinfo.SetApplyAllDamage(true);
            try
            {
                SuppressOnHitLifeBurn = true;
                return target.TakeDamage(dinfo);
            }
            finally
            {
                SuppressOnHitLifeBurn = false;
            }
        }

        public static bool IsHeatOrExplosionDamage(DamageDef def)
        {
            if (def == null)
            {
                return false;
            }

            if (HeatOrBlastDefs.Count == 0)
            {
                AddDamageDef("Burn");
                AddDamageDef("Flame");
                AddDamageDef("Bomb");
                AddDamageDef("Explosion");
            }

            return HeatOrBlastDefs.Contains(def)
                   || def.defName.IndexOf("Burn", System.StringComparison.OrdinalIgnoreCase) >= 0
                   || def.defName.IndexOf("Flame", System.StringComparison.OrdinalIgnoreCase) >= 0
                   || def.defName.IndexOf("Bomb", System.StringComparison.OrdinalIgnoreCase) >= 0
                   || def.defName.IndexOf("Explosion", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static void AddDamageDef(string defName)
        {
            DamageDef def = DefDatabase<DamageDef>.GetNamedSilentFail(defName);
            if (def != null)
            {
                HeatOrBlastDefs.Add(def);
            }
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

using System.Collections.Generic;
using System.Runtime.CompilerServices;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.Zhaoli
{
    public static class ZhaoliShieldLayerUtility
    {
        public const string ShieldHediffDefName = "MXZL_ZhaoliShieldLayers";
        public const int ShieldLayersPerExecution = 5;

        private sealed class ShieldCacheEntry
        {
            public ShieldCacheEntry(HediffComp_ZhaoliShieldLayers comp)
            {
                Comp = comp;
            }

            public HediffComp_ZhaoliShieldLayers Comp { get; }
        }

        private static ConditionalWeakTable<Pawn, ShieldCacheEntry> ShieldByPawn =
            new();

        private static HediffDef shieldHediffDef;

        public static HediffComp_ZhaoliShieldLayers GetShieldComp(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return null;
            }

            if (ShieldByPawn.TryGetValue(pawn, out ShieldCacheEntry cached) && cached.Comp != null)
            {
                return cached.Comp;
            }

            HediffDef hediffDef = shieldHediffDef ?? (shieldHediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(ShieldHediffDefName));
            if (hediffDef == null)
            {
                return null;
            }

            HediffWithComps hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef) as HediffWithComps;
            HediffComp_ZhaoliShieldLayers comp = hediff?.GetComp<HediffComp_ZhaoliShieldLayers>();
            Cache(pawn, comp);
            return comp;
        }

        public static HediffComp_ZhaoliShieldLayers EnsureShieldComp(Pawn pawn)
        {
            HediffComp_ZhaoliShieldLayers comp = GetShieldComp(pawn);
            if (comp != null || pawn?.health == null)
            {
                return comp;
            }

            HediffDef hediffDef = shieldHediffDef ?? (shieldHediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(ShieldHediffDefName));
            if (hediffDef == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.GetOrAddHediff(hediffDef);
            pawn.health.Notify_HediffChanged(hediff);
            comp = (hediff as HediffWithComps)?.GetComp<HediffComp_ZhaoliShieldLayers>();
            Cache(pawn, comp);
            return comp;
        }

        public static void AddLayers(Pawn pawn, int layerCount)
        {
            if (layerCount <= 0)
            {
                return;
            }

            EnsureShieldComp(pawn)?.AddLayers(layerCount);
        }

        public static void Invalidate(Pawn pawn)
        {
            if (pawn != null)
            {
                ShieldByPawn.Remove(pawn);
            }
        }

        internal static void ClearCache()
        {
            ShieldByPawn = new ConditionalWeakTable<Pawn, ShieldCacheEntry>();
        }

        private static void Cache(Pawn pawn, HediffComp_ZhaoliShieldLayers comp)
        {
            if (pawn == null || comp == null)
            {
                return;
            }

            ShieldByPawn.Remove(pawn);
            ShieldByPawn.Add(pawn, new ShieldCacheEntry(comp));
        }
    }

    public class HediffCompProperties_ZhaoliShieldLayers : HediffCompProperties
    {
        public bool showGizmo = true;
        public bool drawActiveShield = true;
        public bool drawOnlyInCombat = true;
        public string activeShieldTexPath = "MiliraXianZhaoli/Effect/Zhaoli_Shield/Shield";
        public Vector2 activeShieldDrawSize = new(1.55f, 1.55f);
        public float activeShieldAlpha = 0.62f;
        public float activeShieldAltitudeOffset = 0.5f;
        public float activeShieldPulseMin = 0.98f;
        public float activeShieldPulseMax = 1.03f;
        public int activeShieldPulseTicks = 90;
        public float activeShieldRotationDegreesPerTick = 0.15f;

        public HediffCompProperties_ZhaoliShieldLayers()
        {
            compClass = typeof(HediffComp_ZhaoliShieldLayers);
        }
    }

    public class HediffComp_ZhaoliShieldLayers : HediffComp
    {
        private int shieldLayers;

        public HediffCompProperties_ZhaoliShieldLayers PropsShield => (HediffCompProperties_ZhaoliShieldLayers)props;

        public int ShieldLayers => shieldLayers;

        public bool ShouldDrawActiveShield
        {
            get
            {
                if (!PropsShield.drawActiveShield || Pawn == null || !Pawn.Spawned || Pawn.Dead || shieldLayers <= 0)
                {
                    return false;
                }

                return !PropsShield.drawOnlyInCombat || Pawn.Drafted;
            }
        }

        public float ActiveShieldPulseScale
        {
            get
            {
                int period = Mathf.Max(1, PropsShield.activeShieldPulseTicks);
                float min = Mathf.Min(PropsShield.activeShieldPulseMin, PropsShield.activeShieldPulseMax);
                float max = Mathf.Max(PropsShield.activeShieldPulseMin, PropsShield.activeShieldPulseMax);
                float t = (Find.TickManager.TicksGame % period) / (float)period;
                float wave = 0.5f + 0.5f * Mathf.Sin(t * Mathf.PI * 2f);
                return Mathf.Lerp(min, max, wave);
            }
        }

        public float ActiveShieldAngle
        {
            get
            {
                return Find.TickManager.TicksGame * PropsShield.activeShieldRotationDegreesPerTick % 360f;
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref shieldLayers, "mxzl_shieldLayers", 0);
        }

        public override void CompPostPostRemoved()
        {
            ZhaoliShieldLayerUtility.Invalidate(Pawn);
            base.CompPostPostRemoved();
        }

        public override bool CompDisallowVisible()
        {
            return false;
        }

        public override IEnumerable<Gizmo> CompGetGizmos()
        {
            yield break;
        }

        public void AddLayers(int layerCount)
        {
            shieldLayers = Mathf.Max(0, shieldLayers + layerCount);
        }

        public override void Notify_PawnPostApplyDamage(DamageInfo dinfo, float totalDamageDealt)
        {
            base.Notify_PawnPostApplyDamage(dinfo, totalDamageDealt);
            if (Pawn == null || Pawn.Dead || shieldLayers <= 0 || totalDamageDealt <= 0f)
            {
                return;
            }

            shieldLayers = Mathf.Max(0, shieldLayers - 1);
            PlayShieldHitFx();
        }

        private void PlayShieldHitFx()
        {
            if (Pawn == null || !Pawn.Spawned || Pawn.Map == null)
            {
                return;
            }

            SoundDefOf.EnergyShield_AbsorbDamage.PlayOneShot(new TargetInfo(Pawn.Position, Pawn.Map));
            FleckMaker.Static(Pawn.TrueCenter(), Pawn.Map, FleckDefOf.PsycastAreaEffect, 1.1f);
            FleckMaker.Static(Pawn.TrueCenter(), Pawn.Map, FleckDefOf.FlashHollow, 0.85f);
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                return "MX_ZL_ShieldLayersLabel".Translate(shieldLayers).ToString();
            }
        }

        public override string CompDescriptionExtra
        {
            get
            {
            return "MX_ZL_ShieldLayersTip".Translate(shieldLayers).ToString();
            }
        }
    }

}

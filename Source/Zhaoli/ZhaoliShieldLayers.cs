using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Zhaoli
{
    public static class ZhaoliShieldLayerUtility
    {
        public const string ShieldHediffDefName = "MXZL_ZhaoliShieldLayers";
        public const int ShieldLayersPerExecution = 5;

        public static HediffComp_ZhaoliShieldLayers GetShieldComp(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return null;
            }

            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(ShieldHediffDefName);
            if (hediffDef == null)
            {
                return null;
            }

            HediffWithComps hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef) as HediffWithComps;
            return hediff?.GetComp<HediffComp_ZhaoliShieldLayers>();
        }

        public static HediffComp_ZhaoliShieldLayers EnsureShieldComp(Pawn pawn)
        {
            HediffComp_ZhaoliShieldLayers comp = GetShieldComp(pawn);
            if (comp != null || pawn?.health == null)
            {
                return comp;
            }

            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(ShieldHediffDefName);
            if (hediffDef == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.GetOrAddHediff(hediffDef);
            pawn.health.Notify_HediffChanged(hediff);
            return (hediff as HediffWithComps)?.GetComp<HediffComp_ZhaoliShieldLayers>();
        }

        public static void AddLayers(Pawn pawn, int layerCount)
        {
            if (layerCount <= 0)
            {
                return;
            }

            EnsureShieldComp(pawn)?.AddLayers(layerCount);
        }
    }

    public class HediffCompProperties_ZhaoliShieldLayers : HediffCompProperties
    {
        public bool showGizmo = true;

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

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref shieldLayers, "mxzl_shieldLayers", 0);
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

        public bool TryAbsorb(ref DamageInfo dinfo, ref bool absorbed)
        {
            if (absorbed || Pawn == null || Pawn.Dead || shieldLayers <= 0)
            {
                return false;
            }

            shieldLayers = Mathf.Max(0, shieldLayers - 1);
            absorbed = true;
            return true;
        }

        public override string CompLabelInBracketsExtra
        {
            get
            {
                return shieldLayers + "层";
            }
        }

        public override string CompDescriptionExtra
        {
            get
            {
                return "当前层数：" + shieldLayers + "。每通过告死吸纳一个单位，获得5层讳亡护盾。护盾会免疫一次伤害，并在命中时消耗1层。持续伤害每次生效也会持续消耗层数。";
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    public static class Patch_ZhaoliShieldLayers_PreApplyDamage
    {
        [HarmonyPostfix]
        public static void Postfix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (absorbed || !ZhaoliKarmaUtility.IsZhaoli(__instance))
            {
                return;
            }

            HediffComp_ZhaoliShieldLayers shieldComp = ZhaoliShieldLayerUtility.GetShieldComp(__instance);
            shieldComp?.TryAbsorb(ref dinfo, ref absorbed);
        }
    }
}

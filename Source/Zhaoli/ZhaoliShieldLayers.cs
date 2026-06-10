using System.Collections.Generic;
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
        public bool drawActiveShield = true;
        public bool drawOnlyInCombat = true;
        public string activeShieldTexPath = "MiliraXianZhaoli/Effect/Zhaoli_Shield/Shield";
        public Vector2 activeShieldDrawSize = new Vector2(1.55f, 1.55f);
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
            PlayAbsorbFx();
            return true;
        }

        private void PlayAbsorbFx()
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

    [StaticConstructorOnStartup]
    [HarmonyPatch(typeof(PawnRenderer), nameof(PawnRenderer.RenderPawnAt))]
    public static class Patch_ZhaoliShieldLayers_Draw
    {
        private static readonly AccessTools.FieldRef<PawnRenderer, Pawn> PawnRef =
            AccessTools.FieldRefAccess<PawnRenderer, Pawn>("pawn");

        private static readonly Dictionary<string, Material> ShieldMaterialByPath = new Dictionary<string, Material>();

        [HarmonyPostfix]
        public static void Postfix(PawnRenderer __instance, Vector3 drawLoc, Rot4? rotOverride = null, bool neverAimWeapon = false)
        {
            if (__instance == null)
            {
                return;
            }

            Pawn pawn = PawnRef(__instance);
            HediffComp_ZhaoliShieldLayers shield = ZhaoliShieldLayerUtility.GetShieldComp(pawn);
            if (shield == null || !shield.ShouldDrawActiveShield)
            {
                return;
            }

            string texPath = shield.PropsShield.activeShieldTexPath;
            if (texPath.NullOrEmpty())
            {
                return;
            }

            Material shieldMat = GetShieldMaterial(texPath, shield.PropsShield.activeShieldAlpha);
            if (shieldMat == null)
            {
                return;
            }

            float pulseScale = shield.ActiveShieldPulseScale;
            Vector2 drawSize = shield.PropsShield.activeShieldDrawSize;

            Vector3 pos = drawLoc;
            pos.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            pos += Altitudes.AltIncVect * shield.PropsShield.activeShieldAltitudeOffset;

            Matrix4x4 matrix = Matrix4x4.TRS(
                pos,
                Quaternion.AngleAxis(shield.ActiveShieldAngle, Vector3.up),
                new Vector3(drawSize.x * pulseScale, 1f, drawSize.y * pulseScale));

            Graphics.DrawMesh(MeshPool.plane10, matrix, shieldMat, 0);
        }

        private static Material GetShieldMaterial(string texPath, float alpha)
        {
            string cacheKey = texPath + "|" + Mathf.Clamp01(alpha).ToString("F3");
            Material shieldMat;
            if (ShieldMaterialByPath.TryGetValue(cacheKey, out shieldMat))
            {
                return shieldMat;
            }

            shieldMat = MaterialPool.MatFrom(texPath, ShaderDatabase.Transparent, new Color(1f, 1f, 1f, Mathf.Clamp01(alpha)));
            ShieldMaterialByPath[cacheKey] = shieldMat;
            return shieldMat;
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

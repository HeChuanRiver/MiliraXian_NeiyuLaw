using System;
using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Zhaoli
{
    public class CompProperties_AbilityZhaoliMinghuo : CompProperties_AbilityEffect
    {
        public float karmaCost = 3f;
        public int durationTicks = 60000;
        public float overlayScale = 1.8f;

        public CompProperties_AbilityZhaoliMinghuo()
        {
            compClass = typeof(CompAbilityEffect_ZhaoliMinghuo);
        }
    }

    public class HediffCompProperties_ZhaoliMinghuo : HediffCompProperties
    {
        public float damageMultiplier = 1.5f;
        public float armorPenetrationMultiplier = 1.5f;
        public float hitChanceMultiplier = 1.5f;
        public float attackSpeedMultiplier = 1.5f;
        public float rangeOffset = 2f;
        public float fireDamageFactor = 1f;

        public HediffCompProperties_ZhaoliMinghuo()
        {
            compClass = typeof(HediffComp_ZhaoliMinghuo);
        }
    }

    public class CompAbilityEffect_ZhaoliMinghuo : CompAbilityEffect
    {
        private new CompProperties_AbilityZhaoliMinghuo Props => (CompProperties_AbilityZhaoliMinghuo)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            if (caster == null)
            {
                return;
            }

            ThingWithComps weapon = caster.equipment?.Primary;
            if (weapon == null)
            {
                return;
            }

            if (!ZhaoliKarmaUtility.TryConsumeKarma(caster, Props.karmaCost))
            {
                if (caster.Faction == Faction.OfPlayer)
                {
                    Messages.Message("因果不足，无法施放冥火。", caster, MessageTypeDefOf.RejectInput, historical: false);
                }

                return;
            }

            HediffDef hediffDef = ZhaoliEffectUtility.MinghuoHediffDef;
            if (hediffDef == null)
            {
                Log.Error("[MiliraXian.Characters.Zhaoli] Missing HediffDef: " + ZhaoliMinghuoUtility.MinghuoHediffDefName);
                return;
            }

            Hediff hediff = caster.health.GetOrAddHediff(hediffDef);
            HediffComp_ZhaoliMinghuo minghuoComp = hediff?.TryGetComp<HediffComp_ZhaoliMinghuo>();
            HediffComp_Disappears disappearsComp = hediff?.TryGetComp<HediffComp_Disappears>();
            if (minghuoComp == null || disappearsComp == null)
            {
                Log.Error("[MiliraXian.Characters.Zhaoli] 冥火 Hediff 缺少必要 comp。");
                return;
            }

            minghuoComp.BindWeapon(weapon);
            disappearsComp.SetDuration(Props.durationTicks);
            caster.health.Notify_HediffChanged(hediff);
            if (caster.Spawned)
            {
                FleckMaker.Static(caster.Position, caster.Map, FleckDefOf.FireGlow, Mathf.Max(1.5f, Props.overlayScale * 1.3f));
                FleckMaker.AttachedOverlay(caster, FleckDefOf.MicroSparksFast, Vector3.zero, Mathf.Max(1f, Props.overlayScale * 0.75f));
            }

            FleckMaker.AttachedOverlay(caster, FleckDefOf.FlashHollow, Vector3.zero, Props.overlayScale);
            if (caster.Spawned)
            {
                MoteMaker.ThrowText(caster.DrawPos, caster.Map, "冥火", 3.65f);
            }

            if (caster.Faction == Faction.OfPlayer)
            {
                Messages.Message(
                    "冥火已附着于当前主武器：伤害/破甲/命中/攻速 x1.5，攻击距离 +2，并附加额外火焰伤害。",
                    caster,
                    MessageTypeDefOf.PositiveEvent,
                    historical: false);
            }
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            Pawn targetPawn = target.Pawn;
            if (caster == null)
            {
                return false;
            }

            if (targetPawn != caster)
            {
                if (throwMessages)
                {
                    Messages.Message("冥火只能由昭离对自己施放。", caster, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            ThingWithComps weapon = caster.equipment?.Primary;
            if (weapon == null)
            {
                if (throwMessages)
                {
                    Messages.Message("昭离当前未装备主武器，无法施放冥火。", caster, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            if (ZhaoliKarmaUtility.GetCurrentKarma(caster) < Props.karmaCost)
            {
                if (throwMessages)
                {
                    Messages.Message("因果不足，无法施放冥火。", caster, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            return base.Valid(target, throwMessages);
        }
    }

    public class HediffComp_ZhaoliMinghuo : HediffComp
    {
        private ThingWithComps boundWeapon;

        public HediffCompProperties_ZhaoliMinghuo PropsMinghuo => (HediffCompProperties_ZhaoliMinghuo)props;

        public ThingWithComps BoundWeapon => boundWeapon;

        public void BindWeapon(ThingWithComps weapon)
        {
            boundWeapon = weapon;
        }

        public bool IsActiveFor(Pawn pawn, ThingWithComps weapon)
        {
            if (pawn == null || weapon == null || boundWeapon == null)
            {
                return false;
            }

            if (parent == null || parent.pawn != pawn || pawn.Dead)
            {
                return false;
            }

            return weapon == boundWeapon;
        }

        public float GetFireDamageAmount(Verb_MeleeAttackDamage verb)
        {
            float baseDamage = 0f;
            if (verb?.tool != null && verb.tool.power > 0f)
            {
                baseDamage = verb.tool.power;
            }
            else if (verb?.verbProps != null && verb.verbProps.meleeDamageBaseAmount > 0)
            {
                baseDamage = verb.verbProps.meleeDamageBaseAmount;
            }

            return Mathf.Max(1f, baseDamage * PropsMinghuo.fireDamageFactor);
        }

        public override string CompTipStringExtra
        {
            get
            {
                return null;
            }
        }

        public override string CompDescriptionExtra
        {
            get
            {
                if (boundWeapon == null)
                {
                    return "冥火尚未绑定到武器。";
                }

                return GetPanelSummary();
            }
        }

        public override void CompExposeData()
        {
            Scribe_References.Look(ref boundWeapon, "boundWeapon");
        }

        private string GetPanelSummary()
        {
            return string.Join("\n", new[]
            {
                "当前附着武器：" + boundWeapon.LabelCap,
                "伤害：x" + PropsMinghuo.damageMultiplier.ToString("0.##"),
                "破甲：x" + PropsMinghuo.armorPenetrationMultiplier.ToString("0.##"),
                "命中：x" + PropsMinghuo.hitChanceMultiplier.ToString("0.##"),
                "攻速：x" + PropsMinghuo.attackSpeedMultiplier.ToString("0.##"),
                "攻击距离：+" + PropsMinghuo.rangeOffset.ToString("0.##"),
                "额外火焰伤害：已启用"
            });
        }
    }

    internal static class ZhaoliMinghuoUtility
    {
        public const string MinghuoHediffDefName = "MXZL_ZhaoliMinghuo";

        public static HediffComp_ZhaoliMinghuo GetActiveComp(Verb verb)
        {
            Pawn pawn = verb?.CasterPawn;
            ThingWithComps weapon = verb?.EquipmentSource;
            if (pawn?.health?.hediffSet == null || weapon == null)
            {
                return null;
            }

            HediffDef hediffDef = ZhaoliEffectUtility.MinghuoHediffDef;
            if (hediffDef == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef);
            HediffComp_ZhaoliMinghuo comp = hediff?.TryGetComp<HediffComp_ZhaoliMinghuo>();
            return comp != null && comp.IsActiveFor(pawn, weapon) ? comp : null;
        }
    }

    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedMeleeDamageAmount), new Type[] { typeof(Verb), typeof(Pawn) })]
    internal static class Patch_VerbProperties_AdjustedMeleeDamageAmount_ZhaoliMinghuo
    {
        public static void Postfix(Verb ownerVerb, Pawn attacker, ref float __result)
        {
            HediffComp_ZhaoliMinghuo comp = ZhaoliMinghuoUtility.GetActiveComp(ownerVerb);
            if (comp != null)
            {
                __result *= comp.PropsMinghuo.damageMultiplier;
            }
        }
    }

    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedArmorPenetration), new Type[] { typeof(Verb), typeof(Pawn) })]
    internal static class Patch_VerbProperties_AdjustedArmorPenetration_ZhaoliMinghuo
    {
        public static void Postfix(Verb ownerVerb, Pawn attacker, ref float __result)
        {
            HediffComp_ZhaoliMinghuo comp = ZhaoliMinghuoUtility.GetActiveComp(ownerVerb);
            if (comp != null)
            {
                __result *= comp.PropsMinghuo.armorPenetrationMultiplier;
            }
        }
    }

    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedCooldown), new Type[] { typeof(Verb), typeof(Pawn) })]
    internal static class Patch_VerbProperties_AdjustedCooldown_ZhaoliMinghuo
    {
        public static void Postfix(Verb ownerVerb, Pawn attacker, ref float __result)
        {
            HediffComp_ZhaoliMinghuo comp = ZhaoliMinghuoUtility.GetActiveComp(ownerVerb);
            if (comp != null && comp.PropsMinghuo.attackSpeedMultiplier > 0f)
            {
                __result = Mathf.Max(0.1f, __result / comp.PropsMinghuo.attackSpeedMultiplier);
            }
        }
    }

    [HarmonyPatch(typeof(VerbProperties), nameof(VerbProperties.AdjustedRange), new Type[] { typeof(Verb), typeof(Thing) })]
    internal static class Patch_VerbProperties_AdjustedRange_ZhaoliMinghuo
    {
        public static void Postfix(Verb ownerVerb, Thing attacker, ref float __result)
        {
            HediffComp_ZhaoliMinghuo comp = ZhaoliMinghuoUtility.GetActiveComp(ownerVerb);
            if (comp != null)
            {
                __result += comp.PropsMinghuo.rangeOffset;
            }
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttack), "GetNonMissChance")]
    internal static class Patch_Verb_MeleeAttack_GetNonMissChance_ZhaoliMinghuo
    {
        public static void Postfix(Verb_MeleeAttack __instance, ref float __result)
        {
            HediffComp_ZhaoliMinghuo comp = ZhaoliMinghuoUtility.GetActiveComp(__instance);
            if (comp != null)
            {
                __result = Mathf.Clamp01(__result * comp.PropsMinghuo.hitChanceMultiplier);
            }
        }
    }

    [HarmonyPatch(typeof(Verb_MeleeAttackDamage), "DamageInfosToApply")]
    internal static class Patch_Verb_MeleeAttackDamage_DamageInfosToApply_ZhaoliMinghuo
    {
        public static void Postfix(Verb_MeleeAttackDamage __instance, LocalTargetInfo target, ref IEnumerable<DamageInfo> __result)
        {
            HediffComp_ZhaoliMinghuo comp = ZhaoliMinghuoUtility.GetActiveComp(__instance);
            if (comp == null)
            {
                return;
            }

            __result = AppendFireDamage(__result, __instance, target, comp);
        }

        private static IEnumerable<DamageInfo> AppendFireDamage(IEnumerable<DamageInfo> sourceInfos, Verb_MeleeAttackDamage verb, LocalTargetInfo target, HediffComp_ZhaoliMinghuo comp)
        {
            foreach (DamageInfo sourceInfo in sourceInfos)
            {
                yield return sourceInfo;
            }

            if (!target.HasThing || target.Thing == null)
            {
                yield break;
            }

            if (target.Thing.SpawnedOrAnyParentSpawned && target.Thing.MapHeld != null)
            {
                FleckMaker.AttachedOverlay(target.Thing, FleckDefOf.MicroSparksFast, Vector3.zero, 0.9f);
                FleckMaker.Static(target.Thing.PositionHeld, target.Thing.MapHeld, FleckDefOf.FireGlow, 1.1f);
            }

            float fireDamageAmount = comp.GetFireDamageAmount(verb);
            ThingDef sourceDef = verb.EquipmentSource != null ? verb.EquipmentSource.def : verb.CasterPawn.def;
            BodyPartGroupDef bodyPartGroupDef = verb.verbProps.AdjustedLinkedBodyPartsGroup(verb.tool);
            HediffDef hediffDef = verb.HediffCompSource?.Def;
            QualityCategory quality = QualityCategory.Normal;
            if (verb.EquipmentSource != null)
            {
                verb.EquipmentSource.TryGetQuality(out quality);
            }

            Vector3 direction = (target.Thing.Position - verb.CasterPawn.Position).ToVector3();
            DamageInfo fireInfo = new DamageInfo(DamageDefOf.Flame, Rand.Range(fireDamageAmount * 0.8f, fireDamageAmount * 1.2f), fireDamageAmount * 0.015f, -1f, verb.caster, null, sourceDef);
            fireInfo.SetBodyRegion(BodyPartHeight.Undefined, BodyPartDepth.Outside);
            fireInfo.SetWeaponBodyPartGroup(bodyPartGroupDef);
            fireInfo.SetWeaponHediff(hediffDef);
            fireInfo.SetAngle(direction);
            fireInfo.SetTool(verb.tool);
            fireInfo.SetWeaponQuality(quality);
            yield return fireInfo;
        }
    }
}

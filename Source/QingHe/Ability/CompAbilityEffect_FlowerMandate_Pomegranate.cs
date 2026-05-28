using MiliraXian.Characters.QingHe.Things;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities
{
    public class CompProperties_AbilityFlowerMandate_Pomegranate : CompProperties_AbilityEffect
    {
        public ThingDef summonDef;
        public HediffDef resourceCostDef;
        public float resourceCost = 1f;
        public int durationTicks = 900;
        public int maxActiveSummons = 3;
        public string summonEffecterDefName = "MXNL_ForFeatherCastingCircle";
        public float summonEffectScale = 1f;
        public string fallbackSummonFleckDefName = "PsycastAreaEffect";
        public string missingResourceMessage = "花令不足。";
        public string maxActiveSummonsMessage = "召花令维持数量已达上限。";
        public string placeholderMessage;

        public CompProperties_AbilityFlowerMandate_Pomegranate()
        {
            compClass = typeof(CompAbilityEffect_FlowerMandate_Pomegranate);
        }
    }

    public class CompAbilityEffect_FlowerMandate_Pomegranate : CompAbilityEffect
    {
        public new CompProperties_AbilityFlowerMandate_Pomegranate Props => (CompProperties_AbilityFlowerMandate_Pomegranate)props;

        public override bool GizmoDisabled(out string reason)
        {
            if (HasReachedSummonLimit(parent?.pawn))
            {
                reason = Props.maxActiveSummonsMessage;
                return true;
            }

            if (Props.resourceCostDef != null
                && Props.resourceCost > 0f
                && PawnSpecialResourceUtility.GetCurrentResource(parent.pawn, Props.resourceCostDef) < Props.resourceCost)
            {
                reason = Props.missingResourceMessage;
                return true;
            }

            return base.GizmoDisabled(out reason);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            if (!base.Valid(target, throwMessages))
            {
                return false;
            }

            if (HasReachedSummonLimit(parent?.pawn))
            {
                if (throwMessages)
                {
                    Messages.Message(Props.maxActiveSummonsMessage, parent?.pawn, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            return true;
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);
            Pawn caster = parent?.pawn;
            if (caster == null || caster.Map == null || Props.summonDef == null)
            {
                return;
            }

            IntVec3 cell = target.Cell;
            if (!cell.IsValid || !cell.InBounds(caster.Map) || !cell.Standable(caster.Map))
            {
                return;
            }

            if (HasReachedSummonLimit(caster))
            {
                Messages.Message(Props.maxActiveSummonsMessage, caster, MessageTypeDefOf.RejectInput, historical: false);
                return;
            }

            if (Props.resourceCostDef != null && Props.resourceCost > 0f)
            {
                PawnSpecialResourceUtility.TryConsumeResource(caster, Props.resourceCostDef, Props.resourceCost);
            }

            Thing thing = GenSpawn.Spawn(Props.summonDef, cell, caster.Map, WipeMode.Vanish);
            thing.SetFaction(caster.Faction);
            thing.TryGetComp<CompFlowerMandate_PomegranateLifetime>()?.Init(caster, Props.durationTicks);
            PlaySummonVisual(caster.Map, cell, Props.summonEffecterDefName, Props.fallbackSummonFleckDefName, Props.summonEffectScale);
        }

        public override void DrawEffectPreview(LocalTargetInfo target)
        {
            base.DrawEffectPreview(target);
            GenDraw.DrawRadiusRing(target.Cell, ResolveAttackRange(), new Color(1f, 0.55f, 0.78f, 0.35f));
        }

        private float ResolveAttackRange()
        {
            ThingDef turretDef = null;
            if (Props.summonDef?.comps != null)
            {
                for (int i = 0; i < Props.summonDef.comps.Count; i++)
                {
                    if (Props.summonDef.comps[i] is CompProperties_TurretGun turretGunProps)
                    {
                        turretDef = turretGunProps.turretDef;
                        break;
                    }
                }
            }

            if (turretDef?.Verbs != null && turretDef.Verbs.Count > 0)
            {
                return turretDef.Verbs[0].range;
            }

            return 18f;
        }

        private static void PlaySummonVisual(Map map, IntVec3 cell, string effecterDefName, string fallbackFleckDefName, float scale)
        {
            if (!effecterDefName.NullOrEmpty())
            {
                GraphicsUtility.Fx(map, cell, effecterDefName, scale);
                return;
            }

            if (!fallbackFleckDefName.NullOrEmpty())
            {
                GraphicsUtility.Fleck(map, cell, fallbackFleckDefName, scale);
            }
        }

        private bool HasReachedSummonLimit(Pawn caster)
        {
            return Props.maxActiveSummons > 0 && CountActiveSummons(caster) >= Props.maxActiveSummons;
        }

        private int CountActiveSummons(Pawn caster)
        {
            if (caster == null || caster.Map == null || Props.summonDef == null)
            {
                return 0;
            }

            int count = 0;
            var summons = caster.Map.listerThings.ThingsOfDef(Props.summonDef);
            for (int i = 0; i < summons.Count; i++)
            {
                if (summons[i]?.TryGetComp<CompFlowerMandate_PomegranateLifetime>()?.WasSummonedBy(caster) == true)
                {
                    count++;
                }
            }

            return count;
        }
    }
}

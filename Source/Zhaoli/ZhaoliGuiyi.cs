using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Zhaoli
{
    public class CompProperties_AbilityZhaoliGuiyi : CompProperties_AbilityEffect
    {
        public float karmaCost = 1f;
        public float overlayScale = 1.5f;

        public CompProperties_AbilityZhaoliGuiyi()
        {
            compClass = typeof(CompAbilityEffect_ZhaoliGuiyi);
        }
    }

    public class CompAbilityEffect_ZhaoliGuiyi : CompAbilityEffect
    {
        private new CompProperties_AbilityZhaoliGuiyi Props => (CompProperties_AbilityZhaoliGuiyi)props;

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            Pawn targetPawn = target.Pawn;
            if (caster == null || targetPawn == null)
            {
                return;
            }

            if (!ZhaoliKarmaUtility.TryConsumeKarma(caster, Props.karmaCost))
            {
                if (caster.Faction == Faction.OfPlayer)
                {
                    Messages.Message("因果不足，无法施放诡医。", caster, MessageTypeDefOf.RejectInput, historical: false);
                }

                return;
            }

            int restoredParts = RestoreMissingParts(targetPawn);
            int removedHediffs = RemoveNegativeHediffs(targetPawn);
            if (restoredParts + removedHediffs > 0)
            {
                FleckMaker.AttachedOverlay(targetPawn, FleckDefOf.FlashHollow, Vector3.zero, Props.overlayScale);
                if (targetPawn.Spawned)
                {
                    MoteMaker.ThrowText(targetPawn.DrawPos, targetPawn.Map, "痊愈", 3.65f);
                }
            }
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return base.CanApplyOn(target, dest) && Valid(target);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            Pawn targetPawn = target.Pawn;
            if (caster == null || targetPawn == null || targetPawn.Dead)
            {
                return false;
            }

            if (targetPawn.HostileTo(caster))
            {
                if (throwMessages)
                {
                    Messages.Message("诡医无法对敌对目标使用。", targetPawn, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            if (ZhaoliKarmaUtility.GetCurrentKarma(caster) < Props.karmaCost)
            {
                if (throwMessages)
                {
                    Messages.Message("因果不足，无法施放诡医。", caster, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            if (!HasCurableCondition(targetPawn))
            {
                if (throwMessages)
                {
                    Messages.Message("目标没有需要诡医处理的伤病。", targetPawn, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            return base.Valid(target, throwMessages);
        }

        private static bool HasCurableCondition(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return false;
            }

            if (pawn.health.hediffSet.GetMissingPartsCommonAncestors().Count > 0)
            {
                return true;
            }

            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                if (ShouldRemoveHediff(hediffs[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static int RestoreMissingParts(Pawn pawn)
        {
            List<Hediff_MissingPart> missingParts = pawn.health.hediffSet.GetMissingPartsCommonAncestors();
            int restoredParts = 0;
            for (int i = 0; i < missingParts.Count; i++)
            {
                Hediff_MissingPart missingPart = missingParts[i];
                if (missingPart?.Part == null)
                {
                    continue;
                }

                pawn.health.RestorePart(missingPart.Part);
                restoredParts++;
            }

            return restoredParts;
        }

        private static int RemoveNegativeHediffs(Pawn pawn)
        {
            List<Hediff> hediffsToRemove = new List<Hediff>();
            List<Hediff> hediffs = pawn.health.hediffSet.hediffs;
            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                if (ShouldRemoveHediff(hediff))
                {
                    hediffsToRemove.Add(hediff);
                }
            }

            int removed = 0;
            for (int i = 0; i < hediffsToRemove.Count; i++)
            {
                Hediff hediff = hediffsToRemove[i];
                if (hediff != null && pawn.health.hediffSet.hediffs.Contains(hediff))
                {
                    pawn.health.RemoveHediff(hediff);
                    removed++;
                }
            }

            return removed;
        }

        private static bool ShouldRemoveHediff(Hediff hediff)
        {
            if (hediff == null || hediff is Hediff_MissingPart)
            {
                return false;
            }

            if (hediff.def == DefDatabase<HediffDef>.GetNamedSilentFail(ZhaoliKarmaUtility.DormancyHediffDefName))
            {
                return false;
            }

            if (hediff is Hediff_Injury)
            {
                return true;
            }

            return hediff.def.isBad;
        }
    }
}

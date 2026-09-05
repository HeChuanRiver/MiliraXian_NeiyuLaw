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

    public class CompAbilityEffect_ZhaoliGuiyi : CompAbilityEffect_ZhaoliPowerLimited
    {
        private new CompProperties_AbilityZhaoliGuiyi Props => (CompProperties_AbilityZhaoliGuiyi)props;

        public override bool GizmoDisabled(out string reason)
        {
            if (base.GizmoDisabled(out reason)) return true;
            ZhaoliKarmaUtility.ResetNoCooldownAbilityLock(parent);
            reason = null;
            return false;
        }

        public override IEnumerable<PreCastAction> GetPreCastActions()
        {
            int warmupTicks = GetWarmupTicks();
            if (warmupTicks <= 0)
            {
                yield break;
            }

            yield return new PreCastAction
            {
                ticksAwayFromCast = warmupTicks,
                action = delegate(LocalTargetInfo target, LocalTargetInfo dest)
                {
                    SpawnLinkPulse(target.Pawn, ZhaoliEffectUtility.GuiyiLinkStripeMoteDef);
                }
            };

            int halfWarmupTicks = Mathf.Max(1, warmupTicks / 2);
            if (halfWarmupTicks < warmupTicks)
            {
                yield return new PreCastAction
                {
                    ticksAwayFromCast = halfWarmupTicks,
                    action = delegate(LocalTargetInfo target, LocalTargetInfo dest)
                    {
                        SpawnLinkPulse(target.Pawn, ZhaoliEffectUtility.GuiyiLinkStripeMoteDef);
                    }
                };
            }
        }

        public override IEnumerable<Mote> CustomWarmupMotes(LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            Pawn targetPawn = target.Pawn;
            ThingDef lineDef = ZhaoliEffectUtility.GuiyiLinkLineMoteDef;
            if (caster == null || targetPawn == null || lineDef == null)
            {
                yield break;
            }

            yield return MoteMaker.MakeInteractionOverlay(lineDef, caster, targetPawn);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (ZhaoliPowerBalance.Sealed) return;
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            Pawn targetPawn = target.Pawn;
            if (caster == null || targetPawn == null)
            {
                return;
            }

            HediffComp_ZhaoliKarmaLinks linkComp = ZhaoliKarmaUtility.EnsureLinkComp(caster);
            if (linkComp == null)
            {
                return;
            }

            if (!linkComp.TryAddOrRefreshLink(targetPawn, out bool createdNewLink, out string failureReason))
            {
                if (!failureReason.NullOrEmpty() && caster.Faction == Faction.OfPlayer)
                {
                    Messages.Message(failureReason, targetPawn, MessageTypeDefOf.RejectInput, historical: false);
                }

                return;
            }

            if (!ZhaoliKarmaUtility.TryConsumeKarma(caster, Props.karmaCost))
            {
                if (createdNewLink)
                {
                    linkComp.BreakLink(targetPawn);
                }

                if (caster.Faction == Faction.OfPlayer)
                {
                    Messages.Message("MX_ZL_NotEnoughKarmaGuiyi".Translate(), caster, MessageTypeDefOf.RejectInput, historical: false);
                }

                return;
            }

            int restoredParts = RestoreMissingParts(targetPawn);
            int removedHediffs = RemoveNegativeHediffs(targetPawn);
            if (restoredParts + removedHediffs > 0)
            {
                SpawnLinkPulse(targetPawn, ZhaoliEffectUtility.GuiyiLinkPulseMoteDef);

                if (targetPawn.Spawned)
                {
                    FleckMaker.Static(targetPawn.Position, targetPawn.Map, FleckDefOf.PsycastAreaEffect, Mathf.Max(1f, Props.overlayScale));
                    FleckMaker.AttachedOverlay(targetPawn, FleckDefOf.HealingCross, Vector3.zero, Mathf.Max(1f, Props.overlayScale));
                }

                SpawnHealGlow(targetPawn);
                FleckMaker.AttachedOverlay(targetPawn, FleckDefOf.FlashHollow, Vector3.zero, Props.overlayScale);
                if (targetPawn.Spawned)
                {
                    MoteMaker.ThrowText(targetPawn.DrawPos, targetPawn.Map, "MX_ZL_GuiyiMote".Translate().ToString(), 3.65f);
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

            if (targetPawn == caster)
            {
                if (throwMessages)
                {
                    Messages.Message("MX_ZL_GuiyiCannotTargetSelf".Translate(), caster, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            if (targetPawn.HostileTo(caster))
            {
                if (throwMessages)
                {
                    Messages.Message("MX_ZL_GuiyiCannotTargetHostile".Translate(), targetPawn, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            if (ZhaoliKarmaUtility.GetCurrentKarma(caster) < Props.karmaCost)
            {
                if (throwMessages)
                {
                    Messages.Message("MX_ZL_NotEnoughKarmaGuiyi".Translate(), caster, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            if (!HasCurableCondition(targetPawn))
            {
                if (throwMessages)
                {
                    Messages.Message("MX_ZL_GuiyiNoTreatableInjuries".Translate(), targetPawn, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            HediffComp_ZhaoliKarmaLinks linkComp = ZhaoliKarmaUtility.EnsureLinkComp(caster);
            if (linkComp == null)
            {
                return false;
            }

            if (!linkComp.CanLinkTarget(targetPawn, out string failureReason))
            {
                if (throwMessages && !failureReason.NullOrEmpty())
                {
                    Messages.Message(failureReason, targetPawn, MessageTypeDefOf.RejectInput, historical: false);
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

            for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
            {
                if (ShouldRemoveHediff(pawn.health.hediffSet.hediffs[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static int RestoreMissingParts(Pawn pawn)
        {
            int restoredParts = 0;
            for (int i = 0; i < pawn.health.hediffSet.GetMissingPartsCommonAncestors().Count; i++)
            {
                Hediff_MissingPart missingPart = pawn.health.hediffSet.GetMissingPartsCommonAncestors()[i];
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
            System.Collections.Generic.List<Hediff> hediffsToRemove = new();
            for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
            {
                Hediff hediff = pawn.health.hediffSet.hediffs[i];
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

            if (hediff.def == ZhaoliEffectUtility.DormancyHediffDef)
            {
                return false;
            }

            if (hediff is Hediff_Injury)
            {
                return true;
            }

            return hediff.def.isBad;
        }

        private int GetWarmupTicks()
        {
            if (parent?.def?.verbProperties == null)
            {
                return 0;
            }

            return Mathf.Max(0, parent.def.verbProperties.warmupTime.SecondsToTicks());
        }

        private void SpawnLinkPulse(Pawn targetPawn, ThingDef pulseDef)
        {
            Pawn caster = parent?.pawn;
            if (caster == null || targetPawn == null || pulseDef == null || !caster.Spawned || !targetPawn.Spawned || caster.MapHeld != targetPawn.MapHeld)
            {
                return;
            }

            MoteMaker.MakeInteractionOverlay(pulseDef, caster, targetPawn);
        }

        private void SpawnHealGlow(Pawn targetPawn)
        {
            if (targetPawn == null || !targetPawn.Spawned)
            {
                return;
            }

            ZhaoliVisualUtility.QueueGuiyiHealAnimation(targetPawn, Props.overlayScale);
        }
    }
}

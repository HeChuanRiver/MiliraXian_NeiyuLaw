using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.Zhaoli
{
    public class CompProperties_AbilityZhaoliDingshu : CompProperties_AbilityEffect
    {
        public int markDurationTicks = 720000;
        public float overlayScale = 1.75f;
        public int pulseMoteIntervalTicks = 120;

        public CompProperties_AbilityZhaoliDingshu()
        {
            compClass = typeof(CompAbilityEffect_ZhaoliDingshu);
        }
    }

    internal static class ZhaoliDingshuUtility
    {
        public const string DingshuAbilityDefName = "MX_Zhaoli_Dingshu";
        public const string DingshuMarkHediffDefName = "MXZL_ZhaoliDingshuMark";

        public static HediffDef DingshuMarkHediffDef => DefDatabase<HediffDef>.GetNamedSilentFail(DingshuMarkHediffDefName);

        public static AbilityDef DingshuAbilityDef => DefDatabase<AbilityDef>.GetNamedSilentFail(DingshuAbilityDefName);

        public static bool HasDeadRevivalLock(Pawn pawn)
        {
            if (ZhaoliPowerBalance.Sealed) return false;
            HediffDef markDef = DingshuMarkHediffDef;
            return pawn != null && pawn.Dead && markDef != null && (pawn.health?.hediffSet?.HasHediff(markDef) ?? false);
        }

        public static void ApplyRevivalLock(Pawn pawn, int durationTicks)
        {
            HediffDef markDef = DingshuMarkHediffDef;
            if (pawn?.health == null || markDef == null)
            {
                return;
            }

            Hediff hediff = pawn.health.GetOrAddHediff(markDef);
            pawn.health.Notify_HediffChanged(hediff);
            (hediff as HediffWithComps)?.GetComp<HediffComp_Disappears>()?.SetDuration(durationTicks);
        }

        public static bool CanLinkRevivedPawn(Pawn caster, Pawn target, HediffComp_ZhaoliKarmaLinks linkComp, out string failureReason)
        {
            failureReason = null;
            if (caster == null || target == null || target.health == null || linkComp == null)
            {
                failureReason = "MX_ZL_LinkTargetInvalid".Translate().ToString();
                return false;
            }

            if (target == caster)
            {
                failureReason = "MX_ZL_DingshuCannotTargetSelf".Translate().ToString();
                return false;
            }

            HediffComp_ZhaoliKarmaLinkTarget targetComp = ZhaoliKarmaUtility.GetLinkTargetComp(target);
            if (targetComp != null && targetComp.Zhaoli != null && targetComp.Zhaoli != caster)
            {
                failureReason = "MX_ZL_LinkTargetAlreadyLinkedOther".Translate().ToString();
                return false;
            }

            if (targetComp != null && targetComp.Zhaoli == caster)
            {
                return true;
            }

            if (linkComp.ActiveLinkCount >= linkComp.PropsLinks.maxLinks)
            {
                failureReason = "MX_ZL_LinkLimitReached".Translate().ToString();
                return false;
            }

            return true;
        }

        public static bool TryFinalizeRevivalLink(Pawn caster, Pawn target, HediffComp_ZhaoliKarmaLinks linkComp, out string failureReason)
        {
            failureReason = null;
            if (caster == null || target == null || linkComp == null)
            {
                failureReason = "MX_ZL_LinkTargetInvalid".Translate().ToString();
                return false;
            }

            if (!target.Dead && !target.Destroyed)
            {
                return linkComp.TryAddOrRefreshLink(target, out _, out failureReason);
            }

            GameComponent_ZhaoliKarma karmaComponent = Current.Game?.GetComponent<GameComponent_ZhaoliKarma>();
            if (karmaComponent == null)
            {
                failureReason = "MX_ZL_LinkTargetInvalid".Translate().ToString();
                return false;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            karmaComponent.RegisterPendingDingshuLink(caster, target, currentTick + 300);
            return true;
        }

        public static void RestorePawnCompletely(Pawn pawn, out int restoredParts, out int removedHediffs)
        {
            restoredParts = 0;
            removedHediffs = 0;
            if (pawn?.health?.hediffSet == null)
            {
                return;
            }

            List<Hediff_MissingPart> missingParts = pawn.health.hediffSet.GetMissingPartsCommonAncestors();
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

            List<Hediff> hediffsToRemove = new();
            for (int i = 0; i < pawn.health.hediffSet.hediffs.Count; i++)
            {
                Hediff hediff = pawn.health.hediffSet.hediffs[i];
                if (ShouldRemoveHediff(hediff))
                {
                    hediffsToRemove.Add(hediff);
                }
            }

            for (int i = 0; i < hediffsToRemove.Count; i++)
            {
                Hediff hediff = hediffsToRemove[i];
                if (hediff != null && pawn.health.hediffSet.hediffs.Contains(hediff))
                {
                    pawn.health.RemoveHediff(hediff);
                    removedHediffs++;
                }
            }
        }

        public static void EnsureDingshuAbility(Pawn pawn)
        {
            AbilityDef abilityDef = DingshuAbilityDef;
            if (!ZhaoliKarmaUtility.IsZhaoli(pawn) || pawn?.abilities == null || abilityDef == null)
            {
                return;
            }

            if (pawn.abilities.GetAbility(abilityDef, includeTemporary: false) == null)
            {
                pawn.abilities.GainAbility(abilityDef);
            }
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
    }

    internal static class ZhaoliDingshuVisualUtility
    {
        public static void SpawnPulse(Pawn caster, Thing targetThing, bool useStripe = false)
        {
            ThingDef pulseDef = useStripe ? ZhaoliEffectUtility.DingshuLinkStripeMoteDef : ZhaoliEffectUtility.DingshuLinkPulseMoteDef;
            if (caster == null || targetThing == null || pulseDef == null || !caster.Spawned || !targetThing.Spawned || caster.MapHeld != targetThing.MapHeld)
            {
                return;
            }

            MoteMaker.MakeInteractionOverlay(pulseDef, caster, targetThing);
        }

        public static void SpawnReviveGlow(Thing targetThing, float scale)
        {
            ThingDef glowDef = ZhaoliEffectUtility.DingshuReviveGlowMoteDef;
            if (targetThing == null || glowDef == null || !targetThing.Spawned)
            {
                return;
            }

            MoteMaker.MakeAttachedOverlay(targetThing, glowDef, Vector3.zero, Mathf.Max(1f, scale));
        }
    }

    public class CompAbilityEffect_ZhaoliDingshu : CompAbilityEffect_ZhaoliPowerLimited
    {
        private new CompProperties_AbilityZhaoliDingshu Props => (CompProperties_AbilityZhaoliDingshu)props;

        private bool openingPulseSpawned;
        private int lastPulseTick = -99999;

        public override IEnumerable<Mote> CustomWarmupMotes(LocalTargetInfo target)
        {
            Pawn caster = parent?.pawn;
            Corpse corpse = target.Thing as Corpse;
            ThingDef lineDef = ZhaoliEffectUtility.DingshuLinkLineMoteDef;
            if (caster == null || corpse == null || lineDef == null)
            {
                yield break;
            }

            yield return MoteMaker.MakeInteractionOverlay(lineDef, caster, corpse);
        }

        // Cast visuals belong here because every ability comp is ticked each tick regardless of the
        // pawn's job, so Casting is what keeps motes off when this ability is not being used.
        public override void CompTick()
        {
            if (!parent.Casting)
            {
                openingPulseSpawned = false;
                return;
            }

            Pawn caster = parent.pawn;
            if (caster == null || !caster.Spawned || !(parent.verb?.CurrentTarget.Thing is Corpse corpse))
            {
                return;
            }

            int ticksGame = Find.TickManager.TicksGame;
            if (ticksGame - lastPulseTick < Mathf.Max(1, Props.pulseMoteIntervalTicks))
            {
                return;
            }

            lastPulseTick = ticksGame;
            ZhaoliDingshuVisualUtility.SpawnPulse(caster, corpse, useStripe: !openingPulseSpawned);
            ZhaoliDingshuVisualUtility.SpawnReviveGlow(corpse, Props.overlayScale);
            openingPulseSpawned = true;
        }

        public override bool CanApplyOn(LocalTargetInfo target, LocalTargetInfo dest)
        {
            return base.CanApplyOn(target, dest) && Valid(target);
        }

        public override bool Valid(LocalTargetInfo target, bool throwMessages = false)
        {
            Pawn caster = parent?.pawn;
            Corpse corpse = target.Thing as Corpse;
            Pawn targetPawn = corpse?.InnerPawn;
            if (caster == null || corpse == null || targetPawn == null || !targetPawn.Dead)
            {
                return false;
            }

            if (ZhaoliScenarioUtility.IsRaidState(caster))
            {
                if (throwMessages)
                {
                    Messages.Message("MX_ZL_DingshuCannotUseDuringRaid".Translate(), caster, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            if (ZhaoliDingshuUtility.HasDeadRevivalLock(targetPawn))
            {
                if (throwMessages)
                {
                    Messages.Message("MX_ZL_DingshuMarkedTargetCannotRevive".Translate(), corpse, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            HediffComp_ZhaoliKarmaLinks linkComp = ZhaoliKarmaUtility.EnsureLinkComp(caster);
            if (linkComp == null)
            {
                return false;
            }

            if (!ZhaoliDingshuUtility.CanLinkRevivedPawn(caster, targetPawn, linkComp, out string failureReason))
            {
                if (throwMessages && !failureReason.NullOrEmpty())
                {
                    Messages.Message(failureReason, corpse, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            return base.Valid(target, throwMessages);
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            if (ZhaoliPowerBalance.Sealed) return;
            base.Apply(target, dest);

            Pawn caster = parent?.pawn;
            Corpse corpse = target.Thing as Corpse;
            Pawn targetPawn = corpse?.InnerPawn;
            if (caster == null || corpse == null || targetPawn == null || !targetPawn.Dead)
            {
                return;
            }

            HediffComp_ZhaoliKarmaLinks linkComp = ZhaoliKarmaUtility.EnsureLinkComp(caster);
            if (linkComp == null)
            {
                return;
            }

            if (!ZhaoliDingshuUtility.CanLinkRevivedPawn(caster, targetPawn, linkComp, out _))
            {
                return;
            }

            if (!ResurrectionUtility.TryResurrect(targetPawn, new ResurrectionParams
            {
                gettingScarsChance = 0f,
                canKidnap = false,
                canTimeoutOrFlee = false,
                sappers = false,
                useAvoidGridSmart = true,
                canSteal = false,
                breachers = false,
                canPickUpOpportunisticWeapons = false,
                restoreMissingParts = true,
                noLord = true,
                invisibleStun = true,
                removeDiedThoughts = true
            }))
            {
                if (caster.Faction == Faction.OfPlayer)
                {
                    Messages.Message("MX_ZL_DingshuReviveFailed".Translate(), corpse, MessageTypeDefOf.RejectInput, historical: false);
                }

                return;
            }

            int restoredParts = 0, removedHediffs = 0;
            ZhaoliDingshuUtility.RestorePawnCompletely(targetPawn, out restoredParts, out removedHediffs);
            if (ZhaoliKarmaUtility.IsZhaoli(targetPawn))
            {
                ZhaoliRebirthUtility.NotifyApparelResurrected(targetPawn);
            }

            ZhaoliDingshuUtility.ApplyRevivalLock(targetPawn, Props.markDurationTicks);
            if (!ZhaoliDingshuUtility.TryFinalizeRevivalLink(caster, targetPawn, linkComp, out string failureReason) && caster.Faction == Faction.OfPlayer && !failureReason.NullOrEmpty())
            {
                Messages.Message(failureReason, targetPawn, MessageTypeDefOf.NeutralEvent, historical: false);
            }
            ZhaoliDingshuVisualUtility.SpawnPulse(caster, targetPawn);
            ZhaoliDingshuVisualUtility.SpawnReviveGlow(targetPawn, Props.overlayScale);

            if (targetPawn.Spawned)
            {
                FleckMaker.AttachedOverlay(targetPawn, FleckDefOf.FlashHollow, Vector3.zero, Props.overlayScale);
                MoteMaker.ThrowText(targetPawn.DrawPos, targetPawn.Map, "MX_ZL_DingshuMote".Translate().ToString(), 3.65f);
            }

            if (caster.Faction == Faction.OfPlayer)
            {
                string messageKey = restoredParts + removedHediffs > 0 ? "MX_ZL_DingshuRevivedAndHealed" : "MX_ZL_DingshuRevived";
                Messages.Message(messageKey.Translate(targetPawn.LabelShortCap), targetPawn, MessageTypeDefOf.PositiveEvent);
            }
        }
    }

    public class JobDriver_MX_Dingshu : JobDriver_CastAbility
    {
        public override string GetReport()
        {
            return "MX_ZL_ReportDingshu".Translate().ToString();
        }

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            if (job?.GetTarget(TargetIndex.A).IsValid != true)
            {
                return false;
            }

            return pawn.Reserve(job.GetTarget(TargetIndex.A), job, 1, -1, null, errorOnFailed);
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            yield return Toils_Combat.GotoCastPosition(TargetIndex.A, TargetIndex.B);
            foreach (Toil toil in base.MakeNewToils())
            {
                yield return toil;
            }
        }
    }

    public class ZhaoliPendingDingshuLink : IExposable
    {
        public Pawn zhaoli;
        public Pawn targetPawn;
        public int expireTick;

        public ZhaoliPendingDingshuLink()
        {
        }

        public ZhaoliPendingDingshuLink(Pawn zhaoli, Pawn targetPawn, int expireTick)
        {
            this.zhaoli = zhaoli;
            this.targetPawn = targetPawn;
            this.expireTick = expireTick;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref zhaoli, "zhaoli");
            Scribe_References.Look(ref targetPawn, "targetPawn");
            Scribe_Values.Look(ref expireTick, "expireTick", 0);
        }
    }

    [HarmonyPatch(typeof(ResurrectionUtility), nameof(ResurrectionUtility.TryResurrect))]
    internal static class Patch_ResurrectionUtility_TryResurrect_ZhaoliDingshu
    {
        public static bool Prefix(Pawn pawn)
        {
            return !ZhaoliDingshuUtility.HasDeadRevivalLock(pawn);
        }
    }
}

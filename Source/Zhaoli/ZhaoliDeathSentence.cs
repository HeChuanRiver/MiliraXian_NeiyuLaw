using MiliraXian.Characters;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Zhaoli
{
    internal static class ZhaoliDeathSentenceUtility
    {
        // Immunity belongs to the character, independent of faction, power level,
        // or registration in AL's special-pawn manager.
        public static bool IsImmune(Pawn pawn)
        {
            return pawn?.kindDef?.defName is "MiliraXian_Neiyu" or "MiliraXian_Qinghe"
                or "MiliraXian_Zhaoli" or "MiliraXian_Mingyuan";
        }
    }

    public class StatPart_ZhaoliDeathSentenceImmunity : AbnormalLimitFactor
    {
        protected override float FactorFor(Pawn pawn)
        {
            return ZhaoliDeathSentenceUtility.IsImmune(pawn) ? 0f : 1f;
        }
    }

    public class HediffCompProperties_ZhaoliDeathSentence : HediffCompProperties_OnAbnormalApplied
    {
        public float cutSeverity = 3f;

        public HediffCompProperties_ZhaoliDeathSentence()
        {
            compClass = typeof(HediffComp_ZhaoliDeathSentence);
        }
    }

    public class HediffComp_ZhaoliDeathSentence : HediffComp_OnAbnormalApplied
    {
        private HediffCompProperties_ZhaoliDeathSentence PropsDeathSentence => (HediffCompProperties_ZhaoliDeathSentence)props;

        // Also removes accumulation already present in older saves.
        public override bool CompShouldRemove => ZhaoliDeathSentenceUtility.IsImmune(Pawn);

        public override void NotifyApplied(Pawn source, float amount)
        {
            if (ZhaoliPowerBalance.Sealed || ZhaoliDeathSentenceUtility.IsImmune(Pawn)) return;
            base.NotifyApplied(source, amount);
            if (Pawn == null || Pawn.Dead || Pawn.Destroyed || amount <= 0f)
            {
                return;
            }

            BodyPartRecord part = Pawn.health?.hediffSet?.GetRandomNotMissingPart(
                DamageDefOf.Cut,
                BodyPartHeight.Undefined,
                BodyPartDepth.Outside);
            if (part != null && HediffMaker.MakeHediff(HediffDefOf.Cut, Pawn, part) is Hediff_Injury injury)
            {
                injury.Severity = Mathf.Max(0.1f, PropsDeathSentence.cutSeverity);
                Pawn.health.AddHediff(injury, part);
            }

            if (Pawn.Spawned)
            {
                FleckMaker.Static(Pawn.Position, Pawn.Map, FleckDefOf.FlashHollow, 1.1f);
                FleckMaker.Static(Pawn.Position, Pawn.Map, FleckDefOf.PsycastAreaEffect, 0.9f);
                ShowRemainingCount();
            }
        }

        private void ShowRemainingCount()
        {
            if (parent is not Hediff_Abnormal abnormal)
            {
                return;
            }

            int remaining = Mathf.CeilToInt(Mathf.Max(0f, abnormal.AccumulationLimit - abnormal.Severity));
            if (remaining > 0)
            {
                MoteMaker.ThrowText(Pawn.DrawPos, Pawn.Map, remaining.ToString(), new Color(0.94f, 0.26f, 0.26f), 1.1f);
            }
        }
    }

    public class Hediff_ZhaoliDeathSentenceResult : HediffWithComps, IAbnormalResult
    {
        private const float KarmaPerExecution = 1f;

        private Pawn instigator;
        private HediffDef_Abnormal abnormalDef;
        private bool resolved;

        public override bool Visible => false;

        public override bool ShouldRemove => ZhaoliDeathSentenceUtility.IsImmune(pawn) || base.ShouldRemove;

        public void Initialize(Pawn newInstigator, HediffDef_Abnormal sourceAbnormalDef)
        {
            instigator = newInstigator;
            abnormalDef = sourceAbnormalDef;
            resolved = false;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref instigator, "deathSentenceInstigator");
            Scribe_Defs.Look(ref abnormalDef, "deathSentenceAbnormalDef");
            Scribe_Values.Look(ref resolved, "deathSentenceResolved", false);
        }

        public override void PostRemoved()
        {
            base.PostRemoved();
            Resolve();
        }

        private void Resolve()
        {
            if (resolved)
            {
                return;
            }

            resolved = true;
            // PostRemoved also runs when AL heals bad hediffs or an old save is
            // cleaned up. Removing an immune character's result must never kill it.
            if (ZhaoliPowerBalance.Sealed || ZhaoliDeathSentenceUtility.IsImmune(pawn)) return;
            Pawn target = pawn;
            if (target == null || target.Dead || target.Destroyed)
            {
                return;
            }

            PlayExecutionVisuals(target);
            if (ZhaoliKarmaUtility.IsZhaoli(instigator))
            {
                ZhaoliKarmaUtility.AddKarma(instigator, KarmaPerExecution);
                ZhaoliShieldLayerUtility.AddLayers(instigator, ZhaoliShieldLayerUtility.ShieldLayersPerExecution);
            }

            DamageInfo damageInfo = new(
                DamageDefOf.ExecutionCut,
                99999f,
                999f,
                -1f,
                instigator,
                null,
                instigator?.equipment?.Primary?.def,
                DamageInfo.SourceCategory.ThingOrUnknown,
                target,
                instigatorGuilty: false,
                spawnFilth: false,
                checkForJobOverride: false);
            damageInfo.SetIgnoreArmor(true);
            damageInfo.SetIgnoreInstantKillProtection(true);
            target.Kill(damageInfo);
            DiscardExecutedPawn(target);
        }

        private void PlayExecutionVisuals(Pawn target)
        {
            Map map = target.MapHeld;
            IntVec3 position = target.PositionHeld;
            if (map == null || !position.IsValid)
            {
                return;
            }

            FleckDef soulFleck = ZhaoliEffectUtility.DeathRefusalBubbleFleckDef;
            if (soulFleck != null)
            {
                FleckMaker.Static(position, map, soulFleck, 1.6f);
            }

            FleckMaker.Static(position, map, FleckDefOf.ExplosionFlash, 1.6f);
            FleckMaker.Static(position, map, FleckDefOf.FlashHollow, 1.4f);

            ThingDef soulPulseDef = ZhaoliEffectUtility.SoulAbsorbPulseMoteDef;
            if (soulPulseDef != null && instigator?.Spawned == true && instigator.MapHeld == map)
            {
                MoteMaker.MakeInteractionOverlay(soulPulseDef, new TargetInfo(position, map), instigator);
                FleckMaker.Static(instigator.Position, map, FleckDefOf.PsycastAreaEffect, 1.15f);
            }
        }

        private static void DiscardExecutedPawn(Pawn target)
        {
            // Pawn.Kill can be cancelled by AL void recovery or another death
            // protection. Never discard a survivor or a pawn owned by a container.
            if (target == null || target.Discarded || !target.Dead || target.Spawned
                || (target.ParentHolder != null && target.Corpse == null))
            {
                return;
            }

            Corpse corpse = target.Corpse;
            if (corpse != null && !corpse.Destroyed)
            {
                if (corpse.Spawned)
                {
                    corpse.DeSpawn();
                }

                corpse.InnerPawn = null;
                corpse.Destroy(DestroyMode.Vanish);
            }

            if (Find.WorldPawns != null && Find.WorldPawns.Contains(target))
            {
                Find.WorldPawns.RemoveAndDiscardPawnViaGC(target);
            }
            else if (target.Destroyed && !target.Discarded)
            {
                target.Discard(silentlyRemoveReferences: true);
            }
        }
    }
}

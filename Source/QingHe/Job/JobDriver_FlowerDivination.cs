using System.Collections.Generic;
using MiliraXian.Characters.QingHe.Abilities;
using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Jobs
{
    public class JobDriver_FlowerDivination : JobDriver
    {
        private const int DefaultWarmupTicks = 120;
        private const int DefaultLightningIntervalTicks = 18;
        private const float DefaultLightningRadius = 1.4f;
        private const string FlowerDivinationBurstMoteDefName = "MX_QH_Mote_FlowerDivinationBurst";

        public override bool TryMakePreToilReservations(bool errorOnFailed)
        {
            return true;
        }

        protected override IEnumerable<Toil> MakeNewToils()
        {
            this.FailOn(() => !MX_QHUtility.IsQinghe(pawn));
            this.FailOn(() => !CanStartDivination(throwMessage: false));

            Toil warmup = ToilMaker.MakeToil("WarmupFlowerDivination");
            int warmupTicks = ResolveWarmupTicks();
            warmup.initAction = delegate
            {
                if (!CanStartDivination(throwMessage: true))
                {
                    EndJobWith(JobCondition.Incompletable);
                    return;
                }

                pawn.pather.StopDead();
                PlayWarmupLightning();
            };
            warmup.tickAction = delegate
            {
                pawn.pather.StopDead();

                int elapsedTicks = warmupTicks - ticksLeftThisToil;
                if (elapsedTicks < 0)
                {
                    elapsedTicks = 0;
                }

                int interval = ResolveLightningIntervalTicks();
                if (elapsedTicks % interval == 0)
                {
                    PlayWarmupLightning();
                }
            };
            warmup.defaultCompleteMode = ToilCompleteMode.Delay;
            warmup.defaultDuration = warmupTicks;
            warmup.WithProgressBarToilDelay(TargetIndex.None, false, -0.5f);
            yield return warmup;

            Toil activate = ToilMaker.MakeToil("ActivateFlowerDivination");
            activate.initAction = delegate
            {
                HediffComp_FlowerDivination divination = FlowerCourtUtility.EnsureFlowerDivination(pawn);
                if (divination == null)
                {
                    Messages.Message("清荷尚未建立四时共鸣。", pawn, MessageTypeDefOf.RejectInput, historical: false);
                    return;
                }

                if (!divination.TryStartDivination())
                {
                    divination.CanStartDivination(out string reason);
                    if (!reason.NullOrEmpty())
                    {
                        Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
                    }

                    return;
                }

                PlayActivationLightning();
                PlayActivationBurst();

                string message = divination.Props?.activatedMessage;
                if (!message.NullOrEmpty())
                {
                    Messages.Message(message, pawn, MessageTypeDefOf.PositiveEvent, historical: false);
                }
            };
            activate.defaultCompleteMode = ToilCompleteMode.Instant;
            yield return activate;
        }

        private bool CanStartDivination(bool throwMessage)
        {
            HediffComp_FlowerDivination divination = FlowerCourtUtility.EnsureFlowerDivination(pawn);
            if (divination == null)
            {
                if (throwMessage)
                {
                    Messages.Message("Flower Divination is not ready.", pawn, MessageTypeDefOf.RejectInput, historical: false);
                }

                return false;
            }

            if (divination.CanStartDivination(out string reason))
            {
                return true;
            }

            if (throwMessage && !reason.NullOrEmpty())
            {
                Messages.Message(reason, pawn, MessageTypeDefOf.RejectInput, historical: false);
            }

            return false;
        }

        private int ResolveWarmupTicks()
        {
            HediffComp_FlowerDivination divination = FlowerCourtUtility.EnsureFlowerDivination(pawn);
            return Mathf.Max(1, divination?.Props?.warmupTicks ?? DefaultWarmupTicks);
        }

        private int ResolveLightningIntervalTicks()
        {
            HediffComp_FlowerDivination divination = FlowerCourtUtility.EnsureFlowerDivination(pawn);
            return Mathf.Max(1, divination?.Props?.warmupLightningIntervalTicks ?? DefaultLightningIntervalTicks);
        }

        private float ResolveLightningRadius()
        {
            HediffComp_FlowerDivination divination = FlowerCourtUtility.EnsureFlowerDivination(pawn);
            return Mathf.Max(0.1f, divination?.Props?.warmupLightningRadius ?? DefaultLightningRadius);
        }

        private void PlayWarmupLightning()
        {
            if (pawn?.Map == null || !pawn.Position.InBounds(pawn.Map))
            {
                return;
            }

            Map map = pawn.Map;
            IntVec3 cell = RandomCellNearPawn(map);
            Vector3 loc = cell.ToVector3Shifted();
            FleckMaker.ThrowLightningGlow(loc, map, 0.85f);
            FleckMaker.ThrowMicroSparks(loc, map);
            if (Rand.Chance(0.35f))
            {
                FleckMaker.Static(cell, map, FleckDefOf.ExplosionFlash, 0.45f);
            }
        }

        private void PlayActivationLightning()
        {
            if (pawn?.Map == null || !pawn.Position.InBounds(pawn.Map))
            {
                return;
            }

            Map map = pawn.Map;
            SoundDefOf.Thunder_OnMap.PlayOneShot(SoundInfo.InMap(pawn));
            for (int i = 0; i < 5; i++)
            {
                IntVec3 cell = i == 0 ? pawn.Position : RandomCellNearPawn(map);
                Vector3 loc = cell.ToVector3Shifted();
                map.GetComponent<MapComponent_FlowerDivinationVisuals>()?.AddLightningBolt(cell, i == 0 ? 24 : 18);
                FleckMaker.ThrowLightningGlow(loc, map, 1.15f);
                FleckMaker.ThrowMicroSparks(loc, map);
            }

            FleckMaker.Static(pawn.Position, map, FleckDefOf.ExplosionFlash, 0.8f);
        }

        private void PlayActivationBurst()
        {
            if (pawn?.MapHeld == null || !pawn.Spawned)
            {
                return;
            }

            ThingDef moteDef = DefDatabase<ThingDef>.GetNamedSilentFail(FlowerDivinationBurstMoteDefName);
            if (moteDef == null)
            {
                return;
            }

            MoteMaker.MakeAttachedOverlay(pawn, moteDef, Vector3.zero, 1f);
        }

        private IntVec3 RandomCellNearPawn(Map map)
        {
            float radius = ResolveLightningRadius();
            int count = GenRadial.NumCellsInRadius(radius);
            for (int i = 0; i < 12; i++)
            {
                IntVec3 cell = pawn.Position + GenRadial.RadialPattern[Rand.Range(0, count)];
                if (cell.InBounds(map))
                {
                    return cell;
                }
            }

            return pawn.Position;
        }
    }
}

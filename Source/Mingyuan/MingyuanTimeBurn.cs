using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.Mingyuan
{
    public static class MingyuanTimeBurnUtility
    {
        public static void Register(Pawn pawn, Pawn caster, CompProperties_AbilityMingyuanTimeBurn props)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead || props == null)
            {
                return;
            }

            MingyuanUtility.EnsureHediff(pawn, MingyuanUtility.TimeBurnFrozenDef);
            Current.Game?.GetComponent<GameComponent_MingyuanTimeBurn>()?.Register(pawn, caster, props);
        }

        public static void DissolveBuilding(Thing thing, Pawn caster, CompProperties_AbilityMingyuanTimeBurn props)
        {
            if (thing == null || thing.Destroyed || thing.def?.category != ThingCategory.Building)
            {
                return;
            }

            Map map = thing.MapHeld;
            IntVec3 position = thing.PositionHeld;
            if (map == null)
            {
                return;
            }

            PlayEffectSound(props?.effectSoundDef, position, map);
            TryMakeStaticMote(position, map, props?.collapseMoteDef, props?.collapseMoteScale ?? 1f);

            List<ThingDefCountClass> costs = (thing as Frame)?.TotalMaterialCost() ?? thing.CostListAdjusted();
            DropCostList(costs, position, map);
            thing.Destroy(DestroyMode.Vanish);
        }

        public static void TryMakeStaticMote(IntVec3 cell, Map map, ThingDef moteDef, float scale)
        {
            if (map == null || moteDef == null || !cell.IsValid || !cell.InBounds(map))
            {
                return;
            }

            Mote mote = MoteMaker.MakeStaticMote(cell.ToVector3Shifted(), map, moteDef, Mathf.Max(0.1f, scale), false, Rand.Range(0f, 360f));
            if (mote != null)
            {
                mote.exactPosition = cell.ToVector3Shifted();
            }
        }

        public static void PlayEffectSound(SoundDef soundDef, IntVec3 cell, Map map)
        {
            if (soundDef != null && map != null && cell.IsValid)
            {
                soundDef.PlayOneShot(new TargetInfo(cell, map));
            }
        }

        public static void DropThing(ThingDef thingDef, int count, IntVec3 cell, Map map)
        {
            if (thingDef == null || count <= 0 || map == null || !cell.IsValid)
            {
                return;
            }

            Thing thing = ThingMaker.MakeThing(thingDef);
            thing.stackCount = count;
            if (!GenPlace.TryPlaceThing(thing, cell, map, ThingPlaceMode.Near))
            {
                thing.Destroy(DestroyMode.Vanish);
            }
        }

        private static void DropCostList(List<ThingDefCountClass> costs, IntVec3 cell, Map map)
        {
            if (costs == null)
            {
                return;
            }

            for (int i = 0; i < costs.Count; i++)
            {
                ThingDefCountClass cost = costs[i];
                DropThing(cost?.thingDef, cost?.count ?? 0, cell, map);
            }
        }
    }

    public class MingyuanTimeBurnRecord : IExposable
    {
        public Pawn pawn;
        public Pawn caster;
        public int startTick;
        public int endTick;
        public int nextAgeTick;
        public int durationTicks;
        public int tickIntervalTicks;
        public long startAgeTicks;
        public HediffDef markerHediff;
        public ThingDef collapseMoteDef;
        public SoundDef effectSoundDef;
        public float collapseMoteScale = 1f;
        public int mechSteelCount = 75;
        public int mechPlasteelCount = 25;

        public MingyuanTimeBurnRecord()
        {
        }

        public MingyuanTimeBurnRecord(Pawn pawn, Pawn caster, CompProperties_AbilityMingyuanTimeBurn props, int tick)
        {
            Reset(pawn, caster, props, tick);
        }

        public void Reset(Pawn pawn, Pawn caster, CompProperties_AbilityMingyuanTimeBurn props, int tick)
        {
            this.pawn = pawn;
            this.caster = caster;
            startTick = tick;
            durationTicks = Mathf.Max(1, props.durationTicks);
            endTick = tick + durationTicks;
            tickIntervalTicks = Mathf.Max(1, props.tickIntervalTicks);
            nextAgeTick = tick;
            startAgeTicks = Math.Max(0L, pawn?.ageTracker?.AgeBiologicalTicks ?? 0L);
            markerHediff = MingyuanUtility.TimeBurnFrozenDef;
            collapseMoteDef = props.collapseMoteDef;
            effectSoundDef = props.effectSoundDef;
            collapseMoteScale = Mathf.Max(0.1f, props.collapseMoteScale);
            mechSteelCount = Mathf.Max(0, props.mechSteelCount);
            mechPlasteelCount = Mathf.Max(0, props.mechPlasteelCount);
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_References.Look(ref caster, "caster");
            Scribe_Values.Look(ref startTick, "startTick", 0);
            Scribe_Values.Look(ref endTick, "endTick", 0);
            Scribe_Values.Look(ref nextAgeTick, "nextAgeTick", 0);
            Scribe_Values.Look(ref durationTicks, "durationTicks", 2500);
            Scribe_Values.Look(ref tickIntervalTicks, "tickIntervalTicks", 60);
            Scribe_Values.Look(ref startAgeTicks, "startAgeTicks", 0L);
            Scribe_Defs.Look(ref markerHediff, "markerHediff");
            Scribe_Defs.Look(ref collapseMoteDef, "collapseMoteDef");
            Scribe_Defs.Look(ref effectSoundDef, "effectSoundDef");
            Scribe_Values.Look(ref collapseMoteScale, "collapseMoteScale", 1f);
            Scribe_Values.Look(ref mechSteelCount, "mechSteelCount", 75);
            Scribe_Values.Look(ref mechPlasteelCount, "mechPlasteelCount", 25);
        }
    }

    public class GameComponent_MingyuanTimeBurn : GameComponent
    {
        private List<MingyuanTimeBurnRecord> records = new();
        private int nextProcessTick;

        public GameComponent_MingyuanTimeBurn(Game game)
        {
        }

        public void Register(Pawn pawn, Pawn caster, CompProperties_AbilityMingyuanTimeBurn props)
        {
            if (pawn == null || props == null)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;
            for (int i = 0; i < records.Count; i++)
            {
                MingyuanTimeBurnRecord record = records[i];
                if (record?.pawn == pawn)
                {
                    record.Reset(pawn, caster, props, tick);
                    nextProcessTick = tick;
                    return;
                }
            }

            records.Add(new MingyuanTimeBurnRecord(pawn, caster, props, tick));
            nextProcessTick = tick;
        }

        public override void GameComponentTick()
        {
            base.GameComponentTick();
            if (Current.ProgramState != ProgramState.Playing || records.Count == 0)
            {
                return;
            }

            int tick = Find.TickManager.TicksGame;
            if (nextProcessTick > tick)
            {
                return;
            }

            int nextDueTick = int.MaxValue;
            for (int i = records.Count - 1; i >= 0; i--)
            {
                MingyuanTimeBurnRecord record = records[i];
                if (record?.pawn == null || record.pawn.Destroyed || record.pawn.Discarded || record.pawn.Dead)
                {
                    records.RemoveAt(i);
                    continue;
                }

                if (record.pawn.ageTracker == null)
                {
                    FinalizePawn(record);
                    records.RemoveAt(i);
                    continue;
                }

                if (tick < record.nextAgeTick && tick < record.endTick)
                {
                    nextDueTick = Mathf.Min(nextDueTick, NextDueTick(record));
                    continue;
                }

                long newAge = CalculateAge(record, tick);
                if (record.pawn.ageTracker.AgeBiologicalTicks > newAge)
                {
                    record.pawn.ageTracker.AgeBiologicalTicks = newAge;
                }

                if (tick >= record.endTick || record.pawn.ageTracker.AgeBiologicalTicks <= 0L)
                {
                    record.pawn.ageTracker.AgeBiologicalTicks = 0L;
                    FinalizePawn(record);
                    records.RemoveAt(i);
                    continue;
                }

                record.nextAgeTick = Mathf.Min(tick + Mathf.Max(1, record.tickIntervalTicks), record.endTick);
                nextDueTick = Mathf.Min(nextDueTick, NextDueTick(record));
            }

            nextProcessTick = records.Count > 0 && nextDueTick != int.MaxValue ? Mathf.Max(tick + 1, nextDueTick) : 0;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref records, "mingyuanTimeBurnRecords", LookMode.Deep);
            Scribe_Values.Look(ref nextProcessTick, "nextProcessTick", 0);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                records ??= new();
            }
        }

        private static int NextDueTick(MingyuanTimeBurnRecord record)
        {
            if (record == null)
            {
                return int.MaxValue;
            }

            int nextAge = record.nextAgeTick > 0 ? record.nextAgeTick : record.endTick;
            return Mathf.Min(record.endTick, nextAge);
        }

        private static long CalculateAge(MingyuanTimeBurnRecord record, int tick)
        {
            int remainingTicks = Mathf.Max(0, record.endTick - tick);
            if (remainingTicks <= 0 || record.durationTicks <= 0 || record.startAgeTicks <= 0L)
            {
                return 0L;
            }

            return Math.Max(0L, (long)Math.Floor((double)record.startAgeTicks * remainingTicks / record.durationTicks));
        }

        private static void FinalizePawn(MingyuanTimeBurnRecord record)
        {
            Pawn pawn = record.pawn;
            if (pawn == null || pawn.Destroyed || pawn.Dead)
            {
                return;
            }

            Map map = pawn.MapHeld;
            IntVec3 position = pawn.PositionHeld;
            RemoveMarker(pawn, record.markerHediff);
            MingyuanTimeBurnUtility.PlayEffectSound(record.effectSoundDef, position, map);
            MingyuanTimeBurnUtility.TryMakeStaticMote(position, map, record.collapseMoteDef, record.collapseMoteScale);

            bool isMechanoid = pawn.RaceProps?.IsMechanoid ?? false;
            if (ShouldFinalizeWithoutCorpse(pawn, isMechanoid))
            {
                FinalizePawnWithoutCorpse(pawn, map, position);
                DropMechRemains(record, map, position, isMechanoid);
                return;
            }

            DamageInfo dinfo = new(DamageDefOf.Burn, 1f, 999f, -1f, record.caster);
            dinfo.SetIgnoreArmor(true);
            dinfo.SetIgnoreInstantKillProtection(true);
            pawn.Kill(dinfo);
        }

        private static bool ShouldFinalizeWithoutCorpse(Pawn pawn, bool isMechanoid)
        {
            return isMechanoid || pawn?.kindDef == null;
        }

        private static void FinalizePawnWithoutCorpse(Pawn pawn, Map map, IntVec3 position)
        {
            if (pawn == null || pawn.Destroyed)
            {
                return;
            }

            if (!pawn.Dead)
            {
                pawn.health?.SetDead();
            }

            if (!pawn.Destroyed)
            {
                pawn.Destroy(DestroyMode.Vanish);
            }
        }

        private static void DropMechRemains(MingyuanTimeBurnRecord record, Map map, IntVec3 position, bool isMechanoid)
        {
            if (!isMechanoid || map == null)
            {
                return;
            }

            MingyuanTimeBurnUtility.DropThing(ThingDefOf.Steel, record.mechSteelCount, position, map);
            MingyuanTimeBurnUtility.DropThing(ThingDefOf.Plasteel, record.mechPlasteelCount, position, map);
        }

        private static void RemoveMarker(Pawn pawn, HediffDef markerHediff)
        {
            if (pawn?.health == null || markerHediff == null)
            {
                return;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(markerHediff);
            if (hediff != null)
            {
                pawn.health.RemoveHediff(hediff);
            }
        }
    }
}

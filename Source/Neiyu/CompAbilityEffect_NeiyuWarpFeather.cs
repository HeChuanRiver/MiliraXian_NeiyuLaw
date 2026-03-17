using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu
{
    public class CompProperties_AbilityNeiyuWarpFeather : CompProperties_AbilityEffect
    {

        public string effectADefName = "MXNL_ForFeatherCastingCircle";
        public string fallbackEffectDefName = "Skip_EntryNoDelay";

        public string finishFlashFleckDefName = "PsycastSkipFlashExit";


        public ThingDef featherThingDef;
        public IntRange featherCountRange = new IntRange(4, 8);

        public CompProperties_AbilityNeiyuWarpFeather()
        {
            compClass = typeof(CompAbilityEffect_NeiyuWarpFeather);
        }
    }

    public class CompAbilityEffect_NeiyuWarpFeather : CompAbilityEffect
    {
        public new CompProperties_AbilityNeiyuWarpFeather Props
        {
            get { return (CompProperties_AbilityNeiyuWarpFeather)props; }
        }

        public override void CompTick()
        {
            base.CompTick();
        }

        public override void Apply(LocalTargetInfo target, LocalTargetInfo dest)
        {
            base.Apply(target, dest);

            Pawn caster = parent != null ? parent.pawn : null;
            Map map = caster != null ? caster.MapHeld : null;
            if (caster == null || map == null || !caster.Spawned)
            {
                return;
            }

            FleckDef flashDef = ResolveFinishFlashFleckDef();
            if (flashDef != null)
            {
                FleckMaker.Static(caster.Position, map, flashDef, 1f);
            }
            SpawnFeathers(caster, map);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
        }

        private FleckDef ResolveFinishFlashFleckDef()
        {
            FleckDef def = null;

            if (!Props.finishFlashFleckDefName.NullOrEmpty())
            {
                def = DefDatabase<FleckDef>.GetNamedSilentFail(Props.finishFlashFleckDefName);
            }

            if (def == null)
            {
                def = DefDatabase<FleckDef>.GetNamedSilentFail("ExplosionFlash");
            }

            return def;
        }

        private void SpawnFeathers(Pawn caster, Map map)
        {
            if (Props.featherThingDef == null)
            {
                Log.Warning("[MiliraXian_NeiyuLaw] featherThingDef is null in CompProperties_AbilityNeiyuWarpFeather.");
                return;
            }

            int remaining = Mathf.Max(1, Props.featherCountRange.RandomInRange);

            while (remaining > 0)
            {
                Thing thing = ThingMaker.MakeThing(Props.featherThingDef);
                int placeCount = Mathf.Min(remaining, thing.def.stackLimit);
                thing.stackCount = placeCount;

                Thing lastResultingThing;
                bool placed = GenPlace.TryPlaceThing(thing, caster.Position, map, ThingPlaceMode.Near, out lastResultingThing);
                if (!placed && !thing.Destroyed)
                {
                    thing.Destroy(DestroyMode.Vanish);
                    break;
                }

                remaining -= placeCount;
            }
        }
    }
}

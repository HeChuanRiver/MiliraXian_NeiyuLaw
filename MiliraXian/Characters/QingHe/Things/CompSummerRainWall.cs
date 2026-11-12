using System.Collections.Generic;
using MiliraXian.Characters.Common.AbnormalSystem;
using MiliraXian.Characters.Common.AbnormalSystem.Effects;
using MiliraXian.Characters.QingHe.Defs;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things
{
    /// <summary>
    /// The Lotus Rain wall (RangedRework.md 2.3): a band 15 cells long and 3 thick, laid
    /// perpendicular to the caster -> target line. It slows and poisons hostiles that walk into
    /// it; it does not block movement or projectiles (that is the Lunar Mirror's job).
    /// </summary>
    public class CompProperties_SummerRainWall : CompProperties
    {
        /// <summary>Half of the wall's length, in cells (15 long).</summary>
        public float halfLength = 7.5f;

        /// <summary>Half of the wall's thickness, in cells (3 thick).</summary>
        public float halfThickness = 1.5f;

        /// <summary>How long the wall's effect lasts, in ticks, counted from the cast.</summary>
        public int effectDurationTicks = 900;

        /// <summary>How long the rain and the wet ground take to appear, in ticks.</summary>
        public int fadeInTicks = 24;

        /// <summary>How long the rain and the wet ground take to go, in ticks.</summary>
        public int fadeOutTicks = 54;

        /// <summary>How often the wall is evaluated (the slow refresh and the accumulation).</summary>
        public int pulseIntervalTicks = 10;

        /// <summary>Toxin (flesh) and corrosion (mechanical) per second inside the wall.</summary>
        public float toxinAccumulationPerSecond = 30f;

        public float corrosionAccumulationPerSecond = 30f;

        /// <summary>Where the drench hediff is refreshed while inside; it lingers on its own after.</summary>
        public HediffDef drenchedHediff;

        public FleckDef ambientSplashFleckDef;

        public CompProperties_SummerRainWall()
        {
            compClass = typeof(CompSummerRainWall);
        }
    }

    /// <summary>
    /// The wall's logic. It is spawned once the cast succeeds and lives for its duration, fading
    /// in at the start and out at the end; the rain and the wet ground share that one curve.
    /// </summary>
    public class CompSummerRainWall : ThingComp
    {
        private Pawn caster;
        private Vector2 axisLength = Vector2.right;
        private Vector2 axisThickness = Vector2.up;
        private int startTick;
        private int endTick;
        private int nextPulseTick;
        private int ticksToNextAmbientVisual;

        public CompProperties_SummerRainWall Props => (CompProperties_SummerRainWall)props;

        /// <summary>The wall's direction along its length, in map cells.</summary>
        public Vector2 AxisLength => axisLength;

        /// <summary>The wall's direction across its thickness.</summary>
        public Vector2 AxisThickness => axisThickness;

        public float HalfLength => Props.halfLength;

        public float HalfThickness => Props.halfThickness;

        /// <summary>Seconds since the wall was cast; the picture uses it to animate the fall.</summary>
        public float SecondsSinceStart => (Find.TickManager.TicksGame - startTick) / 60f;

        /// <summary>
        /// 0 to 1: fades in when the wall appears, holds, then fades out. The rain and the wet
        /// ground both use it, so they come and go together.
        /// </summary>
        public float Strength
        {
            get
            {
                int now = Find.TickManager.TicksGame;
                int inTicks = now - startTick;
                if (inTicks < Props.fadeInTicks)
                {
                    return Props.fadeInTicks > 0
                        ? Mathf.Clamp01(inTicks / (float)Props.fadeInTicks)
                        : 1f;
                }
                int left = endTick - now;
                if (left <= 0)
                {
                    return 0f;
                }
                return Props.fadeOutTicks > 0
                    ? Mathf.Clamp01(left / (float)Props.fadeOutTicks)
                    : 1f;
            }
        }

        /// <summary>
        /// Places the wall. Called once the cast has succeeded, so there is no warm-up to wait for:
        /// the rain, the wet ground and the flowers all fade in from here.
        /// </summary>
        public void Init(Pawn newCaster)
        {
            caster = newCaster;
            startTick = Find.TickManager.TicksGame;
            endTick = startTick + Props.effectDurationTicks;
            nextPulseTick = 0;
            ticksToNextAmbientVisual = Rand.RangeInclusive(10, 30);

            // Perpendicular to the caster -> target line; the target is the parent's cell.
            Map map = parent?.Map;
            if (caster != null && map != null)
            {
                Vector2 toTarget = new Vector2(parent.Position.x - caster.Position.x,
                                               parent.Position.z - caster.Position.z);
                if (toTarget.sqrMagnitude > 0.01f)
                {
                    toTarget.Normalize();
                }
                else
                {
                    toTarget = Vector2.right;
                }
                axisThickness = toTarget;
                axisLength = new Vector2(-toTarget.y, toTarget.x);
            }
            // The renderer is wired up by PostSpawnSetup, which RimWorld has already called by now.
        }

        /// <summary>Is this map cell inside the band?</summary>
        public bool Contains(IntVec3 cell)
        {
            Vector2 v = new Vector2(cell.x - parent.Position.x, cell.z - parent.Position.z);
            return Mathf.Abs(Vector2.Dot(v, axisLength)) <= Props.halfLength
                && Mathf.Abs(Vector2.Dot(v, axisThickness)) <= Props.halfThickness;
        }

        public override void CompTick()
        {
            base.CompTick();
            if (parent.Destroyed || parent.Map == null)
            {
                return;
            }

            int now = Find.TickManager.TicksGame;
            if (now >= endTick + Props.fadeOutTicks)
            {
                parent.Destroy();
                return;
            }

            // The effect is live for the whole duration; the fade only changes the picture, so a
            // pawn standing in the rain from the first tick is already slowed.
            if (now < endTick && now >= nextPulseTick)
            {
                nextPulseTick = now + Mathf.Max(1, Props.pulseIntervalTicks);
                Pulse(Props.pulseIntervalTicks / 60f);
            }

            if (ticksToNextAmbientVisual-- <= 0)
            {
                ticksToNextAmbientVisual = 30 + Rand.RangeInclusive(-10, 15);
                PlayAmbientVisual();
            }
        }

        /// <summary>The wall stops early (the caster died, the map is gone).</summary>
        public void CutShort()
        {
            if (parent != null && !parent.Destroyed)
            {
                endTick = Mathf.Min(endTick, Find.TickManager.TicksGame + Props.fadeOutTicks);
            }
        }

        /// <summary>
        /// One evaluation of the band: refresh the drench on everything hostile inside it and add
        /// that slice of a second's worth of toxin or corrosion. Only hostiles are touched.
        /// </summary>
        private void Pulse(float seconds)
        {
            Map map = parent.Map;
            if (caster == null || map == null)
            {
                return;
            }

            float effectFactor = MX_QH_PerkUtility.GetSpellEffectFactor(caster);
            IntVec3 centre = parent.Position;
            int reach = Mathf.CeilToInt(Mathf.Max(Props.halfLength, Props.halfThickness));
            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn pawn = pawns[i];
                if (pawn == null || pawn.Dead || !pawn.Spawned)
                {
                    continue;
                }
                if (pawn.Position.DistanceToSquared(centre) > reach * reach)
                {
                    continue;
                }
                if (!Contains(pawn.Position) || !GenHostility.HostileTo(caster, pawn))
                {
                    continue;
                }

                ApplyDrench(pawn);
                if (pawn.RaceProps?.IsMechanoid == true)
                {
                    AbnormalSystem.ApplyAccumulation(caster, pawn, MX_AbnormalDefOf.MX_AbnormalCorrosion,
                        Props.corrosionAccumulationPerSecond * seconds * effectFactor);
                }
                else
                {
                    AbnormalSystem.ApplyAccumulation(caster, pawn, MX_AbnormalDefOf.MX_AbnormalToxin,
                        Props.toxinAccumulationPerSecond * seconds * effectFactor);
                }
            }
        }

        private void ApplyDrench(Pawn pawn)
        {
            HediffDef def = Props.drenchedHediff ?? MX_QHDefOf.MX_QH_RainDrenched;
            if (def == null)
            {
                return;
            }

            Hediff hediff = pawn.health.hediffSet.GetFirstHediffOfDef(def);
            if (hediff != null)
            {
                hediff.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
                return;
            }

            hediff = HediffMaker.MakeHediff(def, pawn);
            hediff.Severity = 1f;
            pawn.health.AddHediff(hediff);
        }

        private void PlayAmbientVisual()
        {
            Map map = parent?.Map;
            FleckDef splash = Props.ambientSplashFleckDef ?? MX_QHDefOf.GroundWaterSplash;
            float strength = Strength;
            if (map == null || splash == null || strength <= 0.05f)
            {
                return;
            }

            int placed = 0;
            for (int attempt = 0; attempt < 6 && placed < 2; attempt++)
            {
                IntVec3 cell = parent.Position + new IntVec3(
                    Mathf.RoundToInt(Rand.Range(-Props.halfLength, Props.halfLength)),
                    0,
                    Mathf.RoundToInt(Rand.Range(-Props.halfThickness, Props.halfThickness)));
                if (!cell.InBounds(map))
                {
                    continue;
                }
                FleckMaker.Static(cell, map, splash, Rand.Range(0.22f, 0.5f) * strength);
                placed++;
            }
        }

        public override bool DontDrawParent()
        {
            // The picture is drawn by the custom renderer; the Thing must not draw itself.
            return true;
        }

        /// <summary>
        /// Owner 11-13: the wall registers itself with the renderer here rather than making the
        /// renderer search the map for it. This also runs when a save is loaded, so a wall already
        /// standing when the game resumes is drawn again.
        /// </summary>
        public override void PostSpawnSetup(bool respawningAfterLoad)
        {
            base.PostSpawnSetup(respawningAfterLoad);
            RainWallVfx.Add(parent.Map, this);
        }

        public override void PostDestroy(DestroyMode mode, Map previousMap)
        {
            base.PostDestroy(mode, previousMap);
            RainWallVfx.Remove(previousMap ?? parent.Map, this);
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref caster, "caster", false);
            Scribe_Values.Look(ref axisLength, "axisLength", Vector2.right);
            Scribe_Values.Look(ref axisThickness, "axisThickness", Vector2.up);
            Scribe_Values.Look(ref startTick, "startTick", 0);
            Scribe_Values.Look(ref endTick, "endTick", 0);
            Scribe_Values.Look(ref nextPulseTick, "nextPulseTick", 0);
            Scribe_Values.Look(ref ticksToNextAmbientVisual, "ticksToNextAmbientVisual", 0);
        }
    }
}

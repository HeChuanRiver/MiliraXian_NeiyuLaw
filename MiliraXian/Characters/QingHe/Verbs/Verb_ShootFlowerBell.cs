using System.Collections.Generic;
using MiliraXian.Characters.Common.SweepDamage;
using MiliraXian.Characters.QingHe.Things.Projectiles;
using MiliraXian.Characters.QingHe.Things.Weapons;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Verbs
{
    /// <summary>
    /// The Flower Bell's five-tier attack: the warmup is a charge
    /// (<see cref="Stance_FlowerBellCharge"/>); on release she holds still for the throw, then all
    /// the notes of the reached tier leave at once. One burst shot casts them all. Once the throw has
    /// begun only a stun stops it (vanilla resets a bursting verb only then).
    /// </summary>
    public class Verb_ShootFlowerBell : Verb_Shoot
    {
        /// <summary>The throw before the notes leave (0.2 s).</summary>
        internal const int ThrowTicks = 12;

        /// <summary>Degree of the note being cast now; read by the note's launch. -1 outside a cast.</summary>
        internal static int CastingDegree = -1;
        /// <summary>The charge comp and the volley's tier, while a note is cast (for its damage).</summary>
        internal static CompFlowerBellCharge CastingCharge;
        internal static int CastingTier;
        /// <summary>Ground point the note being cast leaves from (under its hovering place), and its height there.</summary>
        internal static Vector3? CastingOrigin;
        internal static float CastingLift;

        private int pendingNotes;
        private int pendingModeStart;
        private IntVec3 targetCell = IntVec3.Invalid;

        private CompFlowerBellCharge Charge => EquipmentSource?.GetComp<CompFlowerBellCharge>();

        public override ThingDef Projectile
        {
            get
            {
                ThingDef projectile = EquipmentSource?.TryGetComp<CompFlowerBellResonance>()?.CurrentProjectileFor(CasterPawn);
                return projectile ?? base.Projectile;
            }
        }

        public override bool TryStartCastOn(LocalTargetInfo castTarg, LocalTargetInfo destTarg, bool surpriseAttack = false,
            bool canHitNonTargetPawns = true, bool preventFriendlyFire = false, bool nonInterruptingSelfCast = false)
        {
            if (!base.TryStartCastOn(castTarg, destTarg, surpriseAttack, canHitNonTargetPawns, preventFriendlyFire, nonInterruptingSelfCast))
            {
                return false;
            }
            CompFlowerBellCharge comp = Charge;
            // The vanilla warmup is up (its checks passed): it becomes the charge, from the charge left.
            if (comp != null && CasterIsPawn && CasterPawn.stances.curStance is Stance_Warmup warmup && warmup.verb == this
                && warmup is not Stance_FlowerBellCharge)
            {
                comp.BeginRun();
                CasterPawn.stances.SetStance(new Stance_FlowerBellCharge(Stance_FlowerBellCharge.ChargeTicksLeft(comp), castTarg, this));
            }
            return true;
        }

        public override void WarmupComplete()
        {
            CompFlowerBellCharge comp = Charge;
            if (comp == null)
            {
                base.WarmupComplete();
                return;
            }
            int notes = comp.Tier;
            if (notes < 1)
            {
                // Not even one tier (should not happen: the stance releases from tier 1 on).
                Reset();
                return;
            }
            pendingNotes = notes;
            pendingModeStart = comp.ModeStart;
            targetCell = currentTarget.Cell;
            comp.Consume();
            if (CasterIsPawn)
            {
                FlowerBellChargeVfx.Released(comp, CasterPawn, notes);
                FlowerBellChimes.Arpeggio(CasterPawn.Map, CasterPawn.Position, pendingModeStart, notes);
                // What the vanilla warmup completion would do (skipped: it would fire at once).
                Find.BattleLog.Add(new BattleLogEntry_RangedFire(caster, currentTarget.HasThing ? currentTarget.Thing : null, EquipmentSource?.def, Projectile, notes > 1));
                if (currentTarget.Thing is Pawn { Downed: false, IsColonyMech: false } target && CasterPawn.skills != null)
                {
                    float xp = target.HostileTo(caster) ? 170f : 20f;
                    CasterPawn.skills.Learn(SkillDefOf.Shooting, xp * verbProps.AdjustedFullCycleTime(this, CasterPawn));
                }
            }
            // One burst shot after the throw; she stands still until then.
            burstShotsLeft = 1;
            state = VerbState.Bursting;
            ticksToNextBurstShot = ThrowTicks;
            if (CasterIsPawn && !NonInterruptingSelfCast)
            {
                CasterPawn.stances.SetStance(new Stance_Cooldown(ThrowTicks + 1, currentTarget, this));
            }
        }

        protected override bool TryCastShot()
        {
            if (pendingNotes <= 0)
            {
                return false;
            }
            // The target died or vanished during the throw: the notes go to where it was (they home).
            if (!currentTarget.IsValid || (currentTarget.HasThing && (!currentTarget.Thing.Spawned || currentTarget.Thing.Map != caster.Map)))
            {
                if (!targetCell.IsValid)
                {
                    return false;
                }
                currentTarget = targetCell;
            }
            bool any = false;
            LocalTargetInfo selected = currentTarget;
            CompFlowerBellCharge comp = Charge;
            List<LocalTargetInfo> targets = AssignTargets(comp, selected, pendingNotes);
            try
            {
                float seconds = Find.TickManager.TicksGame / 60f;
                for (int i = 0; i < pendingNotes; i++)
                {
                    CastingDegree = pendingModeStart + i;
                    CastingCharge = comp;
                    CastingTier = pendingNotes;
                    if (CasterIsPawn)
                    {
                        // Each note leaves from where it hovered, over its petal.
                        CastingOrigin = FlowerBellChargeVfx.HoverGround(CasterPawn, i, out CastingLift, seconds);
                    }
                    currentTarget = targets[i];
                    bool shot = base.TryCastShot();
                    if (!shot && currentTarget != selected)
                    {
                        // No shot line to the scattered target: this note goes to the selected one.
                        currentTarget = selected;
                        shot = base.TryCastShot();
                    }
                    any |= shot;
                }
            }
            finally
            {
                currentTarget = selected;
                CastingDegree = -1;
                CastingCharge = null;
                CastingTier = 0;
                CastingOrigin = null;
                CastingLift = 0f;
            }
            pendingNotes = 0;
            return any;
        }

        /// <summary>
        /// The target of each note. Focus: all the selected one. Scatter: the first the selected one,
        /// the others random pawns and buildings within the scatter radius of it (see <see cref="ScatterCandidates"/>),
        /// those no note of this volley has yet first; with none around, the selected one.
        /// </summary>
        private List<LocalTargetInfo> AssignTargets(CompFlowerBellCharge comp, LocalTargetInfo selected, int count)
        {
            List<LocalTargetInfo> targets = new(count);
            for (int i = 0; i < count; i++)
            {
                targets.Add(selected);
            }
            if (comp == null || comp.AimMode != FlowerBellAimMode.Scatter || count < 2 || caster?.Map == null)
            {
                return targets;
            }
            List<Thing> pool = ScatterCandidates(selected, comp.ScatterRadius);
            if (pool.Count == 0)
            {
                return targets;
            }
            List<Thing> fresh = new(pool);
            for (int i = 1; i < count; i++)
            {
                if (fresh.Count == 0)
                {
                    fresh.AddRange(pool);
                    if (selected.Thing is { Spawned: true } first)
                    {
                        fresh.Add(first);
                    }
                }
                Thing pick = fresh.RandomElement();
                fresh.Remove(pick);
                targets[i] = pick;
            }
            return targets;
        }

        /// <summary>
        /// Standing pawns and targetable buildings within <paramref name="radius"/> of the selected target,
        /// other than it, that an area attack may hit: hostile ones, and neutral ones with that setting on.
        /// Natural rock is left out.
        /// </summary>
        private List<Thing> ScatterCandidates(LocalTargetInfo selected, float radius)
        {
            List<Thing> list = new();
            Pawn self = CasterPawn;
            if (self == null)
            {
                return list;
            }
            foreach (Thing t in GenRadial.RadialDistinctThingsAround(selected.Cell, caster.Map, radius, useCenter: true))
            {
                if (t == caster || t == selected.Thing || t.Destroyed || !t.Spawned)
                {
                    continue;
                }
                if (t is Pawn pawn)
                {
                    if (pawn.Dead || pawn.Downed)
                    {
                        continue;
                    }
                }
                else if (t is not Building b || b.def.building?.isTargetable != true || !b.def.useHitPoints
                    || SweepTargeting.KindOf(b) == SweepHitKind.Terrain)
                {
                    continue;
                }
                if (SweepTargeting.FactionAllows(self, t, SweepTargetFaction.Hostile))
                {
                    list.Add(t);
                }
            }
            return list;
        }

        public override void DrawHighlight(LocalTargetInfo target)
        {
            base.DrawHighlight(target);
            CompFlowerBellCharge comp = Charge;
            if (target.IsValid && comp != null && comp.AimMode == FlowerBellAimMode.Scatter)
            {
                GenDraw.DrawRadiusRing(target.Cell, comp.ScatterRadius);
            }
        }

        public override void Reset()
        {
            base.Reset();
            pendingNotes = 0;
            targetCell = IntVec3.Invalid;
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref pendingNotes, "mx_bellPendingNotes", 0);
            Scribe_Values.Look(ref pendingModeStart, "mx_bellPendingMode", 0);
            Scribe_Values.Look(ref targetCell, "mx_bellTargetCell", IntVec3.Invalid);
        }
    }
}

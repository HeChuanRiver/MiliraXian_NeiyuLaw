using MiliraXian.Characters.QingHe.Things.Projectiles;
using MiliraXian.Characters.QingHe.Things.Weapons;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Verbs
{
    /// <summary>
    /// The Flower Bell's attack (RangedRework.md 2.9, five tiers): the warmup is a charge
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
        /// <summary>The charge comp and the note's index in the volley, while it is cast (for its damage).</summary>
        internal static CompFlowerBellCharge CastingCharge;
        internal static int CastingIndex = -1;
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
            try
            {
                float seconds = Find.TickManager.TicksGame / 60f;
                for (int i = 0; i < pendingNotes; i++)
                {
                    CastingDegree = pendingModeStart + i;
                    CastingCharge = Charge;
                    CastingIndex = i;
                    if (CasterIsPawn)
                    {
                        // Each note leaves from where it hovered, over its petal (RangedRework.md 2.9).
                        CastingOrigin = FlowerBellChargeVfx.HoverGround(CasterPawn, i, out CastingLift, seconds);
                    }
                    any |= base.TryCastShot();
                }
            }
            finally
            {
                CastingDegree = -1;
                CastingCharge = null;
                CastingIndex = -1;
                CastingOrigin = null;
                CastingLift = 0f;
            }
            pendingNotes = 0;
            return any;
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

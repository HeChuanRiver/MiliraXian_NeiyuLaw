using MiliraXian.Characters.Common.SweepDamage;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities.SakuraDance
{
    /// <summary>
    /// Qinghe catches an enemy's Lightning Reversal on her blade: drafted in the sword stance with
    /// Sakura Dance ready, the release cut (the third, charged cut) that reaches her deals nothing; she drops what she is
    /// doing and casts Sakura Dance back at its caster, carrying the caught thunder from the first cut
    /// (no sky bolt). The ability's cooldown is spent as for a normal cast.
    /// </summary>
    [StaticConstructorOnStartup]
    public static class SakuraThunderReturn
    {
        /// <summary>Set on the dance job (<see cref="Verse.AI.Job.count"/>) of a returning cast.</summary>
        public const int JobMarker = 0x5A17;

        /// <summary>True while the returning cast is being activated (the effect marks its job).</summary>
        public static bool Casting { get; private set; }

        /// <summary>The release cut's sweep (the third cut, Lightning Reversal); only it can be caught.</summary>
        private static SweepDamageDef releaseSweep;

        static SakuraThunderReturn()
        {
            releaseSweep = DefDatabase<SweepDamageDef>.GetNamedSilentFail("MX_QH_SakuraThunderSweep");
            SweepHitInterceptors.Register(TryCatch);
        }

        private static bool TryCatch(SweepDamageSource source, Thing target)
        {
            if (releaseSweep == null || source.def != releaseSweep || target is not Pawn pawn
                || source.caster is not { Spawned: true, Dead: false } caster || caster.Map != pawn.Map
                || caster == pawn || !CanReturn(pawn, out Ability ability))
            {
                return false;
            }
            // The dance aims at a cell other than her own (CompAbilityEffect_SakuraDance.Valid).
            LocalTargetInfo at = new(caster);
            if (caster.Position == pawn.Position)
            {
                return false;
            }
            // Drop the current action, then the dance replaces the job (the effect starts it forced).
            pawn.stances?.CancelBusyStanceHard();
            pawn.pather?.StopDead();
            pawn.rotationTracker?.FaceTarget(caster);
            Casting = true;
            try
            {
                ability.Activate(at, at);
            }
            finally
            {
                Casting = false;
            }
            return pawn.CurJobDef == MX_QHDefOf.MX_QH_SakuraDance;
        }

        private static bool CanReturn(Pawn pawn, out Ability ability)
        {
            ability = null;
            if (QinghePowerBalance.Sealed || !pawn.Spawned || pawn.Dead || pawn.Downed || !pawn.Drafted
                || pawn.InMentalState || pawn.stances?.stunner?.Stunned == true
                // Dancing, aiming the dance or executing: invulnerable, the cut does nothing and is not caught.
                || SakuraDanceInvulnerability.Active(pawn)
                || Common.Combat.CombatPosture.Get(pawn)?.Broken == true
                || MX_QH_HediffUtility.GetCombatStance(pawn)?.CurrentStance != MX_QHDefOf.MX_QH_Stance_Sword
                || pawn.abilities == null)
            {
                return false;
            }
            foreach (Ability a in pawn.abilities.AllAbilitiesForReading)
            {
                if (a.CompOfType<CompAbilityEffect_SakuraDance>() != null)
                {
                    if (a.CanCast)
                    {
                        ability = a;
                        return true;
                    }
                    return false;
                }
            }
            return false;
        }
    }
}

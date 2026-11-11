using MiliraXian.Characters.Common;
using MiliraXian.Characters.Common.Combat;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Vfx;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    /// <summary>
    /// Ranged evade of the Florescence stance: a projectile aimed at the bell's wielder is dodged with
    /// the stance's chance. The projectile is not stopped, it flies on along its path and misses.
    /// A successful evade shows "闪", flashes the body snapshot (FlowerBellEvadeVfx) and speeds up the
    /// flower decree recovery for a second.
    /// </summary>
    [StaticConstructorOnStartup]
    internal sealed class FlowerBellRangedEvadeBlocker : CombatBlocker
    {
        public override int Priority => 5;

        static FlowerBellRangedEvadeBlocker()
        {
            CombatBlockers.Register(new FlowerBellRangedEvadeBlocker());
        }

        // Qinghe in the Florescence stance, whatever ranged weapon she holds (the stance's weaponClass
        // already limits it to ranged ones). Decorative mode seals the stances.
        public override bool Covers(Pawn pawn) =>
            !QinghePowerBalance.Sealed && MX_QHCharacterUtility.IsQinghe(pawn)
            && MX_QH_HediffUtility.GetCombatStance(pawn)?.CurrentStance == MX_QHDefOf.MX_QH_Stance_Bell;

        public override bool TryBlockProjectile(Projectile projectile, Pawn defender)
        {
            if (projectile == null || defender?.Map == null || projectile.Map != defender.Map)
            {
                return false;
            }
            // Helpless: downed, stunned, or out of her mind.
            if (defender.Downed || defender.Dead || defender.InMentalState
                || defender.stances?.stunner?.Stunned == true)
            {
                return false;
            }

            float chance = defender.GetStatValue(MX_QHDefOf.MX_QH_RangedEvadeChance);
            if (!Rand.Chance(chance))
            {
                return false;
            }

            // The projectile flies on and never calls Impact: a homing one drops her as its target and
            // does not pick her again when it retargets.
            projectile.TryGetComp<CompProjectileHomingCurve>()?.NotifyEvadedBy(defender);

            MoteMaker.ThrowText(defender.DrawPos + new Vector3(0f, 0f, 1.5f), defender.Map, "闪避", Color.white, 1.5f);

            FlowerBellEvadeVfx.GetOrCreate(defender.Map)?.Notify(defender);

            BoostDecreeRecovery(defender);

            return true;
        }

        /// <summary>One second of faster flower decree recovery, refreshed rather than stacked.</summary>
        private static void BoostDecreeRecovery(Pawn pawn)
        {
            HediffDef def = MX_QHDefOf.MX_QH_FlowerDecreeEvasionBoost;
            Hediff existing = pawn.health?.hediffSet?.GetFirstHediffOfDef(def);
            if (existing != null)
            {
                existing.TryGetComp<HediffComp_Disappears>()?.ResetElapsedTicks();
                return;
            }
            Hediff hediff = HediffMaker.MakeHediff(def, pawn);
            pawn.health?.AddHediff(hediff);
        }
    }
}

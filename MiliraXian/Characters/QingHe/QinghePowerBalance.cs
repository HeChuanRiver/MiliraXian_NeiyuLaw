using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using RimWorld;
using Verse;
using MiliraXian.Characters.Common;
using static MiliraXian.Characters.Common.CharacterPowerProfile;

namespace MiliraXian.Characters.QingHe
{
    internal static class QinghePowerBalance
    {
        internal static readonly CharacterPowerProfile Profile = new CharacterPowerProfile();

        public static bool Sealed => Profile.Sealed;
        public static bool ZeroLevelPassivesEnabled => !Sealed;
        public static int MaxEffectiveLevel => Sealed ? 0 : HediffComp_AuraMastery.MaxAuraMasteryLevel;
        public static void SetDecorative(bool decorative)
        {
            Profile.SetDecorative(decorative);
        }

        public static void ApplyDecorative(bool decorative)
        {
            if (Sealed == decorative) return;
            SetDecorative(decorative);
            // Settings can be written from the main menu, where no pawns exist to sync.
            if (Current.Game == null)
            {
                return;
            }

            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
            {
                if (pawn == null || pawn.Dead || !MX_QHCharacterUtility.IsQinghe(pawn))
                {
                    continue;
                }

                MX_QH_HediffUtility.GetAuraMasteryComp(pawn)?.SetEffectiveLevel();
                MX_QH_HediffUtility.SyncAuraShieldForPowerLevel(pawn);
                SyncSealHediff(pawn);
            }
        }

        /// <summary>
        /// Sealing suppresses passive recovery as a stat factor of zero rather than a code-side
        /// gate, so it also cancels transient recovery offsets such as the sword-pressure afterglow.
        /// </summary>
        internal static void SyncSealHediff(Pawn pawn)
        {
            Hediff seal = pawn.health?.hediffSet?.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_PowerSealed);
            if (Sealed)
            {
                if (seal == null)
                {
                    pawn.health.AddHediff(MX_QHDefOf.MX_QH_PowerSealed);
                }
            }
            else if (seal != null)
            {
                pawn.health.RemoveHediff(seal);
            }
        }

        internal static void Initialize()
        {
            Profile.Apply();
        }
    }

    [Verse.StaticConstructorOnStartup]
    internal static class QinghePowerBalanceBootstrap
    {
        static QinghePowerBalanceBootstrap() => QinghePowerBalance.Initialize();
    }
}

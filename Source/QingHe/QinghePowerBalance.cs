using System.Collections.Generic;
using MiliraXian.Characters.Neiyu;
using MiliraXian.Characters.QingHe.Hediffs;
using Verse;
using static MiliraXian.Characters.CharacterPowerProfile;

namespace MiliraXian.Characters.QingHe
{
    internal static class QinghePowerBalance
    {
        internal static readonly CharacterPowerProfile Profile = new CharacterPowerProfile();

        public static bool IsOriginal => Profile.Original;
        public static bool IsBalanced => Profile.Balanced;
        public static bool Sealed => Profile.Sealed;
        public static bool ZeroLevelPassivesEnabled => !Sealed;
        public static int MaxEffectiveLevel => IsOriginal ? 24 : IsBalanced ? 12 : 0;
        public static void SetLevel(CharacterPowerLevel level)
        {
            Profile.SetLevel(level);
            IReadOnlyList<Pawn> pawns = Find.CurrentMap?.mapPawns?.AllPawnsSpawned;
            if (pawns == null) return;
            for (int i = 0; i < pawns.Count; i++)
            {
                if (MX_QHCharacterUtility.IsQinghe(pawns[i]))
                {
                    MX_QH_HediffUtility.GetDivineGraceComp(pawns[i])?.SyncForPowerLevel();
                    MX_QH_HediffUtility.SyncDivineProtectionForPowerLevel(pawns[i]);
                }
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

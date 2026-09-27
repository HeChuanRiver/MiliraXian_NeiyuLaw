using MiliraXian.CharacterLib;
using MiliraXian.Characters.Neiyu;
using Verse;

namespace MiliraXian.Characters.Zhaoli
{
    internal static class ZhaoliSCMActivity
    {
        public static void Sync(Pawn pawn)
        {
            if (!ZhaoliKarmaUtility.IsZhaoli(pawn)) return;
            string reason = ZhaoliScenarioUtility.IsHideoutState(pawn) ? "角色正在藏身处任务中，暂时无法召回。"
                : ZhaoliScenarioUtility.IsRaidState(pawn) ? "角色正在袭击中，暂时无法召回。"
                : Current.Game?.GetComponent<GameComponent_ZhaoliRebirth>()?.IsPending(pawn) == true
                    || Current.Game?.GetComponent<GameComponent_ZhaoliKarma>()?.IsPending(pawn) == true
                    ? "角色正在重生，暂时无法召回。" : null;
            CharacterSCM.SetUnavailable(pawn, reason != null, reason);
        }
    }

    public abstract class HediffComp_ZhaoliScenarioState : HediffComp
    {
        public override void CompPostPostAdd(DamageInfo? dinfo) => ZhaoliSCMActivity.Sync(Pawn);
        public override void CompPostPostRemoved()
        {
            ZhaoliSCMActivity.Sync(Pawn);
            NeiyuSpecialPawnIntegration.TryRegister(Pawn);
        }
        public override void CompExposeData()
        {
            if (Scribe.mode == LoadSaveMode.PostLoadInit) ZhaoliSCMActivity.Sync(Pawn);
        }
    }
}

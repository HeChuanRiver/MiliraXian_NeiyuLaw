using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Zhaoli
{
    [StaticConstructorOnStartup]
    internal static class ZhaoliPlayerChallengeUtility
    {
        private static readonly Texture2D Icon = ContentFinder<Texture2D>.Get("MiliraXianZhaoli/UI/Zhaoli_DeathField");

        internal static Gizmo CreateGizmo(Pawn pawn)
        {
            var command = new Command_Action
            {
                defaultLabel = "挑战昭离",
                defaultDesc = "让昭离传送至本地图边境，立即与殖民地敌对。战败后，原来的昭离将在一天后返回。\n战斗会造成真实伤亡与建筑损毁。点击后需两次确认。",
                icon = Icon,
                action = () => ShowWarning(pawn, pawn.Map)
            };
            string reason = DisabledReason(pawn);
            if (reason != null) command.Disable(reason);
            return command;
        }

        private static string DisabledReason(Pawn pawn)
        {
            if (!ZhaoliKarmaUtility.IsZhaoli(pawn) || pawn.Faction != Faction.OfPlayer || !pawn.IsFreeColonist)
                return "需要我方昭离。";
            if (!pawn.Spawned || pawn.Map?.IsPlayerHome != true) return "需要位于殖民地地图。";
            if (pawn.Dead || pawn.Downed || pawn.InMentalState || pawn.stances?.stunner?.Stunned == true)
                return "昭离目前无法主动应战。";
            if (ZhaoliPowerBalance.Sealed) return "昭离的力量已封印。";
            HediffDef dormancy = DefDatabase<HediffDef>.GetNamedSilentFail(ZhaoliKarmaUtility.DormancyHediffDefName);
            if (ZhaoliScenarioUtility.IsRaidState(pawn) || ZhaoliScenarioUtility.IsHideoutState(pawn)
                || (dormancy != null && pawn.health.hediffSet.GetFirstHediffOfDef(dormancy) != null))
                return "昭离正在其他特殊状态中。";
            if (Current.Game?.GetComponent<GameComponent_ZhaoliKarma>() == null
                || MXZL_ZhaoliDefOf.MXZL_ZhaoliRaidState == null) return "挑战所需的角色状态不可用。";
            return null;
        }

        private static bool StillAvailable(Pawn pawn, Map map)
        {
            string reason = DisabledReason(pawn);
            if (reason == null && pawn.Map == map) return true;
            Messages.Message(reason ?? "昭离已离开确认时的地图，未开始挑战。", MessageTypeDefOf.RejectInput, false);
            return false;
        }

        private static void ShowWarning(Pawn pawn, Map map)
        {
            if (!StillAvailable(pawn, map)) return;
            Find.WindowStack.Add(new Dialog_MessageBox(
                "昭离将立即传送至本地图边境，转为敌对，并按四阶段袭击流程作战。你将失去对她的指挥权，不能通过此按钮中止战斗。\n\n"
                + "流场、替身死亡与第四阶段补刀均按真实战斗结算，可能杀死殖民者及其亲属，也可能摧毁建筑。其他人的伤亡与损失不会随昭离回归而恢复。\n\n"
                + "昭离原有的死亡成长与因果链接会保留，挑战期间使用袭击替身。原链接仍会正常到期。她最终战败后，一天后以原角色返回我方，恢复挑战前的护盾与因果；不会另行生成一名昭离。",
                "已知晓风险，继续", () => ShowFinalConfirmation(pawn, map), "取消", null, "挑战昭离：风险警告"));
        }

        private static void ShowFinalConfirmation(Pawn pawn, Map map)
        {
            if (!StillAvailable(pawn, map)) return;
            var dialog = new Dialog_MessageBox(
                "确认后，昭离将立即从殖民地边境开始进攻。\n\n请先保存游戏并安排防守。这是一场会造成永久伤亡与损毁的真实战斗。\n\n确定现在开始？",
                "开始真实战斗", () => TryStart(pawn, map), "取消", null, "最终确认", buttonADestructive: true)
            {
                interactionDelay = 3f
            };
            Find.WindowStack.Add(dialog);
        }

        private static void TryStart(Pawn pawn, Map map)
        {
            if (!StillAvailable(pawn, map)) return;
            Faction faction = ZhaoliScenarioUtility.ResolveHostileFaction();
            if (faction == null || !faction.HostileTo(Faction.OfPlayer))
            {
                Messages.Message("没有可用的敌对派系，未开始挑战。", MessageTypeDefOf.RejectInput, false);
                return;
            }
            if (!CellFinder.TryFindRandomEdgeCellWith(
                cell => cell.Standable(map) && !cell.Fogged(map) && cell.GetFirstPawn(map) == null
                    && map.reachability.CanReachColony(cell), map, CellFinder.EdgeRoadChance_Hostile, out IntVec3 cell))
            {
                Messages.Message("找不到可通往殖民地的安全边境落点，未开始挑战。", MessageTypeDefOf.RejectInput, false);
                return;
            }

            Hediff state = HediffMaker.MakeHediff(MXZL_ZhaoliDefOf.MXZL_ZhaoliRaidState, pawn);
            var raid = state.TryGetComp<HediffComp_ZhaoliRaidState>();
            if (raid == null || ZhaoliKarmaUtility.GetLinkComp(pawn) == null
                || ZhaoliKarmaUtility.GetKarmaComp(pawn) == null || ZhaoliShieldLayerUtility.GetShieldComp(pawn) == null)
            {
                Messages.Message("昭离的战斗组件不完整，未开始挑战。", MessageTypeDefOf.RejectInput, false);
                return;
            }

            raid.PreparePlayerChallenge();
            pawn.health.AddHediff(state);
            pawn.jobs?.StopAll(false, true);
            PawnKindDef kind = pawn.kindDef;
            pawn.SetFaction(faction);
            if (pawn.kindDef != kind) pawn.ChangeKind(kind);
            pawn.Position = cell;
            pawn.Notify_Teleported();
            raid.InitializeRaid(null);
            Find.LetterStack.ReceiveLetter("昭离挑战已开始",
                "昭离已传送至地图边境并开始进攻。战斗中的伤亡与损毁会永久保留。击败她后，原来的昭离将在一天后返回。",
                LetterDefOf.ThreatBig, pawn);
        }
    }

    // The existing raid component owns the temporary challenge state; the existing
    // karma component owns the delayed resurrection of this same pawn.
    public partial class HediffComp_ZhaoliRaidState
    {
        private bool playerChallenge;
        private bool challengeReturnPending;
        private int challengeShieldLayers;
        private float challengeKarma;
        private float challengeFood = -1f;
        private Pawn_WorkSettings challengeWorkSettings;
        private MedicalCareCategory challengeMedicalCare;

        internal bool IsPlayerChallenge => playerChallenge;

        internal void PreparePlayerChallenge()
        {
            playerChallenge = true;
            challengeShieldLayers = ZhaoliShieldLayerUtility.GetShieldComp(Pawn).StoredLayers;
            challengeKarma = ZhaoliKarmaUtility.GetKarmaComp(Pawn).CurrentValue;
            challengeFood = Pawn.needs?.food?.CurLevelPercentage ?? -1f;
            challengeWorkSettings = Pawn.workSettings;
            // Keep the player's original tracker here while native deaths replace
            // the raid pawn's temporary work tracker.
            Pawn.workSettings = new Pawn_WorkSettings(Pawn);
            challengeMedicalCare = Pawn.playerSettings?.medCare ?? MedicalCareCategory.Best;
            ZhaoliKarmaUtility.GetLinkComp(Pawn).SuspendPlayerLinksForChallenge();
        }

        internal bool TrySchedulePlayerChallengeReturn()
        {
            if (!playerChallenge || !Pawn.Dead) return false;
            if (challengeReturnPending) return true;
            var component = Current.Game?.GetComponent<GameComponent_ZhaoliKarma>();
            if (component == null) return false;
            challengeReturnPending = true;
            ZhaoliRebirthUtility.PreparePawnForPendingRebirth(Pawn);
            component.RegisterPendingRebirth(Pawn, Find.TickManager.TicksGame + ZhaoliScenarioUtility.NextDayDelayTicks);
            Messages.Message("昭离挑战结束。原来的昭离将在一天后返回，其他伤亡与损毁不会恢复。", MessageTypeDefOf.NeutralEvent, true);
            return true;
        }

        internal void FinishPlayerChallengeReturn()
        {
            if (!playerChallenge || !challengeReturnPending || Pawn.Dead) return;
            PawnKindDef kind = Pawn.kindDef;
            if (Pawn.Faction != Faction.OfPlayer) Pawn.SetFaction(Faction.OfPlayer);
            if (Pawn.kindDef != kind) Pawn.ChangeKind(kind);
            ZhaoliShieldLayerUtility.EnsureShieldComp(Pawn)?.SetLayers(challengeShieldLayers);
            ZhaoliKarmaUtility.EnsureKarmaComp(Pawn)?.SetValue(challengeKarma);
            ZhaoliKarmaUtility.GetLinkComp(Pawn)?.RestorePlayerLinksAfterChallenge();
            if (challengeWorkSettings != null) Pawn.workSettings = challengeWorkSettings;
            challengeWorkSettings = null;
            if (Pawn.playerSettings != null) Pawn.playerSettings.medCare = challengeMedicalCare;
            if (challengeFood >= 0f && Pawn.needs?.food != null) Pawn.needs.food.CurLevelPercentage = challengeFood;
            Hediff field = Pawn.health.hediffSet.GetFirstHediffOfDef(MXZL_ZhaoliDefOf.MXZL_ZhaoliDeathFieldActive);
            if (field != null) Pawn.health.RemoveHediff(field);
            Pawn.health.RemoveHediff(parent);
        }

        private void ExposePlayerChallengeData()
        {
            Scribe_Values.Look(ref playerChallenge, "playerChallenge", false);
            Scribe_Values.Look(ref challengeReturnPending, "challengeReturnPending", false);
            Scribe_Values.Look(ref challengeShieldLayers, "challengeShieldLayers", 0);
            Scribe_Values.Look(ref challengeKarma, "challengeKarma", 0f);
            Scribe_Values.Look(ref challengeFood, "challengeFood", -1f);
            Scribe_Values.Look(ref challengeMedicalCare, "challengeMedicalCare", MedicalCareCategory.Best);
            Scribe_Deep.Look(ref challengeWorkSettings, "challengeWorkSettings", Pawn);
        }
    }
}

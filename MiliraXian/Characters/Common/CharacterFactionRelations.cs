using System.Collections.Generic;
using HarmonyLib;
using Milira;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Common
{
    public class CharacterFactionRelationExtension : DefModExtension
    {
        public List<FactionDef> playerFactions;
        public FactionDef targetFaction;
    }

    public class GoodwillSituationWorker_CharacterFactionRelation : GoodwillSituationWorker
    {
        public override int GetNaturalGoodwillOffset(Faction other)
        {
            Faction player = Faction.OfPlayerSilentFail;
            if (player == null)
            {
                return 0;
            }

            CharacterFactionRelationExtension ext = def.GetModExtension<CharacterFactionRelationExtension>();
            if (other.def == ext.targetFaction && ext.playerFactions.Contains(player.def))
            {
                return def.naturalGoodwillOffset;
            }

            return 0;
        }
    }

    public static class CharacterFactionRelations
    {
        public static IEnumerable<GoodwillSituationDef> RelationDefsFor(FactionDef playerFactionDef)
        {
            List<GoodwillSituationDef> defs = DefDatabase<GoodwillSituationDef>.AllDefsListForReading;
            for (int i = 0; i < defs.Count; i++)
            {
                CharacterFactionRelationExtension ext = defs[i].GetModExtension<CharacterFactionRelationExtension>();
                if (ext != null && ext.playerFactions.Contains(playerFactionDef))
                {
                    yield return defs[i];
                }
            }
        }
    }

    // 米莉拉的 OverallControl.CheckMiliraPermanentEnemyStatus 会把非自家玩家派系从
    // Milira_Faction.permanentEnemyToEveryoneExcept 运行时名单移除，触发永久敌对好感上限，
    // 下一次情境重算（每 1000 tick）时关系被强制翻成敌对。直接 postfix 删除方法本身，
    // 覆盖 StartedNewGame/LoadedGame/移交坠落天使 gizmo 全部调用点（米莉拉帝国同款做法）。
    // 不强置 turnToFriend，保留坠落天使和解剧情线。
    [HarmonyPatch(typeof(MiliraGameComponent_OverallControl), nameof(MiliraGameComponent_OverallControl.CheckMiliraPermanentEnemyStatus))]
    public static class Patch_MiliraOverallControl_RestoreExemptions
    {
        public static void Postfix()
        {
            Faction playerFaction = Find.FactionManager?.OfPlayer;
            if (playerFaction == null)
            {
                return;
            }

            bool restored = false;
            foreach (GoodwillSituationDef def in CharacterFactionRelations.RelationDefsFor(playerFaction.def))
            {
                List<FactionDef> exemptions = def.GetModExtension<CharacterFactionRelationExtension>().targetFaction.permanentEnemyToEveryoneExcept;
                if (exemptions != null && !exemptions.Contains(playerFaction.def))
                {
                    exemptions.Add(playerFaction.def);
                    restored = true;
                }
            }

            if (restored)
            {
                Find.GoodwillSituationManager.RecalculateAll(false);
            }
        }
    }
}

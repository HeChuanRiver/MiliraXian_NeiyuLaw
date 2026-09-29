using RimWorld;
using Verse;

namespace MiliraXian.Characters.Common
{
    public static class CharacterFactionUtility
    {
        private const string CharacterFactionDefName = "MiliraXian_Faction";

        private static Faction cachedFaction;
        private static FactionManager cachedManager;

        // 派系实例属于某一局游戏的世界，跨档后旧引用即失效，缓存以 FactionManager 实例为键。
        public static Faction MiliraXianFaction
        {
            get
            {
                FactionManager factionManager = Find.FactionManager;
                if (factionManager == null)
                {
                    return null;
                }

                if (factionManager != cachedManager)
                {
                    cachedManager = factionManager;
                    FactionDef factionDef = DefDatabase<FactionDef>.GetNamedSilentFail(CharacterFactionDefName);
                    cachedFaction = null;
                    if (factionDef != null)
                    {
                        cachedFaction = factionManager.FirstFactionOfDef(factionDef);
                        if (cachedFaction == null)
                        {
                            // 旧存档的世界生成时本派系尚不存在，首次需要时补建。
                            cachedFaction = FactionGenerator.NewGeneratedFaction(new FactionGeneratorParms(factionDef));
                            factionManager.Add(cachedFaction);
                        }
                    }
                }

                return cachedFaction;
            }
        }
    }

    public class GameComponent_CharacterFaction : GameComponent
    {
        public GameComponent_CharacterFaction(Game game)
        {
        }

        public override void FinalizeInit()
        {
            _ = CharacterFactionUtility.MiliraXianFaction;
        }

        public override void StartedNewGame()
        {
            Faction playerFaction = Find.FactionManager?.OfPlayer;
            if (playerFaction == null)
            {
                return;
            }

            foreach (GoodwillSituationDef def in CharacterFactionRelations.RelationDefsFor(playerFaction.def))
            {
                Faction target = Find.FactionManager.FirstFactionOfDef(def.GetModExtension<CharacterFactionRelationExtension>().targetFaction);
                if (target == null)
                {
                    continue;
                }

                int goodwill = def.naturalGoodwillOffset;
                FactionRelationKind kind = goodwill <= -75 ? FactionRelationKind.Hostile
                    : goodwill >= 75 ? FactionRelationKind.Ally
                    : FactionRelationKind.Neutral;
                // SetRelation 的镜像侧不复制 baseGoodwill（取字段默认值 100），两侧都直接写。
                FactionRelation playerSide = playerFaction.RelationWith(target);
                playerSide.baseGoodwill = goodwill;
                playerSide.kind = kind;
                FactionRelation targetSide = target.RelationWith(playerFaction);
                targetSide.baseGoodwill = goodwill;
                targetSide.kind = kind;
            }
        }
    }
}

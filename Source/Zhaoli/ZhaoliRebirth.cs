using System.Collections.Generic;
using RimWorld;
using RimWorld.Planet;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Zhaoli
{
    public static class ZhaoliRebirthUtility
    {
        public const string RebirthHediffDefName = "MXZL_ZhaoliRebirth";
        public const int RebirthDelayTicks = 600000;

        public static HediffComp_ZhaoliRebirth GetRebirthComp(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return null;
            }

            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(RebirthHediffDefName);
            if (hediffDef == null)
            {
                return null;
            }

            HediffWithComps hediff = pawn.health.hediffSet.GetFirstHediffOfDef(hediffDef) as HediffWithComps;
            return hediff?.GetComp<HediffComp_ZhaoliRebirth>();
        }

        public static HediffComp_ZhaoliRebirth EnsureRebirthComp(Pawn pawn)
        {
            HediffComp_ZhaoliRebirth comp = GetRebirthComp(pawn);
            if (comp != null || pawn?.health == null)
            {
                return comp;
            }

            HediffDef hediffDef = DefDatabase<HediffDef>.GetNamedSilentFail(RebirthHediffDefName);
            if (hediffDef == null)
            {
                return null;
            }

            Hediff hediff = pawn.health.GetOrAddHediff(hediffDef);
            pawn.health.Notify_HediffChanged(hediff);
            return (hediff as HediffWithComps)?.GetComp<HediffComp_ZhaoliRebirth>();
        }

        public static bool ShouldUseRecruitGrowth(Pawn pawn)
        {
            return pawn != null
                   && pawn.Faction == Faction.OfPlayer
                   && !ZhaoliScenarioUtility.IsRaidState(pawn)
                   && !ZhaoliScenarioUtility.IsHideoutState(pawn);
        }

        public static void RegisterRecruitGrowthDeath(Pawn pawn)
        {
            if (!ShouldUseRecruitGrowth(pawn))
            {
                return;
            }

            EnsureRebirthComp(pawn)?.RegisterRecruitGrowthDeath();
        }

        public static bool TryScheduleRebirth(Pawn pawn)
        {
            if (pawn == null || pawn.Discarded || !pawn.Dead || !ZhaoliKarmaUtility.IsZhaoli(pawn))
            {
                return false;
            }

            if (ZhaoliScenarioUtility.IsRaidState(pawn))
            {
                return false;
            }

            GameComponent_ZhaoliKarma rebirthComponent = Current.Game?.GetComponent<GameComponent_ZhaoliKarma>();
            if (rebirthComponent == null || rebirthComponent.IsPending(pawn))
            {
                return false;
            }

            RegisterRecruitGrowthDeath(pawn);
            PreparePawnForPendingRebirth(pawn);
            rebirthComponent.RegisterPendingRebirth(pawn, Find.TickManager.TicksGame + RebirthDelayTicks);
            Messages.Message("昭离被死亡接纳，十日后将于玩家基地归来。", pawn, MessageTypeDefOf.PawnDeath);
            return true;
        }

        public static bool TryFindRebirthLocation(out Map map, out IntVec3 cell)
        {
            map = null;
            cell = IntVec3.Invalid;

            IReadOnlyList<Map> playerHomeMaps = Current.Game?.PlayerHomeMaps;
            if (playerHomeMaps == null || playerHomeMaps.Count == 0)
            {
                return false;
            }

            int startIndex = Rand.Range(0, playerHomeMaps.Count);
            for (int i = 0; i < playerHomeMaps.Count; i++)
            {
                Map candidateMap = playerHomeMaps[(startIndex + i) % playerHomeMaps.Count];
                if (candidateMap == null || !candidateMap.IsPlayerHome)
                {
                    continue;
                }

                if (TryFindCellOnMap(candidateMap, out cell))
                {
                    map = candidateMap;
                    return true;
                }
            }

            return false;
        }

        private static void DetachAndDestroyCorpse(Pawn pawn)
        {
            Corpse corpse = pawn.Corpse;
            if (corpse == null)
            {
                return;
            }

            corpse.InnerPawn = null;
            corpse.Destroy();
        }

        public static void PreparePawnForPendingRebirth(Pawn pawn)
        {
            if (pawn == null || pawn.Discarded)
            {
                return;
            }

            DetachAndDestroyCorpse(pawn);
            if (!pawn.Spawned && !pawn.IsWorldPawn())
            {
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            }
        }

        public static void NotifyApparelResurrected(Pawn pawn)
        {
            if (pawn?.apparel == null)
            {
                return;
            }

            List<Apparel> wornApparel = pawn.apparel.WornApparel;
            for (int i = 0; i < wornApparel.Count; i++)
            {
                wornApparel[i]?.Notify_PawnResurrected(pawn);
            }
        }

        private static bool TryFindCellOnMap(Map map, out IntVec3 cell)
        {
            List<IntVec3> homeCells = new List<IntVec3>();
            Area homeArea = map.areaManager?.Home;
            if (homeArea != null)
            {
                foreach (IntVec3 candidateCell in homeArea.ActiveCells)
                {
                    if (candidateCell.Standable(map) && !candidateCell.Fogged(map))
                    {
                        homeCells.Add(candidateCell);
                    }
                }
            }

            if (homeCells.Count > 0)
            {
                cell = homeCells[Rand.Range(0, homeCells.Count)];
                return true;
            }

            return CellFinderLoose.TryGetRandomCellWith(
                candidateCell => candidateCell.Standable(map) && !candidateCell.Fogged(map),
                map,
                1000,
                out cell);
        }
    }

    public class HediffCompProperties_ZhaoliRebirth : HediffCompProperties
    {
        public HediffCompProperties_ZhaoliRebirth()
        {
            compClass = typeof(HediffComp_ZhaoliRebirth);
        }
    }

    public class HediffComp_ZhaoliRebirth : HediffComp
    {
        private int recruitGrowthDeaths;

        public int RecruitGrowthDeaths => recruitGrowthDeaths;

        public override bool CompDisallowVisible()
        {
            return true;
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Values.Look(ref recruitGrowthDeaths, "recruitGrowthDeaths", 0);
        }

        public void RegisterRecruitGrowthDeath()
        {
            recruitGrowthDeaths = Mathf.Max(0, recruitGrowthDeaths + 1);
        }

        public override void Notify_PawnDied(DamageInfo? dinfo, Hediff culprit = null)
        {
            base.Notify_PawnDied(dinfo, culprit);
            if (Patch_Pawn_Kill_ZhaoliSubstitute.HasPendingSubstitute(Pawn))
            {
                return;
            }
            ZhaoliRebirthUtility.TryScheduleRebirth(Pawn);
        }
    }

    public class ZhaoliPendingRebirth : IExposable
    {
        public Pawn pawn;
        public int rebirthTick;

        public ZhaoliPendingRebirth()
        {
        }

        public ZhaoliPendingRebirth(Pawn pawn, int rebirthTick)
        {
            this.pawn = pawn;
            this.rebirthTick = rebirthTick;
        }

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref rebirthTick, "rebirthTick", 0);
        }
    }

    public class GameComponent_ZhaoliRebirth : GameComponent
    {
        private List<ZhaoliPendingRebirth> pendingRebirths = new List<ZhaoliPendingRebirth>();

        public GameComponent_ZhaoliRebirth(Game game)
        {
        }

        public bool IsPending(Pawn pawn)
        {
            if (pawn == null)
            {
                return false;
            }

            for (int i = 0; i < pendingRebirths.Count; i++)
            {
                if (pendingRebirths[i]?.pawn == pawn)
                {
                    return true;
                }
            }

            return false;
        }

        public void RegisterPendingRebirth(Pawn pawn, int rebirthTick)
        {
            if (pawn == null)
            {
                return;
            }

            for (int i = 0; i < pendingRebirths.Count; i++)
            {
                if (pendingRebirths[i]?.pawn == pawn)
                {
                    pendingRebirths[i].rebirthTick = rebirthTick;
                    return;
                }
            }

            pendingRebirths.Add(new ZhaoliPendingRebirth(pawn, rebirthTick));
        }

        public override void GameComponentTick()
        {
            if (Current.ProgramState != ProgramState.Playing || pendingRebirths.Count == 0)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            for (int i = pendingRebirths.Count - 1; i >= 0; i--)
            {
                ZhaoliPendingRebirth pendingRebirth = pendingRebirths[i];
                if (pendingRebirth?.pawn == null || pendingRebirth.pawn.Destroyed)
                {
                    pendingRebirths.RemoveAt(i);
                    continue;
                }

                if (!pendingRebirth.pawn.Dead)
                {
                    pendingRebirths.RemoveAt(i);
                    continue;
                }

                if (currentTick < pendingRebirth.rebirthTick)
                {
                    continue;
                }

                ZhaoliRebirthUtility.PreparePawnForPendingRebirth(pendingRebirth.pawn);

                if (!ZhaoliRebirthUtility.TryFindRebirthLocation(out Map map, out IntVec3 cell))
                {
                    continue;
                }

                if (!ResurrectionUtility.TryResurrect(pendingRebirth.pawn, new ResurrectionParams
                {
                    gettingScarsChance = 0f,
                    canKidnap = false,
                    canTimeoutOrFlee = false,
                    sappers = false,
                    useAvoidGridSmart = true,
                    canSteal = false,
                    breachers = false,
                    canPickUpOpportunisticWeapons = false,
                    restoreMissingParts = true,
                    noLord = true,
                    dontSpawn = true,
                    invisibleStun = true,
                    removeDiedThoughts = false
                }))
                {
                    continue;
                }

                if (pendingRebirth.pawn.IsWorldPawn())
                {
                    Find.WorldPawns.RemovePawn(pendingRebirth.pawn);
                }

                GenSpawn.Spawn(pendingRebirth.pawn, cell, map);
                pendingRebirths.RemoveAt(i);
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Collections.Look(ref pendingRebirths, "pendingRebirths", LookMode.Deep);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                pendingRebirths.RemoveAll(entry => entry == null || entry.pawn == null || entry.pawn.Destroyed);
            }
        }
    }
}

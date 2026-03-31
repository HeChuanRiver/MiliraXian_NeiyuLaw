using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using MiliraXian.Characters;
using MiliraXian.Characters.Neiyu;
using MiliraXian.Characters.QingHe;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;
using Verse.Sound;

namespace MiliraXian.Characters.Zhaoli
{
    [DefOf]
    public static class MXZL_ZhaoliDefOf
    {
        public static IncidentDef MXZL_ZhaoliMurmur;
        public static IncidentDef MXZL_ZhaoliReturn;
        public static QuestScriptDef MXZL_ZhaoliMurmurQuest;
        public static QuestScriptDef MXZL_ZhaoliReturnQuest;
        public static SitePartDef MXZL_ZhaoliHideoutSitePart;
        public static WorldObjectDef MXZL_ZhaoliHideoutWorldObject;
        public static HediffDef MXZL_ZhaoliHideoutState;
        public static HediffDef MXZL_ZhaoliRaidState;
        public static ThingDef MX_Zhaoli_DuanzhanBlade;
        public static AbilityDef MX_Zhaoli_DeathField;
        public static AbilityDef MX_Zhaoli_Guiyi;
        public static AbilityDef MX_Zhaoli_Minghuo;
        public static AbilityDef MX_Zhaoli_Minshen;
        public static AbilityDef MX_Zhaoli_Duanzhan;

        static MXZL_ZhaoliDefOf()
        {
            DefOfHelper.EnsureInitializedInCtor(typeof(MXZL_ZhaoliDefOf));
        }
    }

    internal static class ZhaoliScenarioUtility
    {
        public const string ZhaoliPawnKindDefName = "MiliraXian_Zhaoli";
        public const string MiliraFactionDefName = "Milira_Faction";
        public const string MurmurQuestDefName = "MXZL_ZhaoliMurmurQuest";
        public const string ReturnQuestDefName = "MXZL_ZhaoliReturnQuest";
        public const int MurmurTriggerTicks = 15 * GenDate.TicksPerDay;
        public const int MurmurRequiredPawnDeaths = 16;
        public const int HideoutLifetimeTicks = 3 * GenDate.TicksPerDay;
        public const int NextDayDelayTicks = GenDate.TicksPerDay;
        public const int RaidStartingShieldLayers = 500;
        public const float RaidStartingKarma = 25f;
        public const float DeathFieldRaidBonusKarma = 20f;
        public const float HatredPerTenDamage = 1f;
        public const int HatredPerHit = 3;
        public const int TargetSwitchGraceTicks = 240;
        public const int AiTickInterval = 15;

        public static bool QuestExists(string questDefName)
        {
            if (Find.QuestManager == null)
            {
                return false;
            }

            List<Quest> quests = Find.QuestManager.QuestsListForReading;
            for (int index = 0; index < quests.Count; index++)
            {
                Quest quest = quests[index];
                if (quest?.root?.defName == questDefName)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool PlayerHasZhaoli()
        {
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
            {
                if (pawn == null || pawn.Dead || !ZhaoliKarmaUtility.IsZhaoli(pawn))
                {
                    continue;
                }

                if (pawn.Faction == Faction.OfPlayer && !IsHideoutState(pawn) && !IsRaidState(pawn))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool IsSpecialPawn(Pawn pawn)
        {
            return NeiyuEquipmentUtility.IsNeiyu(pawn) || ZhaoliKarmaUtility.IsZhaoli(pawn) || MX_QHUtility.IsQinghe(pawn);
        }

        public static bool ShouldDeathFieldAffectTarget(Pawn caster, Pawn target)
        {
            if (caster == null || target == null || target == caster || target.Dead || target.Destroyed)
            {
                return false;
            }

            if (IsRaidState(caster) && IsSpecialPawn(target))
            {
                return false;
            }

            return true;
        }

        public static HediffComp_ZhaoliHideoutState GetHideoutStateComp(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return null;
            }

            HediffWithComps hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MXZL_ZhaoliDefOf.MXZL_ZhaoliHideoutState) as HediffWithComps;
            return hediff?.GetComp<HediffComp_ZhaoliHideoutState>();
        }

        public static HediffComp_ZhaoliRaidState GetRaidStateComp(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return null;
            }

            HediffWithComps hediff = pawn.health.hediffSet.GetFirstHediffOfDef(MXZL_ZhaoliDefOf.MXZL_ZhaoliRaidState) as HediffWithComps;
            return hediff?.GetComp<HediffComp_ZhaoliRaidState>();
        }

        public static bool IsHideoutState(Pawn pawn)
        {
            return GetHideoutStateComp(pawn) != null;
        }

        public static bool IsRaidState(Pawn pawn)
        {
            return GetRaidStateComp(pawn) != null;
        }

        public static Faction ResolveFriendlyFaction()
        {
            FactionDef miliraFactionDef = DefDatabase<FactionDef>.GetNamedSilentFail(MiliraFactionDefName);
            Faction miliraFaction = miliraFactionDef != null ? Find.FactionManager.FirstFactionOfDef(miliraFactionDef) : null;
            if (miliraFaction != null && !miliraFaction.IsPlayer && !miliraFaction.HostileTo(Faction.OfPlayer))
            {
                return miliraFaction;
            }

            List<Faction> factions = Find.FactionManager.AllFactionsListForReading;
            for (int index = 0; index < factions.Count; index++)
            {
                Faction faction = factions[index];
                if (faction == null || faction.IsPlayer || faction.Hidden || faction.temporary || faction.HostileTo(Faction.OfPlayer))
                {
                    continue;
                }

                if (faction.def?.humanlikeFaction == true)
                {
                    return faction;
                }
            }

            for (int index = 0; index < factions.Count; index++)
            {
                Faction faction = factions[index];
                if (faction == null || faction.IsPlayer || faction.Hidden || faction.temporary || faction.HostileTo(Faction.OfPlayer))
                {
                    continue;
                }

                return faction;
            }

            if (Find.FactionManager.OfAncients != null && !Find.FactionManager.OfAncients.IsPlayer && !Find.FactionManager.OfAncients.HostileTo(Faction.OfPlayer))
            {
                return Find.FactionManager.OfAncients;
            }

            return null;
        }

        public static Faction ResolveHostileFaction()
        {
            FactionDef miliraFactionDef = DefDatabase<FactionDef>.GetNamedSilentFail(MiliraFactionDefName);
            Faction miliraFaction = miliraFactionDef != null ? Find.FactionManager.FirstFactionOfDef(miliraFactionDef) : null;
            if (miliraFaction != null && miliraFaction.HostileTo(Faction.OfPlayer))
            {
                return miliraFaction;
            }

            return Find.FactionManager.RandomEnemyFaction(allowHidden: false, allowDefeated: false, allowNonHumanlike: false)
                   ?? Find.FactionManager.OfPirates
                   ?? Find.FactionManager.OfAncientsHostile
                   ?? Find.FactionManager.OfMechanoids;
        }

        public static Pawn GenerateZhaoliPawn(Faction faction)
        {
            PawnKindDef zhaoliKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(ZhaoliPawnKindDefName);
            if (zhaoliKind == null)
            {
                Log.Error("[MiliraXian.Characters.Zhaoli] Missing PawnKindDef: " + ZhaoliPawnKindDefName);
                return null;
            }

            PawnGenerationRequest request = new PawnGenerationRequest(
                zhaoliKind,
                faction,
                PawnGenerationContext.NonPlayer,
                -1,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: true,
                mustBeCapableOfViolence: true,
                colonistRelationChanceFactor: 20f,
                forceAddFreeWarmLayerIfNeeded: false,
                allowGay: true,
                allowPregnant: true,
                allowFood: true,
                allowAddictions: true,
                inhabitant: false,
                certainlyBeenInCryptosleep: false,
                forceRedressWorldPawnIfFormerColonist: false,
                worldPawnFactionDoesntMatter: false);

            Pawn pawn = PawnGenerator.GeneratePawn(request);
            EnsurePrimaryWeapon(pawn);
            return pawn;
        }

        public static void EnsurePrimaryWeapon(Pawn pawn)
        {
            if (pawn?.equipment == null)
            {
                return;
            }

            ThingDef weaponDef = MXZL_ZhaoliDefOf.MX_Zhaoli_DuanzhanBlade;
            if (weaponDef == null)
            {
                Log.Error("[MiliraXian.Characters.Zhaoli] Missing ThingDef: MX_Zhaoli_DuanzhanBlade");
                return;
            }

            if (pawn.equipment.Primary != null && pawn.equipment.Primary.def == weaponDef)
            {
                return;
            }

            if (pawn.equipment.Primary != null)
            {
                pawn.equipment.DestroyEquipment(pawn.equipment.Primary);
            }

            ThingWithComps weapon = ThingMaker.MakeThing(weaponDef) as ThingWithComps;
            if (weapon == null)
            {
                Log.Error("[MiliraXian.Characters.Zhaoli] Weapon is not ThingWithComps: " + weaponDef.defName);
                return;
            }

            PawnGenerator.PostProcessGeneratedGear(weapon, pawn);
            CompEquippable compEquippable = weapon.TryGetComp<CompEquippable>();
            if (compEquippable != null)
            {
                if (pawn.kindDef.weaponStyleDef != null)
                {
                    compEquippable.parent.StyleDef = pawn.kindDef.weaponStyleDef;
                }
                else if (pawn.Ideo != null)
                {
                    compEquippable.parent.StyleDef = pawn.Ideo.GetStyleFor(weapon.def);
                }
            }

            pawn.equipment.AddEquipment(weapon);
        }

        public static bool TryConsumeHerbalMedicine(Map map, int requiredCount)
        {
            if (map == null || requiredCount <= 0)
            {
                return false;
            }

            List<Thing> sources = new List<Thing>();
            List<Thing> spawnedThings = map.listerThings.ThingsOfDef(ThingDefOf.MedicineHerbal);
            for (int index = 0; index < spawnedThings.Count; index++)
            {
                if (spawnedThings[index] != null && !spawnedThings[index].Destroyed)
                {
                    sources.Add(spawnedThings[index]);
                }
            }

            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int pawnIndex = 0; pawnIndex < pawns.Count; pawnIndex++)
            {
                Pawn pawn = pawns[pawnIndex];
                if (pawn == null || pawn.Destroyed)
                {
                    continue;
                }

                if (pawn.carryTracker?.CarriedThing?.def == ThingDefOf.MedicineHerbal)
                {
                    sources.Add(pawn.carryTracker.CarriedThing);
                }

                if (pawn.inventory?.innerContainer == null)
                {
                    continue;
                }

                for (int thingIndex = 0; thingIndex < pawn.inventory.innerContainer.Count; thingIndex++)
                {
                    Thing thing = pawn.inventory.innerContainer[thingIndex];
                    if (thing?.def == ThingDefOf.MedicineHerbal)
                    {
                        sources.Add(thing);
                    }
                }
            }

            int total = 0;
            for (int index = 0; index < sources.Count; index++)
            {
                total += sources[index].stackCount;
                if (total >= requiredCount)
                {
                    break;
                }
            }

            if (total < requiredCount)
            {
                return false;
            }

            int remaining = requiredCount;
            for (int index = 0; index < sources.Count && remaining > 0; index++)
            {
                Thing source = sources[index];
                if (source == null || source.Destroyed)
                {
                    continue;
                }

                int take = Mathf.Min(remaining, source.stackCount);
                Thing removed = source.SplitOff(take);
                removed.Destroy(DestroyMode.Vanish);
                remaining -= take;
            }

            return remaining <= 0;
        }

        public static void SpawnHumanMeatReward(Pawn receiver, int count)
        {
            if (receiver?.MapHeld == null || count <= 0)
            {
                return;
            }

            int remaining = count;
            while (remaining > 0)
            {
                Thing meat = ThingMaker.MakeThing(ThingDefOf.Meat_Human);
                int stackCount = Mathf.Min(remaining, meat.def.stackLimit);
                meat.stackCount = stackCount;
                GenPlace.TryPlaceThing(meat, receiver.PositionHeld, receiver.MapHeld, ThingPlaceMode.Near);
                remaining -= stackCount;
            }
        }

        public static void PlayHideoutDeparture(Pawn pawn)
        {
            if (pawn?.MapHeld == null || !pawn.PositionHeld.IsValid)
            {
                return;
            }

            FleckMaker.Static(pawn.PositionHeld, pawn.MapHeld, FleckDefOf.PsycastSkipFlashEntry, 1.5f);
            FleckMaker.Static(pawn.PositionHeld, pawn.MapHeld, FleckDefOf.ExplosionFlash, 1.6f);
            SoundDefOf.PsychicPulseGlobal.PlayOneShot(new TargetInfo(pawn.PositionHeld, pawn.MapHeld));
        }

        public static void PlayHideoutRefusal(Pawn pawn)
        {
            if (pawn?.MapHeld == null || !pawn.PositionHeld.IsValid)
            {
                return;
            }

            FleckMaker.Static(pawn.PositionHeld, pawn.MapHeld, FleckDefOf.PsycastAreaEffect, 1.6f);
            FleckMaker.Static(pawn.PositionHeld, pawn.MapHeld, FleckDefOf.ExplosionFlash, 1.8f);
            SoundDefOf.PsychicPulseGlobal.PlayOneShot(new TargetInfo(pawn.PositionHeld, pawn.MapHeld));
        }

        public static void DestroyHideoutPawn(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            if (pawn.IsWorldPawn())
            {
                Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
                return;
            }

            if (pawn.Spawned)
            {
                pawn.Destroy(DestroyMode.Vanish);
            }
        }

        public static void CleanupRaidRemains(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            Map map = pawn.MapHeld;
            IntVec3 position = pawn.PositionHeld;

            if (pawn.equipment?.Primary != null && !pawn.equipment.Primary.Destroyed)
            {
                pawn.equipment.Primary.Destroy(DestroyMode.Vanish);
            }

            if (map != null && position.IsValid && position.InBounds(map))
            {
                List<Thing> things = position.GetThingList(map).ToList();
                for (int index = things.Count - 1; index >= 0; index--)
                {
                    if (things[index]?.def == MXZL_ZhaoliDefOf.MX_Zhaoli_DuanzhanBlade)
                    {
                        things[index].Destroy(DestroyMode.Vanish);
                    }
                }
            }

            Corpse corpse = pawn.Corpse;
            if (corpse != null)
            {
                corpse.InnerPawn = null;
                corpse.Destroy(DestroyMode.Vanish);
            }
        }

        public static Map ResolveBestHomeMap(Map preferred)
        {
            if (preferred != null && preferred.IsPlayerHome)
            {
                return preferred;
            }

            return Find.AnyPlayerHomeMap;
        }
    }

    public class QuestNode_Root_ZhaoliMurmur_AvailableQuest : QuestNode
    {
        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            Slate slate = QuestGen.slate;
            if (!slate.TryGet("map", out Map map))
            {
                map = QuestGen_Get.GetMap(mustBeInfestable: false, null, canBeSpace: false);
            }

            if (map == null)
            {
                return;
            }

            quest.AcceptanceRequirementNotSpace(map.Parent);
            quest.Signal(quest.InitiateSignal, delegate
            {
                GameComponent_ZhaoliScenario component = Current.Game?.GetComponent<GameComponent_ZhaoliScenario>();
                if (component == null || !component.StartHideout(map))
                {
                    QuestGen_End.End(quest, QuestEndOutcome.Fail);
                    return;
                }

                QuestGen_End.End(quest, QuestEndOutcome.Success);
            });
        }

        protected override bool TestRunInt(Slate slate)
        {
            if (!slate.TryGet("map", out Map _))
            {
                return QuestGen_Get.GetMap(mustBeInfestable: false, null, canBeSpace: false) != null;
            }

            return Find.AnyPlayerHomeMap != null;
        }
    }

    public class QuestNode_Root_ZhaoliReturn_AvailableQuest : QuestNode
    {
        protected override void RunInt()
        {
            Quest quest = QuestGen.quest;
            Slate slate = QuestGen.slate;
            if (!slate.TryGet("map", out Map map))
            {
                map = QuestGen_Get.GetMap(mustBeInfestable: false, null, canBeSpace: false);
            }

            if (map == null)
            {
                return;
            }

            quest.AcceptanceRequirementNotSpace(map.Parent);
            quest.Signal(quest.InitiateSignal, delegate
            {
                Pawn pawn = ZhaoliScenarioUtility.GenerateZhaoliPawn(ZhaoliScenarioUtility.ResolveFriendlyFaction());
                if (pawn == null)
                {
                    QuestGen_End.End(quest, QuestEndOutcome.Fail);
                    return;
                }

                quest.SetFaction(Gen.YieldSingle(pawn), Faction.OfPlayer);
                quest.PawnsArrive(
                    Gen.YieldSingle(pawn),
                    null,
                    map.Parent,
                    PawnsArrivalModeDefOf.EdgeWalkIn,
                    joinPlayer: false,
                    walkInSpot: null,
                    null,
                    null,
                    null,
                    null,
                    isSingleReward: false,
                    rewardDetailsHidden: false,
                    sendStandardLetter: false);

                Current.Game?.GetComponent<GameComponent_ZhaoliScenario>()?.MarkScenarioCompleted();
                QuestGen_End.End(quest, QuestEndOutcome.Success);
            });
        }

        protected override bool TestRunInt(Slate slate)
        {
            if (!slate.TryGet("map", out Map _))
            {
                return QuestGen_Get.GetMap(mustBeInfestable: false, null, canBeSpace: false) != null;
            }

            return Find.AnyPlayerHomeMap != null;
        }
    }

    public class IncidentWorker_ZhaoliMurmur : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms) || !(parms.target is Map map) || !map.IsPlayerHome)
            {
                return false;
            }

            GameComponent_ZhaoliScenario component = Current.Game?.GetComponent<GameComponent_ZhaoliScenario>();
            if (component == null || !component.CanOfferMurmur(map))
            {
                return false;
            }

            QuestScriptDef questDef = def.questScriptDef ?? parms.questScriptDef;
            return questDef == null || questDef.CanRun(parms.points, parms.target);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!(parms.target is Map map))
            {
                return false;
            }

            QuestScriptDef questDef = def.questScriptDef ?? parms.questScriptDef;
            if (questDef == null)
            {
                return false;
            }

            Slate slate = new Slate();
            slate.Set("points", parms.points);
            slate.Set("map", map);

            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, slate);
            if (quest == null)
            {
                return false;
            }

            if (!quest.hidden && questDef.sendAvailableLetter)
            {
                QuestUtility.SendLetterQuestAvailable(quest);
            }

            Current.Game?.GetComponent<GameComponent_ZhaoliScenario>()?.MarkMurmurOffered(map);
            return true;
        }
    }

    public class IncidentWorker_ZhaoliReturn : IncidentWorker
    {
        protected override bool CanFireNowSub(IncidentParms parms)
        {
            if (!base.CanFireNowSub(parms) || !(parms.target is Map map) || !map.IsPlayerHome)
            {
                return false;
            }

            GameComponent_ZhaoliScenario component = Current.Game?.GetComponent<GameComponent_ZhaoliScenario>();
            if (component == null || !component.CanOfferReturn(map))
            {
                return false;
            }

            QuestScriptDef questDef = def.questScriptDef ?? parms.questScriptDef;
            return questDef == null || questDef.CanRun(parms.points, parms.target);
        }

        protected override bool TryExecuteWorker(IncidentParms parms)
        {
            if (!(parms.target is Map map))
            {
                return false;
            }

            QuestScriptDef questDef = def.questScriptDef ?? parms.questScriptDef;
            if (questDef == null)
            {
                return false;
            }

            Slate slate = new Slate();
            slate.Set("points", parms.points);
            slate.Set("map", map);

            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, slate);
            if (quest == null)
            {
                return false;
            }

            if (!quest.hidden && questDef.sendAvailableLetter)
            {
                QuestUtility.SendLetterQuestAvailable(quest);
            }

            Current.Game?.GetComponent<GameComponent_ZhaoliScenario>()?.MarkReturnOffered(map);
            return true;
        }
    }

    public class GameComponent_ZhaoliScenario : GameComponent
    {
        private const int TickCheckInterval = 250;

        private bool scenarioCompleted;
        private bool murmurOffered;
        private bool returnOffered;
        private int qualifyingPawnDeathCount;
        private int scheduledRaidTick = -1;
        private int scheduledReturnTick = -1;
        private Map scenarioHomeMap;
        private Site activeHideoutSite;
        private Pawn currentRaidPawn;

        public GameComponent_ZhaoliScenario(Game game)
        {
        }

        public bool CanOfferMurmur(Map map)
        {
            if (scenarioCompleted || murmurOffered || returnOffered || activeHideoutSite != null || currentRaidPawn != null)
            {
                return false;
            }

            if (ZhaoliScenarioUtility.PlayerHasZhaoli())
            {
                return false;
            }

            if (Find.TickManager == null || Find.TickManager.TicksGame < ZhaoliScenarioUtility.MurmurTriggerTicks)
            {
                return false;
            }

            if (qualifyingPawnDeathCount < ZhaoliScenarioUtility.MurmurRequiredPawnDeaths)
            {
                return false;
            }

            if (map == null || !map.IsPlayerHome)
            {
                return false;
            }

            return !ZhaoliScenarioUtility.QuestExists(ZhaoliScenarioUtility.MurmurQuestDefName);
        }

        public bool CanOfferReturn(Map map)
        {
            if (scenarioCompleted || returnOffered || currentRaidPawn != null || scheduledReturnTick < 0)
            {
                return false;
            }

            if (Find.TickManager == null || Find.TickManager.TicksGame < scheduledReturnTick)
            {
                return false;
            }

            if (ZhaoliScenarioUtility.PlayerHasZhaoli())
            {
                return false;
            }

            return map != null && map.IsPlayerHome && !ZhaoliScenarioUtility.QuestExists(ZhaoliScenarioUtility.ReturnQuestDefName);
        }

        public override void GameComponentTick()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
            {
                return;
            }

            if (activeHideoutSite != null && activeHideoutSite.Destroyed)
            {
                activeHideoutSite = null;
            }

            if (currentRaidPawn != null && currentRaidPawn.Destroyed)
            {
                currentRaidPawn = null;
            }

            if (!scenarioCompleted && ZhaoliScenarioUtility.PlayerHasZhaoli())
            {
                scenarioCompleted = true;
            }
            else if (scenarioCompleted && !ZhaoliScenarioUtility.PlayerHasZhaoli() && (activeHideoutSite != null || currentRaidPawn != null || scheduledRaidTick >= 0 || scheduledReturnTick >= 0))
            {
                scenarioCompleted = false;
            }

            if (ZhaoliScenarioUtility.QuestExists(ZhaoliScenarioUtility.MurmurQuestDefName))
            {
                murmurOffered = true;
            }

            if (ZhaoliScenarioUtility.QuestExists(ZhaoliScenarioUtility.ReturnQuestDefName))
            {
                returnOffered = true;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (scheduledRaidTick >= 0 && currentTick >= scheduledRaidTick)
            {
                if (TryExecuteScheduledRaid())
                {
                    scheduledRaidTick = -1;
                }
            }

            if (scheduledReturnTick >= 0 && currentTick >= scheduledReturnTick)
            {
                if (TryExecuteScheduledReturn())
                {
                    scheduledReturnTick = -1;
                }
            }

            if (!scenarioCompleted && !murmurOffered && currentTick % TickCheckInterval == 0)
            {
                TryExecuteMurmur();
            }
        }

        public void NotifyQualifyingPawnDeath(Pawn pawn)
        {
            if (pawn == null || scenarioCompleted || murmurOffered || returnOffered || activeHideoutSite != null || currentRaidPawn != null)
            {
                return;
            }

            if (Find.TickManager == null || Find.TickManager.TicksGame < ZhaoliScenarioUtility.MurmurTriggerTicks)
            {
                return;
            }

            qualifyingPawnDeathCount++;
            Map homeMap = ZhaoliScenarioUtility.ResolveBestHomeMap(pawn.MapHeld) ?? ZhaoliScenarioUtility.ResolveBestHomeMap(scenarioHomeMap);
            if (homeMap != null)
            {
                scenarioHomeMap = homeMap;
            }
        }

        public void MarkMurmurOffered(Map map)
        {
            murmurOffered = true;
            scenarioHomeMap = ZhaoliScenarioUtility.ResolveBestHomeMap(map);
        }

        public void MarkReturnOffered(Map map)
        {
            returnOffered = true;
            scenarioHomeMap = ZhaoliScenarioUtility.ResolveBestHomeMap(map);
        }

        public void MarkScenarioCompleted()
        {
            scenarioCompleted = true;
            scheduledRaidTick = -1;
            scheduledReturnTick = -1;
            activeHideoutSite = null;
            currentRaidPawn = null;
        }

        public bool StartHideout(Map homeMap)
        {
            homeMap = ZhaoliScenarioUtility.ResolveBestHomeMap(homeMap);
            if (homeMap == null || activeHideoutSite != null)
            {
                return false;
            }

            if (!TileFinder.TryFindPassableTileWithTraversalDistance(
                    homeMap.Tile,
                    1,
                    3,
                    out PlanetTile siteTile,
                    tile => !Find.WorldObjects.AnyWorldObjectAt(tile),
                    ignoreFirstTilePassability: false,
                    tileFinderMode: TileFinderMode.Random,
                    canTraverseImpassable: false,
                    exitOnFirstTileFound: false))
            {
                return false;
            }

            Site site = SiteMaker.MakeSite(
                MXZL_ZhaoliDefOf.MXZL_ZhaoliHideoutSitePart,
                siteTile,
                ZhaoliScenarioUtility.ResolveFriendlyFaction(),
                ifHostileThenMustRemainHostile: false,
                threatPoints: 0f,
                worldObjectDef: MXZL_ZhaoliDefOf.MXZL_ZhaoliHideoutWorldObject);
            if (site == null)
            {
                return false;
            }

            site.customLabel = "不存在者藏身处";
            Find.WorldObjects.Add(site);
            site.GetComponent<TimeoutComp>()?.StartTimeout(ZhaoliScenarioUtility.HideoutLifetimeTicks);
            site.GetComponent<WorldObjectComp_ZhaoliHideout>()?.InitializeHomeMap(homeMap);
            activeHideoutSite = site;
            scenarioHomeMap = homeMap;
            return true;
        }

        public void NotifyHideoutMedicineAccepted(Map homeMap)
        {
            scenarioCompleted = false;
            scenarioHomeMap = ZhaoliScenarioUtility.ResolveBestHomeMap(homeMap);
            activeHideoutSite = null;
            ScheduleReturn(scenarioHomeMap, ZhaoliScenarioUtility.NextDayDelayTicks);
        }

        public void NotifyHideoutRejected(Map homeMap)
        {
            scenarioCompleted = false;
            scenarioHomeMap = ZhaoliScenarioUtility.ResolveBestHomeMap(homeMap);
            activeHideoutSite = null;
            ScheduleRaid(scenarioHomeMap, ZhaoliScenarioUtility.NextDayDelayTicks);
        }

        public void NotifyHideoutExpired(Map homeMap)
        {
            scenarioCompleted = false;
            scenarioHomeMap = ZhaoliScenarioUtility.ResolveBestHomeMap(homeMap);
            activeHideoutSite = null;
            ScheduleRaid(scenarioHomeMap, 0);
        }

        public void NotifyRaidZhaoliDefeated(Pawn pawn)
        {
            if (pawn == null || currentRaidPawn != pawn)
            {
                return;
            }

            Map homeMap = ZhaoliScenarioUtility.ResolveBestHomeMap(scenarioHomeMap);
            scenarioCompleted = false;
            ZhaoliScenarioUtility.CleanupRaidRemains(pawn);
            currentRaidPawn = null;
            ScheduleReturn(homeMap, ZhaoliScenarioUtility.NextDayDelayTicks);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref scenarioCompleted, "scenarioCompleted", defaultValue: false);
            Scribe_Values.Look(ref murmurOffered, "murmurOffered", defaultValue: false);
            Scribe_Values.Look(ref returnOffered, "returnOffered", defaultValue: false);
            Scribe_Values.Look(ref qualifyingPawnDeathCount, "qualifyingPawnDeathCount", 0);
            Scribe_Values.Look(ref scheduledRaidTick, "scheduledRaidTick", -1);
            Scribe_Values.Look(ref scheduledReturnTick, "scheduledReturnTick", -1);
            Scribe_References.Look(ref scenarioHomeMap, "scenarioHomeMap");
            Scribe_References.Look(ref activeHideoutSite, "activeHideoutSite");
            Scribe_References.Look(ref currentRaidPawn, "currentRaidPawn");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (activeHideoutSite != null && activeHideoutSite.Destroyed)
                {
                    activeHideoutSite = null;
                }

                if (currentRaidPawn != null && currentRaidPawn.Destroyed)
                {
                    currentRaidPawn = null;
                }
            }
        }

        private void ScheduleRaid(Map homeMap, int delayTicks)
        {
            if (scenarioCompleted)
            {
                return;
            }

            scenarioHomeMap = ZhaoliScenarioUtility.ResolveBestHomeMap(homeMap);
            scheduledRaidTick = (Find.TickManager?.TicksGame ?? 0) + Mathf.Max(0, delayTicks);
        }

        private void ScheduleReturn(Map homeMap, int delayTicks)
        {
            if (scenarioCompleted)
            {
                return;
            }

            scenarioHomeMap = ZhaoliScenarioUtility.ResolveBestHomeMap(homeMap);
            scheduledReturnTick = (Find.TickManager?.TicksGame ?? 0) + Mathf.Max(0, delayTicks);
        }

        private void TryExecuteMurmur()
        {
            Map map = ZhaoliScenarioUtility.ResolveBestHomeMap(scenarioHomeMap);
            if (map == null || !CanOfferMurmur(map))
            {
                return;
            }

            IncidentDef incident = MXZL_ZhaoliDefOf.MXZL_ZhaoliMurmur;
            if (incident == null)
            {
                Log.Error("[MiliraXian.Characters.Zhaoli] Missing IncidentDef: MXZL_ZhaoliMurmur");
                murmurOffered = true;
                return;
            }

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, map);
            parms.target = map;
            parms.forced = true;
            parms.bypassStorytellerSettings = true;
            if (incident.Worker.TryExecute(parms))
            {
                murmurOffered = true;
            }
        }

        private bool TryExecuteScheduledRaid()
        {
            if (currentRaidPawn != null && !currentRaidPawn.Dead && !currentRaidPawn.Destroyed)
            {
                return true;
            }

            Map map = ZhaoliScenarioUtility.ResolveBestHomeMap(scenarioHomeMap);
            if (map == null)
            {
                return false;
            }

            Faction faction = ZhaoliScenarioUtility.ResolveHostileFaction();
            if (faction == null)
            {
                Log.Error("[MiliraXian.Characters.Zhaoli] Unable to resolve hostile faction for raid.");
                return false;
            }

            Pawn pawn = ZhaoliScenarioUtility.GenerateZhaoliPawn(faction);
            if (pawn == null)
            {
                return false;
            }

            Hediff raidState = pawn.health.GetOrAddHediff(MXZL_ZhaoliDefOf.MXZL_ZhaoliRaidState);
            HediffComp_ZhaoliRaidState raidComp = (raidState as HediffWithComps)?.GetComp<HediffComp_ZhaoliRaidState>();
            raidComp?.InitializeRaid(map);

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(IncidentCategoryDefOf.ThreatBig, map);
            parms.target = map;
            parms.faction = faction;
            parms.forced = true;
            parms.bypassStorytellerSettings = true;

            if (!PawnsArrivalModeDefOf.EdgeWalkIn.Worker.TryResolveRaidSpawnCenter(parms))
            {
                return false;
            }

            PawnsArrivalModeDefOf.EdgeWalkIn.Worker.Arrive(new List<Pawn> { pawn }, parms);
            pawn.SetFaction(faction);
            LordMaker.MakeNewLord(faction, new LordJob_AssaultColony(faction, canKidnap: false, canTimeoutOrFlee: false, sappers: false, useAvoidGridSmart: true, canSteal: false, breachers: false, canPickUpOpportunisticWeapons: false), map, new[] { pawn });
            currentRaidPawn = pawn;
            Find.LetterStack.ReceiveLetter(
                "昭离来袭",
                "昭离的身影自殖民地边缘现身。她没有停驻、没有谈判，也不会撤退。此战只会以所有生灵的死亡，或她自己的彻底终结告终。",
                LetterDefOf.ThreatBig,
                pawn);
            return true;
        }

        private bool TryExecuteScheduledReturn()
        {
            Map map = ZhaoliScenarioUtility.ResolveBestHomeMap(scenarioHomeMap);
            if (map == null || !CanOfferReturn(map))
            {
                return false;
            }

            IncidentDef incident = MXZL_ZhaoliDefOf.MXZL_ZhaoliReturn;
            if (incident == null)
            {
                Log.Error("[MiliraXian.Characters.Zhaoli] Missing IncidentDef: MXZL_ZhaoliReturn");
                returnOffered = true;
                return false;
            }

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, map);
            parms.target = map;
            parms.forced = true;
            parms.bypassStorytellerSettings = true;
            if (incident.Worker.TryExecute(parms))
            {
                returnOffered = true;
                return true;
            }

            return false;
        }
    }

    public class WorldObjectCompProperties_ZhaoliHideout : WorldObjectCompProperties
    {
        public WorldObjectCompProperties_ZhaoliHideout()
        {
            compClass = typeof(WorldObjectComp_ZhaoliHideout);
        }
    }

    public class WorldObjectComp_ZhaoliHideout : WorldObjectComp
    {
        private Map homeMap;
        private Pawn hideoutPawn;
        private bool resolved;
        private bool consequenceQueued;

        public Map HomeMap => homeMap;

        public Pawn HideoutPawn => hideoutPawn;

        public bool Resolved => resolved;

        public void InitializeHomeMap(Map map)
        {
            homeMap = ZhaoliScenarioUtility.ResolveBestHomeMap(map);
        }

        public override void PostMapGenerate()
        {
            base.PostMapGenerate();
            if (parent is MapParent mapParent)
            {
                EnsurePawnSpawned(mapParent.Map);
            }
        }

        public override void CompTick()
        {
            base.CompTick();
            if (resolved || consequenceQueued || Find.TickManager == null || Find.TickManager.TicksGame % 250 != 0)
            {
                return;
            }

            TimeoutComp timeoutComp = parent.GetComponent<TimeoutComp>();
            if (timeoutComp != null && timeoutComp.Passed)
            {
                consequenceQueued = true;
                Current.Game?.GetComponent<GameComponent_ZhaoliScenario>()?.NotifyHideoutExpired(homeMap);
            }
        }

        public override void PostDestroy()
        {
            base.PostDestroy();
            if (!resolved && !consequenceQueued)
            {
                consequenceQueued = true;
                Current.Game?.GetComponent<GameComponent_ZhaoliScenario>()?.NotifyHideoutExpired(homeMap);
            }
        }

        public override string CompInspectStringExtra()
        {
            if (resolved)
            {
                return "昭离的痕迹已经散去。";
            }

            return "昭离在此等待一场以生死为价的交接。";
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref homeMap, "homeMap");
            Scribe_References.Look(ref hideoutPawn, "hideoutPawn");
            Scribe_Values.Look(ref resolved, "resolved", defaultValue: false);
            Scribe_Values.Look(ref consequenceQueued, "consequenceQueued", defaultValue: false);
        }

        public void EnsurePawnSpawned(Map map)
        {
            if (resolved || map == null)
            {
                return;
            }

            if (hideoutPawn != null && !hideoutPawn.Destroyed && !hideoutPawn.Dead && hideoutPawn.Spawned)
            {
                return;
            }

            hideoutPawn = ZhaoliScenarioUtility.GenerateZhaoliPawn(ZhaoliScenarioUtility.ResolveFriendlyFaction());
            if (hideoutPawn == null)
            {
                return;
            }

            Hediff hediff = hideoutPawn.health.GetOrAddHediff(MXZL_ZhaoliDefOf.MXZL_ZhaoliHideoutState);
            hideoutPawn.health.Notify_HediffChanged(hediff);
            IntVec3 spawnCell = CellFinder.RandomClosewalkCellNear(map.Center, map, 8);
            GenSpawn.Spawn(hideoutPawn, spawnCell, map);
        }

        public bool TryDeliverHerbs(Pawn interactor)
        {
            if (resolved || interactor?.MapHeld == null)
            {
                return false;
            }

            if (!ZhaoliScenarioUtility.TryConsumeHerbalMedicine(interactor.MapHeld, 5))
            {
                return false;
            }

            resolved = true;
            TimeoutComp timeoutComp = parent.GetComponent<TimeoutComp>();
            timeoutComp?.StopTimeout();
            if (hideoutPawn != null)
            {
                ZhaoliScenarioUtility.PlayHideoutDeparture(hideoutPawn);
                ZhaoliScenarioUtility.DestroyHideoutPawn(hideoutPawn);
            }

            Current.Game?.GetComponent<GameComponent_ZhaoliScenario>()?.NotifyHideoutMedicineAccepted(homeMap ?? interactor.MapHeld);
            if (!ParentHasMap)
            {
                parent.Destroy();
            }

            return true;
        }

        public void Reject(Pawn interactor)
        {
            if (resolved || interactor == null)
            {
                return;
            }

            resolved = true;
            TimeoutComp timeoutComp = parent.GetComponent<TimeoutComp>();
            timeoutComp?.StopTimeout();
            if (hideoutPawn != null)
            {
                ZhaoliScenarioUtility.PlayHideoutRefusal(hideoutPawn);
                ZhaoliScenarioUtility.DestroyHideoutPawn(hideoutPawn);
            }

            ZhaoliScenarioUtility.SpawnHumanMeatReward(interactor, 250);
            Current.Game?.GetComponent<GameComponent_ZhaoliScenario>()?.NotifyHideoutRejected(homeMap ?? interactor.MapHeld);
            if (!ParentHasMap)
            {
                parent.Destroy();
            }
        }
    }

    public class SitePartWorker_ZhaoliHideout : SitePartWorker
    {
        public override void PostMapGenerate(Map map)
        {
            base.PostMapGenerate(map);
            (map?.Parent as Site)?.GetComponent<WorldObjectComp_ZhaoliHideout>()?.EnsurePawnSpawned(map);
        }
    }

    public class FloatMenuOptionProvider_ZhaoliHideoutInteraction : FloatMenuOptionProvider
    {
        protected override bool Drafted => false;

        protected override bool Undrafted => true;

        protected override bool Multiselect => true;

        protected override bool RequiresManipulation => false;

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
        {
            if (clickedPawn == null || !ZhaoliScenarioUtility.IsHideoutState(clickedPawn))
            {
                yield break;
            }

            WorldObjectComp_ZhaoliHideout hideoutComp = (clickedPawn.MapHeld?.Parent as Site)?.GetComponent<WorldObjectComp_ZhaoliHideout>();
            if (hideoutComp == null || hideoutComp.Resolved)
            {
                yield break;
            }

            Pawn interactor = context.FirstSelectedPawn;
            if (interactor == null || interactor.Faction != Faction.OfPlayer)
            {
                yield break;
            }

            yield return new FloatMenuOption(
                "交付 5 份草药",
                delegate
                {
                    if (!hideoutComp.TryDeliverHerbs(interactor))
                    {
                        Messages.Message("远征队现在拿不出 5 份草药，昭离仍停留在原地，等待下一次答复。", interactor, MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    Messages.Message("昭离收下草药后，于原地化作一束冰冷的光痕散去。她留下的回声说明日仍会归来。", interactor, MessageTypeDefOf.PositiveEvent, historical: true);
                });
            yield return new FloatMenuOption(
                "暂缓交付",
                delegate
                {
                    Messages.Message("昭离没有追问，只是继续在原地等待。三日之内，你仍可回来交接。", interactor, MessageTypeDefOf.NeutralEvent, historical: false);
                });
            yield return new FloatMenuOption(
                "拒绝昭离",
                delegate
                {
                    hideoutComp.Reject(interactor);
                    Messages.Message("昭离收下拒绝，伴随着一阵失真的低鸣消失。远征队身边多出了大量人肉，而更坏的东西将于明日抵达。", interactor, MessageTypeDefOf.ThreatBig, historical: true);
                });
        }
    }

    public class HediffCompProperties_ZhaoliHideoutState : HediffCompProperties
    {
        public int immobilizeTickInterval = 30;
        public int stunTicks = 60;

        public HediffCompProperties_ZhaoliHideoutState()
        {
            compClass = typeof(HediffComp_ZhaoliHideoutState);
        }
    }

    public class HediffComp_ZhaoliHideoutState : HediffComp
    {
        private HediffCompProperties_ZhaoliHideoutState PropsHideout => (HediffCompProperties_ZhaoliHideoutState)props;

        public override bool CompDisallowVisible()
        {
            return true;
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Pawn == null || Pawn.Dead || !Pawn.Spawned)
            {
                return;
            }

            if (PropsHideout.immobilizeTickInterval > 0 && Pawn.IsHashIntervalTick(PropsHideout.immobilizeTickInterval))
            {
                Pawn.pather?.StopDead();
                Pawn.stances?.stunner?.StunFor(PropsHideout.stunTicks, Pawn, addBattleLog: false, showMote: false);
            }
        }
    }

    public class HediffCompProperties_ZhaoliRaidState : HediffCompProperties
    {
        public int minPreferredCluster = 3;

        public HediffCompProperties_ZhaoliRaidState()
        {
            compClass = typeof(HediffComp_ZhaoliRaidState);
        }
    }

    public class HediffComp_ZhaoliRaidState : HediffComp
    {
        private List<ZhaoliHateEntry> hateEntries = new List<ZhaoliHateEntry>();
        private List<Thing> retaliatoryThings = new List<Thing>();
        private Pawn primaryHatredTarget;
        private Thing currentAttackTarget;
        private int lastTargetSwitchTick = -99999;
        private int ignoreHatredUntilTick = -1;
        private bool damageAppliedSinceSwitch;
        private bool raidInitialized;
        private bool linksPrepared;

        private HediffCompProperties_ZhaoliRaidState PropsRaid => (HediffCompProperties_ZhaoliRaidState)props;

        public override bool CompDisallowVisible()
        {
            return true;
        }

        public void InitializeRaid(Map targetMap)
        {
            raidInitialized = true;
            linksPrepared = false;
            currentAttackTarget = null;
            primaryHatredTarget = null;
            lastTargetSwitchTick = Find.TickManager?.TicksGame ?? 0;
            ignoreHatredUntilTick = -1;
            damageAppliedSinceSwitch = false;
            ZhaoliShieldLayerUtility.AddLayers(Pawn, ZhaoliScenarioUtility.RaidStartingShieldLayers);
            ZhaoliKarmaUtility.EnsureKarmaComp(Pawn)?.SetValue(ZhaoliScenarioUtility.RaidStartingKarma);
            if (targetMap != null)
            {
                Current.Game?.GetComponent<GameComponent_ZhaoliScenario>()?.MarkMurmurOffered(targetMap);
            }
        }

        public void RegisterIncomingAggro(Thing instigator, float rawDamageAmount)
        {
            if (Pawn == null || instigator == null || instigator == Pawn)
            {
                return;
            }

            if (instigator is Pawn pawn)
            {
                if (pawn.Dead || pawn.Destroyed)
                {
                    return;
                }

                float hatred = ZhaoliScenarioUtility.HatredPerHit + Mathf.Floor(Mathf.Max(0f, rawDamageAmount) / 10f) * ZhaoliScenarioUtility.HatredPerTenDamage;
                AddHatred(pawn, hatred);
                return;
            }

            if (instigator is Building building && !building.Destroyed && !retaliatoryThings.Contains(building))
            {
                retaliatoryThings.Add(building);
            }
        }

        public void NotifyDamageDealt(Thing target, float totalDamageDealt)
        {
            if (currentAttackTarget != null && target == currentAttackTarget && totalDamageDealt > 0f)
            {
                damageAppliedSinceSwitch = true;
                ignoreHatredUntilTick = -1;
            }
        }

        public override void CompExposeData()
        {
            base.CompExposeData();
            Scribe_Collections.Look(ref hateEntries, "hateEntries", LookMode.Deep);
            Scribe_Collections.Look(ref retaliatoryThings, "retaliatoryThings", LookMode.Reference);
            Scribe_References.Look(ref primaryHatredTarget, "primaryHatredTarget");
            Scribe_References.Look(ref currentAttackTarget, "currentAttackTarget");
            Scribe_Values.Look(ref lastTargetSwitchTick, "lastTargetSwitchTick", -99999);
            Scribe_Values.Look(ref ignoreHatredUntilTick, "ignoreHatredUntilTick", -1);
            Scribe_Values.Look(ref damageAppliedSinceSwitch, "damageAppliedSinceSwitch", defaultValue: false);
            Scribe_Values.Look(ref raidInitialized, "raidInitialized", defaultValue: false);
            Scribe_Values.Look(ref linksPrepared, "linksPrepared", defaultValue: false);
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (hateEntries == null)
                {
                    hateEntries = new List<ZhaoliHateEntry>();
                }

                if (retaliatoryThings == null)
                {
                    retaliatoryThings = new List<Thing>();
                }
            }
        }

        public override void CompPostTick(ref float severityAdjustment)
        {
            if (Pawn == null || Pawn.Dead || !Pawn.Spawned)
            {
                return;
            }

            if (!raidInitialized)
            {
                InitializeRaid(Pawn.MapHeld);
            }

            if (!linksPrepared)
            {
                PrepareInitialLinks();
                linksPrepared = true;
            }

            if (!Pawn.IsHashIntervalTick(ZhaoliScenarioUtility.AiTickInterval))
            {
                return;
            }

            RunRaidAi();
        }

        private void RunRaidAi()
        {
            CleanupEntries();
            if (Pawn.MapHeld == null || Pawn.jobs == null)
            {
                return;
            }

            if (Pawn.CurJobDef != null && Pawn.CurJobDef.abilityCasting)
            {
                return;
            }

            if (TryCastMinghuo())
            {
                return;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (currentTick >= ignoreHatredUntilTick)
            {
                Thing preferredTarget = GetPreferredHatredTarget();
                if (preferredTarget != null && preferredTarget != currentAttackTarget)
                {
                    primaryHatredTarget = preferredTarget as Pawn;
                    currentAttackTarget = preferredTarget;
                    lastTargetSwitchTick = currentTick;
                    damageAppliedSinceSwitch = false;
                    ignoreHatredUntilTick = -1;
                    if (TryUseGapCloser(preferredTarget))
                    {
                        return;
                    }
                }
                else if (currentAttackTarget == null)
                {
                    currentAttackTarget = FindNearestTarget();
                }

                if (currentAttackTarget != null && !damageAppliedSinceSwitch && currentTick - lastTargetSwitchTick > ZhaoliScenarioUtility.TargetSwitchGraceTicks)
                {
                    ignoreHatredUntilTick = currentTick + ZhaoliScenarioUtility.TargetSwitchGraceTicks;
                    currentAttackTarget = FindNearestTarget();
                }
            }
            else
            {
                currentAttackTarget = FindNearestTarget();
            }

            if (currentAttackTarget == null)
            {
                currentAttackTarget = FindNearestTarget();
            }

            if (currentAttackTarget is Pawn targetPawn)
            {
                if (TryCastField(targetPawn) || TryCastMinshen(targetPawn) || TryUseGapCloser(targetPawn))
                {
                    return;
                }

                OrderAttack(targetPawn);
                return;
            }

            if (currentAttackTarget is Building targetBuilding)
            {
                OrderAttack(targetBuilding);
            }
        }

        private void CleanupEntries()
        {
            hateEntries.RemoveAll(entry => entry == null || entry.pawn == null || entry.pawn.Destroyed || entry.pawn.Dead);
            retaliatoryThings.RemoveAll(thing => thing == null || thing.Destroyed);
            if (primaryHatredTarget != null && (primaryHatredTarget.Destroyed || primaryHatredTarget.Dead))
            {
                primaryHatredTarget = null;
            }

            if (currentAttackTarget != null && currentAttackTarget.Destroyed)
            {
                currentAttackTarget = null;
            }
        }

        private void PrepareInitialLinks()
        {
            if (Pawn?.MapHeld == null)
            {
                return;
            }

            List<Pawn> candidates = new List<Pawn>();
            List<Pawn> colonists = Pawn.MapHeld.mapPawns.FreeColonistsSpawned;
            for (int index = 0; index < colonists.Count; index++)
            {
                Pawn pawn = colonists[index];
                if (pawn == null || pawn.Dead || pawn.Destroyed || pawn == Pawn)
                {
                    continue;
                }

                candidates.Add(pawn);
            }

            if (candidates.Count == 0)
            {
                return;
            }

            int linkCount = Mathf.Min(Rand.RangeInclusive(1, 3), candidates.Count);
            HediffComp_ZhaoliKarmaLinks linkComp = ZhaoliKarmaUtility.EnsureLinkComp(Pawn);
            for (int created = 0; created < linkCount && candidates.Count > 0; created++)
            {
                int pickIndex = Rand.Range(0, candidates.Count);
                Pawn target = candidates[pickIndex];
                candidates.RemoveAt(pickIndex);
                if (target == null)
                {
                    continue;
                }

                if (!Pawn.relations.DirectRelationExists(PawnRelationDefOf.Kin, target))
                {
                    Pawn.relations.AddDirectRelation(PawnRelationDefOf.Kin, target);
                }

                linkComp?.TryAddOrRefreshLink(target, out _, out _);
            }
        }

        private void AddHatred(Pawn pawn, float hatred)
        {
            if (pawn == null || hatred <= 0f)
            {
                return;
            }

            for (int index = 0; index < hateEntries.Count; index++)
            {
                if (hateEntries[index].pawn == pawn)
                {
                    hateEntries[index].hatred += hatred;
                    return;
                }
            }

            hateEntries.Add(new ZhaoliHateEntry
            {
                pawn = pawn,
                hatred = hatred
            });
        }

        private Thing GetPreferredHatredTarget()
        {
            Pawn bestPawn = null;
            float bestHatred = float.MinValue;
            for (int index = 0; index < hateEntries.Count; index++)
            {
                ZhaoliHateEntry entry = hateEntries[index];
                if (entry?.pawn == null || !IsValidRaidTarget(entry.pawn))
                {
                    continue;
                }

                if (entry.hatred > bestHatred)
                {
                    bestHatred = entry.hatred;
                    bestPawn = entry.pawn;
                }
            }

            if (bestPawn != null)
            {
                return bestPawn;
            }

            return FindNearestTarget();
        }

        private Thing FindNearestTarget()
        {
            Thing nearest = null;
            float bestDistance = float.MaxValue;
            IReadOnlyList<Pawn> pawns = Pawn.MapHeld.mapPawns.AllPawnsSpawned;
            for (int index = 0; index < pawns.Count; index++)
            {
                Pawn candidate = pawns[index];
                if (!IsValidRaidTarget(candidate))
                {
                    continue;
                }

                float distance = Pawn.Position.DistanceToSquared(candidate.Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = candidate;
                }
            }

            for (int index = 0; index < retaliatoryThings.Count; index++)
            {
                Thing candidate = retaliatoryThings[index];
                if (!(candidate is Building building) || building.Destroyed || !building.Spawned)
                {
                    continue;
                }

                float distance = Pawn.Position.DistanceToSquared(building.Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearest = building;
                }
            }

            return nearest;
        }

        private bool IsValidRaidTarget(Pawn candidate)
        {
            if (candidate == null || candidate == Pawn || candidate.Dead || candidate.Destroyed || !candidate.Spawned)
            {
                return false;
            }

            if (candidate.Faction == Pawn.Faction)
            {
                return false;
            }

            return true;
        }

        private bool TryCastMinghuo()
        {
            if (Pawn.CurJobDef != null && Pawn.CurJobDef.abilityCasting)
            {
                return false;
            }

            Ability ability = Pawn.abilities?.GetAbility(MXZL_ZhaoliDefOf.MX_Zhaoli_Minghuo, includeTemporary: true);
            if (ability == null || !ability.CanCast)
            {
                return false;
            }

            HediffDef minghuoDef = ZhaoliEffectUtility.MinghuoHediffDef;
            if (minghuoDef != null && Pawn.health?.hediffSet?.HasHediff(minghuoDef) == true)
            {
                return false;
            }

            LocalTargetInfo self = new LocalTargetInfo(Pawn);
            if (!ability.CanApplyOn(self))
            {
                return false;
            }

            return Pawn.jobs.TryTakeOrderedJob(ability.GetJob(self, LocalTargetInfo.Invalid));
        }

        private bool TryCastField(Pawn target)
        {
            Ability ability = Pawn.abilities?.GetAbility(MXZL_ZhaoliDefOf.MX_Zhaoli_DeathField, includeTemporary: true);
            if (ability == null || !ability.CanCast || target == null)
            {
                return false;
            }

            if (Pawn.CurJobDef != null && Pawn.CurJobDef.abilityCasting)
            {
                return false;
            }

            LocalTargetInfo targetInfo = new LocalTargetInfo(target.Position);
            if (!ability.CanApplyOn(targetInfo))
            {
                return false;
            }

            if (CountRegularVictimsAround(target.Position, 9f) < PropsRaid.minPreferredCluster)
            {
                return false;
            }

            return Pawn.jobs.TryTakeOrderedJob(ability.GetJob(targetInfo, LocalTargetInfo.Invalid));
        }

        private bool TryCastMinshen(Pawn target)
        {
            Ability ability = Pawn.abilities?.GetAbility(MXZL_ZhaoliDefOf.MX_Zhaoli_Minshen, includeTemporary: true);
            if (ability == null || !ability.CanCast || target == null)
            {
                return false;
            }

            if (Pawn.CurJobDef != null && Pawn.CurJobDef.abilityCasting)
            {
                return false;
            }

            LocalTargetInfo targetInfo = new LocalTargetInfo(target.Position);
            if (!ability.CanApplyOn(targetInfo))
            {
                return false;
            }

            if (CountRegularVictimsAround(target.Position, 6.5f) < 2)
            {
                return false;
            }

            return Pawn.jobs.TryTakeOrderedJob(ability.GetJob(targetInfo, LocalTargetInfo.Invalid));
        }

        private bool TryUseGapCloser(Thing target)
        {
            if (!(target is Pawn targetPawn) || Pawn.CurJobDef != null && Pawn.CurJobDef.abilityCasting)
            {
                return false;
            }

            if (Pawn.Position.DistanceTo(targetPawn.Position) <= 6f)
            {
                return false;
            }

            Ability ability = Pawn.abilities?.GetAbility(MXZL_ZhaoliDefOf.MX_Zhaoli_Duanzhan, includeTemporary: true);
            if (ability == null || !ability.CanCast)
            {
                return false;
            }

            LocalTargetInfo targetInfo = new LocalTargetInfo(targetPawn.Position);
            if (!ability.CanApplyOn(targetInfo))
            {
                return false;
            }

            return Pawn.jobs.TryTakeOrderedJob(ability.GetJob(targetInfo, LocalTargetInfo.Invalid));
        }

        private int CountRegularVictimsAround(IntVec3 center, float radius)
        {
            int count = 0;
            IReadOnlyList<Pawn> pawns = Pawn.MapHeld.mapPawns.AllPawnsSpawned;
            for (int index = 0; index < pawns.Count; index++)
            {
                Pawn candidate = pawns[index];
                if (!IsValidRaidTarget(candidate) || !candidate.Position.InHorDistOf(center, radius))
                {
                    continue;
                }

                if (!ZhaoliScenarioUtility.ShouldDeathFieldAffectTarget(Pawn, candidate))
                {
                    continue;
                }

                count++;
            }

            return count;
        }

        private void OrderAttack(Thing target)
        {
            if (target == null || Pawn.jobs == null)
            {
                return;
            }

            if (Pawn.CurJobDef != null && Pawn.CurJobDef.abilityCasting)
            {
                return;
            }

            if (Pawn.CurJob != null && Pawn.CurJob.targetA.Thing == target && (Pawn.CurJobDef == JobDefOf.AttackMelee || Pawn.CurJobDef == JobDefOf.AttackStatic))
            {
                return;
            }

            Job job = JobMaker.MakeJob(target is Pawn ? JobDefOf.AttackMelee : JobDefOf.AttackStatic, target);
            job.expiryInterval = 180;
            job.checkOverrideOnExpire = true;
            Pawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
        }
    }

    public class ZhaoliHateEntry : IExposable
    {
        public Pawn pawn;
        public float hatred;

        public void ExposeData()
        {
            Scribe_References.Look(ref pawn, "pawn");
            Scribe_Values.Look(ref hatred, "hatred", 0f);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.Kill))]
    internal static class Patch_Pawn_Kill_ZhaoliScenario
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Pawn __instance)
        {
            GameComponent_ZhaoliScenario component = Current.Game?.GetComponent<GameComponent_ZhaoliScenario>();
            if (__instance == null || component == null)
            {
                return;
            }

            if (__instance.Dead)
            {
                component.NotifyQualifyingPawnDeath(__instance);
            }

            if (__instance.Dead && ZhaoliScenarioUtility.IsRaidState(__instance))
            {
                component.NotifyRaidZhaoliDefeated(__instance);
            }
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.PreApplyDamage))]
    internal static class Patch_Pawn_PreApplyDamage_ZhaoliScenario
    {
        public static void Postfix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (__instance == null)
            {
                return;
            }

            if (ZhaoliScenarioUtility.IsHideoutState(__instance))
            {
                absorbed = true;
                return;
            }

            HediffComp_ZhaoliRaidState raidComp = ZhaoliScenarioUtility.GetRaidStateComp(__instance);
            raidComp?.RegisterIncomingAggro(dinfo.Instigator, dinfo.Amount);
        }
    }

    [HarmonyPatch(typeof(Thing), nameof(Thing.TakeDamage))]
    internal static class Patch_Thing_TakeDamage_ZhaoliScenario
    {
        public static void Postfix(Thing __instance, DamageInfo dinfo, DamageWorker.DamageResult __result)
        {
            if (__instance == null || __result.totalDamageDealt <= 0f || !(dinfo.Instigator is Pawn pawn))
            {
                return;
            }

            ZhaoliScenarioUtility.GetRaidStateComp(pawn)?.NotifyDamageDealt(__instance, __result.totalDamageDealt);
        }
    }

    [HarmonyPatch(typeof(ZhaoliKarmaUtility), nameof(ZhaoliKarmaUtility.AddKarma))]
    internal static class Patch_ZhaoliKarmaUtility_AddKarma_RaidCap
    {
        public static bool Prefix(Pawn pawn, float value)
        {
            if (!ZhaoliScenarioUtility.IsRaidState(pawn))
            {
                return true;
            }

            HediffComp_PawnSpecialResource comp = ZhaoliKarmaUtility.EnsureKarmaComp(pawn);
            if (comp == null)
            {
                return false;
            }

            comp.SetValue(Mathf.Clamp(comp.CurrentValue + value, 0f, 100f));
            return false;
        }
    }

    [HarmonyPatch(typeof(ZhaoliRebirthUtility), nameof(ZhaoliRebirthUtility.TryScheduleRebirth))]
    internal static class Patch_ZhaoliRebirthUtility_TryScheduleRebirth_RaidBlock
    {
        public static bool Prefix(Pawn pawn, ref bool __result)
        {
            if (!ZhaoliScenarioUtility.IsRaidState(pawn))
            {
                return true;
            }

            __result = false;
            return false;
        }
    }
}

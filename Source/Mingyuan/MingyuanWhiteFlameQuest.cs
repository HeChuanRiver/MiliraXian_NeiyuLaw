using System;
using System.Collections.Generic;
using System.Linq;
using LudeonTK;
using MiliraXian.Characters.Neiyu;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;
using Verse.AI;
using Verse.AI.Group;

namespace MiliraXian.Characters.Mingyuan
{
    internal enum MingyuanWhiteFlameStage
    {
        Waiting,
        Omen,
        Offered,
        Defending,
        AwaitingDecision,
        Completed,
        Reforming
    }

    internal static class MingyuanWhiteFlameUtility
    {
        public const string QuestDefName = "MX_Mingyuan_WhiteFlameQuest";
        public const string MarkerDefName = "MX_Mingyuan_QuestRebirthFlame";
        public const string IncomingDefName = "MX_Mingyuan_QuestRebirthFlameIncoming";
        public const string ChoiceLetterDefName = "MX_Mingyuan_ArrivalChoice";
        public const string MingyuanPawnKindDefName = "MiliraXian_Mingyuan";
        public const string NeiyuPawnKindDefName = "MiliraXian_Neiyu";
        public const string QinghePawnKindDefName = "MiliraXian_Qinghe";
        public const string ZhaoliPawnKindDefName = "MiliraXian_Zhaoli";

        private const string RainbowBowDefName = "MX_Mingyuan_RainbowBow";
        private const string CinderSwordDefName = "MX_Mingyuan_CinderSword";

        public static bool MingyuanExistsAnywhere(Pawn except = null)
        {
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
            {
                if (pawn != null && pawn != except && pawn.kindDef?.defName == MingyuanPawnKindDefName)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool HasPlayerPawn(string pawnKindDefName)
        {
            List<Pawn> pawns = PawnsFinder.AllMapsCaravansAndTravellingTransporters_Alive_OfPlayerFaction;
            for (int index = 0; index < pawns.Count; index++)
            {
                Pawn pawn = pawns[index];
                if (pawn != null && !pawn.Dead && pawn.kindDef?.defName == pawnKindDefName)
                {
                    return true;
                }
            }

            return false;
        }

        public static Quest FindBlockingQuest()
        {
            if (Find.QuestManager == null)
            {
                return null;
            }

            List<Quest> quests = Find.QuestManager.QuestsListForReading;
            for (int index = 0; index < quests.Count; index++)
            {
                Quest candidate = quests[index];
                if (candidate?.root?.defName != QuestDefName)
                {
                    continue;
                }

                if (candidate.State == QuestState.NotYetAccepted || candidate.State == QuestState.Ongoing)
                {
                    return candidate;
                }
            }

            return null;
        }

        public static string BuildQuestDescription()
        {
            string description = "MX_Mingyuan_QuestDescription".Translate().ToString();
            if (HasPlayerPawn(NeiyuPawnKindDefName))
            {
                description += "\n\n" + "MX_Mingyuan_QuestDialogue_Neiyu".Translate();
            }

            if (HasPlayerPawn(QinghePawnKindDefName))
            {
                description += "\n\n" + "MX_Mingyuan_QuestDialogue_Qinghe".Translate();
            }

            if (HasPlayerPawn(ZhaoliPawnKindDefName))
            {
                description += "\n\n" + "MX_Mingyuan_QuestDialogue_Zhaoli".Translate();
            }

            return description;
        }

        public static string BuildArrivalDialogue()
        {
            string text = "MX_Mingyuan_ArrivalText".Translate().ToString();
            if (HasPlayerPawn(NeiyuPawnKindDefName))
            {
                text += "\n\n" + "MX_Mingyuan_ArrivalDialogue_Neiyu".Translate();
            }

            if (HasPlayerPawn(QinghePawnKindDefName))
            {
                text += "\n\n" + "MX_Mingyuan_ArrivalDialogue_Qinghe".Translate();
            }

            if (HasPlayerPawn(ZhaoliPawnKindDefName))
            {
                text += "\n\n" + "MX_Mingyuan_ArrivalDialogue_Zhaoli".Translate();
            }

            return text;
        }

        public static Pawn GenerateMingyuanPawn()
        {
            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(MingyuanPawnKindDefName);
            if (kindDef == null)
            {
                Log.Error("[MiliraXian.Characters.Mingyuan] Missing PawnKindDef: " + MingyuanPawnKindDefName);
                return null;
            }

            Faction faction = Find.FactionManager?.OfAncients;
            PawnGenerationRequest request = new PawnGenerationRequest(
                kindDef,
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
            if (pawn == null)
            {
                return null;
            }

            EnsureRecruitLoadoutAndAbilities(pawn);
            if (!pawn.Spawned && !pawn.IsWorldPawn())
            {
                Find.WorldPawns.PassToWorld(pawn, PawnDiscardDecideMode.KeepForever);
            }

            return pawn;
        }

        public static bool JoinPlayerFaction(Pawn pawn)
        {
            Faction playerFaction = Faction.OfPlayer;
            PawnKindDef mingyuanKind = DefDatabase<PawnKindDef>.GetNamedSilentFail(MingyuanPawnKindDefName);
            if (pawn == null || pawn.Destroyed || pawn.Dead || playerFaction == null || mingyuanKind == null)
            {
                return false;
            }

            if (pawn.Faction != playerFaction)
            {
                pawn.SetFaction(playerFaction);
            }

            // Pawn.SetFaction normally converts a recruited humanlike to the player's
            // basic member kind. Mingyuan must retain her unique PawnKindDef.
            if (pawn.kindDef != mingyuanKind)
            {
                pawn.ChangeKind(mingyuanKind);
            }

            // The neutral arrival uses a temporary defend-point lord so Mingyuan
            // stays near the rebirth site. Never carry that AI state into recruitment.
            Lord lingeringLord = pawn.GetLord();
            if (lingeringLord != null)
            {
                lingeringLord.RemovePawn(pawn);
            }

            pawn.jobs?.StopAll();
            EnsureRecruitLoadoutAndAbilities(pawn);
            return pawn.Faction == playerFaction && pawn.kindDef == mingyuanKind;
        }

        public static void EnsureRecruitLoadoutAndAbilities(Pawn pawn)
        {
            if (!MingyuanUtility.IsMingyuan(pawn))
            {
                return;
            }

            EnsureFixedWeapons(pawn);
            if (pawn.abilities != null && pawn.kindDef?.abilities != null)
            {
                for (int index = 0; index < pawn.kindDef.abilities.Count; index++)
                {
                    AbilityDef abilityDef = pawn.kindDef.abilities[index];
                    if (abilityDef != null && pawn.abilities.GetAbility(abilityDef) == null)
                    {
                        pawn.abilities.GainAbility(abilityDef);
                    }
                }
            }

            MingyuanUtility.EnsureHediff(pawn, MingyuanUtility.BurningBodyDef);
            MingyuanUtility.EnsureHediff(pawn, MingyuanUtility.ShieldDef);
            MingyuanUtility.EnsureHediff(pawn, MingyuanUtility.RebirthDef);
        }

        public static Map ResolveSpawnMap(Map preferredMap)
        {
            if (preferredMap != null && Current.Game?.Maps?.Contains(preferredMap) == true && preferredMap.IsPlayerHome)
            {
                return preferredMap;
            }

            return Find.AnyPlayerHomeMap;
        }

        private static void EnsureFixedWeapons(Pawn pawn)
        {
            if (pawn?.equipment == null)
            {
                return;
            }

            ThingDef bowDef = DefDatabase<ThingDef>.GetNamedSilentFail(RainbowBowDefName);
            ThingDef swordDef = DefDatabase<ThingDef>.GetNamedSilentFail(CinderSwordDefName);
            if (bowDef == null || swordDef == null)
            {
                Log.Error("[MiliraXian.Characters.Mingyuan] Missing fixed Mingyuan weapon def.");
                return;
            }

            ThingWithComps primary = pawn.equipment.Primary;
            if (primary == null || primary.def != bowDef)
            {
                if (primary != null)
                {
                    if (primary.def == swordDef && pawn.inventory?.innerContainer != null)
                    {
                        pawn.equipment.TryTransferEquipmentToContainer(primary, pawn.inventory.innerContainer);
                    }
                    else
                    {
                        pawn.equipment.DestroyEquipment(primary);
                    }
                }

                ThingWithComps bow = MakeExcellentGear(bowDef, pawn);
                if (bow != null)
                {
                    pawn.equipment.AddEquipment(bow);
                }
            }

            if (pawn.inventory?.innerContainer == null || ContainsDef(pawn.inventory.innerContainer, swordDef))
            {
                return;
            }

            ThingWithComps sword = MakeExcellentGear(swordDef, pawn);
            if (sword != null && !pawn.inventory.innerContainer.TryAdd(sword, canMergeWithExistingStacks: false))
            {
                sword.Destroy(DestroyMode.Vanish);
            }
        }

        private static ThingWithComps MakeExcellentGear(ThingDef thingDef, Pawn pawn)
        {
            ThingWithComps gear = ThingMaker.MakeThing(thingDef) as ThingWithComps;
            if (gear == null)
            {
                return null;
            }

            PawnGenerator.PostProcessGeneratedGear(gear, pawn);
            gear.TryGetComp<CompQuality>()?.SetQuality(QualityCategory.Excellent, ArtGenerationContext.Outsider);
            gear.HitPoints = gear.MaxHitPoints;
            return gear;
        }

        private static bool ContainsDef(ThingOwner owner, ThingDef thingDef)
        {
            for (int index = 0; index < owner.Count; index++)
            {
                if (owner[index]?.def == thingDef)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public class QuestNode_Root_MingyuanWhiteFlame : QuestNode
    {
        protected override void RunInt()
        {
            Slate slate = QuestGen.slate;
            Quest quest = QuestGen.quest;
            if (!slate.TryGet("map", out Map map))
            {
                map = Find.AnyPlayerHomeMap;
            }

            if (map == null)
            {
                return;
            }

            quest.AcceptanceRequirementNotSpace(map.Parent);
            quest.AddPart(new QuestPart_MingyuanWhiteFlame
            {
                map = map,
                inSignal = quest.InitiateSignal,
                signalListenMode = QuestPart.SignalListenMode.OngoingOrNotYetAccepted
            });
        }

        protected override bool TestRunInt(Slate slate)
        {
            if (slate.TryGet("map", out Map map))
            {
                return map != null && map.IsPlayerHome;
            }

            return Find.AnyPlayerHomeMap != null;
        }
    }

    public class QuestPart_MingyuanWhiteFlame : QuestPart
    {
        public Map map;
        public string inSignal;

        public override string DescriptionPart
        {
            get
            {
                return Current.Game?.GetComponent<GameComponent_MingyuanWhiteFlameQuest>()
                    ?.GetQuestProgressDescription(quest);
            }
        }

        public override IEnumerable<GlobalTargetInfo> QuestLookTargets
        {
            get
            {
                GameComponent_MingyuanWhiteFlameQuest component =
                    Current.Game?.GetComponent<GameComponent_MingyuanWhiteFlameQuest>();
                Thing marker = component?.Marker;
                if (marker != null && marker.Spawned)
                {
                    yield return marker;
                }
                else if (map?.Parent != null)
                {
                    yield return map.Parent;
                }
            }
        }

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            if (signal.tag == inSignal)
            {
                Current.Game?.GetComponent<GameComponent_MingyuanWhiteFlameQuest>()
                    ?.BeginDefense(quest, map);
            }
        }

        public override void Cleanup()
        {
            Current.Game?.GetComponent<GameComponent_MingyuanWhiteFlameQuest>()
                ?.NotifyQuestCleanedUp(quest);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref map, "map");
            Scribe_Values.Look(ref inSignal, "inSignal");
        }
    }

    public class Thing_MingyuanQuestRebirthFlame : Building
    {
        public override void SpawnSetup(Map map, bool respawningAfterLoad)
        {
            base.SpawnSetup(map, respawningAfterLoad);
            if (!respawningAfterLoad)
            {
                Current.Game?.GetComponent<GameComponent_MingyuanWhiteFlameQuest>()
                    ?.NotifyMarkerLanded(this);
            }
        }

        protected override void Tick()
        {
            base.Tick();
            GameComponent_MingyuanWhiteFlameQuest component =
                Current.Game?.GetComponent<GameComponent_MingyuanWhiteFlameQuest>();
            MingyuanWhiteFlameVfx.TickMarker(this, component?.GetMarkerVisualIntensity(this) ?? 1f);
        }

        protected override void DrawAt(Vector3 drawLoc, bool flip = false)
        {
            base.DrawAt(drawLoc, flip);
            GameComponent_MingyuanWhiteFlameQuest component =
                Current.Game?.GetComponent<GameComponent_MingyuanWhiteFlameQuest>();
            MingyuanWhiteFlameVfx.DrawMarker(
                drawLoc,
                Find.TickManager?.TicksGame ?? 0,
                component?.GetMarkerVisualIntensity(this) ?? 1f);
        }

        public override void PreApplyDamage(ref DamageInfo dinfo, out bool absorbed)
        {
            if (Current.Game?.GetComponent<GameComponent_MingyuanWhiteFlameQuest>()
                    ?.IsMarkerProtectedDuringReformation(this) == true)
            {
                absorbed = true;
                return;
            }

            base.PreApplyDamage(ref dinfo, out absorbed);
        }

        public override void Destroy(DestroyMode mode = DestroyMode.Vanish)
        {
            Map map = MapHeld;
            IntVec3 cell = PositionHeld;
            bool violent = mode != DestroyMode.Vanish && map != null && cell.IsValid;
            base.Destroy(mode);
            if (violent)
            {
                MingyuanWhiteFlameVfx.PlayFailure(map, cell);
                Current.Game?.GetComponent<GameComponent_MingyuanWhiteFlameQuest>()
                    ?.NotifyMarkerDestroyed(this);
            }
        }

        public override string GetInspectString()
        {
            string baseText = base.GetInspectString();
            string questText = Current.Game?.GetComponent<GameComponent_MingyuanWhiteFlameQuest>()
                ?.GetMarkerInspectString(this);
            if (questText.NullOrEmpty())
            {
                return baseText;
            }

            return baseText.NullOrEmpty() ? questText : baseText + "\n" + questText;
        }
    }

    public class ChoiceLetter_MingyuanArrival : ChoiceLetter
    {
        public Pawn mingyuan;
        public Map targetMap;
        public IntVec3 targetCell;
        private bool autoOpenOnArrival = true;

        public override bool CanDismissWithRightClick => false;

        public override bool ShouldAutomaticallyOpenLetter => autoOpenOnArrival;

        public override void OpenLetter()
        {
            autoOpenOnArrival = false;
            base.OpenLetter();
        }

        public override IEnumerable<DiaOption> Choices
        {
            get
            {
                if (ArchivedOnly)
                {
                    yield return Option_Close;
                    yield break;
                }

                GameComponent_MingyuanWhiteFlameQuest component =
                    Current.Game?.GetComponent<GameComponent_MingyuanWhiteFlameQuest>();

                DiaOption welcome = new DiaOption("MX_Mingyuan_Welcome".Translate().ToString());
                welcome.resolveTree = true;
                if (component == null || mingyuan == null || MingyuanWhiteFlameUtility.ResolveSpawnMap(targetMap) == null)
                {
                    welcome.Disable("MX_Mingyuan_NoPlayerHomeMap".Translate().ToString());
                }
                welcome.action = delegate
                {
                    if (component?.WelcomeMingyuan(mingyuan, targetMap, targetCell) == true)
                    {
                        Find.LetterStack.RemoveLetter(this);
                    }
                };
                yield return welcome;

                DiaOption defer = new DiaOption("MX_Mingyuan_Defer".Translate().ToString());
                defer.resolveTree = true;
                defer.action = delegate
                {
                    component?.DeferMingyuan(mingyuan);
                    Find.LetterStack.RemoveLetter(this);
                };
                yield return defer;

                if (lookTargets != null && lookTargets.IsValid)
                {
                    yield return Option_JumpToLocationAndPostpone;
                }
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_References.Look(ref mingyuan, "mingyuan");
            Scribe_References.Look(ref targetMap, "targetMap");
            Scribe_Values.Look(ref targetCell, "targetCell");
            Scribe_Values.Look(ref autoOpenOnArrival, "autoOpenOnArrival", true);
        }
    }

    public class FloatMenuOptionProvider_MingyuanArrivalInteraction : FloatMenuOptionProvider
    {
        protected override bool Drafted => true;

        protected override bool Undrafted => true;

        protected override bool Multiselect => true;

        protected override bool RequiresManipulation => false;

        public override bool SelectedPawnValid(Pawn pawn, FloatMenuContext context)
        {
            return pawn != null
                && pawn.Faction == Faction.OfPlayer
                && pawn.Spawned
                && !pawn.Dead
                && base.SelectedPawnValid(pawn, context);
        }

        public override IEnumerable<FloatMenuOption> GetOptionsFor(Pawn clickedPawn, FloatMenuContext context)
        {
            GameComponent_MingyuanWhiteFlameQuest component =
                Current.Game?.GetComponent<GameComponent_MingyuanWhiteFlameQuest>();
            if (component?.IsAwaitingDecisionFor(clickedPawn) != true)
            {
                yield break;
            }

            yield return new FloatMenuOption(
                "MX_Mingyuan_Talk".Translate(clickedPawn.Named("MINGYUAN")).ToString(),
                delegate
                {
                    if (!component.TryOpenArrivalDecision(clickedPawn))
                    {
                        Messages.Message(
                            "MX_Mingyuan_TalkUnavailable".Translate(),
                            clickedPawn,
                            MessageTypeDefOf.RejectInput,
                            historical: false);
                    }
                });
        }
    }

    public class GameComponent_MingyuanWhiteFlameQuest : GameComponent
    {
        private const int MinimumDaysPassed = 60;
        private const float MinimumColonyWealth = 135000f;
        private const int WaitingCheckInterval = 250;
        private const int OmenDurationTicks = 12000;
        private const int OmenHeatLetterTick = 6000;
        private const int OmenVisualInterval = 180;
        private const int DefenseDurationTicks = GenDate.TicksPerDay / 2;
        private const int FirstWaveDelayTicks = 2500;
        private const int WaveSpacingTicks = 9000;
        private const int WaveWarningLeadTicks = 600;
        private const int ActiveCheckInterval = 60;
        private const int FailureRetryTicks = 20 * GenDate.TicksPerDay;
        private const int DecisionCheckInterval = 250;
        private const int ReformationDurationTicks = 720;
        private const string FallbackMechKindDefName = "Mech_Lancer";
        private const string EliteMechKindDefName = "Mech_CentipedeBlaster";

        private static readonly float[] WavePointFactors = { 0.7f, 1f, 1.3f };

        private MingyuanWhiteFlameStage stage;
        private int nextProcessTick;
        private int nextOfferTick;
        private int omenStartedTick;
        private bool heatOmenLetterSent;
        private int defenseStartedTick;
        private int defenseEndTick;
        private int nextWaveTick;
        private int wavesSpawned;
        private int wavesWarned;
        private int markerLandedTick;
        private int reformationStartedTick;
        private int reformationEndTick;
        private int reformationPhase;
        private Map targetMap;
        private Quest activeQuest;
        private Thing incomingFlame;
        private Thing marker;
        private Pawn pendingMingyuan;
        private List<Pawn> wavePawns = new List<Pawn>();

        private bool resolvingQuest;

        public Thing Marker => marker;

        public GameComponent_MingyuanWhiteFlameQuest(Game game)
        {
        }

        public override void GameComponentTick()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
            {
                return;
            }

            int currentTick = Find.TickManager.TicksGame;
            if (currentTick < nextProcessTick || stage == MingyuanWhiteFlameStage.Completed)
            {
                return;
            }

            if (stage != MingyuanWhiteFlameStage.Waiting
                && MingyuanWhiteFlameUtility.MingyuanExistsAnywhere(pendingMingyuan))
            {
                ConcludeBecauseMingyuanExists();
                return;
            }

            switch (stage)
            {
                case MingyuanWhiteFlameStage.Waiting:
                    ProcessWaiting(currentTick);
                    break;
                case MingyuanWhiteFlameStage.Omen:
                    ProcessOmen(currentTick);
                    break;
                case MingyuanWhiteFlameStage.Offered:
                    ProcessOffered(currentTick);
                    break;
                case MingyuanWhiteFlameStage.Defending:
                    ProcessDefense(currentTick);
                    break;
                case MingyuanWhiteFlameStage.AwaitingDecision:
                    ProcessAwaitingDecision(currentTick);
                    break;
                case MingyuanWhiteFlameStage.Reforming:
                    ProcessReformation(currentTick);
                    break;
            }
        }

        public override void LoadedGame()
        {
            base.LoadedGame();
            nextProcessTick = Find.TickManager?.TicksGame ?? 0;
            if (wavePawns == null)
            {
                wavePawns = new List<Pawn>();
            }

            wavePawns.RemoveAll(pawn => pawn == null);
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref stage, "mingyuanWhiteFlameStage", MingyuanWhiteFlameStage.Waiting);
            Scribe_Values.Look(ref nextProcessTick, "mingyuanWhiteFlameNextProcessTick", 0);
            Scribe_Values.Look(ref nextOfferTick, "mingyuanWhiteFlameNextOfferTick", 0);
            Scribe_Values.Look(ref omenStartedTick, "mingyuanWhiteFlameOmenStartedTick", 0);
            Scribe_Values.Look(ref heatOmenLetterSent, "mingyuanWhiteFlameHeatLetterSent", false);
            Scribe_Values.Look(ref defenseStartedTick, "mingyuanWhiteFlameDefenseStartedTick", 0);
            Scribe_Values.Look(ref defenseEndTick, "mingyuanWhiteFlameDefenseEndTick", 0);
            Scribe_Values.Look(ref nextWaveTick, "mingyuanWhiteFlameNextWaveTick", 0);
            Scribe_Values.Look(ref wavesSpawned, "mingyuanWhiteFlameWavesSpawned", 0);
            Scribe_Values.Look(ref wavesWarned, "mingyuanWhiteFlameWavesWarned", 0);
            Scribe_Values.Look(ref markerLandedTick, "mingyuanWhiteFlameMarkerLandedTick", 0);
            Scribe_Values.Look(ref reformationStartedTick, "mingyuanWhiteFlameReformationStartedTick", 0);
            Scribe_Values.Look(ref reformationEndTick, "mingyuanWhiteFlameReformationEndTick", 0);
            Scribe_Values.Look(ref reformationPhase, "mingyuanWhiteFlameReformationPhase", 0);
            Scribe_References.Look(ref targetMap, "mingyuanWhiteFlameTargetMap");
            Scribe_References.Look(ref activeQuest, "mingyuanWhiteFlameQuest");
            Scribe_References.Look(ref incomingFlame, "mingyuanWhiteFlameIncoming");
            Scribe_References.Look(ref marker, "mingyuanWhiteFlameMarker");
            Scribe_References.Look(ref pendingMingyuan, "mingyuanWhiteFlamePawn");
            Scribe_Collections.Look(ref wavePawns, "mingyuanWhiteFlameEnemies", LookMode.Reference);

            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (wavePawns == null)
                {
                    wavePawns = new List<Pawn>();
                }

                wavePawns.RemoveAll(pawn => pawn == null);
                resolvingQuest = false;
            }
        }

        public void BeginDefense(Quest quest, Map map)
        {
            if (quest == null || stage != MingyuanWhiteFlameStage.Offered || activeQuest != quest)
            {
                return;
            }

            Map resolvedMap = MingyuanWhiteFlameUtility.ResolveSpawnMap(map ?? targetMap);
            if (resolvedMap == null || MingyuanWhiteFlameUtility.MingyuanExistsAnywhere())
            {
                ConcludeBecauseMingyuanExists();
                return;
            }

            ThingDef markerDef = DefDatabase<ThingDef>.GetNamedSilentFail(MingyuanWhiteFlameUtility.MarkerDefName);
            ThingDef incomingDef = DefDatabase<ThingDef>.GetNamedSilentFail(MingyuanWhiteFlameUtility.IncomingDefName);
            if (markerDef == null || incomingDef == null)
            {
                Log.Error("[MiliraXian.Characters.Mingyuan] Missing white flame quest ThingDef.");
                ScheduleRetry(FailureRetryTicks, sendMessage: false);
                EndActiveQuest(QuestEndOutcome.Fail, sendLetter: false);
                return;
            }

            targetMap = resolvedMap;
            IntVec3 dropCell = DropCellFinder.TryFindSafeLandingSpotCloseToColony(
                targetMap, new IntVec2(1, 1), Faction.OfPlayer);
            if (!dropCell.IsValid)
            {
                dropCell = MingyuanUtility.FindStandableCellNear(targetMap.Center, targetMap, 30);
            }

            marker = ThingMaker.MakeThing(markerDef);
            marker.SetFactionDirect(Faction.OfPlayer);
            incomingFlame = SkyfallerMaker.SpawnSkyfaller(incomingDef, marker, dropCell, targetMap);
            if (incomingFlame == null)
            {
                Log.Error("[MiliraXian.Characters.Mingyuan] Failed to spawn the white flame skyfaller.");
                marker.Destroy(DestroyMode.Vanish);
                marker = null;
                ScheduleRetry(FailureRetryTicks, sendMessage: false);
                EndActiveQuest(QuestEndOutcome.Fail, sendLetter: false);
                return;
            }

            defenseStartedTick = 0;
            defenseEndTick = 0;
            markerLandedTick = 0;
            nextWaveTick = int.MaxValue;
            wavesSpawned = 0;
            wavesWarned = 0;
            reformationStartedTick = 0;
            reformationEndTick = 0;
            reformationPhase = 0;
            wavePawns.Clear();
            stage = MingyuanWhiteFlameStage.Defending;
            nextProcessTick = Find.TickManager.TicksGame + ActiveCheckInterval;

            MingyuanWhiteFlameVfx.PlayAcceptance(targetMap, dropCell);

            Find.LetterStack.ReceiveLetter(
                "MX_Mingyuan_DefenseStartedLabel".Translate(),
                "MX_Mingyuan_DefenseStartedText".Translate(),
                LetterDefOf.ThreatBig,
                new TargetInfo(dropCell, targetMap));
        }

        public void NotifyMarkerLanded(Thing landedMarker)
        {
            if (stage != MingyuanWhiteFlameStage.Defending
                || landedMarker == null
                || landedMarker.Destroyed
                || landedMarker != marker)
            {
                return;
            }

            incomingFlame = null;
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            if (markerLandedTick > 0)
            {
                return;
            }

            markerLandedTick = currentTick;
            defenseStartedTick = currentTick;
            defenseEndTick = currentTick + DefenseDurationTicks;
            nextWaveTick = currentTick + FirstWaveDelayTicks;
            wavesWarned = wavesSpawned;
            nextProcessTick = currentTick + ActiveCheckInterval;
            Messages.Message(
                "MX_Mingyuan_MarkerLandedMessage".Translate(),
                landedMarker,
                MessageTypeDefOf.PositiveEvent,
                historical: false);
        }

        public void NotifyMarkerDestroyed(Thing destroyedMarker)
        {
            if (stage == MingyuanWhiteFlameStage.Defending && destroyedMarker == marker)
            {
                FailDefense("MX_Mingyuan_FlameDestroyedText".Translate().ToString());
            }
        }

        public bool IsMarkerProtectedDuringReformation(Thing queriedMarker)
        {
            return stage == MingyuanWhiteFlameStage.Reforming && queriedMarker != null && queriedMarker == marker;
        }

        public float GetMarkerVisualIntensity(Thing queriedMarker)
        {
            if (queriedMarker == null || queriedMarker != marker)
            {
                return 1f;
            }

            if (stage == MingyuanWhiteFlameStage.Reforming)
            {
                return 1.9f;
            }

            if (stage == MingyuanWhiteFlameStage.Defending
                && wavesWarned > wavesSpawned
                && Find.TickManager != null
                && nextWaveTick - Find.TickManager.TicksGame <= WaveWarningLeadTicks)
            {
                return 1.55f;
            }

            return 1f + wavesSpawned * 0.12f;
        }

        public void NotifyQuestCleanedUp(Quest quest)
        {
            if (resolvingQuest || quest == null || quest != activeQuest)
            {
                return;
            }

            if (stage == MingyuanWhiteFlameStage.Offered
                || stage == MingyuanWhiteFlameStage.Defending
                || stage == MingyuanWhiteFlameStage.Reforming
                || stage == MingyuanWhiteFlameStage.AwaitingDecision)
            {
                CleanupTemporaryObjects(removeEnemies: true, removePendingPawn: true);
                activeQuest = null;
                stage = MingyuanWhiteFlameStage.Waiting;
                nextOfferTick = (Find.TickManager?.TicksGame ?? 0) + FailureRetryTicks;
                nextProcessTick = nextOfferTick;
                Messages.Message("MX_Mingyuan_QuestAbandoned".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }
        }

        public bool WelcomeMingyuan(Pawn pawn, Map preferredMap, IntVec3 preferredCell)
        {
            if (stage != MingyuanWhiteFlameStage.AwaitingDecision
                || pawn == null
                || pawn.Destroyed
                || pawn.Dead
                || pawn != pendingMingyuan)
            {
                return false;
            }

            if (MingyuanWhiteFlameUtility.MingyuanExistsAnywhere(pawn))
            {
                ConcludeBecauseMingyuanExists();
                return false;
            }

            Map spawnMap = MingyuanWhiteFlameUtility.ResolveSpawnMap(preferredMap ?? targetMap);
            if (spawnMap == null)
            {
                return false;
            }

            if (pawn.IsWorldPawn())
            {
                Find.WorldPawns.RemovePawn(pawn);
            }

            if (!MingyuanWhiteFlameUtility.JoinPlayerFaction(pawn))
            {
                Log.Error("[MiliraXian.Characters.Mingyuan] Failed to transfer Mingyuan to the player faction.");
                return false;
            }

            IntVec3 spawnCell = preferredCell.IsValid && preferredCell.InBounds(spawnMap)
                ? MingyuanUtility.FindStandableCellNear(preferredCell, spawnMap, 8)
                : MingyuanUtility.FindStandableCellNear(spawnMap.Center, spawnMap, 20);
            if (!pawn.Spawned)
            {
                GenSpawn.Spawn(pawn, spawnCell, spawnMap);
            }

            MingyuanWhiteFlameVfx.PlayWelcome(pawn);
            NeiyuSpecialPawnIntegration.TryRegister(pawn);
            pendingMingyuan = null;
            stage = MingyuanWhiteFlameStage.Completed;
            EndActiveQuest(QuestEndOutcome.Success, sendLetter: false);
            Find.LetterStack.ReceiveLetter(
                "MX_Mingyuan_JoinedLabel".Translate(),
                "MX_Mingyuan_JoinedText".Translate(),
                LetterDefOf.PositiveEvent,
                pawn);
            return true;
        }

        public bool IsAwaitingDecisionFor(Pawn pawn)
        {
            return stage == MingyuanWhiteFlameStage.AwaitingDecision
                && pawn != null
                && pawn == pendingMingyuan
                && !pawn.Destroyed
                && !pawn.Dead;
        }

        public bool TryOpenArrivalDecision(Pawn pawn)
        {
            if (!IsAwaitingDecisionFor(pawn))
            {
                return false;
            }

            ChoiceLetter_MingyuanArrival letter = FindDecisionLetter(pawn);
            if (letter == null)
            {
                SendDecisionLetter();
                letter = FindDecisionLetter(pawn);
            }

            if (letter == null)
            {
                return false;
            }

            letter.OpenLetter();
            return true;
        }

        public void DeferMingyuan(Pawn pawn)
        {
            if (stage != MingyuanWhiteFlameStage.AwaitingDecision || pawn == null || pawn != pendingMingyuan)
            {
                return;
            }

            MingyuanWhiteFlameVfx.PlayDeparture(pawn);
            DiscardPendingMingyuan();
            int delayDays = Rand.RangeInclusive(15, 30);
            stage = MingyuanWhiteFlameStage.Waiting;
            nextOfferTick = Find.TickManager.TicksGame + delayDays * GenDate.TicksPerDay;
            nextProcessTick = nextOfferTick;
            EndActiveQuest(QuestEndOutcome.Unknown, sendLetter: false);
            Find.LetterStack.ReceiveLetter(
                "MX_Mingyuan_DeferredLabel".Translate(),
                "MX_Mingyuan_DeferredText".Translate(delayDays),
                LetterDefOf.NeutralEvent);
        }

        public string GetQuestProgressDescription(Quest quest)
        {
            if (quest == null || quest != activeQuest)
            {
                return null;
            }

            if (stage == MingyuanWhiteFlameStage.Defending && Find.TickManager != null)
            {
                if (markerLandedTick <= 0 || marker == null || !marker.Spawned)
                {
                    return "MX_Mingyuan_IncomingProgress".Translate().ToString();
                }

                int remainingTicks = Mathf.Max(0, defenseEndTick - Find.TickManager.TicksGame);
                float remainingHours = remainingTicks / (GenDate.TicksPerDay / 24f);
                return "MX_Mingyuan_DefenseProgress".Translate(wavesSpawned, remainingHours.ToString("0.0")).ToString();
            }

            if (stage == MingyuanWhiteFlameStage.Reforming && Find.TickManager != null)
            {
                int reformationTicksRemaining = Mathf.Max(0, reformationEndTick - Find.TickManager.TicksGame);
                return "MX_Mingyuan_ReformationProgress".Translate((reformationTicksRemaining / 60f).ToString("0.0")).ToString();
            }

            if (stage == MingyuanWhiteFlameStage.AwaitingDecision)
            {
                return "MX_Mingyuan_AwaitingDecisionProgress".Translate().ToString();
            }

            return null;
        }

        public string GetMarkerInspectString(Thing queriedMarker)
        {
            if (queriedMarker == null || queriedMarker != marker)
            {
                return null;
            }

            if (stage == MingyuanWhiteFlameStage.Reforming && Find.TickManager != null)
            {
                int reformationTicksRemaining = Mathf.Max(0, reformationEndTick - Find.TickManager.TicksGame);
                return "MX_Mingyuan_ReformationProgress".Translate((reformationTicksRemaining / 60f).ToString("0.0")).ToString();
            }

            if (stage != MingyuanWhiteFlameStage.Defending || markerLandedTick <= 0)
            {
                return "MX_Mingyuan_IncomingProgress".Translate().ToString();
            }

            int remainingTicks = Mathf.Max(0, defenseEndTick - Find.TickManager.TicksGame);
            float remainingHours = remainingTicks / (GenDate.TicksPerDay / 24f);
            return "MX_Mingyuan_MarkerInspect".Translate(wavesSpawned, remainingHours.ToString("0.0")).ToString();
        }

        internal bool DebugOfferNow(Map map, out string reason)
        {
            if (stage != MingyuanWhiteFlameStage.Waiting)
            {
                reason = "quest component is already active (stage=" + stage + ")";
                return false;
            }

            if (map == null || !map.IsPlayerHome)
            {
                reason = "no player home map";
                return false;
            }

            if (MingyuanWhiteFlameUtility.MingyuanExistsAnywhere())
            {
                reason = "Mingyuan already exists";
                return false;
            }

            targetMap = map;
            bool offered = TryOfferQuest(map);
            reason = offered ? null : "quest generation failed";
            return offered;
        }

        internal bool DebugStartOmenNow(Map map, out string reason)
        {
            if (stage != MingyuanWhiteFlameStage.Waiting)
            {
                reason = "quest component is already active (stage=" + stage + ")";
                return false;
            }

            if (map == null || !map.IsPlayerHome)
            {
                reason = "no player home map";
                return false;
            }

            if (MingyuanWhiteFlameUtility.MingyuanExistsAnywhere())
            {
                reason = "Mingyuan already exists";
                return false;
            }

            int currentTick = Find.TickManager.TicksGame;
            targetMap = map;
            omenStartedTick = currentTick;
            heatOmenLetterSent = false;
            stage = MingyuanWhiteFlameStage.Omen;
            nextProcessTick = currentTick;
            MingyuanWhiteFlameVfx.PlayOmen(map, map.Center, intense: true);
            Find.LetterStack.ReceiveLetter(
                "MX_Mingyuan_OmenAshLabel".Translate(),
                "MX_Mingyuan_OmenAshText".Translate(),
                LetterDefOf.NeutralEvent,
                map.Parent);
            reason = null;
            return true;
        }

        private void ProcessWaiting(int currentTick)
        {
            nextProcessTick = currentTick + WaitingCheckInterval;
            if (currentTick < nextOfferTick
                || currentTick < MinimumDaysPassed * GenDate.TicksPerDay
                || MingyuanWhiteFlameUtility.MingyuanExistsAnywhere())
            {
                return;
            }

            Quest existingQuest = MingyuanWhiteFlameUtility.FindBlockingQuest();
            if (existingQuest != null)
            {
                activeQuest = existingQuest;
                targetMap = Find.AnyPlayerHomeMap;
                stage = MingyuanWhiteFlameStage.Offered;
                if (existingQuest.State == QuestState.Ongoing)
                {
                    BeginDefense(existingQuest, targetMap);
                }
                return;
            }

            Map map = FindBestEligibleMap();
            if (map == null)
            {
                return;
            }

            targetMap = map;
            omenStartedTick = currentTick;
            heatOmenLetterSent = false;
            stage = MingyuanWhiteFlameStage.Omen;
            nextProcessTick = currentTick + OmenVisualInterval;
            Find.LetterStack.ReceiveLetter(
                "MX_Mingyuan_OmenAshLabel".Translate(),
                "MX_Mingyuan_OmenAshText".Translate(),
                LetterDefOf.NeutralEvent,
                map.Parent);
        }

        private void ProcessOmen(int currentTick)
        {
            nextProcessTick = currentTick + OmenVisualInterval;
            if (targetMap == null || Current.Game?.Maps?.Contains(targetMap) != true || !targetMap.IsPlayerHome)
            {
                stage = MingyuanWhiteFlameStage.Waiting;
                targetMap = null;
                nextOfferTick = currentTick + FailureRetryTicks;
                return;
            }

            SpawnOmenVisuals(currentTick);
            int elapsed = currentTick - omenStartedTick;
            if (!heatOmenLetterSent && elapsed >= OmenHeatLetterTick)
            {
                heatOmenLetterSent = true;
                IntVec3 center = targetMap.mapPawns.FreeColonistsSpawned.Count > 0
                    ? targetMap.mapPawns.FreeColonistsSpawned.RandomElement().Position
                    : targetMap.Center;
                MingyuanWhiteFlameVfx.PlayOmen(targetMap, center, intense: true);
                Find.LetterStack.ReceiveLetter(
                    "MX_Mingyuan_OmenHeatLabel".Translate(),
                    "MX_Mingyuan_OmenHeatText".Translate(),
                    LetterDefOf.NeutralEvent,
                    targetMap.Parent);
            }

            if (elapsed < OmenDurationTicks)
            {
                return;
            }

            targetMap.weatherManager?.eventHandler?.AddEvent(new WeatherEvent_LightningFlash(targetMap));
            if (!TryOfferQuest(targetMap))
            {
                stage = MingyuanWhiteFlameStage.Waiting;
                nextOfferTick = currentTick + GenDate.TicksPerDay;
                nextProcessTick = nextOfferTick;
            }
        }

        private void ProcessOffered(int currentTick)
        {
            nextProcessTick = currentTick + WaitingCheckInterval;
            if (activeQuest != null && activeQuest.State == QuestState.Ongoing)
            {
                BeginDefense(activeQuest, targetMap);
                return;
            }

            if (activeQuest == null
                || (activeQuest.State != QuestState.NotYetAccepted && activeQuest.State != QuestState.Ongoing))
            {
                CleanupTemporaryObjects(removeEnemies: true, removePendingPawn: true);
                activeQuest = null;
                stage = MingyuanWhiteFlameStage.Waiting;
                nextOfferTick = currentTick + FailureRetryTicks;
                nextProcessTick = nextOfferTick;
            }
        }

        private void ProcessDefense(int currentTick)
        {
            nextProcessTick = currentTick + ActiveCheckInterval;
            if (activeQuest == null || activeQuest.State != QuestState.Ongoing)
            {
                if (activeQuest != null)
                {
                    NotifyQuestCleanedUp(activeQuest);
                }
                else
                {
                    ResetInterruptedQuest(currentTick);
                }
                return;
            }

            if (targetMap == null || Current.Game?.Maps?.Contains(targetMap) != true)
            {
                FailDefense("MX_Mingyuan_MapLostText".Translate().ToString());
                return;
            }

            bool markerStillIncoming = incomingFlame != null && !incomingFlame.Destroyed;
            if ((marker == null || marker.Destroyed) && !markerStillIncoming)
            {
                FailDefense("MX_Mingyuan_FlameDestroyedText".Translate().ToString());
                return;
            }

            if (marker != null && marker.Spawned)
            {
                incomingFlame = null;
                if (markerLandedTick <= 0)
                {
                    NotifyMarkerLanded(marker);
                }

                if (wavesWarned <= wavesSpawned
                    && wavesSpawned < WavePointFactors.Length
                    && currentTick >= nextWaveTick - WaveWarningLeadTicks)
                {
                    wavesWarned = wavesSpawned + 1;
                    MingyuanWhiteFlameVfx.PlayWaveWarning(marker, wavesSpawned);
                    Find.LetterStack.ReceiveLetter(
                        "MX_Mingyuan_WaveWarningLabel".Translate(wavesSpawned + 1),
                        "MX_Mingyuan_WaveWarningText".Translate(wavesSpawned + 1, WaveWarningLeadTicks / 60),
                        LetterDefOf.ThreatSmall,
                        marker);
                }

                if (wavesSpawned < WavePointFactors.Length && currentTick >= nextWaveTick)
                {
                    if (SpawnWave(wavesSpawned))
                    {
                        wavesSpawned++;
                        nextWaveTick = defenseStartedTick + FirstWaveDelayTicks + wavesSpawned * WaveSpacingTicks;
                    }
                    else
                    {
                        nextWaveTick = currentTick + ActiveCheckInterval;
                    }
                }
            }

            if (wavesSpawned >= WavePointFactors.Length
                && currentTick >= defenseEndTick
                && !HasActiveWaveEnemies())
            {
                BeginReformation();
            }
        }

        private void ProcessReformation(int currentTick)
        {
            nextProcessTick = currentTick + 30;
            if (activeQuest == null || activeQuest.State != QuestState.Ongoing)
            {
                if (activeQuest != null)
                {
                    NotifyQuestCleanedUp(activeQuest);
                }
                else
                {
                    ResetInterruptedQuest(currentTick);
                }
                return;
            }

            if (targetMap == null
                || Current.Game?.Maps?.Contains(targetMap) != true
                || marker == null
                || marker.Destroyed
                || !marker.Spawned)
            {
                FailDefense("MX_Mingyuan_MapLostText".Translate().ToString());
                return;
            }

            int elapsed = currentTick - reformationStartedTick;
            int targetPhase = elapsed >= 600 ? 3 : elapsed >= 390 ? 2 : elapsed >= 180 ? 1 : 0;
            while (reformationPhase <= targetPhase)
            {
                MingyuanWhiteFlameVfx.PlayReformationPhase(marker, reformationPhase);
                reformationPhase++;
            }

            if (currentTick >= reformationEndTick)
            {
                CompleteReformation();
            }
        }

        private void ProcessAwaitingDecision(int currentTick)
        {
            nextProcessTick = currentTick + DecisionCheckInterval;
            if (activeQuest == null || activeQuest.State != QuestState.Ongoing)
            {
                if (activeQuest != null)
                {
                    NotifyQuestCleanedUp(activeQuest);
                }
                else
                {
                    ResetInterruptedQuest(currentTick);
                }
                return;
            }

            if (pendingMingyuan == null || pendingMingyuan.Destroyed || pendingMingyuan.Dead)
            {
                RemoveDecisionLetters(pendingMingyuan);
                DiscardPendingMingyuan();
                if (MingyuanWhiteFlameUtility.MingyuanExistsAnywhere())
                {
                    ConcludeBecauseMingyuanExists();
                    return;
                }

                pendingMingyuan = MingyuanWhiteFlameUtility.GenerateMingyuanPawn();
                if (pendingMingyuan == null)
                {
                    ScheduleRetry(FailureRetryTicks, sendMessage: false);
                    EndActiveQuest(QuestEndOutcome.Fail, sendLetter: false);
                    return;
                }
            }

            if (pendingMingyuan.Faction == Faction.OfPlayer)
            {
                if (!MingyuanWhiteFlameUtility.JoinPlayerFaction(pendingMingyuan))
                {
                    return;
                }

                NeiyuSpecialPawnIntegration.TryRegister(pendingMingyuan);
                pendingMingyuan = null;
                stage = MingyuanWhiteFlameStage.Completed;
                EndActiveQuest(QuestEndOutcome.Success, sendLetter: false);
                return;
            }

            EnsurePendingMingyuanManifested();

            if (!HasActiveDecisionLetter())
            {
                SendDecisionLetter();
            }
        }

        private Map FindBestEligibleMap()
        {
            Map bestMap = null;
            float bestWealth = float.MinValue;
            List<Map> maps = Current.Game?.Maps;
            if (maps == null)
            {
                return null;
            }

            for (int index = 0; index < maps.Count; index++)
            {
                Map map = maps[index];
                if (map == null || !map.IsPlayerHome || map.mapPawns.FreeColonistsCount <= 0)
                {
                    continue;
                }

                float wealth = map.wealthWatcher?.WealthTotal ?? 0f;
                if (wealth >= MinimumColonyWealth && wealth > bestWealth)
                {
                    bestWealth = wealth;
                    bestMap = map;
                }
            }

            return bestMap;
        }

        private bool TryOfferQuest(Map map)
        {
            if (map == null || MingyuanWhiteFlameUtility.MingyuanExistsAnywhere())
            {
                return false;
            }

            QuestScriptDef questDef = DefDatabase<QuestScriptDef>.GetNamedSilentFail(MingyuanWhiteFlameUtility.QuestDefName);
            if (questDef == null)
            {
                Log.Error("[MiliraXian.Characters.Mingyuan] Missing QuestScriptDef: " + MingyuanWhiteFlameUtility.QuestDefName);
                return false;
            }

            Slate slate = new Slate();
            slate.Set("points", StorytellerUtility.DefaultThreatPointsNow(map));
            slate.Set("map", map);
            Quest quest = QuestUtility.GenerateQuestAndMakeAvailable(questDef, slate);
            if (quest == null)
            {
                return false;
            }

            quest.description = MingyuanWhiteFlameUtility.BuildQuestDescription();
            activeQuest = quest;
            targetMap = map;
            stage = MingyuanWhiteFlameStage.Offered;
            nextProcessTick = Find.TickManager.TicksGame + WaitingCheckInterval;
            if (!quest.hidden && questDef.sendAvailableLetter)
            {
                QuestUtility.SendLetterQuestAvailable(quest);
            }

            return true;
        }

        private void SpawnOmenVisuals(int currentTick)
        {
            List<Pawn> colonists = targetMap.mapPawns.FreeColonistsSpawned;
            IntVec3 center = colonists.Count > 0 ? colonists.RandomElement().Position : targetMap.Center;
            MingyuanWhiteFlameVfx.PlayOmen(targetMap, center, intense: false);

            if (currentTick % 900 < OmenVisualInterval)
            {
                IntVec3 heatCell = CellFinder.RandomClosewalkCellNear(center, targetMap, 10);
                FleckMaker.ThrowHeatGlow(heatCell, targetMap, 1.2f);
            }
        }

        private bool SpawnWave(int waveIndex)
        {
            Faction mechanoids = Faction.OfMechanoids;
            if (mechanoids == null || marker == null || !marker.Spawned)
            {
                return false;
            }

            float minimumPoints = Mathf.Max(35f,
                mechanoids.def.MinPointsToGeneratePawnGroup(PawnGroupKindDefOf.Combat) * 1.05f);
            float points = Mathf.Max(minimumPoints,
                StorytellerUtility.DefaultThreatPointsNow(targetMap) * WavePointFactors[waveIndex]);
            List<Pawn> pawns = PawnGroupMakerUtility.GeneratePawns(new PawnGroupMakerParms
            {
                groupKind = PawnGroupKindDefOf.Combat,
                points = points,
                faction = mechanoids,
                generateFightersOnly = true,
                dontUseSingleUseRocketLaunchers = true,
                raidStrategy = RaidStrategyDefOf.ImmediateAttack
            }, warnOnZeroResults: false).ToList();

            if (pawns.Count == 0)
            {
                Pawn fallback = GenerateSpecificMech(FallbackMechKindDefName, mechanoids);
                if (fallback != null)
                {
                    pawns.Add(fallback);
                }
            }

            if (waveIndex == WavePointFactors.Length - 1)
            {
                Pawn elite = GenerateSpecificMech(EliteMechKindDefName, mechanoids);
                if (elite != null)
                {
                    pawns.Add(elite);
                }
            }

            if (pawns.Count == 0 || !ArriveAtMapEdge(pawns, mechanoids))
            {
                for (int index = 0; index < pawns.Count; index++)
                {
                    Pawn pawn = pawns[index];
                    if (pawn != null && !pawn.Spawned && !pawn.Destroyed)
                    {
                        pawn.Destroy(DestroyMode.Vanish);
                    }
                }

                Log.Warning("[MiliraXian.Characters.Mingyuan] White flame wave " + (waveIndex + 1) + " could not arrive.");
                return false;
            }

            for (int index = pawns.Count - 1; index >= 0; index--)
            {
                Pawn pawn = pawns[index];
                if (pawn != null && !pawn.Spawned && !pawn.Destroyed)
                {
                    pawn.Destroy(DestroyMode.Vanish);
                }

                if (pawn == null || !pawn.Spawned || pawn.Destroyed)
                {
                    pawns.RemoveAt(index);
                }
            }
            if (pawns.Count == 0)
            {
                return false;
            }

            LordMaker.MakeNewLord(
                mechanoids,
                new LordJob_AssaultThings(mechanoids, new List<Thing> { marker }),
                targetMap,
                pawns);
            wavePawns.AddRange(pawns);
            MingyuanWhiteFlameVfx.PlayWaveArrival(pawns, waveIndex);

            string textKey = waveIndex == WavePointFactors.Length - 1
                ? "MX_Mingyuan_WaveFinalText"
                : "MX_Mingyuan_WaveText";
            Find.LetterStack.ReceiveLetter(
                "MX_Mingyuan_WaveLabel".Translate(waveIndex + 1),
                textKey.Translate(waveIndex + 1, WavePointFactors[waveIndex].ToString("0.0")),
                LetterDefOf.ThreatBig,
                pawns.Cast<Thing>().ToList());
            return true;
        }

        private bool ArriveAtMapEdge(List<Pawn> pawns, Faction faction)
        {
            IncidentParms parms = new IncidentParms
            {
                target = targetMap,
                faction = faction,
                points = StorytellerUtility.DefaultThreatPointsNow(targetMap),
                raidArrivalMode = PawnsArrivalModeDefOf.EdgeWalkIn,
                raidStrategy = RaidStrategyDefOf.ImmediateAttack,
                sendLetter = false
            };

            if (parms.raidArrivalMode.Worker.TryResolveRaidSpawnCenter(parms))
            {
                parms.raidArrivalMode.Worker.Arrive(pawns, parms);
                return pawns.Any(pawn => pawn?.Spawned == true);
            }

            bool spawnedAny = false;
            for (int index = 0; index < pawns.Count; index++)
            {
                Pawn pawn = pawns[index];
                if (pawn == null)
                {
                    continue;
                }

                if (CellFinder.TryFindRandomEdgeCellWith(
                    cell => cell.Standable(targetMap) && targetMap.reachability.CanReach(cell, marker.Position, PathEndMode.Touch, TraverseParms.For(TraverseMode.PassDoors)),
                    targetMap,
                    CellFinder.EdgeRoadChance_Hostile,
                    out IntVec3 spawnCell))
                {
                    GenSpawn.Spawn(pawn, spawnCell, targetMap);
                    spawnedAny = true;
                }
            }

            return spawnedAny;
        }

        private Pawn GenerateSpecificMech(string pawnKindDefName, Faction faction)
        {
            PawnKindDef kindDef = DefDatabase<PawnKindDef>.GetNamedSilentFail(pawnKindDefName);
            if (kindDef == null)
            {
                return null;
            }

            PawnGenerationRequest request = new PawnGenerationRequest(
                kindDef,
                faction,
                PawnGenerationContext.NonPlayer,
                targetMap?.Tile ?? -1,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: true,
                colonistRelationChanceFactor: 0f,
                forceAddFreeWarmLayerIfNeeded: false,
                allowGay: false,
                allowPregnant: false,
                allowFood: false,
                allowAddictions: false,
                inhabitant: false,
                certainlyBeenInCryptosleep: false,
                forceRedressWorldPawnIfFormerColonist: false,
                worldPawnFactionDoesntMatter: false);
            return PawnGenerator.GeneratePawn(request);
        }

        private bool HasActiveWaveEnemies()
        {
            for (int index = 0; index < wavePawns.Count; index++)
            {
                Pawn pawn = wavePawns[index];
                if (pawn != null
                    && !pawn.Dead
                    && !pawn.Destroyed
                    && pawn.Spawned
                    && pawn.MapHeld == targetMap
                    && pawn.Faction != null
                    && pawn.Faction.HostileTo(Faction.OfPlayer))
                {
                    return true;
                }
            }

            return false;
        }

        private void BeginReformation()
        {
            wavePawns.Clear();
            int currentTick = Find.TickManager.TicksGame;
            reformationStartedTick = currentTick;
            reformationEndTick = currentTick + ReformationDurationTicks;
            reformationPhase = 0;
            stage = MingyuanWhiteFlameStage.Reforming;
            nextProcessTick = currentTick + 30;
            MingyuanWhiteFlameVfx.PlayReformationPhase(marker, 0);
            reformationPhase = 1;
            Find.LetterStack.ReceiveLetter(
                "MX_Mingyuan_ReformationLabel".Translate(),
                "MX_Mingyuan_ReformationText".Translate(),
                LetterDefOf.PositiveEvent,
                marker);
        }

        private void CompleteReformation()
        {
            IntVec3 emergenceCell = marker != null && marker.PositionHeld.IsValid
                ? marker.PositionHeld
                : targetMap.Center;
            pendingMingyuan = MingyuanWhiteFlameUtility.GenerateMingyuanPawn();
            if (pendingMingyuan == null)
            {
                ScheduleRetry(FailureRetryTicks, sendMessage: false);
                EndActiveQuest(QuestEndOutcome.Fail, sendLetter: false);
                return;
            }

            if (pendingMingyuan.IsWorldPawn())
            {
                Find.WorldPawns.RemovePawn(pendingMingyuan);
            }

            DestroyIncomingAndMarker();
            IntVec3 spawnCell = MingyuanUtility.FindStandableCellNear(emergenceCell, targetMap, 8);
            if (!pendingMingyuan.Spawned)
            {
                GenSpawn.Spawn(pendingMingyuan, spawnCell, targetMap, Rot4.South);
            }

            pendingMingyuan.jobs?.StopAll();
            if (pendingMingyuan.Faction != null && pendingMingyuan.GetLord() == null)
            {
                LordMaker.MakeNewLord(
                    pendingMingyuan.Faction,
                    new LordJob_DefendPoint(spawnCell, 1.5f, 5f, isCaravanSendable: false, addFleeToil: false),
                    targetMap,
                    Gen.YieldSingle(pendingMingyuan));
            }

            stage = MingyuanWhiteFlameStage.AwaitingDecision;
            nextProcessTick = Find.TickManager.TicksGame + DecisionCheckInterval;
            MingyuanWhiteFlameVfx.PlayManifestation(pendingMingyuan);
            SendDecisionLetter(spawnCell);
        }

        private void EnsurePendingMingyuanManifested()
        {
            if (pendingMingyuan == null || pendingMingyuan.Destroyed || pendingMingyuan.Dead)
            {
                return;
            }

            Map spawnMap = MingyuanWhiteFlameUtility.ResolveSpawnMap(targetMap);
            if (spawnMap == null)
            {
                return;
            }

            targetMap = spawnMap;
            if (pendingMingyuan.IsWorldPawn())
            {
                Find.WorldPawns.RemovePawn(pendingMingyuan);
            }

            if (!pendingMingyuan.Spawned)
            {
                IntVec3 spawnCell = MingyuanUtility.FindStandableCellNear(spawnMap.Center, spawnMap, 20);
                GenSpawn.Spawn(pendingMingyuan, spawnCell, spawnMap, Rot4.South);
            }

            if (pendingMingyuan.Faction != null && pendingMingyuan.GetLord() == null)
            {
                LordMaker.MakeNewLord(
                    pendingMingyuan.Faction,
                    new LordJob_DefendPoint(pendingMingyuan.Position, 1.5f, 5f, isCaravanSendable: false, addFleeToil: false),
                    pendingMingyuan.Map,
                    Gen.YieldSingle(pendingMingyuan));
            }
        }

        private void SendDecisionLetter(IntVec3? emergenceCell = null)
        {
            Map decisionMap = MingyuanWhiteFlameUtility.ResolveSpawnMap(targetMap);
            if (pendingMingyuan == null || decisionMap == null)
            {
                return;
            }

            targetMap = decisionMap;

            LetterDef letterDef = DefDatabase<LetterDef>.GetNamedSilentFail(MingyuanWhiteFlameUtility.ChoiceLetterDefName);
            if (letterDef == null)
            {
                Log.Error("[MiliraXian.Characters.Mingyuan] Missing LetterDef: " + MingyuanWhiteFlameUtility.ChoiceLetterDefName);
                return;
            }

            IntVec3 cell = pendingMingyuan.Spawned && pendingMingyuan.Map == decisionMap
                ? pendingMingyuan.Position
                : emergenceCell.HasValue && emergenceCell.Value.InBounds(decisionMap)
                    ? emergenceCell.Value
                    : MingyuanUtility.FindStandableCellNear(decisionMap.Center, decisionMap, 20);
            ChoiceLetter_MingyuanArrival letter = (ChoiceLetter_MingyuanArrival)LetterMaker.MakeLetter(
                "MX_Mingyuan_ArrivalLabel".Translate(),
                MingyuanWhiteFlameUtility.BuildArrivalDialogue(),
                letterDef,
                new TargetInfo(cell, decisionMap),
                null,
                activeQuest);
            letter.mingyuan = pendingMingyuan;
            letter.targetMap = decisionMap;
            letter.targetCell = cell;
            Find.LetterStack.ReceiveLetter(letter);
        }

        private bool HasActiveDecisionLetter()
        {
            return FindDecisionLetter(pendingMingyuan) != null;
        }

        private static ChoiceLetter_MingyuanArrival FindDecisionLetter(Pawn pawn)
        {
            List<Letter> letters = Find.LetterStack?.LettersListForReading;
            if (letters == null)
            {
                return null;
            }

            for (int index = 0; index < letters.Count; index++)
            {
                if (letters[index] is ChoiceLetter_MingyuanArrival choice && choice.mingyuan == pawn)
                {
                    return choice;
                }
            }

            return null;
        }

        private void FailDefense(string reason)
        {
            if (marker != null && !marker.Destroyed && marker.MapHeld != null && marker.PositionHeld.IsValid)
            {
                MingyuanWhiteFlameVfx.PlayFailure(marker.MapHeld, marker.PositionHeld);
            }

            CleanupTemporaryObjects(removeEnemies: true, removePendingPawn: true);
            stage = MingyuanWhiteFlameStage.Waiting;
            nextOfferTick = Find.TickManager.TicksGame + FailureRetryTicks;
            nextProcessTick = nextOfferTick;
            EndActiveQuest(QuestEndOutcome.Fail, sendLetter: false);
            Find.LetterStack.ReceiveLetter(
                "MX_Mingyuan_FailedLabel".Translate(),
                reason + "\n\n" + "MX_Mingyuan_RetryTwentyDays".Translate(),
                LetterDefOf.NegativeEvent,
                targetMap?.Parent);
        }

        private void ConcludeBecauseMingyuanExists()
        {
            bool shouldNotify = stage == MingyuanWhiteFlameStage.Offered
                                || stage == MingyuanWhiteFlameStage.Defending
                                || stage == MingyuanWhiteFlameStage.Reforming
                                || stage == MingyuanWhiteFlameStage.AwaitingDecision;
            CleanupTemporaryObjects(removeEnemies: true, removePendingPawn: true);
            stage = MingyuanWhiteFlameStage.Completed;
            EndActiveQuest(QuestEndOutcome.Unknown, sendLetter: false);
            if (shouldNotify)
            {
                Find.LetterStack.ReceiveLetter(
                    "MX_Mingyuan_QuestConcludedLabel".Translate(),
                    "MX_Mingyuan_QuestConcludedText".Translate(),
                    LetterDefOf.NeutralEvent);
            }
        }

        private void ScheduleRetry(int delayTicks, bool sendMessage)
        {
            CleanupTemporaryObjects(removeEnemies: true, removePendingPawn: true);
            stage = MingyuanWhiteFlameStage.Waiting;
            nextOfferTick = (Find.TickManager?.TicksGame ?? 0) + delayTicks;
            nextProcessTick = nextOfferTick;
            if (sendMessage)
            {
                Messages.Message("MX_Mingyuan_QuestRetryScheduled".Translate(), MessageTypeDefOf.NeutralEvent, false);
            }
        }

        private void EndActiveQuest(QuestEndOutcome outcome, bool sendLetter)
        {
            Quest quest = activeQuest;
            activeQuest = null;
            if (quest == null || quest.Historical)
            {
                return;
            }

            resolvingQuest = true;
            try
            {
                quest.End(outcome, sendLetter, playSound: sendLetter);
            }
            finally
            {
                resolvingQuest = false;
            }
        }

        private void CleanupTemporaryObjects(bool removeEnemies, bool removePendingPawn)
        {
            DestroyIncomingAndMarker();
            if (removeEnemies)
            {
                for (int index = wavePawns.Count - 1; index >= 0; index--)
                {
                    RemoveTemporaryEnemy(wavePawns[index]);
                }

                wavePawns.Clear();
            }

            if (removePendingPawn)
            {
                RemoveDecisionLetters(pendingMingyuan);
                DiscardPendingMingyuan();
            }

            wavesSpawned = 0;
            wavesWarned = 0;
            markerLandedTick = 0;
            defenseStartedTick = 0;
            defenseEndTick = 0;
            nextWaveTick = 0;
            reformationStartedTick = 0;
            reformationEndTick = 0;
            reformationPhase = 0;
        }

        private void DestroyIncomingAndMarker()
        {
            if (incomingFlame != null && !incomingFlame.Destroyed)
            {
                incomingFlame.Destroy(DestroyMode.Vanish);
            }

            incomingFlame = null;
            if (marker != null && !marker.Destroyed)
            {
                marker.Destroy(DestroyMode.Vanish);
            }

            marker = null;
        }

        private static void RemoveDecisionLetters(Pawn pawn)
        {
            List<Letter> letters = Find.LetterStack?.LettersListForReading;
            if (letters == null)
            {
                return;
            }

            for (int index = letters.Count - 1; index >= 0; index--)
            {
                if (letters[index] is ChoiceLetter_MingyuanArrival choice
                    && (pawn == null || choice.mingyuan == pawn))
                {
                    Find.LetterStack.RemoveLetter(choice);
                }
            }
        }

        private void DiscardPendingMingyuan()
        {
            Pawn pawn = pendingMingyuan;
            pendingMingyuan = null;
            if (pawn == null || pawn.Destroyed)
            {
                return;
            }

            Corpse corpse = pawn.Corpse;
            if (corpse != null && !corpse.Destroyed)
            {
                corpse.Destroy(DestroyMode.Vanish);
            }
            else if (pawn.Spawned)
            {
                pawn.Destroy(DestroyMode.Vanish);
            }
            else if (pawn.IsWorldPawn())
            {
                Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
            }
            else
            {
                pawn.Discard();
            }
        }

        private void ResetInterruptedQuest(int currentTick)
        {
            CleanupTemporaryObjects(removeEnemies: true, removePendingPawn: true);
            activeQuest = null;
            stage = MingyuanWhiteFlameStage.Waiting;
            nextOfferTick = currentTick + FailureRetryTicks;
            nextProcessTick = nextOfferTick;
        }

        private static void RemoveTemporaryEnemy(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || pawn.Faction == Faction.OfPlayer)
            {
                return;
            }

            Corpse corpse = pawn.Corpse;
            if (corpse != null && !corpse.Destroyed)
            {
                corpse.Destroy(DestroyMode.Vanish);
                return;
            }

            if (pawn.Spawned)
            {
                pawn.Destroy(DestroyMode.Vanish);
            }
            else if (pawn.IsWorldPawn())
            {
                Find.WorldPawns.RemoveAndDiscardPawnViaGC(pawn);
            }
            else
            {
                pawn.Discard();
            }
        }
    }

    internal static class MingyuanWhiteFlameDebugActions
    {
        [DebugAction("Milira Xian - Mingyuan", "Start full White Flame Omen sequence", false, false, false, false, false, 0, false,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void StartWhiteFlameOmenSequence()
        {
            GameComponent_MingyuanWhiteFlameQuest component =
                Current.Game?.GetComponent<GameComponent_MingyuanWhiteFlameQuest>();
            if (component == null)
            {
                Messages.Message("Mingyuan White Flame component is unavailable.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (component.DebugStartOmenNow(Find.CurrentMap, out string reason))
            {
                Messages.Message("Mingyuan White Flame omen sequence started.", MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                Messages.Message("Could not start Mingyuan White Flame omen: " + reason, MessageTypeDefOf.RejectInput, false);
            }
        }

        [DebugAction("Milira Xian - Mingyuan", "Offer White Flame Omen quest now", false, false, false, false, false, 0, false,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void OfferWhiteFlameQuestNow()
        {
            GameComponent_MingyuanWhiteFlameQuest component =
                Current.Game?.GetComponent<GameComponent_MingyuanWhiteFlameQuest>();
            if (component == null)
            {
                Messages.Message("Mingyuan White Flame component is unavailable.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (component.DebugOfferNow(Find.CurrentMap, out string reason))
            {
                Messages.Message("Mingyuan White Flame quest offered.", MessageTypeDefOf.PositiveEvent, false);
            }
            else
            {
                Messages.Message("Could not offer Mingyuan White Flame quest: " + reason, MessageTypeDefOf.RejectInput, false);
            }
        }
    }
}

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using LudeonTK;
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
        public static HediffDef MXZL_ZhaoliDeathFieldActive;
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
        public const string MurmurQuestDefName = "MXZL_ZhaoliMurmurQuest";
        public const string ReturnQuestDefName = "MXZL_ZhaoliReturnQuest";
        public const int MurmurTriggerTicks = 15 * GenDate.TicksPerDay;
        public const int MurmurRequiredPawnDeaths = 16;
        public const int HideoutLifetimeTicks = 3 * GenDate.TicksPerDay;
        public const int NextDayDelayTicks = GenDate.TicksPerDay;
        public const int RaidStartingShieldLayers = 500;
        public const float RaidStartingKarma = 25f;
        public const float DeathFieldRaidBonusKarma = 20f;
        public const int RaidTransitionBaseShieldLayers = 120;
        public const int RaidTransitionShieldLayersPerPhase = 40;
        public const float HatredPerTenDamage = 1f;
        public const int HatredPerHit = 3;
        public const int TargetSwitchGraceTicks = 900;
        public const int TargetLockTicks = 1800;
        public const int RaidSkillWeaveTicks = 300;
        public const int RaidOpeningAttackTicks = 240;
        public const int DeathFieldDenseLivingThreshold = 5;
        public const int DeathFieldSingleHumanlikeDelayTicks = 360;
        public const int DeathFieldRecastGuardTicks = 900;
        public const float DeathFieldEvaluationRadius = 9f;
        public const int AiTickInterval = 15;
        private const string WrongNeiyuInnerClothingDefName = "MiliraXian_NeiyuInner";
        private const string WrongNeiyuEarringDefName = "MX_Apparel_EarringsZhenzhu";
        private const string DefaultClothingDefName = "MX_ZhaoliNormal";
        private const string DefaultHoodDefName = "MX_ZhaoliHood";
        private const string HostileAncientsFactionDefName = "AncientsHostile";
        private static readonly HashSet<int> PendingLoadoutStabilizationPawnIds = new HashSet<int>();

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
                if (quest?.root?.defName == questDefName && QuestBlocksNewOffer(quest))
                {
                    return true;
                }
            }

            return false;
        }

        public static Quest FindActiveQuest(string questDefName)
        {
            if (Find.QuestManager == null)
            {
                return null;
            }

            List<Quest> quests = Find.QuestManager.QuestsListForReading;
            for (int index = 0; index < quests.Count; index++)
            {
                Quest quest = quests[index];
                if (quest?.root?.defName == questDefName && QuestBlocksNewOffer(quest))
                {
                    return quest;
                }
            }

            return null;
        }

        public static bool QuestBlocksNewOffer(Quest quest)
        {
            if (quest == null)
            {
                return false;
            }

            QuestState state = quest.State;
            return state == QuestState.NotYetAccepted || state == QuestState.Ongoing;
        }

        public static bool QuestWasNeverAccepted(string questDefName)
        {
            if (Find.QuestManager == null)
            {
                return false;
            }

            bool foundNotYetAccepted = false;
            bool foundNeverAcceptedEnded = false;
            bool foundAcceptedEnded = false;
            List<Quest> quests = Find.QuestManager.QuestsListForReading;
            for (int index = 0; index < quests.Count; index++)
            {
                Quest quest = quests[index];
                if (quest?.root?.defName != questDefName)
                {
                    continue;
                }

                QuestState state = quest.State;
                if (state == QuestState.Ongoing)
                {
                    return false;
                }

                if (state == QuestState.NotYetAccepted)
                {
                    foundNotYetAccepted = true;
                    continue;
                }

                if (state == QuestState.EndedOfferExpired || state == QuestState.EndedInvalid)
                {
                    foundNeverAcceptedEnded = true;
                    continue;
                }

                foundAcceptedEnded = true;
            }

            if (foundNotYetAccepted)
            {
                return true;
            }

            if (foundAcceptedEnded)
            {
                return false;
            }

            if (foundNeverAcceptedEnded)
            {
                return true;
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

        public static Faction ResolveAncientsFaction()
        {
            FactionManager factionManager = Find.FactionManager;
            if (factionManager == null)
            {
                return null;
            }

            return factionManager.OfAncients ?? factionManager.FirstFactionOfDef(FactionDefOf.Ancients);
        }

        public static Faction ResolveHostileAncientsFaction()
        {
            FactionManager factionManager = Find.FactionManager;
            if (factionManager == null)
            {
                return null;
            }

            Faction hostileAncients = factionManager.OfAncientsHostile;
            if (hostileAncients != null)
            {
                return hostileAncients;
            }

            FactionDef hostileAncientsDef = DefDatabase<FactionDef>.GetNamedSilentFail(HostileAncientsFactionDefName);
            return hostileAncientsDef != null ? factionManager.FirstFactionOfDef(hostileAncientsDef) : null;
        }

        public static Faction ResolveFriendlyFaction()
        {
            Faction ancientsFaction = ResolveAncientsFaction();
            if (ancientsFaction != null && !ancientsFaction.IsPlayer)
            {
                return ancientsFaction;
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
            Faction hostileAncients = ResolveHostileAncientsFaction();
            if (hostileAncients != null && hostileAncients.HostileTo(Faction.OfPlayer))
            {
                return hostileAncients;
            }

            return Find.FactionManager.RandomEnemyFaction(allowHidden: false, allowDefeated: false, allowNonHumanlike: false)
                   ?? Find.FactionManager.OfPirates
                   ?? hostileAncients
                   ?? Find.FactionManager.OfMechanoids;
        }

        public static Pawn GenerateZhaoliPawn(Faction faction)
        {
            faction = faction ?? ResolveAncientsFaction();
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
            EnsureDefaultLoadout(pawn);
            MarkForLoadoutStabilization(pawn);
            return pawn;
        }

        public static void EnsureDefaultLoadout(Pawn pawn)
        {
            if (!ZhaoliKarmaUtility.IsZhaoli(pawn))
            {
                return;
            }

            EnsureZhaoliBodyType(pawn);
            EnsurePrimaryWeapon(pawn);
            RemoveWrongDefaultApparel(pawn);
            EnsureDefaultClothing(pawn);
            EnsureDefaultHood(pawn);
        }

        public static void EnsureZhaoliBodyType(Pawn pawn)
        {
            if (!ZhaoliKarmaUtility.IsZhaoli(pawn) || pawn?.story == null)
            {
                return;
            }

            if (pawn.story.bodyType == BodyTypeDefOf.Female)
            {
                return;
            }

            pawn.story.bodyType = BodyTypeDefOf.Female;
            pawn.Drawer?.renderer?.SetAllGraphicsDirty();
        }

        public static void MarkForLoadoutStabilization(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            PendingLoadoutStabilizationPawnIds.Add(pawn.thingIDNumber);
        }

        public static bool ShouldFinalizeLoadout(Pawn pawn)
        {
            return pawn != null && PendingLoadoutStabilizationPawnIds.Contains(pawn.thingIDNumber);
        }

        public static void ClearLoadoutStabilization(Pawn pawn)
        {
            if (pawn == null)
            {
                return;
            }

            PendingLoadoutStabilizationPawnIds.Remove(pawn.thingIDNumber);
        }

        public static Pawn GenerateLinkedRelativePawn(Pawn colonist, Map map)
        {
            if (colonist == null)
            {
                return null;
            }

            PawnKindDef kindDef = colonist.kindDef ?? colonist.Faction?.def?.basicMemberKind ?? Faction.OfPlayer?.def?.basicMemberKind;
            Faction faction = ResolveFriendlyFaction() ?? colonist.Faction ?? Faction.OfPlayer;
            if (kindDef == null || faction == null)
            {
                return null;
            }

            PawnGenerationRequest request = new PawnGenerationRequest(
                kindDef,
                faction,
                PawnGenerationContext.NonPlayer,
                map?.Tile ?? -1,
                forceGenerateNewPawn: true,
                allowDead: false,
                allowDowned: false,
                canGeneratePawnRelations: false,
                mustBeCapableOfViolence: false,
                colonistRelationChanceFactor: 0f,
                forceAddFreeWarmLayerIfNeeded: false,
                allowGay: true,
                allowPregnant: true,
                allowFood: true,
                allowAddictions: true,
                inhabitant: false,
                certainlyBeenInCryptosleep: false,
                forceRedressWorldPawnIfFormerColonist: false,
                worldPawnFactionDoesntMatter: false);

            Pawn relative = PawnGenerator.GeneratePawn(request);
            if (relative == null)
            {
                return null;
            }

            if (!relative.relations.DirectRelationExists(PawnRelationDefOf.Kin, colonist))
            {
                relative.relations.AddDirectRelation(PawnRelationDefOf.Kin, colonist);
            }

            Find.WorldPawns?.PassToWorld(relative, PawnDiscardDecideMode.KeepForever);
            return relative;
        }

        public static void EnsurePrimaryWeapon(Pawn pawn)
        {
            if (!ZhaoliKarmaUtility.IsZhaoli(pawn) || pawn?.equipment == null)
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
                ZhaoliBladeVerbLoadFixUtility.EnsureCleanVerbTracker(pawn.equipment.Primary);
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
            ZhaoliBladeVerbLoadFixUtility.EnsureCleanVerbTracker(weapon);
        }

        public static void EnsureDefaultClothing(Pawn pawn)
        {
            EnsureDefaultApparel(pawn, DefaultClothingDefName, "Default clothing");
        }

        public static void EnsureDefaultHood(Pawn pawn)
        {
            EnsureDefaultApparel(pawn, DefaultHoodDefName, "Default hood");
        }

        private static void RemoveWrongDefaultApparel(Pawn pawn)
        {
            if (!ZhaoliKarmaUtility.IsZhaoli(pawn) || pawn?.apparel == null)
            {
                return;
            }

            RemoveWornApparel(pawn, WrongNeiyuInnerClothingDefName);
            RemoveWornApparel(pawn, WrongNeiyuEarringDefName);
        }

        private static void RemoveWornApparel(Pawn pawn, string defName)
        {
            Apparel wrongApparel = FindWornApparel(pawn, defName);
            if (wrongApparel == null)
            {
                return;
            }

            pawn.apparel.Remove(wrongApparel);
            if (!wrongApparel.Destroyed)
            {
                wrongApparel.Destroy(DestroyMode.Vanish);
            }
        }

        public static bool HasInitialLoadoutEquipped(Pawn pawn)
        {
            return pawn?.equipment?.Primary?.def == MXZL_ZhaoliDefOf.MX_Zhaoli_DuanzhanBlade
                   && HasWornApparel(pawn, DefaultClothingDefName)
                   && HasWornApparel(pawn, DefaultHoodDefName)
                   && !HasWornApparel(pawn, WrongNeiyuEarringDefName);
        }

        private static void EnsureDefaultApparel(Pawn pawn, string defName, string missingLabel)
        {
            if (!ZhaoliKarmaUtility.IsZhaoli(pawn) || pawn?.apparel == null)
            {
                return;
            }

            Apparel existing = FindWornApparel(pawn, defName);
            if (existing != null)
            {
                EnsureForcedApparel(pawn, existing);
                return;
            }

            ThingDef apparelDef = DefDatabase<ThingDef>.GetNamedSilentFail(defName);
            if (apparelDef == null)
            {
                Log.Error("[MiliraXian.Characters.Zhaoli] Missing ThingDef: " + defName);
                return;
            }

            Apparel apparel = FindDroppedApparelOnMap(pawn, apparelDef);
            if (apparel == null)
            {
                apparel = ThingMaker.MakeThing(apparelDef) as Apparel;
            }
            if (apparel == null)
            {
                Log.Error("[MiliraXian.Characters.Zhaoli] " + missingLabel + " is not Apparel: " + defName);
                return;
            }

            PawnGenerator.PostProcessGeneratedGear(apparel, pawn);
            pawn.apparel.Wear(apparel, dropReplacedApparel: true);
            EnsureForcedApparel(pawn, apparel);
        }

        private static Apparel FindWornApparel(Pawn pawn, string defName)
        {
            if (pawn?.apparel == null)
            {
                return null;
            }

            for (int index = 0; index < pawn.apparel.WornApparel.Count; index++)
            {
                Apparel apparel = pawn.apparel.WornApparel[index];
                if (apparel?.def?.defName == defName)
                {
                    return apparel;
                }
            }

            return null;
        }

        private static bool HasWornApparel(Pawn pawn, string defName)
        {
            return FindWornApparel(pawn, defName) != null;
        }

        private static Apparel FindDroppedApparelOnMap(Pawn pawn, ThingDef apparelDef)
        {
            if (pawn?.MapHeld == null)
            {
                return null;
            }

            List<Thing> things = pawn.MapHeld.listerThings.ThingsOfDef(apparelDef);
            for (int index = 0; index < things.Count; index++)
            {
                Apparel apparel = things[index] as Apparel;
                if (apparel != null && apparel.def == apparelDef && apparel.Wearer == null)
                {
                    return apparel;
                }
            }

            return null;
        }

        private static void EnsureForcedApparel(Pawn pawn, Apparel apparel)
        {
            if (pawn?.apparel == null || apparel == null)
            {
                return;
            }

            if (pawn.apparel.IsLocked(apparel))
            {
                pawn.apparel.Unlock(apparel);
            }

            if (pawn.outfits?.forcedHandler != null)
            {
                pawn.outfits.forcedHandler.SetForced(apparel, forced: true);
            }
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

    [HarmonyPatch(typeof(StartingPawnUtility), nameof(StartingPawnUtility.NewGeneratedStartingPawn))]
    internal static class Patch_StartingPawnUtility_NewGeneratedStartingPawn_ZhaoliLoadout
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Pawn __result)
        {
            if (!ZhaoliKarmaUtility.IsZhaoli(__result))
            {
                return;
            }

            ZhaoliScenarioUtility.MarkForLoadoutStabilization(__result);
            ZhaoliScenarioUtility.EnsureDefaultLoadout(__result);
        }
    }

    [HarmonyPatch(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) })]
    internal static class Patch_PawnGenerator_GeneratePawn_ZhaoliLoadout
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(ref Pawn __result)
        {
            if (!ZhaoliKarmaUtility.IsZhaoli(__result))
            {
                return;
            }

            ZhaoliScenarioUtility.EnsureDefaultLoadout(__result);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.SpawnSetup))]
    internal static class Patch_Pawn_SpawnSetup_ZhaoliLoadout
    {
        [HarmonyPriority(Priority.Last)]
        public static void Postfix(Pawn __instance)
        {
            if (!ZhaoliScenarioUtility.ShouldFinalizeLoadout(__instance))
            {
                return;
            }

            ZhaoliScenarioUtility.EnsureDefaultLoadout(__instance);
            Current.Game?.GetComponent<GameComponent_ZhaoliScenario>()?.RegisterPendingLoadout(__instance);
            if (ZhaoliScenarioUtility.HasInitialLoadoutEquipped(__instance))
            {
                ZhaoliScenarioUtility.ClearLoadoutStabilization(__instance);
            }
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
            quest.AddPart(new QuestPart_ZhaoliStartHideoutOnAccept
            {
                inSignal = quest.InitiateSignal,
                mapParent = map.Parent
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

    public class QuestPart_ZhaoliStartHideoutOnAccept : QuestPart
    {
        public string inSignal;
        public MapParent mapParent;
        public Site hideoutSite;

        public override IEnumerable<GlobalTargetInfo> QuestLookTargets
        {
            get
            {
                foreach (GlobalTargetInfo target in base.QuestLookTargets)
                {
                    yield return target;
                }

                if (hideoutSite != null && !hideoutSite.Destroyed)
                {
                    yield return hideoutSite;
                }
                else if (mapParent != null)
                {
                    yield return mapParent;
                }
            }
        }

        public override void Notify_QuestSignalReceived(Signal signal)
        {
            base.Notify_QuestSignalReceived(signal);
            if (signal.tag != inSignal)
            {
                return;
            }

            Map map = ZhaoliScenarioUtility.ResolveBestHomeMap(mapParent?.Map);
            GameComponent_ZhaoliScenario component = Current.Game?.GetComponent<GameComponent_ZhaoliScenario>();
            if (map == null || component == null)
            {
                quest.End(QuestEndOutcome.Fail, sendLetter: false, playSound: false);
                return;
            }

            if (!component.TryStartHideout(map, out hideoutSite))
            {
                Log.Warning("[MiliraXian.Characters.Zhaoli] Failed to start Zhaoli hideout immediately after quest acceptance. The scenario component will retry instead of failing the quest.");
                return;
            }
        }

        public override void ExposeData()
        {
            base.ExposeData();
            Scribe_Values.Look(ref inSignal, "inSignal");
            Scribe_References.Look(ref mapParent, "mapParent");
            Scribe_References.Look(ref hideoutSite, "hideoutSite");
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
                    joinPlayer: true,
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
        private const int LoadoutCheckInterval = 30;
        private const int LoadoutFinalizeDurationTicks = 600;

        private bool scenarioCompleted;
        private bool murmurOffered;
        private bool returnOffered;
        private int qualifyingPawnDeathCount;
        private List<int> qualifyingPawnDeathIds = new List<int>();
        private int scheduledRaidTick = -1;
        private int scheduledReturnTick = -1;
        private Map scenarioHomeMap;
        private Site activeHideoutSite;
        private Pawn currentRaidPawn;
        private int nextScenarioStateCheckTick = -1;
        private bool cachedPlayerHasZhaoli;
        private bool cachedMurmurQuestExists;
        private bool cachedReturnQuestExists;
        private bool cachedMurmurQuestWasNeverAccepted;
        private readonly List<PendingLoadoutFinalize> pendingLoadoutFinalizations = new List<PendingLoadoutFinalize>();

        private class PendingLoadoutFinalize
        {
            public Pawn pawn;
            public int expireTick;
        }

        public GameComponent_ZhaoliScenario(Game game)
        {
        }

        public void RegisterPendingLoadout(Pawn pawn)
        {
            if (!ZhaoliKarmaUtility.IsZhaoli(pawn) || Find.TickManager == null)
            {
                return;
            }

            int expireTick = Find.TickManager.TicksGame + LoadoutFinalizeDurationTicks;
            for (int index = 0; index < pendingLoadoutFinalizations.Count; index++)
            {
                if (pendingLoadoutFinalizations[index].pawn == pawn)
                {
                    pendingLoadoutFinalizations[index].expireTick = expireTick;
                    return;
                }
            }

            pendingLoadoutFinalizations.Add(new PendingLoadoutFinalize
            {
                pawn = pawn,
                expireTick = expireTick
            });
        }

        public bool CanOfferMurmur(Map map)
        {
            return CanOfferMurmur(map, useCachedScenarioState: false);
        }

        private bool CanOfferMurmur(Map map, bool useCachedScenarioState)
        {
            if (scenarioCompleted || murmurOffered || returnOffered || activeHideoutSite != null || currentRaidPawn != null || scheduledRaidTick >= 0 || scheduledReturnTick >= 0)
            {
                return false;
            }

            if (useCachedScenarioState ? cachedPlayerHasZhaoli : ZhaoliScenarioUtility.PlayerHasZhaoli())
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

            return !(useCachedScenarioState ? cachedMurmurQuestExists : ZhaoliScenarioUtility.QuestExists(ZhaoliScenarioUtility.MurmurQuestDefName));
        }

        public bool CanOfferReturn(Map map)
        {
            return CanOfferReturn(map, useCachedScenarioState: false);
        }

        private bool CanOfferReturn(Map map, bool useCachedScenarioState)
        {
            if (scenarioCompleted || returnOffered || currentRaidPawn != null || scheduledReturnTick < 0)
            {
                return false;
            }

            if (Find.TickManager == null || Find.TickManager.TicksGame < scheduledReturnTick)
            {
                return false;
            }

            if (useCachedScenarioState ? cachedPlayerHasZhaoli : ZhaoliScenarioUtility.PlayerHasZhaoli())
            {
                return false;
            }

            return map != null && map.IsPlayerHome && !(useCachedScenarioState ? cachedReturnQuestExists : ZhaoliScenarioUtility.QuestExists(ZhaoliScenarioUtility.ReturnQuestDefName));
        }

        public override void GameComponentTick()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
            {
                return;
            }

            ProcessPendingLoadoutFinalizations();

            if (activeHideoutSite != null && activeHideoutSite.Destroyed)
            {
                activeHideoutSite = null;
            }

            if (currentRaidPawn != null && currentRaidPawn.Destroyed)
            {
                currentRaidPawn = null;
            }

            int currentTick = Find.TickManager.TicksGame;
            bool shouldRefreshScenarioState = nextScenarioStateCheckTick < 0 || currentTick >= nextScenarioStateCheckTick;
            if (shouldRefreshScenarioState)
            {
                RefreshScenarioStateCache(currentTick);
                ApplyScenarioStateCache();
            }

            if (shouldRefreshScenarioState && !scenarioCompleted && !murmurOffered && qualifyingPawnDeathCount < ZhaoliScenarioUtility.MurmurRequiredPawnDeaths)
            {
                BackfillTrackedPawnDeaths();
            }

            if (shouldRefreshScenarioState)
            {
                CleanupUnacceptedMurmurSideEffects(cachedMurmurQuestWasNeverAccepted);
                EnsureAcceptedMurmurQuestStarted();
            }

            if (shouldRefreshScenarioState && scheduledRaidTick >= 0 && currentTick >= scheduledRaidTick)
            {
                if (TryExecuteScheduledRaid(useCachedScenarioState: true))
                {
                    scheduledRaidTick = -1;
                }
            }

            if (shouldRefreshScenarioState && scheduledReturnTick >= 0 && currentTick >= scheduledReturnTick)
            {
                if (TryExecuteScheduledReturn(useCachedScenarioState: true))
                {
                    scheduledReturnTick = -1;
                }
            }

            if (shouldRefreshScenarioState && !scenarioCompleted && !murmurOffered)
            {
                TryExecuteMurmur(useCachedScenarioState: true);
            }
        }

        private void RefreshScenarioStateCache(int currentTick)
        {
            cachedPlayerHasZhaoli = ZhaoliScenarioUtility.PlayerHasZhaoli();
            cachedMurmurQuestExists = ZhaoliScenarioUtility.QuestExists(ZhaoliScenarioUtility.MurmurQuestDefName);
            cachedReturnQuestExists = ZhaoliScenarioUtility.QuestExists(ZhaoliScenarioUtility.ReturnQuestDefName);
            cachedMurmurQuestWasNeverAccepted = ZhaoliScenarioUtility.QuestWasNeverAccepted(ZhaoliScenarioUtility.MurmurQuestDefName);
            nextScenarioStateCheckTick = currentTick + TickCheckInterval;
        }

        private void ApplyScenarioStateCache()
        {
            if (!scenarioCompleted && cachedPlayerHasZhaoli)
            {
                scenarioCompleted = true;
            }
            else if (scenarioCompleted && !cachedPlayerHasZhaoli && (activeHideoutSite != null || currentRaidPawn != null || scheduledRaidTick >= 0 || scheduledReturnTick >= 0))
            {
                scenarioCompleted = false;
            }

            if (cachedMurmurQuestExists)
            {
                murmurOffered = true;
            }
            else if (murmurOffered && CanReleaseMurmurOfferLock())
            {
                murmurOffered = false;
            }

            if (cachedReturnQuestExists)
            {
                returnOffered = true;
            }
        }

        private bool CanReleaseMurmurOfferLock()
        {
            return !scenarioCompleted
                && !returnOffered
                && activeHideoutSite == null
                && currentRaidPawn == null
                && scheduledRaidTick < 0
                && scheduledReturnTick < 0;
        }

        private void CleanupUnacceptedMurmurSideEffects()
        {
            CleanupUnacceptedMurmurSideEffects(ZhaoliScenarioUtility.QuestWasNeverAccepted(ZhaoliScenarioUtility.MurmurQuestDefName));
        }

        private void CleanupUnacceptedMurmurSideEffects(bool questWasNeverAccepted)
        {
            if (!questWasNeverAccepted)
            {
                return;
            }

            scheduledRaidTick = -1;
            if (activeHideoutSite == null)
            {
                return;
            }

            WorldObjectComp_ZhaoliHideout hideoutComp = activeHideoutSite.GetComponent<WorldObjectComp_ZhaoliHideout>();
            if (hideoutComp != null)
            {
                hideoutComp.CancelWithoutConsequence();
            }
            else if (!activeHideoutSite.Destroyed)
            {
                activeHideoutSite.Destroy();
            }

            activeHideoutSite = null;
        }

        private void ProcessPendingLoadoutFinalizations()
        {
            if (Find.TickManager == null || Find.TickManager.TicksGame % LoadoutCheckInterval != 0 || pendingLoadoutFinalizations.Count == 0)
            {
                return;
            }

            for (int index = pendingLoadoutFinalizations.Count - 1; index >= 0; index--)
            {
                PendingLoadoutFinalize pending = pendingLoadoutFinalizations[index];
                Pawn pawn = pending.pawn;
                if (pawn == null || pawn.DestroyedOrNull() || pawn.Dead)
                {
                    ZhaoliScenarioUtility.ClearLoadoutStabilization(pawn);
                    pendingLoadoutFinalizations.RemoveAt(index);
                    continue;
                }

                if (pawn.Spawned)
                {
                    ZhaoliScenarioUtility.EnsureDefaultLoadout(pawn);
                }

                if (ZhaoliScenarioUtility.HasInitialLoadoutEquipped(pawn) || Find.TickManager.TicksGame >= pending.expireTick)
                {
                    ZhaoliScenarioUtility.ClearLoadoutStabilization(pawn);
                    pendingLoadoutFinalizations.RemoveAt(index);
                }
            }
        }

        public void NotifyQualifyingPawnDeath(Pawn pawn)
        {
            if (pawn == null || scenarioCompleted || murmurOffered || returnOffered || activeHideoutSite != null || currentRaidPawn != null)
            {
                return;
            }

            if (!TryRegisterPawnDeath(pawn))
            {
                return;
            }

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
            return TryStartHideout(homeMap, out _);
        }

        public bool TryStartHideout(Map homeMap, out Site site)
        {
            site = null;
            homeMap = ZhaoliScenarioUtility.ResolveBestHomeMap(homeMap);
            if (homeMap == null)
            {
                return false;
            }

            ClearExistingHideoutWithoutConsequence();

            if (!TryFindHideoutSiteTile(homeMap.Tile, out PlanetTile siteTile))
            {
                return false;
            }

            site = SiteMaker.MakeSite(
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

            site.customLabel = "MX_ZL_HideoutSiteCustomLabel".Translate().ToString();
            Find.WorldObjects.Add(site);
            site.GetComponent<TimeoutComp>()?.StartTimeout(ZhaoliScenarioUtility.HideoutLifetimeTicks);
            site.GetComponent<WorldObjectComp_ZhaoliHideout>()?.InitializeHomeMap(homeMap);
            activeHideoutSite = site;
            scenarioHomeMap = homeMap;
            SendHideoutStartedLetter(site);
            return true;
        }

        private void EnsureAcceptedMurmurQuestStarted()
        {
            if (scenarioCompleted || activeHideoutSite != null || currentRaidPawn != null || scheduledRaidTick >= 0 || scheduledReturnTick >= 0)
            {
                return;
            }

            Quest quest = ZhaoliScenarioUtility.FindActiveQuest(ZhaoliScenarioUtility.MurmurQuestDefName);
            if (quest == null || quest.State != QuestState.Ongoing)
            {
                return;
            }

            Map map = ZhaoliScenarioUtility.ResolveBestHomeMap(scenarioHomeMap);
            if (map == null || !TryStartHideout(map, out _))
            {
                return;
            }
        }

        private void SendHideoutStartedLetter(Site site)
        {
            if (site == null || site.Destroyed)
            {
                return;
            }

            Find.LetterStack.ReceiveLetter(
                "MX_ZL_HideoutStartedLetterLabel".Translate(),
                "MX_ZL_HideoutStartedLetterText".Translate(),
                LetterDefOf.NeutralEvent,
                site);
        }

        private void ClearExistingHideoutWithoutConsequence()
        {
            if (activeHideoutSite == null)
            {
                return;
            }

            WorldObjectComp_ZhaoliHideout hideoutComp = activeHideoutSite.GetComponent<WorldObjectComp_ZhaoliHideout>();
            if (hideoutComp != null)
            {
                hideoutComp.CancelWithoutConsequence();
            }
            else if (!activeHideoutSite.Destroyed)
            {
                activeHideoutSite.Destroy();
            }

            activeHideoutSite = null;
        }

        private static bool TryFindHideoutSiteTile(PlanetTile homeTile, out PlanetTile siteTile)
        {
            int[] maxDistances = { 3, 5, 8 };
            for (int index = 0; index < maxDistances.Length; index++)
            {
                if (TileFinder.TryFindPassableTileWithTraversalDistance(
                        homeTile,
                        1,
                        maxDistances[index],
                        out siteTile,
                        tile => !Find.WorldObjects.AnyWorldObjectAt(tile),
                        ignoreFirstTilePassability: false,
                        tileFinderMode: TileFinderMode.Random,
                        canTraverseImpassable: false,
                        exitOnFirstTileFound: false))
                {
                    return true;
                }
            }

            siteTile = PlanetTile.Invalid;
            return false;
        }

        public void NotifyHideoutMedicineAccepted(Map homeMap)
        {
            scenarioCompleted = false;
            scenarioHomeMap = ZhaoliScenarioUtility.ResolveBestHomeMap(homeMap);
            activeHideoutSite = null;
            EndActiveMurmurQuest(QuestEndOutcome.Success);
            ScheduleReturn(scenarioHomeMap, ZhaoliScenarioUtility.NextDayDelayTicks);
        }

        public void NotifyHideoutRejected(Map homeMap)
        {
            scenarioCompleted = false;
            scenarioHomeMap = ZhaoliScenarioUtility.ResolveBestHomeMap(homeMap);
            activeHideoutSite = null;
            EndActiveMurmurQuest(QuestEndOutcome.Success);
            ScheduleRaid(scenarioHomeMap, ZhaoliScenarioUtility.NextDayDelayTicks);
        }

        public void NotifyHideoutExpired(Map homeMap)
        {
            if (ZhaoliScenarioUtility.QuestWasNeverAccepted(ZhaoliScenarioUtility.MurmurQuestDefName))
            {
                scheduledRaidTick = -1;
                activeHideoutSite = null;
                return;
            }

            scenarioCompleted = false;
            scenarioHomeMap = ZhaoliScenarioUtility.ResolveBestHomeMap(homeMap);
            activeHideoutSite = null;
            EndActiveMurmurQuest(QuestEndOutcome.Fail);
            ScheduleRaid(scenarioHomeMap, 0);
        }

        private static void EndActiveMurmurQuest(QuestEndOutcome outcome)
        {
            Quest quest = ZhaoliScenarioUtility.FindActiveQuest(ZhaoliScenarioUtility.MurmurQuestDefName);
            quest?.End(outcome, sendLetter: false, playSound: false);
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
            Scribe_Collections.Look(ref qualifyingPawnDeathIds, "qualifyingPawnDeathIds", LookMode.Value);
            Scribe_Values.Look(ref scheduledRaidTick, "scheduledRaidTick", -1);
            Scribe_Values.Look(ref scheduledReturnTick, "scheduledReturnTick", -1);
            Scribe_References.Look(ref scenarioHomeMap, "scenarioHomeMap");
            Scribe_References.Look(ref activeHideoutSite, "activeHideoutSite");
            Scribe_References.Look(ref currentRaidPawn, "currentRaidPawn");
            if (Scribe.mode == LoadSaveMode.PostLoadInit)
            {
                if (qualifyingPawnDeathIds == null)
                {
                    qualifyingPawnDeathIds = new List<int>();
                }

                qualifyingPawnDeathCount = Mathf.Max(qualifyingPawnDeathCount, qualifyingPawnDeathIds.Count);
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

        private bool TryRegisterPawnDeath(Pawn pawn)
        {
            if (pawn == null || qualifyingPawnDeathCount >= ZhaoliScenarioUtility.MurmurRequiredPawnDeaths)
            {
                return false;
            }

            Map deathMap = pawn.MapHeld;
            if (deathMap == null || !deathMap.IsPlayerHome)
            {
                return false;
            }

            if (qualifyingPawnDeathIds == null)
            {
                qualifyingPawnDeathIds = new List<int>();
            }

            int pawnId = pawn.thingIDNumber;
            if (qualifyingPawnDeathIds.Contains(pawnId))
            {
                return false;
            }

            qualifyingPawnDeathIds.Add(pawnId);
            qualifyingPawnDeathCount = Mathf.Max(qualifyingPawnDeathCount, qualifyingPawnDeathIds.Count);
            return true;
        }

        private void BackfillTrackedPawnDeaths()
        {
            if (qualifyingPawnDeathIds == null)
            {
                qualifyingPawnDeathIds = new List<int>();
            }

            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead)
            {
                if (pawn == null || !pawn.Dead)
                {
                    continue;
                }

                if (!TryRegisterPawnDeath(pawn))
                {
                    continue;
                }

                Map homeMap = ZhaoliScenarioUtility.ResolveBestHomeMap(pawn.MapHeld) ?? ZhaoliScenarioUtility.ResolveBestHomeMap(scenarioHomeMap);
                if (homeMap != null)
                {
                    scenarioHomeMap = homeMap;
                }

                if (qualifyingPawnDeathCount >= ZhaoliScenarioUtility.MurmurRequiredPawnDeaths)
                {
                    break;
                }
            }
        }

        private void ScheduleRaid(Map homeMap, int delayTicks)
        {
            if (scenarioCompleted || ZhaoliScenarioUtility.QuestWasNeverAccepted(ZhaoliScenarioUtility.MurmurQuestDefName))
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

        public bool DebugTryOfferMurmurQuest(Map preferredMap, out string reason)
        {
            reason = null;
            Map map = ZhaoliScenarioUtility.ResolveBestHomeMap(preferredMap);
            if (map == null || !map.IsPlayerHome)
            {
                reason = "No player home map.";
                return false;
            }

            int currentTick = Find.TickManager?.TicksGame ?? 0;
            RefreshScenarioStateCache(currentTick);
            ApplyScenarioStateCache();
            CleanupUnacceptedMurmurSideEffects(cachedMurmurQuestWasNeverAccepted);

            if (cachedPlayerHasZhaoli)
            {
                reason = "Player already has Zhaoli.";
                return false;
            }

            Site existingHideout = ResolveActiveHideoutSite();
            if (existingHideout != null)
            {
                SendHideoutStartedLetter(existingHideout);
                reason = "Zhaoli hideout already exists. A reveal letter was sent.";
                return false;
            }

            if (currentRaidPawn != null)
            {
                reason = "Zhaoli raid is already active.";
                return false;
            }

            if (scheduledRaidTick >= 0)
            {
                reason = "Zhaoli raid is already scheduled.";
                return false;
            }

            if (scheduledReturnTick >= 0 || returnOffered || cachedReturnQuestExists)
            {
                reason = "Zhaoli return flow is already active or completed.";
                return false;
            }

            if (cachedMurmurQuestExists)
            {
                reason = "Zhaoli murmur quest already exists.";
                return false;
            }

            if (scenarioCompleted)
            {
                scenarioCompleted = false;
            }

            if (murmurOffered)
            {
                murmurOffered = false;
            }

            scenarioHomeMap = map;
            if (qualifyingPawnDeathCount < ZhaoliScenarioUtility.MurmurRequiredPawnDeaths)
            {
                BackfillTrackedPawnDeaths();
            }

            if (Find.TickManager == null || Find.TickManager.TicksGame < ZhaoliScenarioUtility.MurmurTriggerTicks)
            {
                reason = "Colony age is below the Zhaoli murmur threshold.";
                return false;
            }

            if (qualifyingPawnDeathCount < ZhaoliScenarioUtility.MurmurRequiredPawnDeaths)
            {
                reason = "Tracked qualifying pawn deaths: " + qualifyingPawnDeathCount + "/" + ZhaoliScenarioUtility.MurmurRequiredPawnDeaths + ".";
                return false;
            }

            if (!CanOfferMurmur(map, useCachedScenarioState: false))
            {
                reason = "Current scenario state still blocks the murmur quest.";
                return false;
            }

            if (!TryExecuteMurmur(useCachedScenarioState: false))
            {
                reason = "Incident worker failed to create the murmur quest.";
                return false;
            }

            return true;
        }

        public bool DebugRevealActiveHideout(out string reason)
        {
            reason = null;
            Site site = ResolveActiveHideoutSite();
            if (site == null)
            {
                reason = "No active Zhaoli hideout found.";
                return false;
            }

            SendHideoutStartedLetter(site);
            return true;
        }

        private Site ResolveActiveHideoutSite()
        {
            if (activeHideoutSite != null && !activeHideoutSite.Destroyed)
            {
                return activeHideoutSite;
            }

            activeHideoutSite = null;
            List<WorldObject> worldObjects = Find.WorldObjects?.AllWorldObjects;
            if (worldObjects == null)
            {
                return null;
            }

            for (int index = 0; index < worldObjects.Count; index++)
            {
                if (worldObjects[index] is Site site && site.def == MXZL_ZhaoliDefOf.MXZL_ZhaoliHideoutWorldObject && !site.Destroyed)
                {
                    activeHideoutSite = site;
                    return site;
                }
            }

            return null;
        }

        private bool TryExecuteMurmur(bool useCachedScenarioState = false)
        {
            Map map = ZhaoliScenarioUtility.ResolveBestHomeMap(scenarioHomeMap);
            if (map == null || !CanOfferMurmur(map, useCachedScenarioState))
            {
                return false;
            }

            IncidentDef incident = MXZL_ZhaoliDefOf.MXZL_ZhaoliMurmur;
            if (incident == null)
            {
                Log.Error("[MiliraXian.Characters.Zhaoli] Missing IncidentDef: MXZL_ZhaoliMurmur");
                murmurOffered = true;
                return false;
            }

            IncidentParms parms = StorytellerUtility.DefaultParmsNow(incident.category, map);
            parms.target = map;
            parms.forced = true;
            parms.bypassStorytellerSettings = true;
            if (incident.Worker.TryExecute(parms))
            {
                murmurOffered = true;
                return true;
            }

            return false;
        }

        private bool TryExecuteScheduledRaid(bool useCachedScenarioState = false)
        {
            if (useCachedScenarioState ? cachedMurmurQuestWasNeverAccepted : ZhaoliScenarioUtility.QuestWasNeverAccepted(ZhaoliScenarioUtility.MurmurQuestDefName))
            {
                scheduledRaidTick = -1;
                return true;
            }

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

            List<Pawn> raidPawns = new List<Pawn> { pawn };
            PawnsArrivalModeDefOf.EdgeWalkIn.Worker.Arrive(raidPawns, parms);
            pawn.SetFaction(faction);
            LordMaker.MakeNewLord(
                faction,
                new LordJob_ZhaoliRaidAnchor(),
                map,
                raidPawns);
            currentRaidPawn = pawn;
            Find.LetterStack.ReceiveLetter(
                "MX_ZL_RaidLetterLabel".Translate(),
                "MX_ZL_RaidLetterText".Translate(),
                LetterDefOf.ThreatBig,
                pawn);
            return true;
        }

        private bool TryExecuteScheduledReturn(bool useCachedScenarioState = false)
        {
            Map map = ZhaoliScenarioUtility.ResolveBestHomeMap(scenarioHomeMap);
            if (map == null || !CanOfferReturn(map, useCachedScenarioState))
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

    internal static class ZhaoliDebugActions
    {
        [DebugAction("Milira Xian - Zhaoli", "Force friendly dead Zhaoli return now", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ForceFriendlyDeadZhaoliReturnNow()
        {
            GameComponent_ZhaoliKarma component = Current.Game?.GetComponent<GameComponent_ZhaoliKarma>();
            if (component == null || Find.TickManager == null)
            {
                Messages.Message("Zhaoli rebirth component is not available.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            int count = 0;
            int currentTick = Find.TickManager.TicksGame;
            foreach (Pawn pawn in PawnsFinder.AllMapsWorldAndTemporary_AliveOrDead.ToList())
            {
                if (pawn == null || pawn.Discarded || !pawn.Dead || pawn.Faction != Faction.OfPlayer || !ZhaoliKarmaUtility.IsZhaoli(pawn))
                {
                    continue;
                }

                if (ZhaoliScenarioUtility.IsHideoutState(pawn) || ZhaoliScenarioUtility.IsRaidState(pawn))
                {
                    continue;
                }

                ZhaoliRebirthUtility.PreparePawnForPendingRebirth(pawn);
                component.RegisterPendingRebirth(pawn, currentTick);
                count++;
            }

            if (count <= 0)
            {
                Messages.Message("No dead friendly Zhaoli found.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            Messages.Message("Queued immediate Zhaoli rebirths: " + count + ".", MessageTypeDefOf.PositiveEvent, false);
        }

        [DebugAction("Milira Xian - Zhaoli", "Retry Zhaoli murmur quest check", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RetryZhaoliMurmurQuestCheck()
        {
            GameComponent_ZhaoliScenario component = Current.Game?.GetComponent<GameComponent_ZhaoliScenario>();
            if (component == null)
            {
                Messages.Message("Zhaoli scenario component is not available.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            Map map = ZhaoliScenarioUtility.ResolveBestHomeMap(Find.CurrentMap);
            if (component.DebugTryOfferMurmurQuest(map, out string reason))
            {
                Messages.Message("Zhaoli murmur quest triggered.", MessageTypeDefOf.PositiveEvent, false);
                return;
            }

            Messages.Message("Zhaoli murmur quest was not triggered: " + reason, MessageTypeDefOf.RejectInput, false);
        }

        [DebugAction("Milira Xian - Zhaoli", "Reveal active Zhaoli hideout", false, false, false, false, false, 0, false, allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void RevealActiveZhaoliHideout()
        {
            GameComponent_ZhaoliScenario component = Current.Game?.GetComponent<GameComponent_ZhaoliScenario>();
            if (component == null)
            {
                Messages.Message("Zhaoli scenario component is not available.", MessageTypeDefOf.RejectInput, false);
                return;
            }

            if (component.DebugRevealActiveHideout(out string reason))
            {
                Messages.Message("Zhaoli hideout reveal letter sent.", MessageTypeDefOf.PositiveEvent, false);
                return;
            }

            Messages.Message("Zhaoli hideout was not revealed: " + reason, MessageTypeDefOf.RejectInput, false);
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
                return "MX_ZL_HideoutInspectExpired".Translate().ToString();
            }

            return "MX_ZL_HideoutInspectWaiting".Translate().ToString();
        }

        public override void PostExposeData()
        {
            base.PostExposeData();
            Scribe_References.Look(ref homeMap, "homeMap");
            Scribe_References.Look(ref hideoutPawn, "hideoutPawn");
            Scribe_Values.Look(ref resolved, "resolved", defaultValue: false);
            Scribe_Values.Look(ref consequenceQueued, "consequenceQueued", defaultValue: false);
        }

        public void CancelWithoutConsequence()
        {
            resolved = true;
            consequenceQueued = true;
            parent.GetComponent<TimeoutComp>()?.StopTimeout();
            if (hideoutPawn != null)
            {
                ZhaoliScenarioUtility.DestroyHideoutPawn(hideoutPawn);
                hideoutPawn = null;
            }

            if (!parent.Destroyed)
            {
                parent.Destroy();
            }
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
            if (!TryResolveHideoutSpawnCell(map, out IntVec3 spawnCell))
            {
                Log.Error("[MiliraXian.Characters.Zhaoli] Failed to find a valid spawn cell for Zhaoli hideout on map " + map);
                return;
            }

            GenSpawn.Spawn(hideoutPawn, spawnCell, map);
        }

        private static bool TryResolveHideoutSpawnCell(Map map, out IntVec3 spawnCell)
        {
            spawnCell = IntVec3.Invalid;
            if (map == null)
            {
                return false;
            }

            IntVec3 root = map.Center;
            if (IsValidHideoutSpawnCell(root, map, requireMapEdgeReachable: true))
            {
                spawnCell = root;
                return true;
            }

            int[] radii = { 8, 16, 32, 48, 64 };
            for (int i = 0; i < radii.Length; i++)
            {
                if (CellFinder.TryRandomClosewalkCellNear(root, map, radii[i], out spawnCell, cell => IsValidHideoutSpawnCell(cell, map, requireMapEdgeReachable: true)))
                {
                    return true;
                }
            }

            if (TryFindNearestHideoutSpawnCell(map, root, cell => IsValidHideoutSpawnCell(cell, map, requireMapEdgeReachable: true), out spawnCell))
            {
                return true;
            }

            return TryFindNearestHideoutSpawnCell(map, root, cell => IsValidHideoutSpawnCell(cell, map, requireMapEdgeReachable: false), out spawnCell);
        }

        private static bool TryFindNearestHideoutSpawnCell(Map map, IntVec3 root, Predicate<IntVec3> validator, out IntVec3 result)
        {
            result = IntVec3.Invalid;
            int bestDistance = int.MaxValue;
            for (int x = 0; x < map.Size.x; x++)
            {
                for (int z = 0; z < map.Size.z; z++)
                {
                    IntVec3 cell = new IntVec3(x, 0, z);
                    if (!validator(cell))
                    {
                        continue;
                    }

                    int distance = (cell - root).LengthHorizontalSquared;
                    if (distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    result = cell;
                }
            }

            return result.IsValid;
        }

        private static bool IsValidHideoutSpawnCell(IntVec3 cell, Map map, bool requireMapEdgeReachable)
        {
            if (!cell.IsValid || !cell.InBounds(map) || !cell.Standable(map) || map.thingGrid.CellContains(cell, ThingCategory.Pawn))
            {
                return false;
            }

            return !requireMapEdgeReachable || map.reachability.CanReachMapEdge(cell, TraverseParms.For(TraverseMode.PassDoors));
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
                "MX_ZL_HideoutOptionGiveMedicine".Translate().ToString(),
                delegate
                {
                    if (!hideoutComp.TryDeliverHerbs(interactor))
                    {
                        Messages.Message("MX_ZL_HideoutNoMedicine".Translate(), interactor, MessageTypeDefOf.RejectInput, historical: false);
                        return;
                    }

                    Messages.Message("MX_ZL_HideoutMedicineAccepted".Translate(), interactor, MessageTypeDefOf.PositiveEvent, historical: true);
                });
            yield return new FloatMenuOption(
                "MX_ZL_HideoutOptionDelay".Translate().ToString(),
                delegate
                {
                    Messages.Message("MX_ZL_HideoutDelayed".Translate(), interactor, MessageTypeDefOf.NeutralEvent, historical: false);
                });
            yield return new FloatMenuOption(
                "MX_ZL_HideoutOptionReject".Translate().ToString(),
                delegate
                {
                    hideoutComp.Reject(interactor);
                    Messages.Message("MX_ZL_HideoutRejected".Translate(), interactor, MessageTypeDefOf.ThreatBig, historical: true);
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
        public int minPreferredCluster = 1;
        public int transitionTicks = ZhaoliProgressionUtility.TransitionDurationTicks;
        public int teleportIntervalTicks = ZhaoliProgressionUtility.PhaseTeleportIntervalTicks;

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
        private int substituteDeathsUsed;
        private int transitionEndTick = -1;
        private int nextPhaseTeleportTick = -1;
        private int lastRaidSkillTick = -99999;
        private int lastDeathFieldTick = -99999;
        private bool damageAppliedSinceSwitch;
        private bool raidInitialized;
        private bool linksPrepared;

        private HediffCompProperties_ZhaoliRaidState PropsRaid => (HediffCompProperties_ZhaoliRaidState)props;
        public int SubstituteDeathsUsed => substituteDeathsUsed;

        private struct DeathFieldClusterInfo
        {
            public int validTargets;
            public int livingTargets;
            public int humanlikeTargets;
            public bool containsCurrentTarget;
        }

        public override string CompLabelInBracketsExtra => "MX_ZL_RaidPhaseLabel".Translate(Mathf.Clamp(substituteDeathsUsed, 0, 3) + 1).ToString();

        public override bool CompDisallowVisible()
        {
            return false;
        }

        public override string CompDescriptionExtra
        {
            get
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append("MX_ZL_RaidSubstituteTriggered".Translate().ToString());
                stringBuilder.Append(substituteDeathsUsed);
                stringBuilder.Append("/3");

                HediffComp_ZhaoliKarmaLinks linkComp = ZhaoliKarmaUtility.GetLinkComp(Pawn);
                if (linkComp != null)
                {
                    stringBuilder.AppendLine();
                    stringBuilder.Append("MX_ZL_RaidRemainingLinks".Translate().ToString());
                    stringBuilder.Append(linkComp.ActiveLinkCount);
                    stringBuilder.Append("/");
                    stringBuilder.Append(ZhaoliProgressionUtility.BossLinkCount);
                }

                stringBuilder.AppendLine();
                stringBuilder.Append(ZhaoliProgressionUtility.BuildRaidBossSummary(substituteDeathsUsed));
                return stringBuilder.ToString();
            }
        }

        public override string CompTipStringExtra
        {
            get
            {
                StringBuilder stringBuilder = new StringBuilder();
                stringBuilder.Append("MX_ZL_RaidCurrentPhase".Translate().ToString());
                stringBuilder.Append(Mathf.Clamp(substituteDeathsUsed, 0, 3) + 1);
                stringBuilder.Append("/4");

                int currentTick = Find.TickManager?.TicksGame ?? -1;
                if (currentTick >= 0 && transitionEndTick > currentTick)
                {
                    stringBuilder.AppendLine();
                    stringBuilder.Append("MX_ZL_RaidTransitionRemaining".Translate().ToString());
                    stringBuilder.Append((transitionEndTick - currentTick).ToStringTicksToPeriod());
                }

                if (substituteDeathsUsed >= 2 && currentTick >= 0 && nextPhaseTeleportTick > currentTick)
                {
                    stringBuilder.AppendLine();
                    stringBuilder.Append("MX_ZL_RaidNextWarp".Translate().ToString());
                    stringBuilder.Append((nextPhaseTeleportTick - currentTick).ToStringTicksToPeriod());
                }

                return stringBuilder.ToString();
            }
        }

        public void InitializeRaid(Map targetMap)
        {
            raidInitialized = true;
            linksPrepared = false;
            currentAttackTarget = null;
            primaryHatredTarget = null;
            lastTargetSwitchTick = Find.TickManager?.TicksGame ?? 0;
            ignoreHatredUntilTick = -1;
            substituteDeathsUsed = 0;
            transitionEndTick = -1;
            nextPhaseTeleportTick = -1;
            lastRaidSkillTick = -99999;
            lastDeathFieldTick = -99999;
            damageAppliedSinceSwitch = false;
            ZhaoliRebirthUtility.RemoveRebirthHediff(Pawn);
            ZhaoliShieldLayerUtility.AddLayers(Pawn, ZhaoliScenarioUtility.RaidStartingShieldLayers);
            ZhaoliKarmaUtility.EnsureKarmaComp(Pawn)?.SetValue(ZhaoliScenarioUtility.RaidStartingKarma);
            EnsurePassiveRaidLord();
            ZhaoliRaidDebugUtility.Log(Pawn, "InitializeRaid", "raid state initialized");
            if (targetMap != null)
            {
                Current.Game?.GetComponent<GameComponent_ZhaoliScenario>()?.MarkMurmurOffered(targetMap);
            }
        }

        public void NotifySubstituteTriggered()
        {
            if (Pawn == null || Pawn.Dead)
            {
                return;
            }

            substituteDeathsUsed = Mathf.Clamp(substituteDeathsUsed + 1, 0, 3);
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            int shieldLayers = ZhaoliScenarioUtility.RaidTransitionBaseShieldLayers + ZhaoliScenarioUtility.RaidTransitionShieldLayersPerPhase * substituteDeathsUsed;
            ZhaoliRebirthUtility.RemoveRebirthHediff(Pawn);
            ZhaoliShieldLayerUtility.AddLayers(Pawn, shieldLayers);
            transitionEndTick = currentTick + Mathf.Max(1, PropsRaid.transitionTicks);
            nextPhaseTeleportTick = transitionEndTick + Mathf.Max(1, PropsRaid.teleportIntervalTicks);
            damageAppliedSinceSwitch = false;
            ignoreHatredUntilTick = transitionEndTick;
            currentAttackTarget = null;
            primaryHatredTarget = null;
            Pawn.jobs?.StopAll(false, true);
            Pawn.pather?.StopDead();
            Pawn.stances?.stunner?.StunFor(Mathf.Max(60, PropsRaid.transitionTicks), Pawn, addBattleLog: false, showMote: false);
            NotifyPhaseTransitionStarted(shieldLayers);
            ActivateTransitionField();
            ZhaoliRaidDebugUtility.Log(Pawn, "TransitionShield", "phase=" + substituteDeathsUsed + " addedLayers=" + shieldLayers);
        }

        private void NotifyPhaseTransitionStarted(int shieldLayers)
        {
            if (Pawn == null)
            {
                return;
            }

            int displayPhase = Mathf.Clamp(substituteDeathsUsed, 0, 3) + 1;
            Messages.Message("MX_ZL_RaidPhaseTransitionStarted".Translate(Pawn.LabelShortCap, displayPhase, shieldLayers), Pawn, MessageTypeDefOf.ThreatBig, historical: true);
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
            Scribe_Values.Look(ref substituteDeathsUsed, "substituteDeathsUsed", 0);
            Scribe_Values.Look(ref transitionEndTick, "transitionEndTick", -1);
            Scribe_Values.Look(ref nextPhaseTeleportTick, "nextPhaseTeleportTick", -1);
            Scribe_Values.Look(ref lastRaidSkillTick, "lastRaidSkillTick", -99999);
            Scribe_Values.Look(ref lastDeathFieldTick, "lastDeathFieldTick", -99999);
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

            EnsureHostileRaidFaction();

            if (!linksPrepared)
            {
                PrepareInitialLinks();
                linksPrepared = true;
            }

            if (Pawn.IsHashIntervalTick(60))
            {
                EnsurePassiveRaidLord();
                ZhaoliRaidDebugUtility.LogUnexpectedState(Pawn, "CompPostTick");
            }

            MaintainLockedPhaseNeeds();

            if (IsInTransition())
            {
                MaintainTransition();
                return;
            }

            if (!Pawn.IsHashIntervalTick(ZhaoliScenarioUtility.AiTickInterval))
            {
                return;
            }

            RunRaidAi();
        }

        private void EnsureHostileRaidFaction()
        {
            if (Pawn == null || Pawn.Dead || !Pawn.Spawned || Faction.OfPlayer == null)
            {
                return;
            }

            if (Pawn.Faction != null && Pawn.Faction.HostileTo(Faction.OfPlayer))
            {
                return;
            }

            Faction hostileFaction = ZhaoliScenarioUtility.ResolveHostileFaction();
            if (hostileFaction == null || hostileFaction == Pawn.Faction)
            {
                return;
            }

            ZhaoliRaidDebugUtility.Log(Pawn, "FactionFix", "changing raid faction from=" + (Pawn.Faction?.def?.defName ?? "null") + " to=" + hostileFaction.def.defName);
            Pawn.SetFaction(hostileFaction);
            EnsurePassiveRaidLord();
        }

        private void EnsurePassiveRaidLord()
        {
            if (Pawn?.MapHeld == null || Pawn.Faction == null)
            {
                return;
            }

            Lord lord = Pawn.GetLord();
            if (lord == null)
            {
                LordMaker.MakeNewLord(Pawn.Faction, new LordJob_ZhaoliRaidAnchor(), Pawn.MapHeld, Gen.YieldSingle(Pawn));
                ZhaoliRaidDebugUtility.Log(Pawn, "EnsureLord", "created missing passive raid lord");
                return;
            }

            if (lord.LordJob is LordJob_ZhaoliRaidAnchor)
            {
                return;
            }

            if (lord.ownedPawns.Count == 1 && lord.ownedPawns[0] == Pawn)
            {
                ZhaoliRaidDebugUtility.Log(Pawn, "EnsureLord", "replacing existing lord job " + lord.LordJob?.GetType().Name + " -> LordJob_ZhaoliRaidAnchor");
                lord.SetJob(new LordJob_ZhaoliRaidAnchor());
                lord.GotoToil(lord.Graph.StartingToil);
                return;
            }

            ZhaoliRaidDebugUtility.Log(Pawn, "EnsureLord", "kept existing lord " + lord.LordJob?.GetType().Name + " because ownedPawns=" + lord.ownedPawns.Count);
        }

        private bool IsInTransition()
        {
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            return transitionEndTick >= 0 && currentTick < transitionEndTick;
        }

        public bool IsTransitionInvulnerable()
        {
            return IsInTransition();
        }

        private void MaintainTransition()
        {
            if (Pawn == null)
            {
                return;
            }

            if (Pawn.IsHashIntervalTick(30))
            {
                Pawn.jobs?.StopAll(false, true);
                Pawn.pather?.StopDead();
                Pawn.stances?.stunner?.StunFor(60, Pawn, addBattleLog: false, showMote: false);
            }
        }

        private void MaintainLockedPhaseNeeds()
        {
            if (substituteDeathsUsed < 3 || Pawn?.needs?.food == null)
            {
                return;
            }

            if (Pawn.IsHashIntervalTick(60))
            {
                Pawn.needs.food.CurLevelPercentage = 0.01f;
            }
        }

        private void ActivateTransitionField()
        {
            if (Pawn?.health == null || MXZL_ZhaoliDefOf.MXZL_ZhaoliDeathFieldActive == null)
            {
                return;
            }

            HediffWithComps field = Pawn.health.hediffSet.GetFirstHediffOfDef(MXZL_ZhaoliDefOf.MXZL_ZhaoliDeathFieldActive) as HediffWithComps;
            if (field == null)
            {
                field = HediffMaker.MakeHediff(MXZL_ZhaoliDefOf.MXZL_ZhaoliDeathFieldActive, Pawn) as HediffWithComps;
                if (field == null)
                {
                    return;
                }

                Pawn.health.AddHediff(field);
            }

            HediffComp_ZhaoliDeathField fieldComp = field.GetComp<HediffComp_ZhaoliDeathField>();
            if (fieldComp == null)
            {
                return;
            }

            float radius = fieldComp.DefaultRadius + ZhaoliProgressionUtility.GetTransitionRadiusBonus(substituteDeathsUsed);
            fieldComp.ActivateAt(Pawn.Position, radius);
            if (Pawn.Spawned)
            {
                FleckMaker.Static(Pawn.Position, Pawn.MapHeld, FleckDefOf.PsycastAreaEffect, Mathf.Max(1.5f, radius * 0.65f));
                FleckMaker.Static(Pawn.Position, Pawn.MapHeld, FleckDefOf.ExplosionFlash, 2.8f);
            }
        }

        private void RunRaidAi()
        {
            CleanupEntries();
            if (Pawn.MapHeld == null || Pawn.jobs == null)
            {
                return;
            }

            if (IsExecutingAbilityJob())
            {
                return;
            }

            bool lockToKillOrders = substituteDeathsUsed >= 3;
            int currentTick = Find.TickManager?.TicksGame ?? 0;
            Thing desiredTarget = ResolveDesiredAttackTarget(lockToKillOrders, currentTick);
            if (desiredTarget != currentAttackTarget)
            {
                SetCurrentAttackTarget(desiredTarget, currentTick);
            }

            SyncForcedEnemyTarget();
            if (currentAttackTarget == null)
            {
                return;
            }

            if (substituteDeathsUsed >= 2 && TryPhaseTeleport(currentTick))
            {
                return;
            }

            if (!lockToKillOrders && TryCastMinghuo(currentTick))
            {
                return;
            }

            if (currentAttackTarget is Pawn targetPawn)
            {
                bool shouldPressAttackFirst = ShouldPressAttackBeforeSkill(targetPawn, currentTick);
                if (!shouldPressAttackFirst && !lockToKillOrders && TryCastField(targetPawn, currentTick))
                {
                    return;
                }

                if (!shouldPressAttackFirst && !lockToKillOrders && TryCastMinshen(targetPawn, currentTick))
                {
                    return;
                }

                if (!shouldPressAttackFirst && TryUseGapCloser(targetPawn, currentTick))
                {
                    return;
                }

                OrderAttack(targetPawn);
                return;
            }

            if (currentAttackTarget is Building targetBuilding)
            {
                OrderAttack(targetBuilding);
                return;
            }
        }

        private void CleanupEntries()
        {
            hateEntries.RemoveAll(entry => entry == null || entry.pawn == null || entry.pawn.Destroyed || entry.pawn.Dead);
            retaliatoryThings.RemoveAll(thing => thing == null || thing.Destroyed || !thing.Spawned || thing.MapHeld != Pawn.MapHeld || thing.Faction == Pawn.Faction);
            if (primaryHatredTarget != null && (primaryHatredTarget.Destroyed || primaryHatredTarget.Dead))
            {
                primaryHatredTarget = null;
            }

            if (currentAttackTarget != null && currentAttackTarget.Destroyed)
            {
                currentAttackTarget = null;
            }

            if (Pawn?.mindState?.enemyTarget != null && (Pawn.mindState.enemyTarget.Destroyed || Pawn.mindState.enemyTarget.MapHeld != Pawn.MapHeld))
            {
                Pawn.mindState.enemyTarget = null;
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

            HediffComp_ZhaoliKarmaLinks linkComp = ZhaoliKarmaUtility.EnsureLinkComp(Pawn);
            if (linkComp == null)
            {
                return;
            }

            int desiredLinkCount = Mathf.Max(0, ZhaoliProgressionUtility.BossLinkCount);
            int attempts = 0;
            int maxAttempts = Mathf.Max(12, desiredLinkCount * 6);
            while (linkComp.ActiveLinkCount < desiredLinkCount && attempts < maxAttempts && candidates.Count > 0)
            {
                attempts++;
                Pawn target = candidates[Rand.Range(0, candidates.Count)];
                if (target == null)
                {
                    continue;
                }

                Pawn relative = ZhaoliScenarioUtility.GenerateLinkedRelativePawn(target, Pawn.MapHeld);
                if (relative == null)
                {
                    Log.Warning("[MiliraXian.Characters.Zhaoli] Failed to generate linked relative for " + target);
                    continue;
                }

                linkComp.TryAddOrRefreshLink(relative, out _, out _);
            }

            ZhaoliRaidDebugUtility.Log(Pawn, "InitialLinks", "prepared=" + linkComp.ActiveLinkCount + "/" + desiredLinkCount + " attempts=" + attempts);
        }

        private bool TryPhaseTeleport(int currentTick)
        {
            if (currentTick < nextPhaseTeleportTick)
            {
                return false;
            }

            Pawn targetPawn = GetPriorityTeleportTarget(currentTick);
            IntVec3 destination = FindTeleportDestination(targetPawn);
            if (targetPawn == null || !destination.IsValid || Pawn.MapHeld == null)
            {
                nextPhaseTeleportTick = currentTick + Mathf.Max(1, PropsRaid.teleportIntervalTicks);
                return false;
            }

            Pawn.jobs?.StopAll(false, true);
            Pawn.pather?.StopDead();
            FleckMaker.Static(Pawn.Position, Pawn.MapHeld, FleckDefOf.PsycastSkipInnerExit, 1.15f);
            Pawn.Position = destination;
            Pawn.Notify_Teleported();
            Pawn.rotationTracker?.FaceCell(targetPawn.Position);
            FleckMaker.Static(destination, Pawn.MapHeld, FleckDefOf.PsycastSkipFlashEntry, 1.2f);
            SetCurrentAttackTarget(targetPawn, currentTick);
            nextPhaseTeleportTick = currentTick + Mathf.Max(1, PropsRaid.teleportIntervalTicks);
            return true;
        }

        private Pawn GetPriorityTeleportTarget(int currentTick)
        {
            if (currentAttackTarget is Pawn currentPawn && IsValidRaidTarget(currentPawn))
            {
                Pawn higherHatredPawn = GetHigherHatredPawn(currentPawn);
                return ShouldSwitchToHigherHatred(currentPawn, higherHatredPawn, currentTick) ? higherHatredPawn : currentPawn;
            }

            if (IsValidRaidTarget(primaryHatredTarget))
            {
                return primaryHatredTarget;
            }

            Pawn hatedTarget = GetNearestHatredPawn();
            if (IsValidRaidTarget(hatedTarget))
            {
                return hatedTarget;
            }

            return FindNearestPawnTarget();
        }

        private Pawn FindNearestPawnTarget()
        {
            Pawn nearest = null;
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

            return nearest;
        }

        private IntVec3 FindTeleportDestination(Pawn targetPawn)
        {
            Map map = Pawn?.MapHeld;
            if (targetPawn == null || map == null || !targetPawn.Spawned || targetPawn.MapHeld != map)
            {
                return IntVec3.Invalid;
            }

            IntVec3 preferred = targetPawn.Position + NeiyuFlowerSwordSkillUtility.StepDirectionAwayFrom(targetPawn.Position, Pawn.Position);
            preferred = NeiyuFlowerSwordSkillUtility.ClampToMap(preferred, map);
            if (JumpUtility.ValidJumpTarget(Pawn, map, preferred))
            {
                return preferred;
            }

            for (int index = 0; index < GenAdj.AdjacentCellsAround.Length; index++)
            {
                IntVec3 candidate = NeiyuFlowerSwordSkillUtility.ClampToMap(targetPawn.Position + GenAdj.AdjacentCellsAround[index], map);
                if (JumpUtility.ValidJumpTarget(Pawn, map, candidate))
                {
                    return candidate;
                }
            }

            return IntVec3.Invalid;
        }

        private bool IsExecutingAbilityJob()
        {
            Job currentJob = Pawn?.CurJob;
            if (currentJob?.ability != null || currentJob?.verbToUse is Verb_CastAbility)
            {
                return true;
            }

            if (Pawn?.CurJobDef?.abilityCasting == true)
            {
                return true;
            }

            return Pawn?.stances?.curStance is Stance_Warmup warmup && warmup.verb is Verb_CastAbility;
        }

        private void SetCurrentAttackTarget(Thing target, int currentTick)
        {
            Thing previousTarget = currentAttackTarget;
            currentAttackTarget = target;
            if (target is Pawn pawn)
            {
                primaryHatredTarget = pawn;
            }

            lastTargetSwitchTick = currentTick;
            damageAppliedSinceSwitch = false;
            ignoreHatredUntilTick = target == null ? -1 : currentTick + ZhaoliScenarioUtility.TargetSwitchGraceTicks;
            ZhaoliRaidDebugUtility.Log(
                Pawn,
                "TargetSwitch",
                "from=" + ZhaoliRaidDebugUtility.DescribeThing(previousTarget) +
                " to=" + ZhaoliRaidDebugUtility.DescribeThing(target) +
                " prevHatred=" + GetHatredValue(previousTarget as Pawn) +
                " newHatred=" + GetHatredValue(target as Pawn) +
                " ignoreUntil=" + ignoreHatredUntilTick);
        }

        private void SyncForcedEnemyTarget()
        {
            if (Pawn?.mindState == null)
            {
                return;
            }

            if (currentAttackTarget == null || currentAttackTarget.Destroyed || !currentAttackTarget.Spawned || currentAttackTarget.MapHeld != Pawn.MapHeld)
            {
                Pawn.mindState.enemyTarget = null;
                return;
            }

            if (Pawn.mindState.enemyTarget != currentAttackTarget)
            {
                Pawn.mindState.enemyTarget = currentAttackTarget;
                Pawn.mindState.lastEngageTargetTick = Find.TickManager?.TicksGame ?? Pawn.mindState.lastEngageTargetTick;
                ZhaoliRaidDebugUtility.Log(Pawn, "EnemyTargetSync", "enemyTarget=" + ZhaoliRaidDebugUtility.DescribeThing(currentAttackTarget));
            }
        }

        private Thing ResolveDesiredAttackTarget(bool lockToKillOrders, int currentTick)
        {
            Thing fallbackTarget = GetFallbackAttackTarget(lockToKillOrders);
            Pawn strongestHatredPawn = GetStrongestHatredPawn();
            if (!IsValidRaidAttackTarget(currentAttackTarget, lockToKillOrders))
            {
                return strongestHatredPawn ?? fallbackTarget;
            }

            if (currentAttackTarget is Pawn currentPawn)
            {
                Pawn higherHatredPawn = GetHigherHatredPawn(currentPawn);
                if (currentTick < ignoreHatredUntilTick)
                {
                    return currentAttackTarget;
                }

                if (ShouldSwitchToHigherHatred(currentPawn, higherHatredPawn, currentTick))
                {
                    return higherHatredPawn;
                }

                if (GetHatredValue(currentPawn) > 0f)
                {
                    return currentAttackTarget;
                }

                if (currentTick - lastTargetSwitchTick < ZhaoliScenarioUtility.TargetLockTicks)
                {
                    return currentAttackTarget;
                }

                if (strongestHatredPawn != null && strongestHatredPawn != currentPawn && ShouldSwitchToHigherHatred(currentPawn, strongestHatredPawn, currentTick))
                {
                    return strongestHatredPawn;
                }
            }
            else if (currentAttackTarget is Building)
            {
                if (currentTick < ignoreHatredUntilTick)
                {
                    return currentAttackTarget;
                }

                if (strongestHatredPawn != null && currentTick - lastTargetSwitchTick >= ZhaoliScenarioUtility.TargetLockTicks)
                {
                    return strongestHatredPawn;
                }
            }

            if (currentTick - lastTargetSwitchTick >= ZhaoliScenarioUtility.TargetLockTicks && fallbackTarget != null && fallbackTarget != currentAttackTarget)
            {
                return fallbackTarget;
            }

            return currentAttackTarget ?? strongestHatredPawn ?? fallbackTarget;
        }

        private bool ShouldSwitchToHigherHatred(Pawn currentPawn, Pawn candidate, int currentTick)
        {
            if (!IsValidRaidTarget(candidate) || candidate == currentPawn)
            {
                return false;
            }

            if (currentTick - lastTargetSwitchTick < ZhaoliScenarioUtility.TargetLockTicks)
            {
                return false;
            }

            float currentHatred = Mathf.Max(0f, GetHatredValue(currentPawn));
            float candidateHatred = Mathf.Max(0f, GetHatredValue(candidate));
            float requiredHatred = Mathf.Max(currentHatred + ZhaoliScenarioUtility.HatredPerHit * 2f, currentHatred * 1.6f);
            return candidateHatred >= requiredHatred;
        }

        private Thing GetFallbackAttackTarget(bool lockToKillOrders)
        {
            return lockToKillOrders ? (Thing)FindNearestPawnTarget() : FindNearestTarget();
        }

        private float GetHatredValue(Pawn pawn)
        {
            if (pawn == null)
            {
                return 0f;
            }

            for (int index = 0; index < hateEntries.Count; index++)
            {
                ZhaoliHateEntry entry = hateEntries[index];
                if (entry?.pawn == pawn)
                {
                    return entry.hatred;
                }
            }

            return 0f;
        }

        private Pawn GetNearestHatredPawn()
        {
            Pawn nearestPawn = null;
            float bestDistance = float.MaxValue;
            for (int index = 0; index < hateEntries.Count; index++)
            {
                ZhaoliHateEntry entry = hateEntries[index];
                if (entry?.pawn == null || !IsValidRaidTarget(entry.pawn))
                {
                    continue;
                }

                float distance = Pawn.Position.DistanceToSquared(entry.pawn.Position);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    nearestPawn = entry.pawn;
                }
            }

            return nearestPawn;
        }

        private Pawn GetHigherHatredPawn(Pawn currentPawn)
        {
            float currentHatred = GetHatredValue(currentPawn);
            Pawn bestPawn = null;
            float bestHatred = currentHatred;
            float bestDistance = float.MaxValue;
            for (int index = 0; index < hateEntries.Count; index++)
            {
                ZhaoliHateEntry entry = hateEntries[index];
                if (entry?.pawn == null || entry.pawn == currentPawn || !IsValidRaidTarget(entry.pawn))
                {
                    continue;
                }

                if (entry.hatred <= bestHatred)
                {
                    continue;
                }

                float distance = Pawn.Position.DistanceToSquared(entry.pawn.Position);
                if (bestPawn == null || entry.hatred > bestHatred || (Mathf.Approximately(entry.hatred, bestHatred) && distance < bestDistance))
                {
                    bestPawn = entry.pawn;
                    bestHatred = entry.hatred;
                    bestDistance = distance;
                }
            }

            return bestPawn;
        }

        private Pawn GetStrongestHatredPawn()
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

            return bestPawn;
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

        private Thing GetPreferredHatredTarget(bool allowBuildings = true)
        {
            Pawn bestPawn = GetNearestHatredPawn();
            if (bestPawn != null)
            {
                return bestPawn;
            }

            return allowBuildings ? FindNearestTarget() : FindNearestPawnTarget();
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
                if (!(candidate is Building building) || !IsValidRetaliatoryBuilding(building))
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

        private bool IsValidRetaliatoryBuilding(Building building)
        {
            if (building == null || building.Destroyed || !building.Spawned || building.MapHeld != Pawn.MapHeld)
            {
                return false;
            }

            return building.Faction != null && building.HostileTo(Pawn);
        }

        private bool IsValidRaidTarget(Pawn candidate)
        {
            if (candidate == null || candidate == Pawn || candidate.Dead || candidate.Destroyed || !candidate.Spawned)
            {
                return false;
            }

            if (!candidate.HostileTo(Pawn))
            {
                return false;
            }

            return true;
        }

        private bool IsValidRaidAttackTarget(Thing target, bool lockToKillOrders)
        {
            if (target is Pawn pawn)
            {
                return IsValidRaidTarget(pawn);
            }

            return !lockToKillOrders && target is Building building && IsValidRetaliatoryBuilding(building);
        }

        private bool ShouldPressAttackBeforeSkill(Pawn target, int currentTick)
        {
            if (!IsValidRaidTarget(target) || damageAppliedSinceSwitch)
            {
                return false;
            }

            if (currentTick - lastTargetSwitchTick >= ZhaoliScenarioUtility.RaidOpeningAttackTicks)
            {
                return false;
            }

            if (Pawn.Position.DistanceToSquared(target.Position) > 64f)
            {
                return false;
            }

            DeathFieldClusterInfo clusterInfo = AnalyzeDeathFieldCluster(target.Position, target);
            return !IsHighValueDeathFieldCluster(clusterInfo);
        }

        private bool CanUseRaidSkill(int currentTick)
        {
            if (IsExecutingAbilityJob())
            {
                return false;
            }

            return currentTick - lastRaidSkillTick >= ZhaoliScenarioUtility.RaidSkillWeaveTicks;
        }

        private void MarkRaidSkillUsed(int currentTick)
        {
            lastRaidSkillTick = currentTick;
        }

        private IntVec3 FindBestDeathFieldCenter(Pawn preferredTarget, Ability ability, out DeathFieldClusterInfo bestInfo)
        {
            bestInfo = new DeathFieldClusterInfo();
            IntVec3 bestCenter = IntVec3.Invalid;
            int bestScore = int.MinValue;
            if (Pawn?.MapHeld == null || ability == null)
            {
                return bestCenter;
            }

            TryConsiderDeathFieldCenter(preferredTarget.Position, preferredTarget, ability, ref bestCenter, ref bestInfo, ref bestScore);
            IReadOnlyList<Pawn> pawns = Pawn.MapHeld.mapPawns.AllPawnsSpawned;
            for (int index = 0; index < pawns.Count; index++)
            {
                Pawn candidate = pawns[index];
                if (!IsValidRaidTarget(candidate))
                {
                    continue;
                }

                TryConsiderDeathFieldCenter(candidate.Position, preferredTarget, ability, ref bestCenter, ref bestInfo, ref bestScore);
            }

            return bestCenter;
        }

        private void TryConsiderDeathFieldCenter(IntVec3 center, Pawn preferredTarget, Ability ability, ref IntVec3 bestCenter, ref DeathFieldClusterInfo bestInfo, ref int bestScore)
        {
            if (!center.IsValid)
            {
                return;
            }

            LocalTargetInfo targetInfo = new LocalTargetInfo(center);
            if (!ability.CanApplyOn(targetInfo))
            {
                return;
            }

            DeathFieldClusterInfo clusterInfo = AnalyzeDeathFieldCluster(center, preferredTarget);
            if (clusterInfo.validTargets <= 0)
            {
                return;
            }

            int score = ScoreDeathFieldCluster(center, clusterInfo);
            if (score > bestScore)
            {
                bestScore = score;
                bestCenter = center;
                bestInfo = clusterInfo;
            }
        }

        private DeathFieldClusterInfo AnalyzeDeathFieldCluster(IntVec3 center, Pawn preferredTarget)
        {
            DeathFieldClusterInfo info = new DeathFieldClusterInfo();
            if (Pawn?.MapHeld == null || !center.IsValid)
            {
                return info;
            }

            IReadOnlyList<Pawn> pawns = Pawn.MapHeld.mapPawns.AllPawnsSpawned;
            for (int index = 0; index < pawns.Count; index++)
            {
                Pawn candidate = pawns[index];
                if (!IsValidDeathFieldVictim(candidate, center, ZhaoliScenarioUtility.DeathFieldEvaluationRadius))
                {
                    continue;
                }

                info.validTargets++;
                if (candidate.RaceProps.IsFlesh)
                {
                    info.livingTargets++;
                }

                if (candidate.RaceProps.Humanlike)
                {
                    info.humanlikeTargets++;
                }

                if (candidate == preferredTarget)
                {
                    info.containsCurrentTarget = true;
                }
            }

            return info;
        }

        private bool IsValidDeathFieldVictim(Pawn candidate, IntVec3 center, float radius)
        {
            if (!IsValidRaidTarget(candidate) || !candidate.Position.InHorDistOf(center, radius))
            {
                return false;
            }

            return ZhaoliScenarioUtility.ShouldDeathFieldAffectTarget(Pawn, candidate);
        }

        private int ScoreDeathFieldCluster(IntVec3 center, DeathFieldClusterInfo info)
        {
            int score = info.validTargets * 3 + info.livingTargets * 8 + info.humanlikeTargets * 16;
            if (info.containsCurrentTarget)
            {
                score += 12;
            }

            score -= Mathf.RoundToInt(Pawn.Position.DistanceToSquared(center) * 0.05f);
            return score;
        }

        private bool ShouldUseDeathField(DeathFieldClusterInfo info, int currentTick)
        {
            if (IsHighValueDeathFieldCluster(info))
            {
                return true;
            }

            if (info.humanlikeTargets > 0)
            {
                return true;
            }

            if (info.validTargets >= Mathf.Max(3, PropsRaid.minPreferredCluster))
            {
                return true;
            }

            return info.containsCurrentTarget && damageAppliedSinceSwitch && currentTick - lastTargetSwitchTick >= ZhaoliScenarioUtility.DeathFieldSingleHumanlikeDelayTicks;
        }

        private bool IsHighValueDeathFieldCluster(DeathFieldClusterInfo info)
        {
            return info.livingTargets > ZhaoliScenarioUtility.DeathFieldDenseLivingThreshold;
        }

        private bool TryCastMinghuo(int currentTick)
        {
            if (IsExecutingAbilityJob())
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
            return TryStartRaidAbilityJob(ability, self, "Minghuo", currentTick);
        }

        private bool TryCastField(Pawn target, int currentTick)
        {
            Ability ability = Pawn.abilities?.GetAbility(MXZL_ZhaoliDefOf.MX_Zhaoli_DeathField, includeTemporary: true);
            if (ability == null || !ability.CanCast || target == null)
            {
                return false;
            }

            if (!CanUseRaidSkill(currentTick) || currentTick - lastDeathFieldTick < ZhaoliScenarioUtility.DeathFieldRecastGuardTicks)
            {
                return false;
            }

            DeathFieldClusterInfo clusterInfo;
            IntVec3 bestCenter = FindBestDeathFieldCenter(target, ability, out clusterInfo);
            if (!bestCenter.IsValid || !ShouldUseDeathField(clusterInfo, currentTick))
            {
                return false;
            }

            LocalTargetInfo targetInfo = new LocalTargetInfo(bestCenter);
            if (!TryStartRaidAbilityJob(ability, targetInfo, "DeathField", currentTick))
            {
                return false;
            }

            lastDeathFieldTick = currentTick;
            return true;
        }

        private bool TryCastMinshen(Pawn target, int currentTick)
        {
            Ability ability = Pawn.abilities?.GetAbility(MXZL_ZhaoliDefOf.MX_Zhaoli_Minshen, includeTemporary: true);
            ZhaoliKarmaUtility.ResetNoCooldownAbilityLock(ability);
            if (ability == null || !ability.CanCast || target == null)
            {
                return false;
            }

            if (!CanUseRaidSkill(currentTick))
            {
                return false;
            }

            LocalTargetInfo targetInfo = new LocalTargetInfo(target.Position);
            int nearbyHostiles = CountHostilePawnsAround(target.Position, 6.5f);
            if (nearbyHostiles < 2 && !target.RaceProps.Humanlike)
            {
                return false;
            }

            return TryStartRaidAbilityJob(ability, targetInfo, "Minshen", currentTick);
        }

        private bool TryUseGapCloser(Thing target, int currentTick)
        {
            if (!(target is Pawn targetPawn) || !CanUseRaidSkill(currentTick))
            {
                return false;
            }

            Ability ability = Pawn.abilities?.GetAbility(MXZL_ZhaoliDefOf.MX_Zhaoli_Duanzhan, includeTemporary: true);
            if (ability == null || !ability.CanCast)
            {
                return false;
            }

            CompProperties_AbilityDuanzhan duanzhanProps = ability.def?.comps?.OfType<CompProperties_AbilityDuanzhan>().FirstOrDefault();
            float maxEffectiveDistance = Mathf.Max(
                ability.verb?.EffectiveRange ?? 0f,
                duanzhanProps?.lineLengthCells ?? 0f,
                duanzhanProps?.impactRadius ?? 0f) + 0.5f;
            if (Pawn.Position.DistanceTo(targetPawn.Position) > maxEffectiveDistance)
            {
                ZhaoliRaidDebugUtility.Log(Pawn, "DuanzhanSkip", "target too far distance=" + Pawn.Position.DistanceTo(targetPawn.Position).ToString("0.##") + " max=" + maxEffectiveDistance.ToString("0.##"));
                return false;
            }

            LocalTargetInfo targetInfo = new LocalTargetInfo(targetPawn.Position);
            return TryStartRaidAbilityJob(ability, targetInfo, "Duanzhan", currentTick);
        }

        private bool TryStartRaidAbilityJob(Ability ability, LocalTargetInfo targetInfo, string source, int currentTick)
        {
            if (!CanStartRaidAbilityJob(ability, targetInfo, source))
            {
                return false;
            }

            Job job = ability.GetJob(targetInfo, targetInfo);
            job.playerForced = true;
            if (!TryStartRaidJob(job, source))
            {
                return false;
            }

            MarkRaidSkillUsed(currentTick);
            return true;
        }

        private bool CanStartRaidAbilityJob(Ability ability, LocalTargetInfo targetInfo, string source)
        {
            if (ability == null || !ability.CanCast || !targetInfo.IsValid)
            {
                return false;
            }

            if (!ability.CanApplyOn(targetInfo))
            {
                ZhaoliRaidDebugUtility.Log(Pawn, "RaidAbilitySkip", "source=" + source + " rejected by CanApplyOn target=" + targetInfo.Cell);
                return false;
            }

            Verb verb = ability.verb;
            if (verb == null || !verb.ValidateTarget(targetInfo, showMessages: false))
            {
                ZhaoliRaidDebugUtility.Log(Pawn, "RaidAbilitySkip", "source=" + source + " rejected by ValidateTarget target=" + targetInfo.Cell);
                return false;
            }

            return true;
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

        private int CountHostilePawnsAround(IntVec3 center, float radius)
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

            if (IsExecutingAbilityJob())
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
            TryStartRaidJob(job, target is Pawn ? "AttackPawn" : "AttackBuilding");
        }

        private bool TryStartRaidJob(Job job, string source)
        {
            if (job == null || Pawn?.jobs == null)
            {
                return false;
            }

            if (Pawn.CurJob != null && Pawn.CurJob.JobIsSameAs(Pawn, job))
            {
                ZhaoliRaidDebugUtility.Log(Pawn, "RaidJobSkip", "source=" + source + " requested same job " + ZhaoliRaidDebugUtility.DescribeJob(job));
                return true;
            }

            ZhaoliRaidDebugUtility.Log(
                Pawn,
                "RaidJobStart",
                "source=" + source +
                " current=" + ZhaoliRaidDebugUtility.DescribeJob(Pawn.CurJob) +
                " requested=" + ZhaoliRaidDebugUtility.DescribeJob(job));
            Pawn.jobs.StartJob(job, JobCondition.InterruptForced, jobGiver: null, resumeCurJobAfterwards: false, cancelBusyStances: true, thinkTree: null, tag: JobTag.Misc, fromQueue: false, canReturnCurJobToPool: false, keepCarryingThingOverride: null, continueSleeping: false, addToJobsThisTick: true, preToilReservationsCanFail: true);
            return true;
        }
    }

    internal sealed class LordJob_ZhaoliRaidAnchor : LordJob
    {
        public override bool AddFleeToil => false;

        public override StateGraph CreateGraph()
        {
            StateGraph stateGraph = new StateGraph();
            stateGraph.AddToil(new LordToil_ZhaoliRaidAnchor());
            return stateGraph;
        }
    }

    internal sealed class LordToil_ZhaoliRaidAnchor : LordToil
    {
        public override bool AssignsDuties => true;

        public override void UpdateAllDuties()
        {
            for (int index = 0; index < lord.ownedPawns.Count; index++)
            {
                Pawn pawn = lord.ownedPawns[index];
                if (pawn?.mindState != null)
                {
                    pawn.mindState.duty = new PawnDuty(DutyDefOf.AssaultColony);
                    pawn.mindState.duty.attackDownedIfStarving = false;
                    pawn.mindState.duty.pickupOpportunisticWeapon = false;
                    pawn.TryGetComp<CompCanBeDormant>()?.WakeUp();
                }
            }
        }
    }

    internal static class ZhaoliRaidDebugUtility
    {
        private const string Prefix = "[MiliraXian.Characters.Zhaoli][RaidAI]";

        public static bool ShouldLog(Pawn pawn)
        {
            return pawn != null && pawn.Spawned && ZhaoliScenarioUtility.IsRaidState(pawn);
        }

        public static void Log(Pawn pawn, string stage, string message)
        {
            if (!ShouldLog(pawn))
            {
                return;
            }

            int currentTick = Find.TickManager?.TicksGame ?? -1;
            Verse.Log.Message(Prefix + "[" + currentTick + "][" + stage + "] " + DescribePawn(pawn) + " :: " + message);
        }

        public static void LogUnexpectedState(Pawn pawn, string source)
        {
            if (!ShouldLog(pawn))
            {
                return;
            }

            string lordJob = pawn.GetLord()?.LordJob?.GetType().Name ?? "null";
            string duty = DescribeDuty(pawn);
            Job currentJob = pawn.CurJob;
            bool invalidController = pawn.mindState?.duty == null || lordJob != nameof(LordJob_ZhaoliRaidAnchor);
            bool abnormalJob = currentJob != null && (currentJob.def == JobDefOf.Wait_Downed || currentJob.def == JobDefOf.Flee || (currentJob.def == JobDefOf.Goto && currentJob.exitMapOnArrival));
            if (invalidController || abnormalJob)
            {
                Log(
                    pawn,
                    source,
                    "unexpected control state currentJob=" + DescribeJob(pawn.CurJob) +
                    " duty=" + duty +
                    " lord=" + lordJob);
            }
        }

        public static string DescribePawn(Pawn pawn)
        {
            if (pawn == null)
            {
                return "pawn=null";
            }

            return pawn.LabelShort + "/" + pawn.ThingID + " pos=" + pawn.Position + " lord=" + (pawn.GetLord()?.LordJob?.GetType().Name ?? "null");
        }

        public static string DescribeDuty(Pawn pawn)
        {
            PawnDuty duty = pawn?.mindState?.duty;
            if (duty == null)
            {
                return "null";
            }

            return (duty.def?.defName ?? "null") +
                "(focus=" + DescribeLocalTarget(duty.focus) +
                ", focus2=" + DescribeLocalTarget(duty.focusSecond) +
                ", radius=" + duty.radius.ToString("0.##") + ")";
        }

        public static string DescribeJob(Job job)
        {
            if (job == null)
            {
                return "null";
            }

            return (job.def?.defName ?? "null") +
                "(A=" + DescribeLocalTarget(job.targetA) +
                ", B=" + DescribeLocalTarget(job.targetB) +
                ", C=" + DescribeLocalTarget(job.targetC) +
                ", expiry=" + job.expiryInterval +
                ", playerForced=" + job.playerForced +
                ", jobGiver=" + (job.jobGiver?.GetType().Name ?? "null") + ")";
        }

        public static string DescribeThing(Thing thing)
        {
            if (thing == null)
            {
                return "null";
            }

            if (thing is Pawn pawn)
            {
                return pawn.LabelShort + "/" + pawn.ThingID + " pos=" + pawn.Position + " dead=" + pawn.Dead;
            }

            return (thing.def?.defName ?? "null") + "/" + thing.ThingID + " pos=" + thing.PositionHeld;
        }

        public static Pawn GetTrackedPawn(Pawn_JobTracker tracker)
        {
            return Traverse.Create(tracker).Field("pawn").GetValue<Pawn>();
        }

        private static string DescribeLocalTarget(LocalTargetInfo target)
        {
            if (!target.IsValid)
            {
                return "invalid";
            }

            if (target.Thing != null)
            {
                return DescribeThing(target.Thing);
            }

            return target.Cell.IsValid ? target.Cell.ToString() : target.ToString();
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
        [HarmonyPriority(Priority.First)]
        public static bool Prefix(Pawn __instance, ref bool absorbed)
        {
            if (__instance == null)
            {
                return true;
            }

            if (ZhaoliScenarioUtility.IsHideoutState(__instance))
            {
                absorbed = true;
                return false;
            }

            HediffComp_ZhaoliRaidState raidComp = ZhaoliScenarioUtility.GetRaidStateComp(__instance);
            if (raidComp != null && raidComp.IsTransitionInvulnerable())
            {
                absorbed = true;
                return false;
            }

            return true;
        }

        public static void Postfix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (__instance == null || absorbed)
            {
                return;
            }

            HediffComp_ZhaoliRaidState raidComp = ZhaoliScenarioUtility.GetRaidStateComp(__instance);
            raidComp?.RegisterIncomingAggro(dinfo.Instigator, dinfo.Amount);
        }
    }

    [HarmonyPatch(typeof(Pawn), nameof(Pawn.DropAndForbidEverything))]
    internal static class Patch_Pawn_DropAndForbidEverything_ZhaoliWeapon
    {
        public sealed class ZhaoliWeaponDropState
        {
            public ThingWithComps weapon;
            public bool stashInInventory;
        }

        public static void Prefix(Pawn __instance, out ZhaoliWeaponDropState __state)
        {
            __state = null;
            if (!ZhaoliKarmaUtility.IsZhaoli(__instance))
            {
                return;
            }

            bool isBeingKilled = __instance.health?.isBeingKilled == true;
            bool isDownedDrop = !isBeingKilled && __instance.Downed;
            if (!isBeingKilled && !isDownedDrop)
            {
                return;
            }

            ThingWithComps primary = __instance.equipment?.Primary;
            if (primary != null && primary.def == MXZL_ZhaoliDefOf.MX_Zhaoli_DuanzhanBlade)
            {
                __instance.equipment.Remove(primary);
                __state = new ZhaoliWeaponDropState
                {
                    weapon = primary,
                    stashInInventory = isDownedDrop
                };
                return;
            }

            if (isBeingKilled && TryRemoveStoredZhaoliWeaponFromInventory(__instance, out ThingWithComps storedWeapon))
            {
                __state = new ZhaoliWeaponDropState
                {
                    weapon = storedWeapon,
                    stashInInventory = false
                };
            }
        }

        public static void Postfix(Pawn __instance, ZhaoliWeaponDropState __state)
        {
            if (__state?.weapon == null || __state.weapon.Destroyed || __instance == null || __instance.Destroyed)
            {
                return;
            }

            if (__state.stashInInventory && __instance.inventory?.innerContainer != null && !__instance.Dead)
            {
                __instance.mindState.droppedWeapon = null;
                if (__instance.inventory.innerContainer.TryAdd(__state.weapon, canMergeWithExistingStacks: false))
                {
                    return;
                }
            }

            if (__instance.equipment == null || __instance.equipment.Primary != null)
            {
                return;
            }

            ZhaoliBladeVerbLoadFixUtility.EnsureCleanVerbTracker(__state.weapon);
            __instance.equipment.AddEquipment(__state.weapon);
        }

        internal static void TryReequipStoredZhaoliWeapon(Pawn pawn)
        {
            if (pawn == null || pawn.Destroyed || pawn.Dead || pawn.Downed || !ZhaoliKarmaUtility.IsZhaoli(pawn) || pawn.equipment == null || pawn.equipment.Primary != null)
            {
                return;
            }

            if (!TryRemoveStoredZhaoliWeaponFromInventory(pawn, out ThingWithComps weapon))
            {
                return;
            }

            ZhaoliBladeVerbLoadFixUtility.EnsureCleanVerbTracker(weapon);
            pawn.equipment.AddEquipment(weapon);
        }

        private static bool TryRemoveStoredZhaoliWeaponFromInventory(Pawn pawn, out ThingWithComps weapon)
        {
            weapon = null;
            if (pawn?.inventory?.innerContainer == null)
            {
                return false;
            }

            foreach (Thing thing in pawn.inventory.innerContainer)
            {
                if (thing is ThingWithComps thingWithComps && thingWithComps.def == MXZL_ZhaoliDefOf.MX_Zhaoli_DuanzhanBlade)
                {
                    weapon = thingWithComps;
                    break;
                }
            }

            if (weapon == null)
            {
                return false;
            }

            pawn.inventory.innerContainer.Remove(weapon);
            return true;
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

    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeDowned")]
    internal static class Patch_Pawn_HealthTracker_MakeDowned_ZhaoliRaidBlock
    {
        public static bool Prefix(Pawn ___pawn, DamageInfo? dinfo, Hediff hediff)
        {
            if (___pawn == null || ___pawn.Dead || !ZhaoliScenarioUtility.IsRaidState(___pawn))
            {
                return true;
            }

            ZhaoliRaidDebugUtility.Log(___pawn, "NoDowned", "prevented MakeDowned and forced death instead");
            ___pawn.Kill(dinfo, hediff);
            return false;
        }
    }

    [HarmonyPatch(typeof(Pawn_HealthTracker), "MakeUndowned")]
    internal static class Patch_Pawn_HealthTracker_MakeUndowned_ZhaoliWeapon
    {
        public static void Postfix(Pawn ___pawn)
        {
            Patch_Pawn_DropAndForbidEverything_ZhaoliWeapon.TryReequipStoredZhaoliWeapon(___pawn);
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.StartJob))]
    internal static class Patch_Pawn_JobTracker_StartJob_ZhaoliRaidLog
    {
        internal sealed class StartJobLogState
        {
            public Pawn pawn;
            public string previousJob;
        }

        public static void Prefix(Pawn_JobTracker __instance, Job newJob, JobCondition lastJobEndCondition, ThinkNode jobGiver, ThinkTreeDef thinkTree, JobTag? tag, bool fromQueue, out StartJobLogState __state)
        {
            Pawn pawn = ZhaoliRaidDebugUtility.GetTrackedPawn(__instance);
            __state = new StartJobLogState
            {
                pawn = pawn,
                previousJob = ZhaoliRaidDebugUtility.DescribeJob(pawn?.CurJob)
            };
            if (!ZhaoliRaidDebugUtility.ShouldLog(pawn))
            {
                return;
            }

            ZhaoliRaidDebugUtility.Log(
                pawn,
                "StartJobRequest",
                "prev=" + __state.previousJob +
                " new=" + ZhaoliRaidDebugUtility.DescribeJob(newJob) +
                " endCond=" + lastJobEndCondition +
                " jobGiver=" + (jobGiver?.GetType().Name ?? "null") +
                " thinkTree=" + (thinkTree?.defName ?? "null") +
                " tag=" + (tag.HasValue ? tag.Value.ToString() : "null") +
                " fromQueue=" + fromQueue +
                " duty=" + ZhaoliRaidDebugUtility.DescribeDuty(pawn));
        }

        public static void Postfix(Pawn_JobTracker __instance, StartJobLogState __state)
        {
            Pawn pawn = __state?.pawn ?? ZhaoliRaidDebugUtility.GetTrackedPawn(__instance);
            if (!ZhaoliRaidDebugUtility.ShouldLog(pawn))
            {
                return;
            }

            ZhaoliRaidDebugUtility.Log(
                pawn,
                "StartJobResult",
                "prev=" + (__state?.previousJob ?? "null") +
                " cur=" + ZhaoliRaidDebugUtility.DescribeJob(pawn.CurJob) +
                " duty=" + ZhaoliRaidDebugUtility.DescribeDuty(pawn));
        }
    }

    [HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.EndCurrentJob))]
    internal static class Patch_Pawn_JobTracker_EndCurrentJob_ZhaoliRaidLog
    {
        public static void Prefix(Pawn_JobTracker __instance, JobCondition condition, bool startNewJob)
        {
            Pawn pawn = ZhaoliRaidDebugUtility.GetTrackedPawn(__instance);
            if (!ZhaoliRaidDebugUtility.ShouldLog(pawn))
            {
                return;
            }

            ZhaoliRaidDebugUtility.Log(
                pawn,
                "EndCurrentJob",
                "condition=" + condition +
                " startNewJob=" + startNewJob +
                " cur=" + ZhaoliRaidDebugUtility.DescribeJob(pawn.CurJob) +
                " duty=" + ZhaoliRaidDebugUtility.DescribeDuty(pawn));
        }
    }

    [HarmonyPatch(typeof(ZhaoliKarmaUtility), nameof(ZhaoliKarmaUtility.AddKarma))]
    internal static class Patch_ZhaoliKarmaUtility_AddKarma_RaidCap
    {
        public static bool Prefix(Pawn pawn, float value)
        {
            if (!ZhaoliRebirthUtility.ShouldBlockTenDayRebirth(pawn))
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

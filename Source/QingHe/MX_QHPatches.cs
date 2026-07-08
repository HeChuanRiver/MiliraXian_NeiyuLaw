using HarmonyLib;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Stats;
using MiliraXian.Characters.QingHe.Things;
using RimWorld;
using System.Collections.Generic;
using UnityEngine;
using Verse;
using Verse.AI;

namespace MiliraXian.Characters.QingHe
{
    [StaticConstructorOnStartup]
    public static class MX_QHPatches
    {
        private static readonly Harmony patcher = new Harmony("MiliraXian.Characters.QingHe");

        static MX_QHPatches()
        {
            RegisterFlowerBellCorrosionArmorStatParts();

            patcher.Patch(AccessTools.Method(typeof(StartingPawnUtility), nameof(StartingPawnUtility.NewGeneratedStartingPawn)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_StartingPawnUtility_NewGeneratedStartingPawn_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(PawnGenerator), nameof(PawnGenerator.GeneratePawn), new[] { typeof(PawnGenerationRequest) }),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_PawnGenerator_GeneratePawn_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.SpawnSetup)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Pawn_SpawnSetup_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.PreApplyDamage)),
                prefix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Pawn_PreApplyDamage_Prefix))
                {
                    priority = Priority.First
                },
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Pawn_PreApplyDamage_Postfix))
                {
                    priority = Priority.Last
                });

            patcher.Patch(AccessTools.Method(typeof(InspirationWorker), nameof(InspirationWorker.CommonalityFor)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_InspirationWorker_CommonalityFor_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(Bill), nameof(Bill.PawnAllowedToStartAnew)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Bill_PawnAllowedToStartAnew_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(MeditationUtility), nameof(MeditationUtility.AllMeditationSpotCandidates)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_MeditationUtility_AllMeditationSpotCandidates_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(MeditationUtility), nameof(MeditationUtility.GetMeditationJob)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_MeditationUtility_GetMeditationJob_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(JobDriver_Meditate), "MeditationTick"),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_JobDriver_Meditate_MeditationTick_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(Book), nameof(Book.OnBookReadTick), new[] { typeof(Pawn), typeof(int), typeof(float) }),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Book_OnBookReadTick_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(JobDriver_PlayMusicalInstrument), "ModifyPlayToil", new[] { typeof(Toil) }),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_JobDriver_PlayMusicalInstrument_ModifyPlayToil_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(QualityUtility), nameof(QualityUtility.GenerateQualityCreatedByPawn), new[] { typeof(Pawn), typeof(SkillDef), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_QualityUtility_GenerateQualityCreatedByPawn_Postfix)));

            patcher.Patch(
                AccessTools.Method(typeof(Projectile), "CheckForFreeInterceptBetween", new[] { typeof(Vector3), typeof(Vector3) }),
                prefix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Projectile_CheckForFreeInterceptBetween_Prefix)));

            patcher.Patch(AccessTools.Method(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.AddDirectRelation)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_PawnRelationsTracker_AddDirectRelation_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.TryRemoveDirectRelation)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_PawnRelationsTracker_TryRemoveDirectRelation_Postfix)));

        }

        public static void Patch_StartingPawnUtility_NewGeneratedStartingPawn_Postfix(Pawn __result)
        {
            if (!MX_QHCharacterUtility.IsQinghe(__result))
            {
                return;
            }

            MX_QHCharacterUtility.MarkForLoadoutStabilization(__result);
            MX_QHCharacterUtility.EnsureDefaultLoadout(__result);
        }

        public static void Patch_PawnGenerator_GeneratePawn_Postfix(ref Pawn __result)
        {
            if (!MX_QHCharacterUtility.IsQinghe(__result))
            {
                return;
            }

            MX_QHCharacterUtility.EnsureDefaultLoadout(__result);
        }

        public static void Patch_Pawn_SpawnSetup_Postfix(Pawn __instance)
        {
            if (!MX_QHCharacterUtility.IsQinghe(__instance))
            {
                return;
            }

            EnsureQingheCoreTraits(__instance);
            MX_QH_HediffUtility.EnsureCoreHediffs(__instance);
            MX_QHSkillUtility.SyncChoices(__instance);
            if (MX_QHCharacterUtility.ShouldFinalizeLoadout(__instance))
            {
                MX_QHCharacterUtility.EnsureDefaultLoadout(__instance);
                MX_QHCharacterUtility.ClearLoadoutStabilization(__instance);
            }
        }

        public static bool Patch_Pawn_PreApplyDamage_Prefix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (__instance?.health?.hediffSet == null)
            {
                return true;
            }

            if (dinfo.Amount <= 0f)
            {
                return true;
            }

            if (!HasDamageImmunityHediff(__instance))
            {
                return true;
            }

            dinfo.SetAmount(0f);
            absorbed = true;
            return false;
        }

        private static bool HasDamageImmunityHediff(Pawn pawn)
        {
            if (pawn?.health?.hediffSet == null)
            {
                return false;
            }

            return (MX_QHDefOf.MX_QH_DivineBlessingImmunity != null
                    && pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_DivineBlessingImmunity) != null)
                || (MX_QHDefOf.MX_QH_AscentSlashInvulnerable != null
                    && pawn.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_AscentSlashInvulnerable) != null);
        }

        public static void Patch_Pawn_PreApplyDamage_Postfix(Pawn __instance, ref DamageInfo dinfo, ref bool absorbed)
        {
            if (__instance?.health?.hediffSet == null)
            {
                return;
            }

            Hediff divineBlessing = __instance.health.hediffSet.GetFirstHediffOfDef(MX_QHDefOf.MX_QH_DivineBlessing);
            HediffComp_DivineBlessing divineBlessingComp = divineBlessing?.TryGetComp<HediffComp_DivineBlessing>();
            if (divineBlessingComp == null)
            {
                return;
            }

            // Lotus shield is processed by pawn ThingComp.PostPreApplyDamage.
            // Divine blessing only checks when damage still reaches the body.
            if (absorbed)
            {
                return;
            }

            divineBlessingComp.NotifyDamageNotAbsorbed(ref dinfo);

            if (!divineBlessingComp.CanTrigger(ref dinfo))
            {
                return;
            }

            divineBlessingComp.Trigger(ref dinfo, ref absorbed);
        }

        public static void Patch_InspirationWorker_CommonalityFor_Postfix(InspirationWorker __instance, Pawn pawn, ref float __result)
        {
            if (pawn?.story?.traits == null || __instance?.def == null)
            {
                return;
            }
            if (__instance.def == MX_QHDefOf.Frenzy_Work || __instance.def == MX_QHDefOf.Inspired_Creativity)
            {
                __result *= 2f;
            }
        }

        public static void Patch_Bill_PawnAllowedToStartAnew_Postfix(Bill __instance, Pawn p, ref bool __result)
        {
            if (!__result || __instance?.recipe == null)
            {
                return;
            }

            MX_QingheRecipeRequirementExtension extension = __instance.recipe.GetModExtension<MX_QingheRecipeRequirementExtension>();
            if (extension?.allowedPawnKinds.NullOrEmpty() != false)
            {
                return;
            }

            if (p?.kindDef != null && extension.allowedPawnKinds.Contains(p.kindDef))
            {
                return;
            }

            JobFailReason.Is(extension.failureReasonKey.Translate());
            __result = false;
        }

        public static void Patch_MeditationUtility_AllMeditationSpotCandidates_Postfix(
            Pawn pawn,
            bool allowFallbackSpots,
            ref IEnumerable<LocalTargetInfo> __result)
        {
            __result = AppendQingheLotusPondMeditationSpots(__result, pawn, allowFallbackSpots);
        }

        private static IEnumerable<LocalTargetInfo> AppendQingheLotusPondMeditationSpots(
            IEnumerable<LocalTargetInfo> original,
            Pawn pawn,
            bool allowFallbackSpots)
        {
            bool yieldedAny = false;
            foreach (LocalTargetInfo target in original)
            {
                yieldedAny = true;
                yield return target;
            }

            if (!MX_QHCharacterUtility.IsQinghe(pawn) || pawn?.Map == null || pawn.IsPrisonerOfColony)
            {
                yield break;
            }

            foreach (Building building in pawn.Map.listerBuildings.AllBuildingsColonistOfDef(MX_QHDefOf.MX_QH_LotusPond))
            {
                if (building == null || !MeditationUtility.IsValidMeditationBuildingForPawn(building, pawn))
                {
                    continue;
                }

                if (!allowFallbackSpots && building.GetAssignedPawn() != pawn)
                {
                    continue;
                }

                if (yieldedAny && building.GetAssignedPawn() != pawn)
                {
                    continue;
                }

                yield return building;
            }
        }

        public static void Patch_MeditationUtility_GetMeditationJob_Postfix(Pawn pawn, ref Job __result)
        {
            if (__result == null || !MX_QHCharacterUtility.IsQinghe(pawn))
            {
                return;
            }

            Building lotusPond = __result.GetTarget(TargetIndex.A).Thing as Building;
            if (lotusPond == null || lotusPond.def != MX_QHDefOf.MX_QH_LotusPond)
            {
                return;
            }

            IntVec3 cell = lotusPond.InteractionCell;
            if (!cell.IsValid
                || !cell.InBounds(lotusPond.Map)
                || !cell.Standable(lotusPond.Map)
                || cell.IsForbidden(pawn)
                || !pawn.CanReserveAndReach(cell, PathEndMode.OnCell, pawn.NormalMaxDanger()))
            {
                __result = null;
                return;
            }

            __result.SetTarget(TargetIndex.A, cell);
        }

        public static void Patch_JobDriver_Meditate_MeditationTick_Postfix(JobDriver_Meditate __instance)
        {
            Pawn pawn = __instance?.pawn;
            if (!MX_QHCharacterUtility.IsQinghe(pawn) || pawn?.Map == null)
            {
                return;
            }

            Building lotusPond = ResolveMeditatingLotusPond(pawn);
            if (lotusPond == null)
            {
                return;
            }

            MX_QH_HediffUtility.AddMeditativeStillnessFromLotusPond(pawn, lotusPond);
        }

        public static void Patch_Book_OnBookReadTick_Postfix(Pawn pawn, int delta, float roomBonusFactor)
        {
            MX_QH_HediffUtility.AddMeditativeStillnessFromReading(pawn, delta, roomBonusFactor);
        }

        public static void Patch_JobDriver_PlayMusicalInstrument_ModifyPlayToil_Postfix(JobDriver_PlayMusicalInstrument __instance, Toil toil)
        {
            toil?.AddPreTickIntervalAction(delta => ApplyQingheInstrumentPerformance(__instance, delta));
        }

        public static void Patch_QualityUtility_GenerateQualityCreatedByPawn_Postfix(Pawn pawn, ref QualityCategory __result)
        {
            if (!MX_QHCharacterUtility.IsQinghe(pawn))
            {
                return;
            }

            MX_QH_HediffUtility.ApplyMeditativeStillnessQualityBonus(pawn, ref __result);
        }

        public static bool Patch_Projectile_CheckForFreeInterceptBetween_Prefix(
            Projectile __instance,
            Vector3 lastExactPos,
            Vector3 newExactPos,
            ref bool __result)
        {
            if (__instance?.Map == null || __instance.Destroyed || MX_QHDefOf.MX_QH_LunarMirror == null)
            {
                return true;
            }

            var shields = __instance.Map.listerThings.ThingsOfDef(MX_QHDefOf.MX_QH_LunarMirror);
            for (int i = 0; i < shields.Count; i++)
            {
                if (shields[i]?.TryGetComp<CompLunarMirrorShield>()?.TryInterceptProjectile(__instance, lastExactPos, newExactPos) == true)
                {
                    GenClamor.DoClamor(__instance, 12f, ClamorDefOf.Impact);
                    __instance.Destroy();
                    __result = true;
                    return false;
                }
            }

            return true;
        }

        private static Building ResolveMeditatingLotusPond(Pawn pawn)
        {
            Job job = pawn?.CurJob;
            if (job == null || pawn.Map == null || MX_QHDefOf.MX_QH_LotusPond == null)
            {
                return null;
            }

            LocalTargetInfo target = job.GetTarget(TargetIndex.A);
            Building assignedLotusPond = pawn.ownership?.AssignedMeditationSpot as Building;
            if (IsMeditatingAtLotusPond(pawn, assignedLotusPond, target))
            {
                return assignedLotusPond;
            }

            foreach (Building building in pawn.Map.listerBuildings.AllBuildingsColonistOfDef(MX_QHDefOf.MX_QH_LotusPond))
            {
                if (IsMeditatingAtLotusPond(pawn, building, target))
                {
                    return building;
                }
            }

            return null;
        }

        private static bool IsMeditatingAtLotusPond(Pawn pawn, Building lotusPond, LocalTargetInfo target)
        {
            if (pawn == null || lotusPond == null || lotusPond.def != MX_QHDefOf.MX_QH_LotusPond || lotusPond.Map != pawn.Map)
            {
                return false;
            }

            IntVec3 interactionCell = lotusPond.InteractionCell;
            if (!interactionCell.IsValid)
            {
                return false;
            }

            return target.Cell == interactionCell && pawn.Position == interactionCell;
        }

        private const int QingheInstrumentPerformanceIntervalTicks = 600;

        private const float QingheInstrumentAudienceJoyGain = 0.03f;

        private static void ApplyQingheInstrumentPerformance(JobDriver_PlayMusicalInstrument driver, int delta)
        {
            Pawn performer = driver?.pawn;
            if (!MX_QHCharacterUtility.IsQinghe(performer)
                || performer.Map == null
                || !performer.Spawned
                || !performer.IsHashIntervalTick(QingheInstrumentPerformanceIntervalTicks, delta))
            {
                return;
            }

            Room room = performer.GetRoom();
            if (room == null)
            {
                return;
            }

            JoyKindDef musicJoy = MX_QHDefOf.HighCulture ?? JoyKindDefOf.Social;
            IReadOnlyList<Pawn> pawns = performer.Map.mapPawns.AllPawnsSpawned;
            for (int i = 0; i < pawns.Count; i++)
            {
                Pawn target = pawns[i];
                if (target == null || target.Dead || target.GetRoom() != room)
                {
                    continue;
                }

                target.needs?.mood?.thoughts?.memories?.TryGainMemory(MX_QHDefOf.MX_QH_QingheInstrumentPerformance, performer);
                target.needs?.joy?.GainJoy(QingheInstrumentAudienceJoyGain, musicJoy);
            }
        }

        public static void Patch_PawnRelationsTracker_AddDirectRelation_Postfix(
            PawnRelationDef def,
            Pawn otherPawn,
            Pawn ___pawn)
        {
            if (def != PawnRelationDefOf.Spouse)
            {
                return;
            }

            HediffComp_LuoshenContract.NotifySpouseRelationAdded(___pawn, otherPawn);
        }

        public static void Patch_PawnRelationsTracker_TryRemoveDirectRelation_Postfix(
            PawnRelationDef def,
            Pawn otherPawn,
            bool __result,
            Pawn ___pawn)
        {
            if (!__result || def != PawnRelationDefOf.Spouse)
            {
                return;
            }

            HediffComp_LuoshenContract.NotifySpouseRelationRemoved(___pawn, otherPawn);
        }

        private static void EnsureQingheCoreTraits(Pawn pawn)
        {
            if (pawn?.story?.traits == null)
            {
                return;
            }

            if (MX_QHDefOf.MX_QH_Trait_LongBreath != null
                && !pawn.story.traits.HasTrait(MX_QHDefOf.MX_QH_Trait_LongBreath))
            {
                pawn.story.traits.GainTrait(new Trait(MX_QHDefOf.MX_QH_Trait_LongBreath));
            }

            if (MX_QHDefOf.MX_QH_Trait_WaterFairy != null
                && !pawn.story.traits.HasTrait(MX_QHDefOf.MX_QH_Trait_WaterFairy))
            {
                pawn.story.traits.GainTrait(new Trait(MX_QHDefOf.MX_QH_Trait_WaterFairy));
            }
        }

        private static void RegisterFlowerBellCorrosionArmorStatParts()
        {
            RegisterFlowerBellCorrosionArmorStatPart(StatDefOf.ArmorRating_Sharp);
            RegisterFlowerBellCorrosionArmorStatPart(StatDefOf.ArmorRating_Blunt);
            RegisterFlowerBellCorrosionArmorStatPart(StatDefOf.ArmorRating_Heat);
        }

        private static void RegisterFlowerBellCorrosionArmorStatPart(StatDef stat)
        {
            if (stat == null)
            {
                return;
            }

            if (stat.parts == null)
            {
                stat.parts = new List<StatPart>();
            }

            for (int i = 0; i < stat.parts.Count; i++)
            {
                if (stat.parts[i] is StatPart_FlowerBellCorrosionArmor)
                {
                    return;
                }
            }

            stat.parts.Add(new StatPart_FlowerBellCorrosionArmor
            {
                parentStat = stat
            });
        }
    }
}

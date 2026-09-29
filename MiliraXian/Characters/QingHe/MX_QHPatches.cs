using HarmonyLib;
using MiliraXian.Characters.QingHe.Abilities;
using MiliraXian.Characters.QingHe.Defs;
using MiliraXian.Characters.QingHe.Hediffs;
using MiliraXian.Characters.QingHe.Things;
using MiliraXian.Characters.QingHe.Jobs;
using MiliraXian.Characters.QingHe.Things.Weapons;
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
        private static readonly Harmony patcher = new("MiliraXian.Characters.QingHe");
        private static readonly AccessTools.FieldRef<Pawn_EquipmentTracker, Pawn> equipmentTrackerPawn =
            AccessTools.FieldRefAccess<Pawn_EquipmentTracker, Pawn>("pawn");

        static MX_QHPatches()
        {
            patcher.Patch(AccessTools.Method(typeof(Thing), nameof(Thing.TakeDamage)),
                prefix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Thing_TakeDamage_Prefix)));

            patcher.Patch(AccessTools.Method(typeof(Verb_MeleeAttackDamage), "ApplyMeleeDamageToTarget"),
                prefix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_VerbMeleeAttackDamage_ApplyMeleeDamageToTarget_Prefix)));

            patcher.Patch(AccessTools.Method(typeof(Pawn), nameof(Pawn.PreApplyDamage)),
                prefix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Pawn_PreApplyDamage_Prefix))
                {
                    priority = Priority.First
                },
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Pawn_PreApplyDamage_Postfix))
                {
                    priority = Priority.Last
                });

            patcher.Patch(
                AccessTools.Method(typeof(Verb_MeleeAttack), "SoundDodge", new[] { typeof(Thing) }),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_VerbMeleeAttack_SoundDodge_Postfix)));

            patcher.Patch(
                AccessTools.Method(typeof(Projectile), "ImpactSomething"),
                prefix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Projectile_ImpactSomething_Prefix)));

            patcher.Patch(
                AccessTools.Method(typeof(VerbProperties), nameof(VerbProperties.AdjustedArmorPenetration), new[] { typeof(Verb), typeof(Pawn) }),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_VerbProperties_AdjustedArmorPenetration_Postfix)));

            patcher.Patch(
                AccessTools.Method(typeof(PawnRenderUtility), nameof(PawnRenderUtility.DrawEquipmentAndApparelExtras)),
                prefix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_PawnRenderUtility_DrawEquipmentAndApparelExtras_Prefix)));

            patcher.Patch(AccessTools.Method(typeof(InspirationWorker), nameof(InspirationWorker.CommonalityFor)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_InspirationWorker_CommonalityFor_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(MeditationUtility), nameof(MeditationUtility.AllMeditationSpotCandidates)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_MeditationUtility_AllMeditationSpotCandidates_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(JobDriver_Meditate), "MeditationTick"),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_JobDriver_Meditate_MeditationTick_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.Notify_EquipmentAdded)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_EquipmentChanged_Postfix)));
            patcher.Patch(AccessTools.Method(typeof(Pawn_EquipmentTracker), nameof(Pawn_EquipmentTracker.Notify_EquipmentRemoved)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_EquipmentChanged_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(Book), nameof(Book.OnBookReadTick), new[] { typeof(Pawn), typeof(int), typeof(float) }),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Book_OnBookReadTick_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(JobDriver_LayDown), nameof(JobDriver_LayDown.LayDownToil), new[] { typeof(bool) }),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_JobDriver_LayDown_LayDownToil_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(JobDriver_PlayMusicalInstrument), "ModifyPlayToil", new[] { typeof(Toil) }),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_JobDriver_PlayMusicalInstrument_ModifyPlayToil_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(QualityUtility), nameof(QualityUtility.GenerateQualityCreatedByPawn), new[] { typeof(Pawn), typeof(SkillDef), typeof(bool) }),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_QualityUtility_GenerateQualityCreatedByPawn_Postfix)));
            patcher.Patch(AccessTools.Method(typeof(GenRecipe), "PostProcessProduct"),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_GenRecipe_PostProcessProduct_Postfix)));

            patcher.Patch(
                AccessTools.Method(typeof(Projectile), "CheckForFreeInterceptBetween", new[] { typeof(Vector3), typeof(Vector3) }),
                prefix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Projectile_CheckForFreeInterceptBetween_Prefix)));

            patcher.Patch(AccessTools.Method(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.AddDirectRelation)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_PawnRelationsTracker_AddDirectRelation_Postfix)));

            patcher.Patch(AccessTools.Method(typeof(Pawn_RelationsTracker), nameof(Pawn_RelationsTracker.TryRemoveDirectRelation)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_PawnRelationsTracker_TryRemoveDirectRelation_Postfix)));

            patcher.Patch(
                AccessTools.Method(typeof(Verse.Profile.MemoryUtility), nameof(Verse.Profile.MemoryUtility.ClearAllMapsAndWorld)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_MemoryUtility_ClearAllMapsAndWorld_Postfix)));

            patcher.Patch(
                AccessTools.Method(typeof(Pawn), nameof(Pawn.Kill)),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Pawn_Kill_Postfix)));

            patcher.Patch(
                AccessTools.Method(typeof(Pawn), nameof(Pawn.DeSpawn), new[] { typeof(DestroyMode) }),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_Pawn_DeSpawn_Postfix)));

            patcher.Patch(
                AccessTools.PropertyGetter(typeof(Pawn_DrawTracker), "DrawPos"),
                postfix: new HarmonyMethod(typeof(MX_QHPatches), nameof(Patch_PawnDrawTracker_DrawPos_Postfix)));

        }

        public static void Patch_MemoryUtility_ClearAllMapsAndWorld_Postfix()
        {
            CompAbilityEffect_AscentSlash.ClearActiveManagers();
        }

        public static void Patch_Pawn_Kill_Postfix(Pawn __instance)
        {
            CompAbilityEffect_AscentSlash.NotifyPawnUnavailable(__instance);
        }

        public static void Patch_Pawn_DeSpawn_Postfix(Pawn __instance)
        {
            CompAbilityEffect_AscentSlash.NotifyPawnUnavailable(__instance);
        }

        public static void Patch_PawnDrawTracker_DrawPos_Postfix(Pawn ___pawn, ref Vector3 __result)
        {
            if (___pawn == null || ___pawn.Destroyed || !___pawn.Spawned)
            {
                return;
            }

            CompAbilityEffect_AscentSlash.ApplyActiveActionDrawPos(___pawn, ref __result);
        }

        private struct DamageHediffState
        {
            public HediffComp_FlowingForm FlowingForm;
            public bool Invulnerable;
        }

        private static bool Patch_Pawn_PreApplyDamage_Prefix(
            Pawn __instance,
            ref DamageInfo dinfo,
            ref bool absorbed,
            out DamageHediffState __state)
        {
            __state = default(DamageHediffState);
            if (__instance?.health?.hediffSet == null)
            {
                return true;
            }

            if (dinfo.Amount <= 0f)
            {
                return true;
            }

            __state = ScanDamageHediffs(__instance.health.hediffSet.hediffs);

            JobDriver_IllusoryReflectionStance reflection = __instance.jobs?.curDriver as JobDriver_IllusoryReflectionStance;
            if (reflection?.TryHandleDamage(ref dinfo, ref absorbed) == true)
            {
                return false;
            }

            if (!__state.Invulnerable)
            {
                return true;
            }

            dinfo.SetAmount(0f);
            absorbed = true;
            return false;
        }

        private static DamageHediffState ScanDamageHediffs(List<Hediff> hediffs)
        {
            DamageHediffState state = default(DamageHediffState);
            if (hediffs == null)
            {
                return state;
            }

            for (int i = 0; i < hediffs.Count; i++)
            {
                Hediff hediff = hediffs[i];
                HediffDef hediffDef = hediff?.def;
                if (hediffDef == null)
                {
                    continue;
                }

                if (hediffDef == MX_QHDefOf.MX_QH_FlowingForm && state.FlowingForm == null)
                {
                    state.FlowingForm = hediff.TryGetComp<HediffComp_FlowingForm>();
                }

                if (hediffDef == MX_QHDefOf.MX_QH_FlowingFormImmunity
                    || hediffDef == MX_QHDefOf.MX_QH_AscentSlashInvulnerable
                    || hediffDef == MX_QHDefOf.MX_QH_IllusoryReflectionInvulnerable)
                {
                    state.Invulnerable = true;
                }
            }

            return state;
        }

        private static void Patch_Pawn_PreApplyDamage_Postfix(
            Pawn __instance,
            ref DamageInfo dinfo,
            ref bool absorbed,
            DamageHediffState __state)
        {
            if (__instance?.health?.hediffSet == null || absorbed)
            {
                return;
            }

            HediffComp_FlowingForm flowingFormComp = __state.FlowingForm;
            if (flowingFormComp == null)
            {
                return;
            }

            // Lotus shield is processed by pawn ThingComp.PostPreApplyDamage.
            // Divine blessing only checks when damage still reaches the body.
            flowingFormComp.NotifyDamageNotAbsorbed(ref dinfo);

            if (!flowingFormComp.CanTrigger(ref dinfo))
            {
                return;
            }

            flowingFormComp.Trigger(ref dinfo, ref absorbed);
        }

        public static void Patch_InspirationWorker_CommonalityFor_Postfix(InspirationWorker __instance, Pawn pawn, ref float __result)
        {
            if (!MX_QHCharacterUtility.IsQinghe(pawn) || __instance?.def == null)
            {
                return;
            }
            if (__instance.def == MX_QHDefOf.Frenzy_Work || __instance.def == MX_QHDefOf.Inspired_Creativity)
            {
                __result *= 2f;
            }
        }

        public static void Patch_MeditationUtility_AllMeditationSpotCandidates_Postfix(
            Pawn pawn,
            bool allowFallbackSpots,
            ref IEnumerable<LocalTargetInfo> __result)
        {
            if (!MX_QHCharacterUtility.IsQinghe(pawn))
            {
                return;
            }

            __result = AppendQingheLotusPondMeditationSpots(__result, pawn, allowFallbackSpots);
        }

        public static void Patch_EquipmentChanged_Postfix(Pawn_EquipmentTracker __instance)
        {
            Pawn pawn = equipmentTrackerPawn(__instance);
            if (MX_QHCharacterUtility.IsQinghe(pawn))
            {
                MX_QH_HediffUtility.GetCombatStance(pawn)?.RefreshStance();
            }
        }

        public static void Patch_VerbProperties_AdjustedArmorPenetration_Postfix(Verb ownerVerb, Pawn attacker, ref float __result)
        {
            if (MX_QH_HediffUtility.GetCombatStance(attacker)?.CurrentStance != MX_QHDefOf.MX_QH_Stance_Sword
                || QingheSwordCombatUtility.ResonanceFor(attacker) != FlowerBellResonance.Autumn
                || ownerVerb?.EquipmentSource?.def != MX_QHDefOf.MX_QH_Weapon_Sword)
            {
                return;
            }

            __result *= 1.5f;
        }

        public static bool Patch_PawnRenderUtility_DrawEquipmentAndApparelExtras_Prefix(Pawn pawn, Vector3 drawPos, Rot4 facing)
        {
            ThingWithComps weapon = pawn?.equipment?.Primary;
            JobDriver_IllusoryReflectionStance reflection = pawn?.jobs?.curDriver as JobDriver_IllusoryReflectionStance;
            if (reflection == null
                || MX_QH_HediffUtility.GetCombatStance(pawn)?.CurrentStance != MX_QHDefOf.MX_QH_Stance_Sword)
            {
                return true;
            }

            Rot4 stanceFacing = reflection.StanceFacing;
            float drawFactor = pawn.ageTracker.CurLifeStage.equipmentDrawDistanceFactor;
            float angle;
            Vector3 offset;
            switch (stanceFacing.AsInt)
            {
                case 0:
                    angle = 18f;
                    offset = new Vector3(0f, 0f, -0.08f);
                    break;
                case 1:
                    angle = 72f;
                    offset = new Vector3(0.20f, 0f, -0.16f);
                    break;
                case 2:
                    angle = 162f;
                    offset = new Vector3(0f, 0f, -0.20f);
                    break;
                default:
                    angle = 288f;
                    offset = new Vector3(-0.20f, 0f, -0.16f);
                    break;
            }

            const float settleDurationTicks = 18f;
            float settleProgress = Mathf.Clamp01(reflection.StanceElapsedTicks / settleDurationTicks);
            float settleRemaining = 1f - Mathf.SmoothStep(0f, 1f, settleProgress);
            float startAngleOffset = stanceFacing == Rot4.North || stanceFacing == Rot4.East
                ? -6f
                : 6f;
            angle += startAngleOffset * settleRemaining;
            offset += new Vector3(0f, 0f, 0.04f * settleRemaining);
            PawnRenderUtility.DrawEquipmentAiming(weapon, drawPos + offset * drawFactor, angle);

            if (pawn.apparel != null)
            {
                for (int i = 0; i < pawn.apparel.WornApparel.Count; i++)
                {
                    pawn.apparel.WornApparel[i].DrawWornExtras();
                }
            }
            return false;
        }

        public static void Patch_VerbMeleeAttackDamage_ApplyMeleeDamageToTarget_Prefix(
            Verb_MeleeAttackDamage __instance, LocalTargetInfo target)
        {
            Pawn caster = __instance.CasterPawn;
            if (MX_QH_HediffUtility.GetCombatStance(caster)?.CurrentStance != MX_QHDefOf.MX_QH_Stance_Sword
                || __instance.EquipmentSource?.def.IsMeleeWeapon != true
                || __instance.verbProps.meleeDamageDef.Worker is DamageWorker_QingheSlash
                || !QingheSwordCombatUtility.IsSwordPressureTarget(caster, target.Thing))
            {
                return;
            }

            // Runs after hit and dodge rolls, once per target rather than per extra damage packet.
            MX_QH_HediffUtility.GetSwordPressure(caster)?.AddProgress(25f);
        }

        public static void Patch_Thing_TakeDamage_Prefix(Thing __instance, DamageInfo dinfo)
        {
            // Melee misses and dodges never reach TakeDamage; shields and armor resolve inside it.
            if (__instance == null || __instance.Destroyed
                || dinfo.Instigator is not Pawn caster
                || !MX_QHCharacterUtility.IsQinghe(caster)
                || dinfo.Def?.Worker is not DamageWorker_QingheSlash)
            {
                return;
            }

            QingheSwordCombatUtility.NotifySwordPressureHit(caster, __instance, dinfo.Def.GetModExtension<QingheSlashExtension>());
        }

        public static void Patch_VerbMeleeAttack_SoundDodge_Postfix(Verb_MeleeAttack __instance, Thing target)
        {
            NotifyHostileAttackAttempt(target as Pawn, __instance?.CasterPawn);
        }

        public static void Patch_Projectile_ImpactSomething_Prefix(
            Projectile __instance,
            LocalTargetInfo ___intendedTarget,
            Thing ___launcher)
        {
            Pawn target = ___intendedTarget.Pawn;
            if (target == null
                || !target.Spawned
                || target.MapHeld != __instance?.MapHeld
                || target.Position != __instance.Position)
            {
                return;
            }

            NotifyHostileAttackAttempt(target, ___launcher);
        }

        private static void NotifyHostileAttackAttempt(Pawn target, Thing instigator)
        {
            if (target?.health?.hediffSet == null || instigator == null)
            {
                return;
            }

            JobDriver_IllusoryReflectionStance reflection = target.jobs?.curDriver as JobDriver_IllusoryReflectionStance;
            reflection?.TryHandleAttackAttempt(instigator);
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

                IntVec3 interactionCell = building.InteractionCell;
                if (interactionCell.IsValid)
                {
                    yield return interactionCell;
                }
            }
        }

        public static void Patch_JobDriver_Meditate_MeditationTick_Postfix(JobDriver_Meditate __instance)
        {
            Pawn pawn = __instance?.pawn;
            if (!MX_QHCharacterUtility.IsQinghe(pawn) || pawn?.Map == null)
            {
                return;
            }

            MX_QH_HediffUtility.AddMeditativeStillnessFromMeditation(pawn, ResolveMeditatingLotusPond(pawn));
        }

        public static void Patch_Book_OnBookReadTick_Postfix(Pawn pawn, int delta, float roomBonusFactor)
        {
            MX_QH_HediffUtility.AddMeditativeStillnessFromReading(pawn, delta, roomBonusFactor);
        }

        public static void Patch_JobDriver_LayDown_LayDownToil_Postfix(JobDriver_LayDown __instance, Toil __result)
        {
            if (!MX_QHCharacterUtility.IsQinghe(__instance?.pawn))
            {
                return;
            }

            __result?.AddPreTickIntervalAction(delta => ApplyQingheSleepStillness(__instance, delta));
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

        public static void Patch_GenRecipe_PostProcessProduct_Postfix(Thing __result, RecipeDef recipeDef, Pawn worker)
        {
            MX_QH_HediffUtility.AddAuraMasteryProgressFromCraft(worker, recipeDef, __result);
        }
        private static void ApplyQingheSleepStillness(JobDriver_LayDown driver, int delta)
        {
            Pawn pawn = driver?.pawn;
            if (driver == null || !driver.asleep || !MX_QHCharacterUtility.IsQinghe(pawn))
            {
                return;
            }

            MX_QH_HediffUtility.AddMeditativeStillnessFromSleep(pawn, delta);
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
            if (pawn?.Map == null || MX_QHDefOf.MX_QH_LotusPond == null)
            {
                return null;
            }

            foreach (Building building in pawn.Map.listerBuildings.AllBuildingsColonistOfDef(MX_QHDefOf.MX_QH_LotusPond))
            {
                if (building != null && building.InteractionCell == pawn.Position)
                {
                    return building;
                }
            }

            return null;
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

    }
}

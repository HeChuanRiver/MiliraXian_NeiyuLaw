using System;
using System.Collections.Generic;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters
{
    internal static class SpecialHaloAnimationRuntime
    {
        private const int HaloLoopTicks = 1080;
        private const int CombatHoldTicks = 120;
        private const float CombatTransitionTicks = 21f;
        private const float CombatPlaybackRate = 4.5f;
        private const string HaloSkipFlagDefName = "Milira_Halo";

        private sealed class HaloState
        {
            public Pawn Pawn;
            public float CombatBlend;
            public int LastUpdateTick;
            public int LastCombatTick = int.MinValue / 2;
            public int LastSeenTick;
            public bool Active;
        }

        private static readonly Dictionary<int, HaloState> States = new Dictionary<int, HaloState>();
        private static readonly List<int> RemovalBuffer = new List<int>();
        private static int lastScanTick = int.MinValue;
        private static int lastMapId = int.MinValue;

        public static void Update()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
            {
                return;
            }

            Map map = Find.CurrentMap;
            int now = Find.TickManager.TicksGame;
            int mapId = map?.uniqueID ?? int.MinValue;
            if (now == lastScanTick && mapId == lastMapId)
            {
                return;
            }

            lastScanTick = now;
            lastMapId = mapId;
            if (map == null)
            {
                DeactivateAll();
                return;
            }

            IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
            for (int index = 0; index < pawns.Count; index++)
            {
                Pawn pawn = pawns[index];
                if (!TryGetHaloKind(pawn, out CharacterUnityVfxKind kind))
                {
                    continue;
                }

                HaloState state = GetOrCreateState(pawn, now);
                state.LastSeenTick = now;
                UpdateCombatBlend(state, now);
                if (!CanDrawAnimatedHalo(pawn))
                {
                    state.Active = false;
                    continue;
                }

                float smoothedBlend = state.CombatBlend * state.CombatBlend * (3f - 2f * state.CombatBlend);
                float playbackRate = Mathf.Lerp(1f, CombatPlaybackRate, smoothedBlend);
                float alphaMultiplier = Mathf.Lerp(1f, 1.28f, smoothedBlend);
                float haloScale = HumanlikeMeshPoolUtility.HumanlikeHeadWidthForPawn(pawn);
                state.Active = CharacterUnityVfxRuntime.TryMaintainDirectionalAttached(
                    kind,
                    pawn,
                    haloScale,
                    HaloLoopTicks,
                    playbackRate,
                    alphaMultiplier);
            }

            RemovalBuffer.Clear();
            foreach (KeyValuePair<int, HaloState> pair in States)
            {
                HaloState state = pair.Value;
                Pawn pawn = state.Pawn;
                if (pawn == null
                    || pawn.Destroyed
                    || pawn.Dead
                    || !pawn.Spawned
                    || pawn.MapHeld != map
                    || now - state.LastSeenTick > 2)
                {
                    state.Active = false;
                    if (pawn == null || pawn.Destroyed || now - state.LastSeenTick > 120)
                    {
                        RemovalBuffer.Add(pair.Key);
                    }
                }
            }

            for (int index = 0; index < RemovalBuffer.Count; index++)
            {
                States.Remove(RemovalBuffer[index]);
            }
        }

        public static bool IsTargetPawn(Pawn pawn)
        {
            return Neiyu.NeiyuEquipmentUtility.IsNeiyu(pawn)
                || Zhaoli.ZhaoliKarmaUtility.IsZhaoli(pawn);
        }

        public static void Reset()
        {
            States.Clear();
            RemovalBuffer.Clear();
            lastScanTick = int.MinValue;
            lastMapId = int.MinValue;
        }

        private static HaloState GetOrCreateState(Pawn pawn, int now)
        {
            if (States.TryGetValue(pawn.thingIDNumber, out HaloState state))
            {
                state.Pawn = pawn;
                return state;
            }

            state = new HaloState
            {
                Pawn = pawn,
                LastUpdateTick = now,
                LastSeenTick = now
            };
            States.Add(pawn.thingIDNumber, state);
            return state;
        }

        private static void UpdateCombatBlend(HaloState state, int now)
        {
            Pawn pawn = state.Pawn;
            if (IsCurrentlyInCombat(pawn, now))
            {
                state.LastCombatTick = now;
            }

            bool combatHeld = now - state.LastCombatTick <= CombatHoldTicks;
            int elapsedTicks = Mathf.Max(0, now - state.LastUpdateTick);
            state.LastUpdateTick = now;
            if (elapsedTicks <= 0)
            {
                return;
            }

            float delta = Mathf.Min(30, elapsedTicks) / CombatTransitionTicks;
            state.CombatBlend = Mathf.MoveTowards(state.CombatBlend, combatHeld ? 1f : 0f, delta);
        }

        private static bool IsCurrentlyInCombat(Pawn pawn, int now)
        {
            if (pawn == null)
            {
                return false;
            }

            if (pawn.mindState?.enemyTarget != null)
            {
                return true;
            }

            if (pawn.stances?.curStance is Stance_Busy busy && busy.verb != null)
            {
                return true;
            }

            if (pawn.mindState == null)
            {
                return false;
            }

            int recentCombatTick = Math.Max(
                Math.Max(pawn.mindState.lastHarmTick, pawn.mindState.lastAttackTargetTick),
                Math.Max(pawn.mindState.lastEngageTargetTick, pawn.mindState.lastCombatantTick));
            return recentCombatTick > 0 && now >= recentCombatTick && now - recentCombatTick <= CombatHoldTicks;
        }

        private static bool CanDrawAnimatedHalo(Pawn pawn)
        {
            return pawn != null
                && pawn.Spawned
                && !pawn.Dead
                && pawn.MapHeld == Find.CurrentMap
                && pawn.GetPosture() == PawnPosture.Standing
                && HasHaloBodyPart(pawn)
                && !HaloHiddenByApparel(pawn);
        }

        private static bool HasHaloBodyPart(Pawn pawn)
        {
            if (pawn.health?.hediffSet == null)
            {
                return false;
            }

            foreach (BodyPartRecord part in pawn.health.hediffSet.GetNotMissingParts())
            {
                if (part.def?.defName == HaloSkipFlagDefName)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool HaloHiddenByApparel(Pawn pawn)
        {
            List<Apparel> apparel = pawn.apparel?.WornApparel;
            if (apparel == null)
            {
                return false;
            }

            for (int apparelIndex = 0; apparelIndex < apparel.Count; apparelIndex++)
            {
                List<RenderSkipFlagDef> skipFlags = apparel[apparelIndex]?.def?.apparel?.renderSkipFlags;
                if (skipFlags == null)
                {
                    continue;
                }

                for (int flagIndex = 0; flagIndex < skipFlags.Count; flagIndex++)
                {
                    if (skipFlags[flagIndex]?.defName == HaloSkipFlagDefName)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool TryGetHaloKind(Pawn pawn, out CharacterUnityVfxKind kind)
        {
            if (Neiyu.NeiyuEquipmentUtility.IsNeiyu(pawn))
            {
                kind = CharacterUnityVfxKind.NeiyuHalo;
                return true;
            }

            if (Zhaoli.ZhaoliKarmaUtility.IsZhaoli(pawn))
            {
                kind = CharacterUnityVfxKind.ZhaoliHalo;
                return true;
            }

            kind = default;
            return false;
        }

        private static void DeactivateAll()
        {
            foreach (HaloState state in States.Values)
            {
                state.Active = false;
            }
        }
    }
}

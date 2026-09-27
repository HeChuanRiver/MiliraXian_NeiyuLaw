using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using AlienRace;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Neiyu
{
    internal struct NeiyuEarPose
    {
        public static readonly NeiyuEarPose Neutral = new NeiyuEarPose(0f, 1f, 1f);

        public NeiyuEarPose(float angle, float scaleX, float scaleY)
        {
            Angle = angle;
            ScaleX = scaleX;
            ScaleY = scaleY;
        }

        public float Angle { get; }
        public float ScaleX { get; }
        public float ScaleY { get; }
    }

    public sealed class GameComponent_NeiyuEarAnimation : GameComponent
    {
        public GameComponent_NeiyuEarAnimation(Game game)
        {
        }

        public override void StartedNewGame()
        {
            NeiyuEarAnimationRuntime.Reset();
        }

        public override void LoadedGame()
        {
            NeiyuEarAnimationRuntime.Reset();
        }

        public override void GameComponentUpdate()
        {
            NeiyuEarAnimationRuntime.Update();
        }
    }

    [StaticConstructorOnStartup]
    internal static class NeiyuEarAnimationRuntime
    {
        private const string BundleRelativePath = "1.6/AssetBundles/Windows/neiyu_ear_anim";
        private const string DriverAssetName = "neiyu_ear_driver";
        private const string PairTransformName = "EarPair_Motion";
        private const string TwitchTrigger = "Twitch";
        private const string AlertTrigger = "Alert";
        private const float DriverScanInterval = 0.5f;
        private const float InactiveDriverLifetime = 3f;
        private const float AlertCooldown = 2.5f;

        private sealed class DriverState
        {
            public Pawn Pawn;
            public GameObject Root;
            public Animator Animator;
            public Transform Pair;
            public System.Random Random;
            public float LastHealth;
            public float NextTwitchAt;
            public float NextAlertAllowedAt;
            public float LastSeenAt;
        }

        private static readonly Dictionary<int, DriverState> Drivers = new Dictionary<int, DriverState>();
        private static readonly ConcurrentDictionary<int, NeiyuEarPose> Poses =
            new ConcurrentDictionary<int, NeiyuEarPose>();
        private static readonly List<int> RemovalBuffer = new List<int>();

        private static AssetBundle fallbackBundle;
        private static GameObject driverPrefab;
        private static bool bundleLoadAttempted;
        private static float nextDriverScanAt;

        public static void Update()
        {
            if (Current.ProgramState != ProgramState.Playing)
            {
                return;
            }

            float now = Time.realtimeSinceStartup;
            if (now >= nextDriverScanAt)
            {
                nextDriverScanAt = now + DriverScanInterval;
                ScanCurrentMap(now);
            }

            bool paused = Find.TickManager == null || Find.TickManager.Paused;
            foreach (DriverState state in Drivers.Values)
            {
                UpdateDriver(state, now, paused);
            }
        }

        public static bool TryGetPose(Pawn pawn, out NeiyuEarPose pose)
        {
            if (pawn != null && Poses.TryGetValue(pawn.thingIDNumber, out pose))
            {
                return true;
            }

            pose = NeiyuEarPose.Neutral;
            return false;
        }

        public static void Reset()
        {
            foreach (DriverState state in Drivers.Values)
            {
                if (state.Root != null)
                {
                    UnityEngine.Object.Destroy(state.Root);
                }
            }

            Drivers.Clear();
            Poses.Clear();
            RemovalBuffer.Clear();
            nextDriverScanAt = 0f;
        }

        private static void ScanCurrentMap(float now)
        {
            Map map = Find.CurrentMap;
            if (map != null)
            {
                IReadOnlyList<Pawn> pawns = map.mapPawns.AllPawnsSpawned;
                for (int index = 0; index < pawns.Count; index++)
                {
                    Pawn pawn = pawns[index];
                    if (!NeiyuEquipmentUtility.IsNeiyu(pawn) || pawn.Dead)
                    {
                        continue;
                    }

                    DriverState state = EnsureDriver(pawn, now);
                    if (state != null)
                    {
                        state.LastSeenAt = now;
                    }
                }
            }

            RemovalBuffer.Clear();
            foreach (KeyValuePair<int, DriverState> pair in Drivers)
            {
                DriverState state = pair.Value;
                Pawn pawn = state.Pawn;
                if (pawn == null
                    || pawn.Destroyed
                    || pawn.Dead
                    || !pawn.Spawned
                    || pawn.Map != map
                    || now - state.LastSeenAt > InactiveDriverLifetime)
                {
                    RemovalBuffer.Add(pair.Key);
                }
            }

            for (int index = 0; index < RemovalBuffer.Count; index++)
            {
                RemoveDriver(RemovalBuffer[index]);
            }
        }

        private static DriverState EnsureDriver(Pawn pawn, float now)
        {
            int id = pawn.thingIDNumber;
            if (Drivers.TryGetValue(id, out DriverState existing))
            {
                return existing;
            }

            if (!TryLoadDriverPrefab())
            {
                return null;
            }

            GameObject root = UnityEngine.Object.Instantiate(driverPrefab);
            root.name = "NeiyuEarDriver_" + id;
            root.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(root);

            Animator animator = root.GetComponent<Animator>();
            Transform pair = root.transform.Find(PairTransformName);
            if (animator == null || pair == null)
            {
                Log.ErrorOnce(
                    "[MiliraXian Neiyu] Ear animation prefab is missing its Animator or EarPair_Motion transform.",
                    153806427);
                UnityEngine.Object.Destroy(root);
                return null;
            }

            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.applyRootMotion = false;

            float health = CurrentHealth(pawn);
            DriverState state = new DriverState
            {
                Pawn = pawn,
                Root = root,
                Animator = animator,
                Pair = pair,
                Random = new System.Random(unchecked(id * 397 ^ 0x4E495955)),
                LastHealth = health,
                NextTwitchAt = now,
                NextAlertAllowedAt = now,
                LastSeenAt = now
            };
            state.NextTwitchAt = now + NextInterval(state, false);
            Drivers.Add(id, state);
            Poses[id] = NeiyuEarPose.Neutral;
            return state;
        }

        private static void UpdateDriver(DriverState state, float now, bool paused)
        {
            Pawn pawn = state.Pawn;
            if (pawn == null || state.Animator == null || state.Pair == null)
            {
                return;
            }

            state.Animator.speed = paused ? 0f : 1f;
            if (paused || pawn.Dead || pawn.Downed || !pawn.Awake())
            {
                Poses[pawn.thingIDNumber] = ReadPose(state.Pair);
                return;
            }

            float health = CurrentHealth(pawn);
            if (health + 0.001f < state.LastHealth && now >= state.NextAlertAllowedAt)
            {
                FireTrigger(state, AlertTrigger);
                state.NextAlertAllowedAt = now + AlertCooldown;
                state.NextTwitchAt = now + NextInterval(state, IsAlerted(pawn));
            }
            state.LastHealth = health;

            if (now >= state.NextTwitchAt)
            {
                FireTrigger(state, TwitchTrigger);
                state.NextTwitchAt = now + NextInterval(state, IsAlerted(pawn));
            }

            Poses[pawn.thingIDNumber] = ReadPose(state.Pair);
        }

        private static void FireTrigger(DriverState state, string trigger)
        {
            state.Animator.ResetTrigger(TwitchTrigger);
            state.Animator.ResetTrigger(AlertTrigger);
            state.Animator.SetTrigger(trigger);
        }

        private static float NextInterval(DriverState state, bool alerted)
        {
            float minimum = alerted ? 6f : 10f;
            float maximum = alerted ? 12f : 22f;
            return Mathf.Lerp(minimum, maximum, (float)state.Random.NextDouble());
        }

        private static bool IsAlerted(Pawn pawn)
        {
            return pawn.Drafted || pawn.InMentalState;
        }

        private static float CurrentHealth(Pawn pawn)
        {
            if (pawn?.health?.summaryHealth == null)
            {
                return 1f;
            }

            return pawn.health.summaryHealth.SummaryHealthPercent;
        }

        private static NeiyuEarPose ReadPose(Transform pair)
        {
            float angle = Mathf.Clamp(Mathf.DeltaAngle(0f, pair.localEulerAngles.z), -5f, 5f);
            Vector3 scale = pair.localScale;
            return new NeiyuEarPose(
                angle,
                Mathf.Clamp(scale.x, 0.94f, 1.06f),
                Mathf.Clamp(scale.y, 0.94f, 1.06f));
        }

        private static bool TryLoadDriverPrefab()
        {
            if (driverPrefab != null)
            {
                return true;
            }

            if (bundleLoadAttempted)
            {
                return false;
            }

            try
            {
                ModContentPack content = NeiyuLawMod.Instance?.Content;
                string modRoot = content?.RootDir;
                if (modRoot.NullOrEmpty())
                {
                    bundleLoadAttempted = true;
                    Log.ErrorOnce("[MiliraXian Neiyu] Cannot resolve the mod directory for ear animation.", 153806428);
                    return false;
                }

                if (content.assetBundles?.loadedAssetBundles != null)
                {
                    List<AssetBundle> loadedBundles = content.assetBundles.loadedAssetBundles;
                    for (int index = 0; index < loadedBundles.Count; index++)
                    {
                        AssetBundle loadedBundle = loadedBundles[index];
                        if (loadedBundle == null)
                        {
                            continue;
                        }

                        driverPrefab = loadedBundle.LoadAsset<GameObject>(DriverAssetName);
                        if (driverPrefab != null)
                        {
                            bundleLoadAttempted = true;
                            Log.Message("[MiliraXian Neiyu] Unity ear animation loaded from RimWorld's asset bundle cache.");
                            return true;
                        }
                    }

                    if (loadedBundles.Count > 0)
                    {
                        bundleLoadAttempted = true;
                        Log.ErrorOnce(
                            "[MiliraXian Neiyu] RimWorld loaded the mod asset bundle, but it does not contain address '"
                            + DriverAssetName + "'.",
                            153806433);
                        return false;
                    }
                }

                string bundlePath = Path.Combine(modRoot, BundleRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(bundlePath))
                {
                    bundleLoadAttempted = true;
                    Log.ErrorOnce("[MiliraXian Neiyu] Ear animation bundle is missing: " + bundlePath, 153806429);
                    return false;
                }

                fallbackBundle = AssetBundle.LoadFromFile(bundlePath);
                if (fallbackBundle == null)
                {
                    bundleLoadAttempted = true;
                    Log.ErrorOnce("[MiliraXian Neiyu] Failed to load ear animation bundle: " + bundlePath, 153806430);
                    return false;
                }

                driverPrefab = fallbackBundle.LoadAsset<GameObject>(DriverAssetName);
                if (driverPrefab == null)
                {
                    bundleLoadAttempted = true;
                    Log.ErrorOnce("[MiliraXian Neiyu] Ear animation driver asset is missing from its bundle.", 153806431);
                    return false;
                }

                bundleLoadAttempted = true;
                Log.Message("[MiliraXian Neiyu] Unity ear animation loaded from its bundle file fallback.");
                return true;
            }
            catch (Exception exception)
            {
                bundleLoadAttempted = true;
                Log.ErrorOnce("[MiliraXian Neiyu] Ear animation failed to load: " + exception, 153806432);
                return false;
            }
        }

        private static void RemoveDriver(int id)
        {
            if (Drivers.TryGetValue(id, out DriverState state))
            {
                if (state.Root != null)
                {
                    UnityEngine.Object.Destroy(state.Root);
                }

                Drivers.Remove(id);
            }

            Poses.TryRemove(id, out _);
        }
    }

    internal static class NeiyuEarRenderUtility
    {
        private const string EarAddonName = "milira ear feather Neiyu";

        public static bool TryGetEarPose(PawnRenderNode node, PawnDrawParms parms, out NeiyuEarPose pose)
        {
            pose = NeiyuEarPose.Neutral;
            if (parms.pawn == null
                || parms.Portrait
                || parms.Statue
                || parms.flags.FlagSet(PawnRenderFlags.Cache)
                || !NeiyuEquipmentUtility.IsNeiyu(parms.pawn)
                || !(node is AlienPawnRenderNode_BodyAddon))
            {
                return false;
            }

            AlienPartGenerator.BodyAddon addon = AlienPawnRenderNodeWorker_BodyAddon.AddonFromNode(node);
            return addon != null
                && addon.Name == EarAddonName
                && NeiyuEarAnimationRuntime.TryGetPose(parms.pawn, out pose);
        }
    }

    [HarmonyPatch(typeof(PawnRenderer), "ParallelGetPreRenderResults")]
    internal static class Patch_NeiyuEarAnimation_DisablePawnCache
    {
        [HarmonyPrefix]
        private static void Prefix(Pawn ___pawn, ref bool disableCache)
        {
            if (NeiyuEquipmentUtility.IsNeiyu(___pawn))
            {
                disableCache = true;
            }
        }
    }

    [HarmonyPatch(typeof(PawnRenderNodeWorker), nameof(PawnRenderNodeWorker.RotationFor))]
    internal static class Patch_NeiyuEarAnimation_Rotation
    {
        [HarmonyPostfix]
        private static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref Quaternion __result)
        {
            if (!NeiyuEarRenderUtility.TryGetEarPose(node, parms, out NeiyuEarPose pose))
            {
                return;
            }

            float angle = parms.facing == Rot4.West ? -pose.Angle : pose.Angle;
            __result *= Quaternion.AngleAxis(angle, Vector3.up);
        }
    }

    [HarmonyPatch(typeof(AlienPawnRenderNodeWorker_BodyAddon), nameof(AlienPawnRenderNodeWorker_BodyAddon.ScaleFor))]
    internal static class Patch_NeiyuEarAnimation_Scale
    {
        [HarmonyPostfix]
        private static void Postfix(PawnRenderNode node, PawnDrawParms parms, ref Vector3 __result)
        {
            if (!NeiyuEarRenderUtility.TryGetEarPose(node, parms, out NeiyuEarPose pose))
            {
                return;
            }

            __result.x *= pose.ScaleX;
            __result.z *= pose.ScaleY;
        }
    }
}

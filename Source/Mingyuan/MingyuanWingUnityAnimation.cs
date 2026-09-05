using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using MiliraXian.Characters.Neiyu;
using RimWorld;
using UnityEngine;
using Verse;
using Verse.Profile;

namespace MiliraXian.Characters.Mingyuan
{
    public sealed class GameComponent_MingyuanWingUnityAnimation : GameComponent
    {
        public GameComponent_MingyuanWingUnityAnimation(Game game)
        {
        }

        public override void StartedNewGame()
        {
            MingyuanWingUnityAnimationRuntime.Reset();
        }

        public override void LoadedGame()
        {
            MingyuanWingUnityAnimationRuntime.Reset();
        }

        public override void GameComponentUpdate()
        {
            MingyuanWingUnityAnimationRuntime.Update();
        }
    }

    [StaticConstructorOnStartup]
    internal static class MingyuanWingUnityAnimationRuntime
    {
        private const string MingyuanPawnKindDefName = "MiliraXian_Mingyuan";
        private const string BundleRelativePath = "1.6/AssetBundles/Windows/mingyuan_wing_anim";
        private const string DriverAssetName = "mingyuan_wing_driver";
        private const string FrameClockTransformName = "FrameClock";
        private const string FrameTextureRoot = "MiliraXianMingyuan/PawnMingyuan/Wings/Milira_Fly";
        private const string NorthAnimationDefName = "Milira_FlyNorth_Mingyuan_Unity";
        private const string EastAnimationDefName = "Milira_FlyEast_Mingyuan_Unity";
        private const string SouthAnimationDefName = "Milira_FlySouth_Mingyuan_Unity";
        private const string WestAnimationDefName = "Milira_FlyWest_Mingyuan_Unity";
        private const float LoopDurationSeconds = 0.60f;
        private const float MaximumDeltaSeconds = 0.10f;
        private const float FrontLayer = 60f;
        private const float BehindLayer = -10f;
        // The legacy Milira_WingBinding node uses the 1.5-cell humanlike body mesh and
        // then applies GraphicState.drawSize 2.5, for a final 3.75-cell flight plane.
        private const float FrameDrawSize = 3.75f;
        private const int AnimationDurationTicks = 36;
        private const int FrameCount = 8;
        private const int NorthFrameSetIndex = 0;
        private const int EastFrameSetIndex = 1;
        private const int SouthFrameSetIndex = 2;

        private static readonly int NorthStateHash = Animator.StringToHash("Base Layer.North");
        private static readonly int SouthStateHash = Animator.StringToHash("Base Layer.South");
        private static readonly int EastStateHash = Animator.StringToHash("Base Layer.East");
        private static readonly Dictionary<int, DriverState> Drivers = new Dictionary<int, DriverState>();
        private static readonly List<int> RemovalBuffer = new List<int>();
        private static readonly Material[][] FrameMaterials =
        {
            new Material[FrameCount],
            new Material[FrameCount],
            new Material[FrameCount]
        };

        private static AssetBundle fallbackBundle;
        private static GameObject driverPrefab;
        private static bool bundleLoadAttempted;
        private static bool runtimeFailed;
        private static bool frameMaterialsLoaded;
        private static bool frameMaterialsFailed;
        private static bool animationDefsResolved;
        private static AnimationDef northAnimation;
        private static AnimationDef eastAnimation;
        private static AnimationDef southAnimation;
        private static AnimationDef westAnimation;

        private sealed class DriverState
        {
            public Pawn Pawn;
            public GameObject Root;
            public Animator Animator;
            public Transform FrameClock;
            public Rot4 Facing;
            public float Phase;
            public float LastRealtime;
        }

        internal static bool TryGetFlyAnimation(Pawn pawn, Rot4 facing, out AnimationDef animationDef)
        {
            animationDef = null;
            if (runtimeFailed
                || pawn?.kindDef?.defName != MingyuanPawnKindDefName
                || !pawn.Spawned
                || pawn.Map != Find.CurrentMap
                || !TryResolveAnimationDefs()
                || !TryLoadFrameMaterials())
            {
                return false;
            }

            DriverState state = EnsureDriver(pawn);
            if (state == null)
            {
                return false;
            }

            state.Facing = facing;
            switch (facing.AsInt)
            {
                case 0:
                    animationDef = northAnimation;
                    break;
                case 1:
                    animationDef = eastAnimation;
                    break;
                case 2:
                    animationDef = southAnimation;
                    break;
                case 3:
                    animationDef = westAnimation;
                    break;
                default:
                    animationDef = eastAnimation;
                    state.Facing = Rot4.East;
                    break;
            }

            return animationDef != null;
        }

        internal static void Update()
        {
            if (Current.ProgramState != ProgramState.Playing)
            {
                if (Drivers.Count > 0)
                {
                    ResetDrivers();
                }

                return;
            }

            float now = Time.realtimeSinceStartup;
            RemovalBuffer.Clear();
            bool failedThisFrame = false;

            foreach (KeyValuePair<int, DriverState> pair in Drivers)
            {
                DriverState state = pair.Value;
                Pawn pawn = state.Pawn;
                if (!IsDriverValid(state)
                    || pawn.Dead
                    || !pawn.Spawned
                    || pawn.Map != Find.CurrentMap
                    || !IsUnityFlightActive(pawn))
                {
                    RemovalBuffer.Add(pair.Key);
                    continue;
                }

                try
                {
                    AdvancePhase(state, now);
                    if (IsVisibleOnCurrentMap(pawn))
                    {
                        SampleAndDraw(state);
                    }
                }
                catch (Exception exception)
                {
                    runtimeFailed = true;
                    failedThisFrame = true;
                    Log.ErrorOnce(
                        "[MiliraXian Mingyuan] Unity wing animation failed during sampling; reverting to the legacy eight-frame animation.\n"
                        + exception,
                        178643201);
                    break;
                }
            }

            if (failedThisFrame)
            {
                SwitchDriversToLegacyFallback();
                ResetDrivers();
                return;
            }

            for (int index = 0; index < RemovalBuffer.Count; index++)
            {
                RemoveDriver(RemovalBuffer[index]);
            }
        }

        internal static void Reset()
        {
            ResetDrivers();
            runtimeFailed = false;
        }

        private static bool TryResolveAnimationDefs()
        {
            if (animationDefsResolved)
            {
                return true;
            }

            northAnimation = DefDatabase<AnimationDef>.GetNamedSilentFail(NorthAnimationDefName);
            eastAnimation = DefDatabase<AnimationDef>.GetNamedSilentFail(EastAnimationDefName);
            southAnimation = DefDatabase<AnimationDef>.GetNamedSilentFail(SouthAnimationDefName);
            westAnimation = DefDatabase<AnimationDef>.GetNamedSilentFail(WestAnimationDefName);
            animationDefsResolved = northAnimation != null
                && eastAnimation != null
                && southAnimation != null
                && westAnimation != null;

            if (!animationDefsResolved)
            {
                Log.ErrorOnce(
                    "[MiliraXian Mingyuan] One or more Unity wing AnimationDefs are missing; using the legacy eight-frame animation.",
                    178643202);
            }

            return animationDefsResolved;
        }

        private static bool TryLoadFrameMaterials()
        {
            if (frameMaterialsLoaded)
            {
                return true;
            }

            if (frameMaterialsFailed)
            {
                return false;
            }

            try
            {
                LoadFrameSet(NorthFrameSetIndex, "North");
                LoadFrameSet(EastFrameSetIndex, "East");
                LoadFrameSet(SouthFrameSetIndex, "South");
                frameMaterialsLoaded = true;
                return true;
            }
            catch (Exception exception)
            {
                frameMaterialsFailed = true;
                Log.ErrorOnce(
                    "[MiliraXian Mingyuan] One or more full flight frames could not be loaded; using the legacy eight-frame animation.\n"
                    + exception,
                    178643203);
                return false;
            }
        }

        private static void LoadFrameSet(int frameSetIndex, string direction)
        {
            Material[] materials = FrameMaterials[frameSetIndex];
            for (int frameIndex = 0; frameIndex < FrameCount; frameIndex++)
            {
                string texturePath = FrameTextureRoot + direction + "_" + (frameIndex + 1);
                Texture2D texture = ContentFinder<Texture2D>.Get(texturePath, reportFailure: false);
                if (texture == null)
                {
                    throw new FileNotFoundException("Missing Mingyuan flight frame texture: " + texturePath);
                }

                // Match Graphic_Single's default flight-frame shader. Transparent alpha
                // crossfades make displaced feather silhouettes pulse at frame boundaries.
                Material material = MaterialPool.MatFrom(texture, ShaderDatabase.Cutout, Color.white);
                if (material == null)
                {
                    throw new InvalidOperationException("Could not create the shared material for " + texturePath);
                }

                materials[frameIndex] = material;
            }
        }

        private static bool IsUnityFlightActive(Pawn pawn)
        {
            if (pawn.Flying)
            {
                return true;
            }

            AnimationDef animation = pawn.Drawer?.renderer?.CurAnimation;
            return animation == northAnimation
                || animation == eastAnimation
                || animation == southAnimation
                || animation == westAnimation;
        }

        private static DriverState EnsureDriver(Pawn pawn)
        {
            int id = pawn.thingIDNumber;
            if (Drivers.TryGetValue(id, out DriverState existing))
            {
                if (IsDriverValid(existing))
                {
                    return existing;
                }

                RemoveDriver(id);
            }

            if (!TryLoadDriverPrefab())
            {
                return null;
            }

            GameObject root = null;
            try
            {
                root = UnityEngine.Object.Instantiate(driverPrefab);
                root.name = "MingyuanWingDriver_" + id;
                root.hideFlags = HideFlags.HideAndDontSave;
                UnityEngine.Object.DontDestroyOnLoad(root);

                Animator animator = root.GetComponent<Animator>();
                Transform frameClock = root.transform.Find(FrameClockTransformName);
                if (animator == null
                    || frameClock == null)
                {
                    throw new InvalidOperationException("The driver prefab is missing its Animator or FrameClock transform.");
                }

                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                animator.updateMode = AnimatorUpdateMode.UnscaledTime;
                animator.speed = 0f;
                animator.Rebind();
                animator.Update(0f);
                if (!animator.HasState(0, NorthStateHash)
                    || !animator.HasState(0, SouthStateHash)
                    || !animator.HasState(0, EastStateHash))
                {
                    throw new InvalidOperationException("The driver Animator does not contain North, South and East states.");
                }

                int randomStartTick = Rand.RangeSeeded(0, AnimationDurationTicks, id);
                DriverState state = new DriverState
                {
                    Pawn = pawn,
                    Root = root,
                    Animator = animator,
                    FrameClock = frameClock,
                    Facing = pawn.Rotation,
                    Phase = GenMath.PositiveMod(-randomStartTick, AnimationDurationTicks) / (float)AnimationDurationTicks,
                    LastRealtime = Time.realtimeSinceStartup
                };
                Drivers.Add(id, state);
                return state;
            }
            catch (Exception exception)
            {
                if (root != null)
                {
                    UnityEngine.Object.Destroy(root);
                }

                runtimeFailed = true;
                Log.ErrorOnce(
                    "[MiliraXian Mingyuan] Unity wing driver initialization failed; using the legacy eight-frame animation.\n"
                    + exception,
                    178643204);
                return null;
            }
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

            bundleLoadAttempted = true;
            try
            {
                ModContentPack content = NeiyuLawMod.Instance?.Content;
                List<AssetBundle> loadedBundles = content?.assetBundles?.loadedAssetBundles;
                if (loadedBundles != null)
                {
                    for (int index = 0; index < loadedBundles.Count; index++)
                    {
                        AssetBundle bundle = loadedBundles[index];
                        if (bundle == null)
                        {
                            continue;
                        }

                        driverPrefab = bundle.LoadAsset<GameObject>(DriverAssetName);
                        if (driverPrefab != null)
                        {
                            return true;
                        }
                    }
                }

                string modRoot = content?.RootDir;
                if (modRoot.NullOrEmpty())
                {
                    throw new InvalidOperationException("The mod root directory could not be resolved.");
                }

                string bundlePath = Path.Combine(modRoot, BundleRelativePath.Replace('/', Path.DirectorySeparatorChar));
                if (!File.Exists(bundlePath))
                {
                    throw new FileNotFoundException("The Unity wing bundle is missing.", bundlePath);
                }

                fallbackBundle = AssetBundle.LoadFromFile(bundlePath);
                if (fallbackBundle == null)
                {
                    throw new InvalidOperationException("Unity could not load the wing AssetBundle at " + bundlePath);
                }

                driverPrefab = fallbackBundle.LoadAsset<GameObject>(DriverAssetName);
                if (driverPrefab == null)
                {
                    throw new InvalidOperationException(
                        "The wing AssetBundle does not contain address '" + DriverAssetName + "'.");
                }

                return true;
            }
            catch (Exception exception)
            {
                Log.ErrorOnce(
                    "[MiliraXian Mingyuan] Unity wing animation is unavailable; using the legacy eight-frame animation.\n"
                    + exception,
                    178643205);
                return false;
            }
        }

        private static bool IsDriverValid(DriverState state)
        {
            return state != null
                && state.Pawn != null
                && !state.Pawn.Destroyed
                && state.Root != null
                && state.Animator != null
                && state.FrameClock != null;
        }

        private static void AdvancePhase(DriverState state, float now)
        {
            float delta = Mathf.Clamp(now - state.LastRealtime, 0f, MaximumDeltaSeconds);
            state.LastRealtime = now;
            TickManager tickManager = Find.TickManager;
            float speed = tickManager == null ? 0f : Mathf.Min(3f, Mathf.Max(0f, tickManager.TickRateMultiplier));
            if (speed > 0f && delta > 0f)
            {
                state.Phase = Mathf.Repeat(state.Phase + delta * speed / LoopDurationSeconds, 1f);
            }
        }

        private static bool IsVisibleOnCurrentMap(Pawn pawn)
        {
            CameraDriver cameraDriver = Find.CameraDriver;
            return cameraDriver != null
                && cameraDriver.CurrentViewRect.ExpandedBy(1).Contains(pawn.Position);
        }

        private static void SampleAndDraw(DriverState state)
        {
            int stateHash;
            int frameSetIndex;
            float layer;
            bool mirror;
            switch (state.Facing.AsInt)
            {
                case 0:
                    stateHash = NorthStateHash;
                    frameSetIndex = NorthFrameSetIndex;
                    layer = FrontLayer;
                    mirror = false;
                    break;
                case 2:
                    stateHash = SouthStateHash;
                    frameSetIndex = SouthFrameSetIndex;
                    layer = BehindLayer;
                    mirror = false;
                    break;
                case 3:
                    stateHash = EastStateHash;
                    frameSetIndex = EastFrameSetIndex;
                    layer = FrontLayer;
                    mirror = true;
                    break;
                default:
                    stateHash = EastStateHash;
                    frameSetIndex = EastFrameSetIndex;
                    layer = FrontLayer;
                    mirror = false;
                    break;
            }

            state.Animator.Play(stateHash, 0, state.Phase);
            state.Animator.Update(0f);
            float frameCursor = Mathf.Repeat(state.FrameClock.localPosition.x, FrameCount);
            int currentFrameIndex = Mathf.Clamp(Mathf.FloorToInt(frameCursor), 0, FrameCount - 1);

            Material[] materials = FrameMaterials[frameSetIndex];
            Material currentMaterial = materials[currentFrameIndex];
            float bodyAngle = state.Facing == Rot4.East ? 8f : state.Facing == Rot4.West ? -8f : 0f;
            Vector3 drawPosition = state.Pawn.DrawPos;
            drawPosition.y += PawnRenderUtility.AltitudeForLayer(layer);
            drawPosition.z += BodyBobAt(state.Phase);
            Matrix4x4 matrix = Matrix4x4.TRS(
                drawPosition,
                Quaternion.AngleAxis(bodyAngle, Vector3.up),
                new Vector3(FrameDrawSize, 1f, FrameDrawSize));
            Mesh mesh = mirror ? MeshPool.plane10Flip : MeshPool.plane10;
            Graphics.DrawMesh(mesh, matrix, currentMaterial, 0);
        }

        private static float BodyBobAt(float phase)
        {
            float tick = phase * AnimationDurationTicks;
            if (tick <= 10f)
            {
                return Mathf.Lerp(-0.0125f, 0.01f, tick / 10f);
            }

            if (tick <= 15f)
            {
                return Mathf.Lerp(0.01f, 0.0125f, (tick - 10f) / 5f);
            }

            return Mathf.Lerp(0.0125f, -0.0125f, (tick - 15f) / 21f);
        }

        private static void RemoveDriver(int id)
        {
            if (!Drivers.TryGetValue(id, out DriverState state))
            {
                return;
            }

            if (state.Root != null)
            {
                UnityEngine.Object.Destroy(state.Root);
            }

            Drivers.Remove(id);
        }

        private static void SwitchDriversToLegacyFallback()
        {
            foreach (DriverState state in Drivers.Values)
            {
                try
                {
                    Pawn pawn = state.Pawn;
                    PawnRenderer renderer = pawn?.Drawer?.renderer;
                    if (renderer != null
                        && MiliraXianCharactersWingRegistry.TryGetFlyAnimation(
                            pawn,
                            state.Facing,
                            out AnimationDef legacyAnimation))
                    {
                        renderer.SetAnimation(legacyAnimation);
                    }
                }
                catch
                {
                    // The original error was already logged once. Cleanup must still complete.
                }
            }
        }

        private static void ResetDrivers()
        {
            foreach (DriverState state in Drivers.Values)
            {
                if (state.Root != null)
                {
                    UnityEngine.Object.Destroy(state.Root);
                }
            }

            Drivers.Clear();
            RemovalBuffer.Clear();
        }
    }

    [HarmonyPatch(typeof(MemoryUtility), nameof(MemoryUtility.ClearAllMapsAndWorld))]
    internal static class Patch_MingyuanWingUnityAnimation_ClearAllMapsAndWorld
    {
        [HarmonyPrefix]
        private static void Prefix()
        {
            MingyuanWingUnityAnimationRuntime.Reset();
        }
    }
}

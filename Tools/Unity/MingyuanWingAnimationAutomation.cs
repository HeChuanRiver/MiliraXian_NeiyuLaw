#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class MingyuanWingAnimationAutomation
{
    private const string Root = "Assets/MingyuanWings";
    private const string AnimationFolder = Root + "/Animations";
    private const string ControllerFolder = Root + "/Controllers";
    private const string PrefabFolder = Root + "/Prefabs";
    private const string NorthClipPath = AnimationFolder + "/MingyuanWing_North.anim";
    private const string SouthClipPath = AnimationFolder + "/MingyuanWing_South.anim";
    private const string EastClipPath = AnimationFolder + "/MingyuanWing_East.anim";
    private const string ControllerPath = ControllerFolder + "/MingyuanWingDriver.controller";
    private const string PrefabPath = PrefabFolder + "/MingyuanWingDriver.prefab";
    private const string BundleName = "mingyuan_wing_anim";
    private const string BundleAddress = "mingyuan_wing_driver";
    private const string FrameClockPath = "FrameClock";
    private const string ModBundleDirectory = @"E:\SteamLibrary\steamapps\common\RimWorld\Mods\MiliraXian_NeiyuLaw\1.6\AssetBundles\Windows";
    private const float Duration = 0.60f;
    private const int FrameCount = 8;

    // Preserve the timing authored for the existing eight-frame RimWorld animation.
    private static readonly float[] FrameTimes =
    {
        0f,
        3f / 60f,
        9f / 60f,
        12f / 60f,
        15f / 60f,
        21f / 60f,
        25f / 60f,
        30f / 60f,
        36f / 60f
    };

    [MenuItem("Tools/MiliraXian/Build Mingyuan Wing Animation Bundle")]
    public static void BuildFromMenu()
    {
        GenerateBuildAndValidate();
    }

    public static void BuildFromCommandLine()
    {
        GenerateBuildAndValidate();
    }

    public static void ValidateFromCommandLine()
    {
        ValidateBuiltBundle();
    }

    private static void GenerateBuildAndValidate()
    {
        EnsureFolder(Root);
        EnsureFolder(AnimationFolder);
        EnsureFolder(ControllerFolder);
        EnsureFolder(PrefabFolder);

        AnimationClip north = ConfigureClip(NorthClipPath);
        AnimationClip south = ConfigureClip(SouthClipPath);
        AnimationClip east = ConfigureClip(EastClipPath);
        AnimatorController controller = ConfigureController(north, south, east);
        CreatePrefab(controller);
        AssetDatabase.SaveAssets();

        ValidateEditorClip(north);
        ValidateEditorClip(south);
        ValidateEditorClip(east);

        string buildDirectory = Path.GetFullPath(Path.Combine("Builds", "WindowsMingyuanWings"));
        Directory.CreateDirectory(buildDirectory);
        AssetBundleBuild build = new AssetBundleBuild
        {
            assetBundleName = BundleName,
            assetNames = new[] { PrefabPath },
            addressableNames = new[] { BundleAddress }
        };

        AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
            buildDirectory,
            new[] { build },
            BuildAssetBundleOptions.ChunkBasedCompression
            | BuildAssetBundleOptions.StrictMode
            | BuildAssetBundleOptions.ForceRebuildAssetBundle,
            BuildTarget.StandaloneWindows64);
        if (manifest == null)
        {
            throw new InvalidOperationException("BuildPipeline returned no Mingyuan wing manifest.");
        }

        string sourceBundle = Path.Combine(buildDirectory, BundleName);
        if (!File.Exists(sourceBundle))
        {
            throw new FileNotFoundException("The Mingyuan wing bundle was not produced.", sourceBundle);
        }

        Directory.CreateDirectory(ModBundleDirectory);
        string destinationBundle = Path.Combine(ModBundleDirectory, BundleName);
        File.Copy(sourceBundle, destinationBundle, true);
        AssetDatabase.Refresh();

        string validationSummary = ValidateBuiltBundle();
        string report =
            "Mingyuan Unity full-frame wing animation build and validation succeeded." + Environment.NewLine
            + "Unity: " + Application.unityVersion + Environment.NewLine
            + "Built: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine
            + "Duration: 0.60 seconds" + Environment.NewLine
            + "Sample rate: 60 FPS" + Environment.NewLine
            + "Frame clock: 8 full-flight frames" + Environment.NewLine
            + "Bundle: " + destinationBundle + Environment.NewLine
            + "Address: " + BundleAddress + Environment.NewLine
            + validationSummary;
        File.WriteAllText(Path.Combine(Root, "MINGYUAN_WING_VALIDATION_REPORT.txt"), report);
        Debug.Log("[MingyuanWings] " + report.Replace(Environment.NewLine, " | "));
    }

    private static AnimationClip ConfigureClip(string path)
    {
        AnimationClip clip = LoadOrCreateClip(path);
        ClearCurves(clip);

        Keyframe[] keys = new Keyframe[FrameTimes.Length];
        for (int index = 0; index < keys.Length; index++)
        {
            keys[index] = new Keyframe(FrameTimes[index], index);
        }

        AnimationCurve frameClock = new AnimationCurve(keys)
        {
            preWrapMode = WrapMode.Loop,
            postWrapMode = WrapMode.Loop
        };
        for (int index = 0; index < frameClock.length; index++)
        {
            AnimationUtility.SetKeyLeftTangentMode(frameClock, index, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(frameClock, index, AnimationUtility.TangentMode.Linear);
        }

        SetCurve(clip, FrameClockPath, "m_LocalPosition.x", frameClock);
        SetCurve(clip, FrameClockPath, "m_LocalPosition.y", ConstantCurve(0f));
        SetCurve(clip, FrameClockPath, "m_LocalPosition.z", ConstantCurve(0f));
        SetLooping(clip);
        return clip;
    }

    private static AnimatorController ConfigureController(AnimationClip north, AnimationClip south, AnimationClip east)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        controller.parameters = Array.Empty<AnimatorControllerParameter>();
        AnimatorControllerLayer[] layers = controller.layers;
        if (layers.Length == 0)
        {
            controller.AddLayer("Base Layer");
            layers = controller.layers;
        }

        AnimatorStateMachine stateMachine = layers[0].stateMachine;
        ChildAnimatorState[] states = stateMachine.states;
        for (int index = 0; index < states.Length; index++)
        {
            stateMachine.RemoveState(states[index].state);
        }

        AnimatorState northState = stateMachine.AddState("North", new Vector3(240f, 20f));
        AnimatorState southState = stateMachine.AddState("South", new Vector3(240f, 100f));
        AnimatorState eastState = stateMachine.AddState("East", new Vector3(240f, 180f));
        northState.motion = north;
        southState.motion = south;
        eastState.motion = east;
        northState.speed = 1f;
        southState.speed = 1f;
        eastState.speed = 1f;
        stateMachine.defaultState = southState;
        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void CreatePrefab(RuntimeAnimatorController controller)
    {
        GameObject root = new GameObject("MingyuanWingDriver");
        try
        {
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;

            GameObject frameClock = new GameObject(FrameClockPath);
            frameClock.transform.SetParent(root.transform, false);
            frameClock.transform.localPosition = Vector3.zero;
            frameClock.transform.localRotation = Quaternion.identity;
            frameClock.transform.localScale = Vector3.one;

            PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void ValidateEditorClip(AnimationClip clip)
    {
        if (Mathf.Abs(clip.length - Duration) > 0.0001f || Mathf.Abs(clip.frameRate - 60f) > 0.001f)
        {
            throw new InvalidOperationException(clip.name + " is not a 0.60 second, 60 FPS clip.");
        }

        AnimationCurve clockCurve = null;
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        for (int index = 0; index < bindings.Length; index++)
        {
            EditorCurveBinding binding = bindings[index];
            if (binding.path == FrameClockPath && binding.propertyName == "m_LocalPosition.x")
            {
                clockCurve = AnimationUtility.GetEditorCurve(clip, binding);
                break;
            }
        }

        if (clockCurve == null || clockCurve.length != FrameTimes.Length)
        {
            throw new InvalidOperationException(clip.name + " does not contain the nine-key eight-frame clock.");
        }

        for (int index = 0; index < FrameTimes.Length; index++)
        {
            Keyframe key = clockCurve.keys[index];
            if (Mathf.Abs(key.time - FrameTimes[index]) > 0.0001f
                || Mathf.Abs(key.value - index) > 0.0001f)
            {
                throw new InvalidOperationException(clip.name + " frame-clock key " + index + " is invalid.");
            }
        }

        float loopError = Mathf.Abs(
            Mathf.Repeat(clockCurve.Evaluate(0f), FrameCount)
            - Mathf.Repeat(clockCurve.Evaluate(Duration), FrameCount));
        if (loopError > 0.0001f)
        {
            throw new InvalidOperationException(clip.name + " frame-clock loop error is " + loopError);
        }
    }

    private static string ValidateBuiltBundle()
    {
        string bundlePath = Path.Combine(ModBundleDirectory, BundleName);
        AssetBundle bundle = AssetBundle.LoadFromFile(bundlePath);
        if (bundle == null)
        {
            throw new InvalidOperationException("Validation could not load the Mingyuan wing bundle: " + bundlePath);
        }

        GameObject instance = null;
        try
        {
            Texture2D[] textures = bundle.LoadAllAssets<Texture2D>();
            Sprite[] sprites = bundle.LoadAllAssets<Sprite>();
            Material[] materials = bundle.LoadAllAssets<Material>();
            if (textures.Length != 0 || sprites.Length != 0 || materials.Length != 0)
            {
                throw new InvalidOperationException(
                    "The clock-only bundle unexpectedly contains textures, sprites or materials: "
                    + textures.Length + "/" + sprites.Length + "/" + materials.Length);
            }

            GameObject prefab = bundle.LoadAsset<GameObject>(BundleAddress);
            if (prefab == null)
            {
                throw new InvalidOperationException("Validation could not load address: " + BundleAddress);
            }

            if (prefab.GetComponentsInChildren<Renderer>(true).Length != 0)
            {
                throw new InvalidOperationException("The runtime prefab must not contain Renderer components.");
            }

            instance = UnityEngine.Object.Instantiate(prefab);
            Animator animator = instance.GetComponent<Animator>();
            Transform frameClock = instance.transform.Find(FrameClockPath);
            if (animator == null || animator.runtimeAnimatorController == null || frameClock == null)
            {
                throw new InvalidOperationException("The runtime prefab is missing its Animator/controller or FrameClock.");
            }

            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.speed = 0f;
            animator.Rebind();
            animator.Update(0f);

            string[] states = { "North", "South", "East" };
            List<string> lines = new List<string>();
            for (int stateIndex = 0; stateIndex < states.Length; stateIndex++)
            {
                string stateName = states[stateIndex];
                int stateHash = Animator.StringToHash("Base Layer." + stateName);
                if (!animator.HasState(0, stateHash))
                {
                    throw new InvalidOperationException("Animator state is missing: " + stateName);
                }

                bool[] sampledFrames = new bool[FrameCount];
                float minimumCursor = float.MaxValue;
                float maximumCursor = float.MinValue;
                for (int sample = 0; sample < 360; sample++)
                {
                    float phase = sample / 360f;
                    animator.Play(stateHash, 0, phase);
                    animator.Update(0f);
                    float cursor = Mathf.Repeat(frameClock.localPosition.x, FrameCount);
                    minimumCursor = Mathf.Min(minimumCursor, cursor);
                    maximumCursor = Mathf.Max(maximumCursor, cursor);
                    sampledFrames[Mathf.Clamp(Mathf.FloorToInt(cursor), 0, FrameCount - 1)] = true;
                }

                int distinctFrames = 0;
                for (int frameIndex = 0; frameIndex < sampledFrames.Length; frameIndex++)
                {
                    if (sampledFrames[frameIndex])
                    {
                        distinctFrames++;
                    }
                }

                if (distinctFrames != FrameCount || minimumCursor > 0.001f || maximumCursor < 7.8f)
                {
                    throw new InvalidOperationException(
                        stateName + " did not traverse all eight frames: "
                        + minimumCursor + "-" + maximumCursor + ", distinct=" + distinctFrames);
                }

                lines.Add(
                    stateName + ": cursor=" + minimumCursor.ToString("0.000")
                    + "-" + maximumCursor.ToString("0.000")
                    + ", distinctFrames=" + distinctFrames);
            }

            lines.Add("Frame timing ticks: 0/3/9/12/15/21/25/30/36");
            lines.Add("Bundle texture/sprite/material assets: 0/0/0");
            lines.Add("Loop modulo error: <=0.0001");
            return string.Join(Environment.NewLine, lines.ToArray()) + Environment.NewLine;
        }
        finally
        {
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            bundle.Unload(true);
        }
    }

    private static AnimationClip LoadOrCreateClip(string path)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }

        clip.frameRate = 60f;
        return clip;
    }

    private static void ClearCurves(AnimationClip clip)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);
        for (int index = 0; index < bindings.Length; index++)
        {
            AnimationUtility.SetEditorCurve(clip, bindings[index], null);
        }

        EditorCurveBinding[] objectBindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
        for (int index = 0; index < objectBindings.Length; index++)
        {
            AnimationUtility.SetObjectReferenceCurve(clip, objectBindings[index], null);
        }

        clip.frameRate = 60f;
    }

    private static AnimationCurve ConstantCurve(float value)
    {
        return new AnimationCurve(new Keyframe(0f, value), new Keyframe(Duration, value));
    }

    private static void SetCurve(AnimationClip clip, string path, string property, AnimationCurve curve)
    {
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), property),
            curve);
    }

    private static void SetLooping(AnimationClip clip)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = true;
        settings.loopBlend = false;
        settings.startTime = 0f;
        settings.stopTime = Duration;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        EditorUtility.SetDirty(clip);
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string name = Path.GetFileName(path);
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, name);
    }
}
#endif

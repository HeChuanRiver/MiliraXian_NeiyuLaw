#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

[InitializeOnLoad]
public static class NeiyuEarAutomation
{
    private const string AutomationVersion = "4";
    private const string Root = "Assets/NeiyuEar";
    private const string AnimationFolder = Root + "/Animations";
    private const string ControllerFolder = Root + "/Controllers";
    private const string PrefabFolder = Root + "/Prefabs";
    private const string IdleClipPath = AnimationFolder + "/Ear_Idle.anim";
    private const string TwitchClipPath = AnimationFolder + "/Ear_Twitch.anim";
    private const string AlertClipPath = AnimationFolder + "/Ear_Alert.anim";
    private const string ControllerPath = ControllerFolder + "/NeiyuEarDriver.controller";
    private const string RuntimePrefabPath = PrefabFolder + "/NeiyuEarDriver_Runtime.prefab";
    private const string PreviewPrefabPath = PrefabFolder + "/NeiyuEarDriver_Preview.prefab";
    private const string PairPath = "EarPair_Motion";
    private const string LeftPath = PairPath + "/EarA_Motion";
    private const string RightPath = PairPath + "/EarB_Motion";
    private const string BundleName = "neiyu_ear_anim";
    private const string BundleAddress = "neiyu_ear_driver";
    private const string ModBundleDirectory = @"E:\SteamLibrary\steamapps\common\RimWorld\Mods\MiliraXian_NeiyuLaw\1.6\AssetBundles\Windows";

    private static readonly string MarkerPath = Path.Combine("Library", "NeiyuEarAutomation.version");
    private static bool queued;
    private static bool running;

    static NeiyuEarAutomation()
    {
        QueueAutoBuild();
    }

    [MenuItem("Tools/Neiyu/Build Ear Animation Bundle")]
    private static void BuildFromMenu()
    {
        GenerateAndBuild();
    }

    private static void QueueAutoBuild()
    {
        if (queued)
        {
            return;
        }

        queued = true;
        EditorApplication.delayCall += TryAutoBuild;
    }

    private static void TryAutoBuild()
    {
        queued = false;
        if (Application.isBatchMode)
        {
            return;
        }

        if (running)
        {
            return;
        }

        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            QueueAutoBuild();
            return;
        }

        if (File.Exists(MarkerPath) && File.ReadAllText(MarkerPath).Trim() == AutomationVersion)
        {
            return;
        }

        GenerateAndBuild();
    }

    public static void BuildFromCommandLine()
    {
        GenerateAndBuild();
    }

    public static void ValidateFromCommandLine()
    {
        string bundlePath = Path.Combine(ModBundleDirectory, BundleName);
        AssetBundle validationBundle = AssetBundle.LoadFromFile(bundlePath);
        if (validationBundle == null)
        {
            throw new InvalidOperationException("Validation could not load bundle: " + bundlePath);
        }

        GameObject instance = null;
        try
        {
            GameObject prefab = validationBundle.LoadAsset<GameObject>(BundleAddress);
            if (prefab == null)
            {
                throw new InvalidOperationException("Validation could not load address: " + BundleAddress);
            }

            instance = UnityEngine.Object.Instantiate(prefab);
            Animator animator = instance.GetComponent<Animator>();
            Transform pair = instance.transform.Find(PairPath);
            if (animator == null || pair == null)
            {
                throw new InvalidOperationException("Validation prefab is missing Animator or EarPair_Motion.");
            }

            bool hasTwitch = false;
            bool hasAlert = false;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
            {
                hasTwitch |= parameter.name == "Twitch" && parameter.type == AnimatorControllerParameterType.Trigger;
                hasAlert |= parameter.name == "Alert" && parameter.type == AnimatorControllerParameterType.Trigger;
            }

            if (!hasTwitch || !hasAlert)
            {
                throw new InvalidOperationException("Validation controller is missing the Twitch or Alert trigger.");
            }

            float twitchMotion = SampleTrigger(animator, pair, "Twitch", 50);
            float alertMotion = SampleTrigger(animator, pair, "Alert", 70);
            if (twitchMotion < 0.5f || alertMotion < 1f)
            {
                throw new InvalidOperationException(
                    "Validation animation motion was too small: Twitch=" + twitchMotion + ", Alert=" + alertMotion);
            }

            string report =
                "Neiyu ear animation validation succeeded." + Environment.NewLine +
                "Unity: " + Application.unityVersion + Environment.NewLine +
                "Validated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                "Bundle address: " + BundleAddress + Environment.NewLine +
                "Twitch sampled motion: " + twitchMotion.ToString("0.000") + Environment.NewLine +
                "Alert sampled motion: " + alertMotion.ToString("0.000") + Environment.NewLine;
            File.WriteAllText(Path.Combine(Root, "NEIYU_EAR_VALIDATION_REPORT.txt"), report);
            Debug.Log("[NeiyuEar] " + report.Replace(Environment.NewLine, " | "));
        }
        finally
        {
            if (instance != null)
            {
                UnityEngine.Object.DestroyImmediate(instance);
            }

            validationBundle.Unload(true);
        }
    }

    private static float SampleTrigger(Animator animator, Transform pair, string trigger, int frames)
    {
        animator.Rebind();
        animator.Update(0f);
        animator.SetTrigger(trigger);
        float maximumMotion = 0f;
        for (int frame = 0; frame < frames; frame++)
        {
            animator.Update(1f / 60f);
            float angle = Mathf.Abs(Mathf.DeltaAngle(0f, pair.localEulerAngles.z));
            float scaleMotion = Mathf.Abs(pair.localScale.x - 1f) * 100f
                + Mathf.Abs(pair.localScale.y - 1f) * 100f;
            maximumMotion = Mathf.Max(maximumMotion, angle + scaleMotion);
        }

        return maximumMotion;
    }

    private static void GenerateAndBuild()
    {
        if (running)
        {
            return;
        }

        running = true;
        try
        {
            EnsureFolder(Root);
            EnsureFolder(AnimationFolder);
            EnsureFolder(ControllerFolder);
            EnsureFolder(PrefabFolder);

            AnimationClip idle = ConfigureIdleClip();
            AnimationClip twitch = ConfigureTwitchClip();
            AnimationClip alert = ConfigureAlertClip();
            AnimatorController controller = ConfigureController(idle, twitch, alert);

            CreateDriverPrefab(RuntimePrefabPath, controller, false);
            CreateDriverPrefab(PreviewPrefabPath, controller, true);
            AssetDatabase.SaveAssets();

            string projectBundleDirectory = Path.GetFullPath(Path.Combine("Builds", "Windows"));
            Directory.CreateDirectory(projectBundleDirectory);
            AssetBundleBuild build = new AssetBundleBuild
            {
                assetBundleName = BundleName,
                assetNames = new[] { RuntimePrefabPath },
                addressableNames = new[] { BundleAddress }
            };

            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
                projectBundleDirectory,
                new[] { build },
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode,
                BuildTarget.StandaloneWindows64);
            if (manifest == null)
            {
                throw new InvalidOperationException("BuildPipeline returned no AssetBundle manifest.");
            }

            string sourceBundle = Path.Combine(projectBundleDirectory, BundleName);
            if (!File.Exists(sourceBundle))
            {
                throw new FileNotFoundException("The expected ear animation bundle was not produced.", sourceBundle);
            }

            Directory.CreateDirectory(ModBundleDirectory);
            string modBundle = Path.Combine(ModBundleDirectory, BundleName);
            File.Copy(sourceBundle, modBundle, true);

            string report =
                "Neiyu ear animation build succeeded." + Environment.NewLine +
                "Automation version: " + AutomationVersion + Environment.NewLine +
                "Unity: " + Application.unityVersion + Environment.NewLine +
                "Built: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
                "Controller: " + ControllerPath + Environment.NewLine +
                "Runtime prefab: " + RuntimePrefabPath + Environment.NewLine +
                "Preview prefab: " + PreviewPrefabPath + Environment.NewLine +
                "Bundle: " + modBundle + Environment.NewLine;
            File.WriteAllText(Path.Combine(Root, "NEIYU_EAR_BUILD_REPORT.txt"), report);
            File.WriteAllText(MarkerPath, AutomationVersion);
            AssetDatabase.Refresh();
            Debug.Log("[NeiyuEar] " + report.Replace(Environment.NewLine, " | "));
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
        finally
        {
            running = false;
        }
    }

    private static AnimationClip ConfigureIdleClip()
    {
        AnimationClip clip = LoadOrCreateClip(IdleClipPath);
        ClearCurves(clip);
        SetRotation(clip, PairPath, Keys(0f, 0f, 1f, 0f));
        SetRotation(clip, LeftPath, Keys(0f, 0f, 1f, 0f));
        SetRotation(clip, RightPath, Keys(0f, 0f, 1f, 0f));
        SetScale(clip, PairPath, Keys(0f, 1f, 1f, 1f), Keys(0f, 1f, 1f, 1f));
        SetLooping(clip, true);
        return clip;
    }

    private static AnimationClip ConfigureTwitchClip()
    {
        AnimationClip clip = LoadOrCreateClip(TwitchClipPath);
        ClearCurves(clip);
        SetRotation(clip, PairPath, Keys(0f, 0f, 0.07f, -2.2f, 0.13f, 1.6f, 0.21f, -1f, 0.31f, 0.4f, 0.42f, 0f));
        SetRotation(clip, LeftPath, Keys(0f, 0f, 0.07f, -8f, 0.13f, 4f, 0.21f, -3f, 0.31f, 1.5f, 0.42f, 0f));
        SetRotation(clip, RightPath, Keys(0f, 0f, 0.07f, 8f, 0.13f, -4f, 0.21f, 3f, 0.31f, -1.5f, 0.42f, 0f));
        SetScale(
            clip,
            PairPath,
            Keys(0f, 1f, 0.07f, 0.985f, 0.13f, 1.012f, 0.21f, 0.994f, 0.31f, 1.003f, 0.42f, 1f),
            Keys(0f, 1f, 0.07f, 1.018f, 0.13f, 0.992f, 0.21f, 1.01f, 0.31f, 0.998f, 0.42f, 1f));
        SetLooping(clip, false);
        return clip;
    }

    private static AnimationClip ConfigureAlertClip()
    {
        AnimationClip clip = LoadOrCreateClip(AlertClipPath);
        ClearCurves(clip);
        SetRotation(clip, PairPath, Keys(0f, 0f, 0.08f, -3.5f, 0.16f, 2.5f, 0.27f, -2f, 0.39f, 1.5f, 0.5f, -0.7f, 0.62f, 0f));
        SetRotation(clip, LeftPath, Keys(0f, 0f, 0.08f, -12f, 0.16f, 7f, 0.27f, -9f, 0.39f, 5f, 0.5f, -2f, 0.62f, 0f));
        SetRotation(clip, RightPath, Keys(0f, 0f, 0.08f, 12f, 0.16f, -7f, 0.27f, 9f, 0.39f, -5f, 0.5f, 2f, 0.62f, 0f));
        SetScale(
            clip,
            PairPath,
            Keys(0f, 1f, 0.08f, 0.97f, 0.16f, 1.025f, 0.27f, 0.982f, 0.39f, 1.015f, 0.5f, 0.995f, 0.62f, 1f),
            Keys(0f, 1f, 0.08f, 1.035f, 0.16f, 0.98f, 0.27f, 1.025f, 0.39f, 0.988f, 0.5f, 1.006f, 0.62f, 1f));
        SetLooping(clip, false);
        return clip;
    }

    private static AnimatorController ConfigureController(AnimationClip idle, AnimationClip twitch, AnimationClip alert)
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(ControllerPath);
        }

        controller.parameters = Array.Empty<AnimatorControllerParameter>();
        controller.AddParameter("Twitch", AnimatorControllerParameterType.Trigger);
        controller.AddParameter("Alert", AnimatorControllerParameterType.Trigger);

        AnimatorControllerLayer[] layers = controller.layers;
        if (layers.Length == 0)
        {
            controller.AddLayer("Base Layer");
            layers = controller.layers;
        }

        AnimatorStateMachine stateMachine = layers[0].stateMachine;
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            stateMachine.RemoveState(childState.state);
        }

        foreach (AnimatorStateTransition transition in stateMachine.anyStateTransitions)
        {
            stateMachine.RemoveAnyStateTransition(transition);
        }

        AnimatorState idleState = stateMachine.AddState("Idle", new Vector3(260f, 40f));
        AnimatorState twitchState = stateMachine.AddState("Twitch", new Vector3(520f, -20f));
        AnimatorState alertState = stateMachine.AddState("Alert", new Vector3(520f, 100f));
        idleState.motion = idle;
        twitchState.motion = twitch;
        alertState.motion = alert;
        stateMachine.defaultState = idleState;

        AnimatorStateTransition anyToTwitch = stateMachine.AddAnyStateTransition(twitchState);
        ConfigureTriggeredTransition(anyToTwitch, "Twitch");
        AnimatorStateTransition anyToAlert = stateMachine.AddAnyStateTransition(alertState);
        ConfigureTriggeredTransition(anyToAlert, "Alert");
        ConfigureReturnTransition(twitchState.AddTransition(idleState));
        ConfigureReturnTransition(alertState.AddTransition(idleState));

        EditorUtility.SetDirty(controller);
        return controller;
    }

    private static void ConfigureTriggeredTransition(AnimatorStateTransition transition, string trigger)
    {
        transition.hasExitTime = false;
        transition.hasFixedDuration = true;
        transition.duration = 0.02f;
        transition.canTransitionToSelf = false;
        transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
        transition.AddCondition(AnimatorConditionMode.If, 0f, trigger);
    }

    private static void ConfigureReturnTransition(AnimatorStateTransition transition)
    {
        transition.hasExitTime = true;
        transition.exitTime = 1f;
        transition.hasFixedDuration = true;
        transition.duration = 0.03f;
        transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
    }

    private static void CreateDriverPrefab(string prefabPath, RuntimeAnimatorController controller, bool includePreviewRenderers)
    {
        GameObject root = new GameObject(includePreviewRenderers ? "NeiyuEarDriver_Preview" : "NeiyuEarDriver_Runtime");
        try
        {
            Animator animator = root.AddComponent<Animator>();
            animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = false;
            animator.updateMode = AnimatorUpdateMode.UnscaledTime;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            GameObject pair = NewChild(root.transform, PairPath, Vector3.zero);
            GameObject left = NewChild(pair.transform, "EarA_Motion", new Vector3(-0.107f, -0.027f, 0f));
            GameObject right = NewChild(pair.transform, "EarB_Motion", new Vector3(0.107f, -0.027f, 0f));

            if (includePreviewRenderers)
            {
                Sprite leftSprite = FindSprite("South_Left");
                Sprite rightSprite = FindSprite("South_Right");
                if (leftSprite != null && rightSprite != null)
                {
                    left.AddComponent<SpriteRenderer>().sprite = leftSprite;
                    right.AddComponent<SpriteRenderer>().sprite = rightSprite;
                }
                else
                {
                    Debug.LogWarning("[NeiyuEar] South_Left/South_Right preview sprites were not found; the runtime animation is unaffected.");
                }
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GameObject NewChild(Transform parent, string name, Vector3 localPosition)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = localPosition;
        return child;
    }

    private static Sprite FindSprite(string name)
    {
        string[] guids = AssetDatabase.FindAssets(name + " t:Sprite", new[] { Root + "/Textures" });
        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                Sprite sprite = asset as Sprite;
                if (sprite != null && sprite.name == name)
                {
                    return sprite;
                }
            }
        }

        return null;
    }

    private static AnimationClip LoadOrCreateClip(string path)
    {
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip != null)
        {
            return clip;
        }

        clip = new AnimationClip { frameRate = 60f };
        AssetDatabase.CreateAsset(clip, path);
        return clip;
    }

    private static void ClearCurves(AnimationClip clip)
    {
        foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
        {
            AnimationUtility.SetEditorCurve(clip, binding, null);
        }

        foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
        {
            AnimationUtility.SetObjectReferenceCurve(clip, binding, null);
        }

        clip.frameRate = 60f;
    }

    private static void SetRotation(AnimationClip clip, string path, AnimationCurve curve)
    {
        SetCurve(clip, path, "localEulerAnglesRaw.z", curve);
    }

    private static void SetScale(AnimationClip clip, string path, AnimationCurve x, AnimationCurve y)
    {
        SetCurve(clip, path, "m_LocalScale.x", x);
        SetCurve(clip, path, "m_LocalScale.y", y);
        SetCurve(clip, path, "m_LocalScale.z", Keys(0f, 1f, x.keys[x.length - 1].time, 1f));
    }

    private static void SetCurve(AnimationClip clip, string path, string propertyName, AnimationCurve curve)
    {
        AnimationUtility.SetEditorCurve(
            clip,
            EditorCurveBinding.FloatCurve(path, typeof(Transform), propertyName),
            curve);
    }

    private static AnimationCurve Keys(params float[] timeValuePairs)
    {
        if (timeValuePairs.Length % 2 != 0)
        {
            throw new ArgumentException("Animation keys must be supplied as time/value pairs.");
        }

        List<Keyframe> keys = new List<Keyframe>(timeValuePairs.Length / 2);
        for (int index = 0; index < timeValuePairs.Length; index += 2)
        {
            keys.Add(new Keyframe(timeValuePairs[index], timeValuePairs[index + 1]));
        }

        AnimationCurve curve = new AnimationCurve(keys.ToArray());
        for (int index = 0; index < curve.length; index++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.ClampedAuto);
        }

        return curve;
    }

    private static void SetLooping(AnimationClip clip, bool loop)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlend = false;
        settings.startTime = 0f;
        settings.stopTime = clip.length;
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

#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

public static class CharacterVfxAutomation
{
    private const string Root = "Assets/CharacterVfx";
    private const string TextureFolder = Root + "/Textures";
    private const string AnimationFolder = Root + "/Animations";
    private const string ControllerFolder = Root + "/Controllers";
    private const string PrefabFolder = Root + "/Prefabs";
    private const string SoftGlowAssetPath = TextureFolder + "/SoftGlow.png";
    private const string BundleName = "miliraxian_character_vfx";
    private const string ModRoot = @"E:\SteamLibrary\steamapps\common\RimWorld\Mods\MiliraXian_NeiyuLaw";
    private const string ModBundleDirectory = ModRoot + @"\1.6\AssetBundles\Windows";

    private sealed class TextureSpec
    {
        public TextureSpec(string key, string relativeSource, float pixelsPerUnit = 100f)
        {
            Key = key;
            RelativeSource = relativeSource;
            PixelsPerUnit = pixelsPerUnit;
        }

        public string Key { get; private set; }
        public string RelativeSource { get; private set; }
        public float PixelsPerUnit { get; private set; }
        public string AssetPath { get { return TextureFolder + "/" + Key + ".png"; } }
    }

    private static readonly TextureSpec[] Textures =
    {
        new TextureSpec("Minghuo1", @"Content\Textures\MiliraXianZhaoli\Effect\Minghuo\MinghuoAuraFrame1.png"),
        new TextureSpec("Minghuo2", @"Content\Textures\MiliraXianZhaoli\Effect\Minghuo\MinghuoAuraFrame2.png"),
        new TextureSpec("Minghuo3", @"Content\Textures\MiliraXianZhaoli\Effect\Minghuo\MinghuoAuraFrame3.png"),
        new TextureSpec("Guiyi1", @"Content\Textures\MiliraXianZhaoli\Effect\Guiyi\GuiyiHealFrame1.png"),
        new TextureSpec("Guiyi2", @"Content\Textures\MiliraXianZhaoli\Effect\Guiyi\GuiyiHealFrame2.png"),
        new TextureSpec("Guiyi3", @"Content\Textures\MiliraXianZhaoli\Effect\Guiyi\GuiyiHealFrame3.png"),
        new TextureSpec("DeathField", @"Content\Textures\MiliraXianZhaoli\Effect\DeathField\DeathFieldArea.png"),
        new TextureSpec("DeathParticle1", @"Content\Textures\MiliraXianZhaoli\Effect\DeathField\DeathFieldParticle1.png"),
        new TextureSpec("DeathParticle2", @"Content\Textures\MiliraXianZhaoli\Effect\DeathField\DeathFieldParticle2.png"),
        new TextureSpec("DeathParticle3", @"Content\Textures\MiliraXianZhaoli\Effect\DeathField\DeathFieldParticle3.png"),
        new TextureSpec("Minshen", @"Content\Textures\MiliraXianZhaoli\Effect\Minshen\MinshenArea.png"),
        new TextureSpec("ForFeather", @"Content\Textures\MiliraXianNeiyu\Effect\ForFeather.png"),
        new TextureSpec("SkyfallGround", @"Content\Textures\MiliraXianNeiyu\Effect\FlyBegin_Ground.png"),
        new TextureSpec("SkyfallFly", @"Content\Textures\MiliraXianNeiyu\Effect\FlyBegin_Fly.png"),
        new TextureSpec("BladeLight", @"Content\Textures\MiliraXianNeiyu\Effect\BladeLight_Neiyu\BladeLight_Neiyu_A.png"),
        new TextureSpec("NeiyuHaloSouth", @"Content\Textures\MiliraXianNeiyu\PawnNeiyu\Halo\MiliraXianHaloNeiyu_south.png", 512f),
        new TextureSpec("NeiyuHaloNorth", @"Content\Textures\MiliraXianNeiyu\PawnNeiyu\Halo\MiliraXianHaloNeiyu_north.png", 512f),
        new TextureSpec("NeiyuHaloEast", @"Content\Textures\MiliraXianNeiyu\PawnNeiyu\Halo\MiliraXianHaloNeiyu_east.png", 512f),
        new TextureSpec("ZhaoliHaloSouth", @"Content\Textures\MiliraXianZhaoli\Pawn\Halo\MiliraXianHaloZhaoli_south.png", 512f),
        new TextureSpec("ZhaoliHaloNorth", @"Content\Textures\MiliraXianZhaoli\Pawn\Halo\MiliraXianHaloZhaoli_north.png", 512f),
        new TextureSpec("ZhaoliHaloEast", @"Content\Textures\MiliraXianZhaoli\Pawn\Halo\MiliraXianHaloZhaoli_east.png", 512f)
    };

    private static readonly string[] Addresses =
    {
        "zhaoli_minghuo_vfx",
        "zhaoli_guiyi_vfx",
        "zhaoli_deathfield_vfx",
        "zhaoli_minshen_vfx",
        "zhaoli_minshen_impact_vfx",
        "neiyu_flower_circle_vfx",
        "neiyu_skyfall_takeoff_vfx",
        "neiyu_skyfall_warning_vfx",
        "neiyu_skyfall_impact_vfx",
        "neiyu_halo_vfx",
        "zhaoli_halo_vfx"
    };

    [MenuItem("Tools/MiliraXian/Build Character VFX Bundle")]
    public static void BuildFromMenu()
    {
        GenerateAndBuild();
    }

    public static void BuildFromCommandLine()
    {
        GenerateAndBuild();
    }

    public static void ValidateFromCommandLine()
    {
        string path = Path.Combine(ModBundleDirectory, BundleName);
        AssetBundle bundle = AssetBundle.LoadFromFile(path);
        if (bundle == null)
        {
            throw new InvalidOperationException("Could not load character VFX bundle: " + path);
        }

        try
        {
            List<string> lines = new List<string>();
            lines.Add("MiliraXian character VFX validation succeeded.");
            lines.Add("Unity: " + Application.unityVersion);
            lines.Add("Validated: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            for (int index = 0; index < Addresses.Length; index++)
            {
                GameObject prefab = bundle.LoadAsset<GameObject>(Addresses[index]);
                if (prefab == null)
                {
                    throw new InvalidOperationException("Missing VFX bundle address: " + Addresses[index]);
                }

                Animator animator = prefab.GetComponent<Animator>();
                SpriteRenderer[] renderers = prefab.GetComponentsInChildren<SpriteRenderer>(true);
                if (animator == null || animator.runtimeAnimatorController == null || renderers.Length == 0)
                {
                    throw new InvalidOperationException("Invalid VFX prefab: " + Addresses[index]);
                }

                int distinctSamples = CountDistinctAnimationSamples(prefab);
                if (distinctSamples < 2)
                {
                    throw new InvalidOperationException("VFX animation did not change when sampled: " + Addresses[index]);
                }

                lines.Add(Addresses[index] + ": renderers=" + renderers.Length + ", distinctSamples=" + distinctSamples);
            }

            string report = string.Join(Environment.NewLine, lines.ToArray()) + Environment.NewLine;
            File.WriteAllText(Path.Combine(Root, "CHARACTER_VFX_VALIDATION_REPORT.txt"), report);
            Debug.Log(report.Replace(Environment.NewLine, " | "));
        }
        finally
        {
            bundle.Unload(true);
        }
    }

    private static int CountDistinctAnimationSamples(GameObject prefab)
    {
        GameObject instance = UnityEngine.Object.Instantiate(prefab);
        try
        {
            Animator animator = instance.GetComponent<Animator>();
            SpriteRenderer[] renderers = instance.GetComponentsInChildren<SpriteRenderer>(true);
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.speed = 0f;
            HashSet<string> signatures = new HashSet<string>();
            float[] samples = { 0f, 0.17f, 0.36f, 0.61f, 0.84f };
            for (int sampleIndex = 0; sampleIndex < samples.Length; sampleIndex++)
            {
                animator.Play("Play", 0, samples[sampleIndex]);
                animator.Update(0f);
                System.Text.StringBuilder signature = new System.Text.StringBuilder();
                for (int rendererIndex = 0; rendererIndex < renderers.Length; rendererIndex++)
                {
                    SpriteRenderer renderer = renderers[rendererIndex];
                    Transform transform = renderer.transform;
                    signature.Append(renderer.sprite != null ? renderer.sprite.name : "null");
                    signature.Append('|').Append(renderer.color.a.ToString("0.000"));
                    signature.Append('|').Append(transform.position.x.ToString("0.000"));
                    signature.Append('|').Append(transform.position.y.ToString("0.000"));
                    signature.Append('|').Append(transform.lossyScale.x.ToString("0.000"));
                    signature.Append('|').Append(transform.lossyScale.y.ToString("0.000"));
                    signature.Append('|').Append(transform.eulerAngles.z.ToString("0.000"));
                    signature.Append(';');
                }

                signatures.Add(signature.ToString());
            }

            return signatures.Count;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(instance);
        }
    }

    private static void GenerateAndBuild()
    {
        EnsureFolder(Root);
        EnsureFolder(TextureFolder);
        EnsureFolder(AnimationFolder);
        EnsureFolder(ControllerFolder);
        EnsureFolder(PrefabFolder);
        ImportTextures();

        Dictionary<string, Sprite> sprites = LoadSprites();
        List<string> prefabPaths = new List<string>();
        prefabPaths.Add(CreateMinghuo(sprites));
        prefabPaths.Add(CreateGuiyi(sprites));
        prefabPaths.Add(CreateDeathField(sprites));
        prefabPaths.Add(CreateMinshen(sprites, false));
        prefabPaths.Add(CreateMinshen(sprites, true));
        prefabPaths.Add(CreateFlowerCircle(sprites));
        prefabPaths.Add(CreateSkyfallTakeoff(sprites));
        prefabPaths.Add(CreateSkyfallWarning(sprites));
        prefabPaths.Add(CreateSkyfallImpact(sprites));
        prefabPaths.Add(CreateNeiyuHalo(sprites));
        prefabPaths.Add(CreateZhaoliHalo(sprites));
        AssetDatabase.SaveAssets();

        string buildDirectory = Path.GetFullPath(Path.Combine("Builds", "WindowsCharacterVfx"));
        Directory.CreateDirectory(buildDirectory);
        AssetBundleBuild build = new AssetBundleBuild
        {
            assetBundleName = BundleName,
            assetNames = prefabPaths.ToArray(),
            addressableNames = Addresses
        };

        AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(
            buildDirectory,
            new[] { build },
            BuildAssetBundleOptions.ChunkBasedCompression |
            BuildAssetBundleOptions.StrictMode |
            BuildAssetBundleOptions.ForceRebuildAssetBundle,
            BuildTarget.StandaloneWindows64);
        if (manifest == null)
        {
            throw new InvalidOperationException("BuildPipeline returned no character VFX manifest.");
        }

        string sourceBundle = Path.Combine(buildDirectory, BundleName);
        if (!File.Exists(sourceBundle))
        {
            throw new FileNotFoundException("Character VFX bundle was not produced.", sourceBundle);
        }

        Directory.CreateDirectory(ModBundleDirectory);
        string destinationBundle = Path.Combine(ModBundleDirectory, BundleName);
        File.Copy(sourceBundle, destinationBundle, true);
        string report =
            "MiliraXian character VFX build succeeded." + Environment.NewLine +
            "Unity: " + Application.unityVersion + Environment.NewLine +
            "Built: " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + Environment.NewLine +
            "Bundle: " + destinationBundle + Environment.NewLine +
            "Addresses: " + string.Join(", ", Addresses) + Environment.NewLine;
        File.WriteAllText(Path.Combine(Root, "CHARACTER_VFX_BUILD_REPORT.txt"), report);
        AssetDatabase.Refresh();
        Debug.Log(report.Replace(Environment.NewLine, " | "));
    }

    private static void ImportTextures()
    {
        for (int index = 0; index < Textures.Length; index++)
        {
            TextureSpec spec = Textures[index];
            string source = Path.Combine(ModRoot, spec.RelativeSource);
            if (!File.Exists(source))
            {
                throw new FileNotFoundException("Missing source texture for Unity VFX.", source);
            }

            string destination = Path.GetFullPath(spec.AssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(source, destination, true);
        }

        WriteSoftGlowTexture();
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        for (int index = 0; index < Textures.Length; index++)
        {
            TextureImporter importer = AssetImporter.GetAtPath(Textures[index].AssetPath) as TextureImporter;
            if (importer == null)
            {
                throw new InvalidOperationException("Could not configure texture: " + Textures[index].AssetPath);
            }

            ConfigureSpriteImporter(importer, Textures[index].PixelsPerUnit);
        }

        TextureImporter glowImporter = AssetImporter.GetAtPath(SoftGlowAssetPath) as TextureImporter;
        if (glowImporter == null)
        {
            throw new InvalidOperationException("Could not configure generated glow texture: " + SoftGlowAssetPath);
        }

        ConfigureSpriteImporter(glowImporter, 64f);
    }

    private static Dictionary<string, Sprite> LoadSprites()
    {
        Dictionary<string, Sprite> result = new Dictionary<string, Sprite>();
        for (int index = 0; index < Textures.Length; index++)
        {
            Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Textures[index].AssetPath);
            if (sprite == null)
            {
                throw new InvalidOperationException("Could not load imported sprite: " + Textures[index].AssetPath);
            }

            result.Add(Textures[index].Key, sprite);
        }

        Sprite softGlow = AssetDatabase.LoadAssetAtPath<Sprite>(SoftGlowAssetPath);
        if (softGlow == null)
        {
            throw new InvalidOperationException("Could not load generated glow sprite: " + SoftGlowAssetPath);
        }

        result.Add("SoftGlow", softGlow);

        return result;
    }

    private static string CreateMinghuo(Dictionary<string, Sprite> sprites)
    {
        const string id = "ZhaoliMinghuo";
        GameObject root = NewRoot(id);
        try
        {
            SpriteRenderer main = NewSprite(root.transform, "MainGlow", sprites["Minghuo1"], 0, new Color(1f, 1f, 1f, 0.82f));
            main.transform.localScale = new Vector3(0.35f, 0.35f, 1f);
            AnimationClip clip = NewClip(id, 0.3f, true);
            SetSprites(clip, "MainGlow", new[]
            {
                SpriteKey(0f, sprites["Minghuo1"]),
                SpriteKey(0.1f, sprites["Minghuo2"]),
                SpriteKey(0.2f, sprites["Minghuo3"]),
                SpriteKey(0.3f, sprites["Minghuo1"])
            });
            SetScale(clip, "MainGlow", Keys(0f, 0.35f, 0.1f, 0.365f, 0.2f, 0.342f, 0.3f, 0.35f), Keys(0f, 0.35f, 0.1f, 0.342f, 0.2f, 0.365f, 0.3f, 0.35f));
            SetRotation(clip, "MainGlow", Keys(0f, -1.2f, 0.1f, 0.8f, 0.2f, -0.4f, 0.3f, -1.2f));
            SetAlpha(clip, "MainGlow", Keys(0f, 0.78f, 0.1f, 0.92f, 0.2f, 0.84f, 0.3f, 0.78f));
            return SavePrefab(root, id, clip);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static string CreateGuiyi(Dictionary<string, Sprite> sprites)
    {
        const string id = "ZhaoliGuiyi";
        GameObject root = NewRoot(id);
        try
        {
            SpriteRenderer main = NewSprite(root.transform, "MainGlow", sprites["Guiyi1"], 0, Color.white);
            main.transform.localScale = new Vector3(0.45f, 0.45f, 1f);
            AnimationClip clip = NewClip(id, 0.3f, false);
            SetSprites(clip, "MainGlow", new[]
            {
                SpriteKey(0f, sprites["Guiyi1"]),
                SpriteKey(0.09f, sprites["Guiyi2"]),
                SpriteKey(0.18f, sprites["Guiyi3"])
            });
            SetScale(clip, "MainGlow", Keys(0f, 0.4f, 0.09f, 0.58f, 0.18f, 0.76f, 0.3f, 0.94f), Keys(0f, 0.4f, 0.09f, 0.58f, 0.18f, 0.76f, 0.3f, 0.94f));
            SetRotation(clip, "MainGlow", Keys(0f, -8f, 0.18f, 4f, 0.3f, 12f));
            SetAlpha(clip, "MainGlow", Keys(0f, 0f, 0.04f, 1f, 0.22f, 0.9f, 0.3f, 0f));
            return SavePrefab(root, id, clip);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static string CreateDeathField(Dictionary<string, Sprite> sprites)
    {
        const string id = "ZhaoliDeathField";
        GameObject root = NewRoot(id);
        try
        {
            SpriteRenderer field = NewSprite(root.transform, "Field", sprites["DeathField"], 0, new Color(1f, 1f, 1f, 0.58f));
            field.transform.localScale = new Vector3(1.75f, 1.75f, 1f);
            SpriteRenderer pulse = NewSprite(root.transform, "PulseGlow", sprites["DeathField"], 1, new Color(0.8f, 0.45f, 1f, 0.2f));
            pulse.transform.localScale = new Vector3(1.75f, 1.75f, 1f);
            GameObject particleRoot = NewChild(root.transform, "Particles", Vector3.zero);
            for (int index = 0; index < 12; index++)
            {
                float angle = index * Mathf.PI * 2f / 12f;
                float radius = 3.2f + (index % 4) * 1.05f;
                Sprite sprite = sprites["DeathParticle" + (index % 3 + 1)];
                SpriteRenderer particle = NewSprite(particleRoot.transform, "ParticleGlow" + index, sprite, 2 + index, new Color(0.9f, 0.62f, 1f, 0f));
                particle.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                particle.transform.localScale = new Vector3(0.16f, 0.16f, 1f);
            }

            AnimationClip clip = NewClip(id, 6f, true);
            SetRotation(clip, "Field", Keys(0f, 0f, 6f, -360f));
            SetScale(clip, "Field", RepeatingWave(6f, 1f, 1.71f, 1.79f), RepeatingWave(6f, 1f, 1.71f, 1.79f));
            SetAlpha(clip, "Field", Keys(0f, 0.48f, 0.3f, 0.6f, 5.7f, 0.6f, 6f, 0.48f));
            SetRotation(clip, "PulseGlow", Keys(0f, 0f, 6f, 180f));
            SetScale(clip, "PulseGlow", RepeatingWave(6f, 1f, 1.72f, 1.94f), RepeatingWave(6f, 1f, 1.72f, 1.94f));
            SetAlpha(clip, "PulseGlow", RepeatingPulse(6f, 1f, 0f, 0.22f));
            SetRotation(clip, "Particles", Keys(0f, 0f, 6f, 120f));
            for (int index = 0; index < 12; index++)
            {
                string path = "Particles/ParticleGlow" + index;
                float phase = index / 12f * 2f;
                SetAlpha(clip, path, ParticlePulse(6f, phase));
                SetScale(clip, path, ParticleScale(6f, phase), ParticleScale(6f, phase));
                SetRotation(clip, path, Keys(0f, index * 13f, 6f, index * 13f + (index % 2 == 0 ? 150f : -150f)));
            }

            return SavePrefab(root, id, clip);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static string CreateMinshen(Dictionary<string, Sprite> sprites, bool impact)
    {
        string id = impact ? "ZhaoliMinshenImpact" : "ZhaoliMinshen";
        GameObject root = NewRoot(id);
        try
        {
            SpriteRenderer field = NewSprite(root.transform, "Field", sprites["Minshen"], 0, new Color(0.55f, 0.22f, 0.72f, 0f));
            field.transform.localScale = new Vector3(impact ? 0.4f : 0.72f, impact ? 0.4f : 0.72f, 1f);
            SpriteRenderer pulse = NewSprite(root.transform, "PulseGlow", sprites["Minshen"], 1, new Color(0.82f, 0.42f, 1f, 0f));
            pulse.transform.localScale = field.transform.localScale;
            for (int index = 0; index < 10; index++)
            {
                float angle = (index * 137.5f + 20f) * Mathf.Deg2Rad;
                float radius = 2.2f + index % 4;
                SpriteRenderer particle = NewSprite(root.transform, "ParticleGlow" + index, sprites["DeathParticle" + (index % 3 + 1)], 2 + index, new Color(0.9f, 0.58f, 1f, 0f));
                particle.transform.localPosition = new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f);
                particle.transform.localScale = Vector3.one * 0.14f;
            }

            AnimationClip clip = NewClip(id, 1f, false);
            if (impact)
            {
                SetScale(clip, "Field", Keys(0f, 0.35f, 0.16f, 1.32f, 0.55f, 1.22f, 1f, 1.42f), Keys(0f, 0.35f, 0.16f, 1.32f, 0.55f, 1.22f, 1f, 1.42f));
                SetAlpha(clip, "Field", Keys(0f, 0f, 0.06f, 0.72f, 0.32f, 0.48f, 1f, 0f));
                SetScale(clip, "PulseGlow", Keys(0f, 0.25f, 0.2f, 1.5f, 1f, 1.72f), Keys(0f, 0.25f, 0.2f, 1.5f, 1f, 1.72f));
                SetAlpha(clip, "PulseGlow", Keys(0f, 0f, 0.05f, 0.65f, 0.28f, 0f, 1f, 0f));
            }
            else
            {
                SetScale(clip, "Field", Keys(0f, 0.68f, 0.7f, 1.25f, 1f, 1.32f), Keys(0f, 0.68f, 0.7f, 1.25f, 1f, 1.32f));
                SetAlpha(clip, "Field", Keys(0f, 0f, 0.12f, 0.32f, 0.72f, 0.48f, 1f, 0.18f));
                SetScale(clip, "PulseGlow", Keys(0f, 0.72f, 0.5f, 1.18f, 1f, 1.45f), Keys(0f, 0.72f, 0.5f, 1.18f, 1f, 1.45f));
                SetAlpha(clip, "PulseGlow", Keys(0f, 0f, 0.25f, 0.16f, 0.55f, 0f, 0.72f, 0.28f, 1f, 0f));
            }

            SetRotation(clip, "Field", Keys(0f, impact ? -15f : 0f, 1f, impact ? 40f : -55f));
            for (int index = 0; index < 10; index++)
            {
                string path = "ParticleGlow" + index;
                Transform transform = root.transform.Find(path);
                Vector3 start = transform.localPosition;
                Vector3 end = impact ? start * 1.35f : start * 0.45f;
                SetPosition(clip, path, Keys(0f, start.x, 1f, end.x), Keys(0f, start.y, 1f, end.y));
                float phase = index * 0.055f;
                SetAlpha(clip, path, Keys(0f, 0f, Mathf.Min(0.9f, phase + 0.08f), 0f, Mathf.Min(0.94f, phase + 0.18f), 0.92f, Mathf.Min(0.98f, phase + 0.42f), 0f, 1f, 0f));
                SetScale(clip, path, Keys(0f, 0.08f, Mathf.Min(0.95f, phase + 0.22f), 0.22f, 1f, 0.1f), Keys(0f, 0.08f, Mathf.Min(0.95f, phase + 0.22f), 0.22f, 1f, 0.1f));
            }

            return SavePrefab(root, id, clip);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static string CreateFlowerCircle(Dictionary<string, Sprite> sprites)
    {
        const string id = "NeiyuFlowerCircle";
        GameObject root = NewRoot(id);
        try
        {
            SpriteRenderer main = NewSprite(root.transform, "Main", sprites["ForFeather"], 0, new Color(1f, 0.72f, 0.88f, 0f));
            SpriteRenderer glow = NewSprite(root.transform, "PulseGlow", sprites["ForFeather"], 1, new Color(1f, 0.48f, 0.82f, 0f));
            AnimationClip clip = NewClip(id, 1.1f, false);
            SetScale(clip, "Main", Keys(0f, 0.18f, 0.28f, 1.08f, 0.82f, 0.96f, 1.1f, 0.3f), Keys(0f, 0.18f, 0.28f, 1.08f, 0.82f, 0.96f, 1.1f, 0.3f));
            SetAlpha(clip, "Main", Keys(0f, 0f, 0.08f, 0.92f, 0.86f, 0.75f, 1.1f, 0f));
            SetRotation(clip, "Main", Keys(0f, -8f, 1.1f, 22f));
            SetScale(clip, "PulseGlow", Keys(0f, 0.12f, 0.38f, 1.22f, 1.1f, 1.55f), Keys(0f, 0.12f, 0.38f, 1.22f, 1.1f, 1.55f));
            SetAlpha(clip, "PulseGlow", Keys(0f, 0f, 0.14f, 0.5f, 0.55f, 0.16f, 1.1f, 0f));
            SetRotation(clip, "PulseGlow", Keys(0f, 8f, 1.1f, -28f));
            return SavePrefab(root, id, clip);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static string CreateSkyfallTakeoff(Dictionary<string, Sprite> sprites)
    {
        const string id = "NeiyuSkyfallTakeoff";
        GameObject root = NewRoot(id);
        try
        {
            NewSprite(root.transform, "GroundGlow", sprites["SkyfallGround"], 0, new Color(1f, 0.7f, 0.9f, 0f));
            NewSprite(root.transform, "FlyGlow", sprites["SkyfallFly"], 1, new Color(1f, 0.82f, 0.94f, 0f));
            AnimationClip clip = NewClip(id, 0.55f, false);
            SetScale(clip, "GroundGlow", Keys(0f, 0.08f, 0.16f, 0.27f, 0.55f, 0.46f), Keys(0f, 0.08f, 0.16f, 0.27f, 0.55f, 0.46f));
            SetAlpha(clip, "GroundGlow", Keys(0f, 0f, 0.06f, 0.9f, 0.3f, 0.5f, 0.55f, 0f));
            SetScale(clip, "FlyGlow", Keys(0f, 0.18f, 0.2f, 0.28f, 0.55f, 0.14f), Keys(0f, 0.18f, 0.2f, 0.28f, 0.55f, 0.48f));
            SetPosition(clip, "FlyGlow", Keys(0f, 0f, 0.55f, 0f), Keys(0f, -1.8f, 0.55f, 2.8f));
            SetAlpha(clip, "FlyGlow", Keys(0f, 0f, 0.08f, 1f, 0.36f, 0.68f, 0.55f, 0f));
            return SavePrefab(root, id, clip);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static string CreateSkyfallWarning(Dictionary<string, Sprite> sprites)
    {
        const string id = "NeiyuSkyfallWarning";
        GameObject root = NewRoot(id);
        try
        {
            NewSprite(root.transform, "Warning", sprites["ForFeather"], 0, new Color(1f, 0.26f, 0.46f, 0f));
            NewSprite(root.transform, "PulseGlow", sprites["ForFeather"], 1, new Color(1f, 0.58f, 0.78f, 0f));
            AnimationClip clip = NewClip(id, 1f, false);
            SetScale(clip, "Warning", RepeatingWave(1f, 0.25f, 1.0f, 1.18f), RepeatingWave(1f, 0.25f, 1.0f, 1.18f));
            SetAlpha(clip, "Warning", Keys(0f, 0f, 0.08f, 0.72f, 0.86f, 0.78f, 1f, 0f));
            SetRotation(clip, "Warning", Keys(0f, 0f, 1f, 35f));
            SetScale(clip, "PulseGlow", RepeatingWave(1f, 0.25f, 0.95f, 1.38f), RepeatingWave(1f, 0.25f, 0.95f, 1.38f));
            SetAlpha(clip, "PulseGlow", RepeatingPulse(1f, 0.25f, 0f, 0.42f));
            return SavePrefab(root, id, clip);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static string CreateSkyfallImpact(Dictionary<string, Sprite> sprites)
    {
        const string id = "NeiyuSkyfallImpact";
        GameObject root = NewRoot(id);
        try
        {
            NewSprite(root.transform, "FlyGlow", sprites["SkyfallFly"], 2, new Color(1f, 0.8f, 0.95f, 0f));
            NewSprite(root.transform, "GroundGlow", sprites["SkyfallGround"], 0, new Color(1f, 0.6f, 0.86f, 0f));
            NewSprite(root.transform, "SlashGlow", sprites["BladeLight"], 1, new Color(1f, 0.72f, 0.9f, 0f));
            AnimationClip clip = NewClip(id, 1f, false);
            SetPosition(clip, "FlyGlow", Keys(0f, 0f, 1f, 0f), Keys(0f, 4f, 0.34f, 0f, 1f, 0f));
            SetScale(clip, "FlyGlow", Keys(0f, 0.18f, 0.34f, 0.34f, 1f, 0.14f), Keys(0f, 0.42f, 0.34f, 0.22f, 1f, 0.1f));
            SetAlpha(clip, "FlyGlow", Keys(0f, 0f, 0.05f, 0.85f, 0.34f, 1f, 0.48f, 0f, 1f, 0f));
            SetScale(clip, "GroundGlow", Keys(0f, 0.08f, 0.34f, 0.08f, 0.48f, 0.42f, 1f, 0.72f), Keys(0f, 0.08f, 0.34f, 0.08f, 0.48f, 0.42f, 1f, 0.72f));
            SetAlpha(clip, "GroundGlow", Keys(0f, 0f, 0.33f, 0f, 0.39f, 0.95f, 0.68f, 0.42f, 1f, 0f));
            SetScale(clip, "SlashGlow", Keys(0f, 0.1f, 0.34f, 0.1f, 0.48f, 0.9f, 1f, 1.28f), Keys(0f, 0.1f, 0.34f, 0.1f, 0.48f, 0.9f, 1f, 1.28f));
            SetRotation(clip, "SlashGlow", Keys(0f, -35f, 0.34f, -35f, 1f, 95f));
            SetAlpha(clip, "SlashGlow", Keys(0f, 0f, 0.34f, 0f, 0.42f, 0.9f, 0.72f, 0.3f, 1f, 0f));
            return SavePrefab(root, id, clip);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static string CreateNeiyuHalo(Dictionary<string, Sprite> sprites)
    {
        const string id = "NeiyuHalo";
        const float length = 18f;
        GameObject root = NewRoot(id);
        try
        {
            GameObject motion = NewChild(root.transform, "HaloMotion", Vector3.zero);
            string[] directions = { "South", "North", "East" };
            Vector2[] centers =
            {
                new Vector2(0.108f, 0.198f),
                new Vector2(-0.108f, 0.198f),
                new Vector2(0.108f, 0.198f)
            };
            float[] ellipseAngles = { -31f, 31f, -31f };

            AnimationClip clip = NewClip(id, length, true);
            SetPosition(
                clip,
                "HaloMotion",
                Keys(0f, 0f, length, 0f),
                Keys(0f, -0.070f, 4.5f, -0.048f, 9f, -0.070f, 13.5f, -0.048f, length, -0.070f));
            SetScale(
                clip,
                "HaloMotion",
                Keys(0f, 0.995f, 4.5f, 1.008f, 9f, 0.995f, 13.5f, 1.008f, length, 0.995f),
                Keys(0f, 0.992f, 4.5f, 1.010f, 9f, 0.992f, 13.5f, 1.010f, length, 0.992f));

            for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
            {
                string direction = directions[directionIndex];
                GameObject group = NewChild(motion.transform, direction, Vector3.zero);
                string groupPath = "HaloMotion/" + direction;
                Sprite haloSprite = sprites["NeiyuHalo" + direction];
                NewSprite(group.transform, "HaloBaseGlow_" + direction, haloSprite, 0, Color.white);
                SpriteRenderer pulse = NewSprite(group.transform, "HaloPulseGlow_" + direction, haloSprite, 1, new Color(1f, 0.72f, 0.86f, 0.12f));
                pulse.transform.localScale = new Vector3(1.014f, 1.014f, 1f);
                SetAlpha(
                    clip,
                    groupPath + "/HaloPulseGlow_" + direction,
                    Keys(0f, 0.06f, 2.25f, 0.18f, 4.5f, 0.06f, 6.75f, 0.15f, 9f, 0.06f, 11.25f, 0.18f, 13.5f, 0.06f, 15.75f, 0.15f, length, 0.06f));

                for (int particleIndex = 0; particleIndex < 3; particleIndex++)
                {
                    Color color = particleIndex == 0
                        ? new Color(1f, 0.38f, 0.72f, 0.78f)
                        : particleIndex == 1
                            ? new Color(1f, 0.64f, 0.30f, 0.72f)
                            : new Color(1f, 0.76f, 0.88f, 0.68f);
                    string particleName = "FlowGlow_" + direction + "_" + particleIndex;
                    SpriteRenderer flow = NewSprite(group.transform, particleName, sprites["SoftGlow"], 2 + particleIndex, color);
                    flow.transform.localScale = new Vector3(0.085f, 0.085f, 1f);
                    string particlePath = groupPath + "/" + particleName;
                    SetEllipsePath(
                        clip,
                        particlePath,
                        centers[directionIndex],
                        new Vector2(0.104f, 0.054f),
                        ellipseAngles[directionIndex],
                        particleIndex / 3f,
                        length);
                    SetScale(
                        clip,
                        particlePath,
                        RepeatingWave(length, 3f, 0.068f, 0.102f),
                        RepeatingWave(length, 3f, 0.068f, 0.102f));
                    SetAlpha(
                        clip,
                        particlePath,
                        RepeatingWave(length, 3f, 0.38f + particleIndex * 0.05f, 0.82f));
                }

                if (direction != "South")
                {
                    SpriteRenderer[] previewRenderers = group.GetComponentsInChildren<SpriteRenderer>(true);
                    for (int rendererIndex = 0; rendererIndex < previewRenderers.Length; rendererIndex++)
                    {
                        previewRenderers[rendererIndex].enabled = false;
                    }
                }
            }

            return SavePrefab(root, id, clip);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static string CreateZhaoliHalo(Dictionary<string, Sprite> sprites)
    {
        const string id = "ZhaoliHalo";
        const float length = 18f;
        GameObject root = NewRoot(id);
        try
        {
            GameObject motion = NewChild(root.transform, "HaloMotion", Vector3.zero);
            string[] directions = { "South", "North", "East" };
            Vector2[] centers =
            {
                new Vector2(0.112f, 0.180f),
                new Vector2(-0.132f, 0.180f),
                new Vector2(0.112f, 0.180f)
            };
            Vector2[] fragmentPath =
            {
                new Vector2(-0.073f, 0.067f),
                new Vector2(-0.020f, 0.052f),
                new Vector2(0.056f, 0.028f),
                new Vector2(0.077f, -0.025f),
                new Vector2(0.040f, -0.083f),
                new Vector2(-0.018f, -0.099f),
                new Vector2(-0.068f, -0.043f)
            };

            AnimationClip clip = NewClip(id, length, true);
            SetPosition(
                clip,
                "HaloMotion",
                Keys(0f, 0f, length, 0f),
                Keys(0f, -0.068f, 4.5f, -0.050f, 9f, -0.068f, 13.5f, -0.050f, length, -0.068f));
            SetScale(
                clip,
                "HaloMotion",
                Keys(0f, 0.996f, 4.5f, 1.006f, 9f, 0.996f, 13.5f, 1.006f, length, 0.996f),
                Keys(0f, 0.994f, 4.5f, 1.008f, 9f, 0.994f, 13.5f, 1.008f, length, 0.994f));

            for (int directionIndex = 0; directionIndex < directions.Length; directionIndex++)
            {
                string direction = directions[directionIndex];
                bool mirrorPattern = direction == "North";
                GameObject group = NewChild(motion.transform, direction, Vector3.zero);
                string groupPath = "HaloMotion/" + direction;
                Sprite haloSprite = sprites["ZhaoliHalo" + direction];
                NewSprite(group.transform, "HaloBaseGlow_" + direction, haloSprite, 0, Color.white);
                SpriteRenderer pulse = NewSprite(group.transform, "HaloPulseGlow_" + direction, haloSprite, 1, new Color(0.76f, 0.36f, 1f, 0.10f));
                pulse.transform.localScale = new Vector3(1.012f, 1.012f, 1f);
                SetAlpha(
                    clip,
                    groupPath + "/HaloPulseGlow_" + direction,
                    Keys(0f, 0.04f, 2.25f, 0.15f, 4.5f, 0.04f, 6.75f, 0.12f, 9f, 0.04f, 11.25f, 0.15f, 13.5f, 0.04f, 15.75f, 0.12f, length, 0.04f));

                for (int particleIndex = 0; particleIndex < fragmentPath.Length; particleIndex++)
                {
                    Vector2 point = fragmentPath[particleIndex];
                    if (mirrorPattern)
                    {
                        point.x = -point.x;
                    }

                    point += centers[directionIndex];
                    string particleName = "FragmentGlow_" + direction + "_" + particleIndex;
                    SpriteRenderer fragment = NewSprite(
                        group.transform,
                        particleName,
                        sprites["SoftGlow"],
                        2 + particleIndex,
                        new Color(0.72f, 0.34f, 1f, 0.7f));
                    fragment.transform.localPosition = new Vector3(point.x, point.y, 0f);
                    fragment.transform.localScale = new Vector3(0.072f, 0.072f, 1f);
                    string particlePath = groupPath + "/" + particleName;
                    float phase = particleIndex / (float)fragmentPath.Length;
                    SetAlpha(clip, particlePath, CircularPulse(length, phase, 0.032f, 0.88f));
                    SetScale(
                        clip,
                        particlePath,
                        CircularPulse(length, phase, 0.052f, 0.094f),
                        CircularPulse(length, phase, 0.052f, 0.094f));
                    SetPosition(
                        clip,
                        particlePath,
                        Keys(0f, point.x, 9f, point.x + (particleIndex % 2 == 0 ? 0.004f : -0.004f), length, point.x),
                        Keys(0f, point.y, 9f, point.y + 0.006f, length, point.y));
                }

                if (direction != "South")
                {
                    SpriteRenderer[] previewRenderers = group.GetComponentsInChildren<SpriteRenderer>(true);
                    for (int rendererIndex = 0; rendererIndex < previewRenderers.Length; rendererIndex++)
                    {
                        previewRenderers[rendererIndex].enabled = false;
                    }
                }
            }

            return SavePrefab(root, id, clip);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static GameObject NewRoot(string id)
    {
        return new GameObject(id + "_Runtime");
    }

    private static GameObject NewChild(Transform parent, string name, Vector3 position)
    {
        GameObject child = new GameObject(name);
        child.transform.SetParent(parent, false);
        child.transform.localPosition = position;
        return child;
    }

    private static SpriteRenderer NewSprite(Transform parent, string name, Sprite sprite, int order, Color color)
    {
        GameObject child = NewChild(parent, name, Vector3.zero);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = order;
        // Keep generated prefabs visible in the Unity editor for inspection.
        // The RimWorld runtime disables these renderers immediately after instantiation
        // and draws their animated state through Graphics.DrawMesh instead.
        renderer.enabled = true;
        return renderer;
    }

    private static AnimationClip NewClip(string id, float length, bool loop)
    {
        string path = AnimationFolder + "/" + id + ".anim";
        AnimationClip existing = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (existing != null)
        {
            AssetDatabase.DeleteAsset(path);
        }

        AnimationClip clip = new AnimationClip { frameRate = 60f, name = id };
        AssetDatabase.CreateAsset(clip, path);
        SetCurve(clip, string.Empty, "m_LocalScale.x", Keys(0f, 1f, length, 1f));
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = loop;
        settings.loopBlend = false;
        settings.startTime = 0f;
        settings.stopTime = length;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        return clip;
    }

    private static string SavePrefab(GameObject root, string id, AnimationClip clip)
    {
        string controllerPath = ControllerFolder + "/" + id + ".controller";
        AnimatorController oldController = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (oldController != null)
        {
            AssetDatabase.DeleteAsset(controllerPath);
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState state = stateMachine.AddState("Play");
        state.motion = clip;
        stateMachine.defaultState = state;
        Animator animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        string prefabPath = PrefabFolder + "/" + id + ".prefab";
        PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
        return prefabPath;
    }

    private static void SetSprites(AnimationClip clip, string path, ObjectReferenceKeyframe[] keys)
    {
        AnimationUtility.SetObjectReferenceCurve(
            clip,
            EditorCurveBinding.PPtrCurve(path, typeof(SpriteRenderer), "m_Sprite"),
            keys);
    }

    private static ObjectReferenceKeyframe SpriteKey(float time, Sprite sprite)
    {
        return new ObjectReferenceKeyframe { time = time, value = sprite };
    }

    private static void SetScale(AnimationClip clip, string path, AnimationCurve x, AnimationCurve y)
    {
        SetCurve(clip, path, "m_LocalScale.x", x);
        SetCurve(clip, path, "m_LocalScale.y", y);
        SetCurve(clip, path, "m_LocalScale.z", Keys(0f, 1f, clip.length, 1f));
    }

    private static void SetPosition(AnimationClip clip, string path, AnimationCurve x, AnimationCurve y)
    {
        SetCurve(clip, path, "m_LocalPosition.x", x);
        SetCurve(clip, path, "m_LocalPosition.y", y);
    }

    private static void SetRotation(AnimationClip clip, string path, AnimationCurve curve)
    {
        SetCurve(clip, path, "localEulerAnglesRaw.z", curve);
    }

    private static void SetAlpha(AnimationClip clip, string path, AnimationCurve curve)
    {
        SetCurve(clip, path, "m_Color.a", curve, typeof(SpriteRenderer));
    }

    private static void SetCurve(AnimationClip clip, string path, string property, AnimationCurve curve)
    {
        SetCurve(clip, path, property, curve, typeof(Transform));
    }

    private static void SetCurve(AnimationClip clip, string path, string property, AnimationCurve curve, Type type)
    {
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
        EditorUtility.SetDirty(clip);
    }

    private static AnimationCurve Keys(params float[] pairs)
    {
        if (pairs.Length % 2 != 0)
        {
            throw new ArgumentException("Animation keys require time/value pairs.");
        }

        Keyframe[] keys = new Keyframe[pairs.Length / 2];
        for (int index = 0; index < keys.Length; index++)
        {
            keys[index] = new Keyframe(pairs[index * 2], pairs[index * 2 + 1]);
        }

        AnimationCurve curve = new AnimationCurve(keys);
        for (int index = 0; index < curve.length; index++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, index, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, index, AnimationUtility.TangentMode.ClampedAuto);
        }

        return curve;
    }

    private static AnimationCurve RepeatingWave(float length, float interval, float low, float high)
    {
        List<float> pairs = new List<float>();
        for (float start = 0f; start < length - 0.0001f; start += interval)
        {
            pairs.Add(start);
            pairs.Add(low);
            pairs.Add(Mathf.Min(length, start + interval * 0.5f));
            pairs.Add(high);
        }

        pairs.Add(length);
        pairs.Add(low);
        return Keys(pairs.ToArray());
    }

    private static AnimationCurve RepeatingPulse(float length, float interval, float low, float high)
    {
        List<float> pairs = new List<float>();
        for (float start = 0f; start < length - 0.0001f; start += interval)
        {
            pairs.Add(start);
            pairs.Add(low);
            pairs.Add(Mathf.Min(length, start + interval * 0.18f));
            pairs.Add(high);
            pairs.Add(Mathf.Min(length, start + interval * 0.52f));
            pairs.Add(low);
        }

        pairs.Add(length);
        pairs.Add(low);
        return Keys(pairs.ToArray());
    }

    private static AnimationCurve ParticlePulse(float length, float phase)
    {
        List<float> pairs = new List<float> { 0f, 0f };
        for (float start = phase; start < length; start += 2f)
        {
            float safeStart = Mathf.Max(0f, start);
            pairs.Add(safeStart);
            pairs.Add(0f);
            pairs.Add(Mathf.Min(length, safeStart + 0.18f));
            pairs.Add(0.9f);
            pairs.Add(Mathf.Min(length, safeStart + 0.75f));
            pairs.Add(0f);
        }

        pairs.Add(length);
        pairs.Add(0f);
        return Keys(pairs.ToArray());
    }

    private static AnimationCurve ParticleScale(float length, float phase)
    {
        List<float> pairs = new List<float> { 0f, 0.08f };
        for (float start = phase; start < length; start += 2f)
        {
            float safeStart = Mathf.Max(0f, start);
            pairs.Add(safeStart);
            pairs.Add(0.08f);
            pairs.Add(Mathf.Min(length, safeStart + 0.28f));
            pairs.Add(0.24f);
            pairs.Add(Mathf.Min(length, safeStart + 0.75f));
            pairs.Add(0.12f);
        }

        pairs.Add(length);
        pairs.Add(0.08f);
        return Keys(pairs.ToArray());
    }

    private static void SetEllipsePath(
        AnimationClip clip,
        string path,
        Vector2 center,
        Vector2 radius,
        float rotationDegrees,
        float phase,
        float length)
    {
        const int samples = 32;
        float rotation = rotationDegrees * Mathf.Deg2Rad;
        float cosRotation = Mathf.Cos(rotation);
        float sinRotation = Mathf.Sin(rotation);
        List<float> xPairs = new List<float>((samples + 1) * 2);
        List<float> yPairs = new List<float>((samples + 1) * 2);
        for (int index = 0; index <= samples; index++)
        {
            float normalized = index / (float)samples;
            float angle = (normalized + phase) * Mathf.PI * 2f;
            float ellipseX = Mathf.Cos(angle) * radius.x;
            float ellipseY = Mathf.Sin(angle) * radius.y;
            float x = center.x + ellipseX * cosRotation - ellipseY * sinRotation;
            float y = center.y + ellipseX * sinRotation + ellipseY * cosRotation;
            xPairs.Add(normalized * length);
            xPairs.Add(x);
            yPairs.Add(normalized * length);
            yPairs.Add(y);
        }

        SetPosition(clip, path, Keys(xPairs.ToArray()), Keys(yPairs.ToArray()));
    }

    private static AnimationCurve CircularPulse(float length, float phase, float low, float high)
    {
        const int samples = 48;
        List<float> pairs = new List<float>((samples + 1) * 2);
        for (int index = 0; index <= samples; index++)
        {
            float normalized = index / (float)samples;
            float distance = Mathf.Abs(normalized - phase);
            distance = Mathf.Min(distance, 1f - distance);
            float strength = Mathf.Clamp01(1f - distance / 0.13f);
            strength = strength * strength * (3f - 2f * strength);
            pairs.Add(normalized * length);
            pairs.Add(Mathf.Lerp(low, high, strength));
        }

        return Keys(pairs.ToArray());
    }

    private static void WriteSoftGlowTexture()
    {
        const int size = 64;
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        try
        {
            Color32[] pixels = new Color32[size * size];
            float center = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - center) / center;
                    float dy = (y - center) / center;
                    float distance = Mathf.Sqrt(dx * dx + dy * dy);
                    float alpha = Mathf.Pow(Mathf.Clamp01(1f - distance), 2.2f);
                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            string destination = Path.GetFullPath(SoftGlowAssetPath);
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.WriteAllBytes(destination, texture.EncodeToPNG());
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(texture);
        }
    }

    private static void ConfigureSpriteImporter(TextureImporter importer, float pixelsPerUnit)
    {
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = pixelsPerUnit;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.filterMode = FilterMode.Bilinear;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.maxTextureSize = 2048;
        importer.SaveAndReimport();
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

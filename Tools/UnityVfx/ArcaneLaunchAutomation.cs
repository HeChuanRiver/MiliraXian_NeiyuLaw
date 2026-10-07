#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Authoring is separate from ArcaneCircle: rebuilding launch resources never rewrites the chant.
public static class ArcaneLaunchAutomation
{
    private const string ModRoot = @"E:\SteamLibrary\steamapps\common\RimWorld\Mods\MiliraXian_NeiyuLaw";
    private const string Root = "Assets/ArcaneLaunch";
    private const string BundleName = "miliraxian_arcane_launch";

    [MenuItem("Tools/MiliraXian/Build ArcaneLaunch Bundle")]
    public static void BuildFromCommandLine()
    {
        Directory.CreateDirectory(Root + "/Art");
        Directory.CreateDirectory(Root + "/Prefabs");
        File.Copy(Path.Combine(ModRoot, @"Tools\UnityVfx\ArcaneEnergy.shader"), Root + "/ArcaneEnergy.shader", true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/ArcaneEnergy.shader");
        if (shader == null) throw new InvalidOperationException("ArcaneEnergy shader was not imported.");
        Sprite[] sprites = new Sprite[5];
        Material[] materials = new Material[5];
        string[] names = { "BeamCore", "BeamInnerGlow", "BeamOuterGlow", "ParticleColumn", "Shockwave" };
        for (int i = 0; i < names.Length; i++)
        {
            sprites[i] = BuildTexture(names[i], i);
            materials[i] = SaveMaterial(names[i], shader, sprites[i].texture);
        }
        GameObject prefab = new GameObject("ArcaneLaunch");
        try
        {
            Transform beam = Child(prefab.transform, "Beam");
            Layer(beam, names[0], sprites[0], materials[0], new Vector2(1.67f, 32f), new Color(1, 1, 1));
            Layer(beam, names[1], sprites[1], materials[1], new Vector2(5.4f, 32f), new Color(0.25f, 0.85f, 1));
            Layer(beam, names[2], sprites[2], materials[2], new Vector2(10.8f, 32f), new Color(0.18f, 0.3f, 1, 0.35f));
            Layer(Child(prefab.transform, "Particle"), names[3], sprites[3], materials[3], Vector2.one, Color.white);
            Layer(Child(prefab.transform, "Ground"), names[4], sprites[4], materials[4], Vector2.one * 24f, new Color(0.35f, 0.9f, 1));
            string prefabPath = Root + "/Prefabs/ArcaneLaunch.prefab";
            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            AssetDatabase.SaveAssets();
            string output = Path.Combine(ModRoot, @".arcane-strike-chant-build\LaunchBundle");
            Directory.CreateDirectory(output);
            AssetBundleBuild build = new AssetBundleBuild
            {
                assetBundleName = BundleName,
                assetNames = new[] { prefabPath },
                addressableNames = new[] { "arcane_launch" }
            };
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(output, new[] { build },
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode,
                BuildTarget.StandaloneWindows64);
            if (manifest == null) throw new InvalidOperationException("ArcaneLaunch AssetBundle build failed.");
            BuildSound();
            File.Copy(Path.Combine(output, BundleName), Path.Combine(ModRoot, @"1.6\AssetBundles\Windows", BundleName), true);
            Debug.Log("ArcaneLaunch AssetBundle built: " + BundleName);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(prefab);
        }
    }

    private static Sprite BuildTexture(string name, int kind)
    {
        int width = kind < 3 ? 128 : 512;
        const int height = 512;
        Color32[] pixels = new Color32[width * height];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1) - 0.5f;
                float v = y / (float)(height - 1) - 0.5f;
                float alpha;
                if (kind < 3)
                {
                    float narrowness = kind == 0 ? 45f : kind == 1 ? 19f : 10f;
                    alpha = Mathf.Exp(-u * u * narrowness);
                    alpha *= Mathf.Clamp01((0.5f - Mathf.Abs(v)) * 18f);
                    if (kind == 0) alpha *= 0.94f + 0.06f * Mathf.Cos(v * 80f);
                }
                else if (kind == 3)
                {
                    float r = u * u + v * v;
                    alpha = Mathf.Exp(-r * 35f)
                        + 0.18f * Mathf.Exp(-Mathf.Abs(u) * 80f - Mathf.Abs(v) * 14f)
                        + 0.18f * Mathf.Exp(-Mathf.Abs(v) * 80f - Mathf.Abs(u) * 14f);
                }
                else
                {
                    float r = Mathf.Sqrt(u * u + v * v);
                    alpha = Mathf.Exp(-Mathf.Pow((r - 0.43f) * 140f, 2))
                        + 0.45f * Mathf.Exp(-Mathf.Pow((r - 0.39f) * 100f, 2));
                    float angle = Mathf.Atan2(v, u);
                    alpha += 0.22f * Mathf.Pow(Mathf.Abs(Mathf.Cos(angle * 8f)), 35f)
                        * Mathf.Exp(-Mathf.Pow((r - 0.34f) * 40f, 2));
                }
                pixels[y * width + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(alpha) * 255f));
            }
        }
        string path = Root + "/Art/" + name + ".png";
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        try
        {
            texture.SetPixels32(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 128f;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    private static Material SaveMaterial(string name, Shader shader, Texture texture)
    {
        string path = Root + "/Art/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }
        material.shader = shader;
        material.mainTexture = texture;
        material.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static Transform Child(Transform parent, string name)
    {
        Transform child = new GameObject(name).transform;
        child.SetParent(parent, false);
        return child;
    }

    private static void Layer(Transform parent, string name, Sprite sprite, Material material, Vector2 size, Color color)
    {
        Transform child = Child(parent, name);
        SpriteRenderer renderer = child.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
        renderer.color = color;
        Vector3 native = sprite.bounds.size;
        child.localScale = new Vector3(size.x / native.x, size.y / native.y, 1);
        if (parent.name == "Beam") child.localPosition = Vector3.up * (size.y * 0.5f);
    }

    private static void BuildSound()
    {
        string folder = Path.Combine(ModRoot, @"Content\Sounds\WorldArcaneStrike");
        Directory.CreateDirectory(folder);
        const int rate = 44100;
        const double length = 4;
        const int samples = (int)(rate * length);
        using (BinaryWriter writer = new BinaryWriter(File.Create(Path.Combine(folder, "ArcaneLaunch.wav"))))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
            System.Random random = new System.Random(31415);
            double lowNoise = 0;
            for (int i = 0; i < samples; i++)
            {
                double t = i / (double)rate;
                double u = t / length;
                double noise = random.NextDouble() * 2 - 1;
                lowNoise = lowNoise * 0.985 + noise * 0.015;
                double rise = Math.Min(1, t * 20);
                double fall = Math.Min(1, (length - t) * 2.5);
                double thump = Math.Sin(Math.PI * 2 * (48 * t + 23 * (1 - Math.Exp(-t * 8)))) * Math.Exp(-t * 7);
                double roar = 0.24 * Math.Sin(Math.PI * 2 * 55 * t) + 0.12 * Math.Sin(Math.PI * 2 * 110 * t)
                    + lowNoise * 1.8 + noise * 0.1;
                double phase = Math.PI * 2 * (180 * t + 145 * t * t / length);
                double whine = Math.Sin(phase) * 0.1 + Math.Sin(phase * 2) * 0.04;
                double signal = (thump * 0.48 + roar + whine * (0.5 + u)) * rise * fall;
                writer.Write((short)(Math.Max(-0.98, Math.Min(0.98, signal)) * short.MaxValue));
            }
        }
    }
}
#endif

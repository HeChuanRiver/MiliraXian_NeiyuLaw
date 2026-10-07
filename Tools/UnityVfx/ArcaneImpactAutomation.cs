#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Permanent authoring source. Only the impact bundle, ash textures and impact sound
// are rebuilt here; the stable chant and launch resources are independent.
public static class ArcaneImpactAutomation
{
    private const string ModRoot = @"E:\SteamLibrary\steamapps\common\RimWorld\Mods\MiliraXian_NeiyuLaw";
    private const string Root = "Assets/ArcaneImpact";
    private const string BundleName = "miliraxian_arcane_impact";

    [MenuItem("Tools/MiliraXian/Build ArcaneImpact Bundle")]
    public static void BuildFromCommandLine()
    {
        Directory.CreateDirectory(Root + "/Art");
        Directory.CreateDirectory(Root + "/Prefabs");
        File.Copy(Path.Combine(ModRoot, @"Tools\UnityVfx\ArcaneCloud.shader"), Root + "/ArcaneCloud.shader", true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/ArcaneCloud.shader");
        if (shader == null) throw new InvalidOperationException("ArcaneCloud shader was not imported.");
        GameObject prefab = new GameObject("ArcaneImpact");
        try
        {
            AddLayer(prefab, "Cloud", shader, false);
            AddLayer(prefab, "Dust", shader, true);
            string prefabPath = Root + "/Prefabs/ArcaneImpact.prefab";
            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            AssetDatabase.SaveAssets();
            string output = Path.Combine(ModRoot, @".arcane-strike-chant-build\ImpactBundle");
            Directory.CreateDirectory(output);
            AssetBundleBuild build = new AssetBundleBuild
            {
                assetBundleName = BundleName,
                assetNames = new[] { prefabPath },
                addressableNames = new[] { "arcane_impact" }
            };
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(output, new[] { build },
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode,
                BuildTarget.StandaloneWindows64);
            if (manifest == null) throw new InvalidOperationException("ArcaneImpact AssetBundle build failed.");
            BuildScorchedTextures();
            BuildSound();
            File.Copy(Path.Combine(output, BundleName), Path.Combine(ModRoot, @"1.6\AssetBundles\Windows", BundleName), true);
            Debug.Log("ArcaneImpact AssetBundle built: " + BundleName);
        }
        finally { UnityEngine.Object.DestroyImmediate(prefab); }
    }

    private static void AddLayer(GameObject root, string name, Shader shader, bool dust)
    {
        const int size = 512;
        Color[] pixels = new Color[size * size];
        Vector3 light = new Vector3(-0.4f, 0.6f, 0.7f).normalized;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float u = x / (float)(size - 1) * 2f - 1f;
                float v = y / (float)(size - 1) * 2f - 1f;
                float r = Mathf.Sqrt(u * u + v * v);
                float angle = Mathf.Atan2(v, u);
                float noise = Fractal(u * 2.1f + 12f, v * 2.1f + 17f);
                float boundary = 0.8f + 0.08f * Mathf.Sin(angle * 7f + noise * 3f)
                    + 0.055f * Mathf.Cos(angle * 11f - noise * 4f);
                float density = Mathf.Clamp01((boundary - r) * (dust ? 5f : 13f));
                density *= Mathf.Clamp01(0.55f + noise * 0.7f);
                Vector3 normal = new Vector3(u, v, Mathf.Sqrt(Mathf.Max(0, 1f - Mathf.Min(1, r * r)))).normalized;
                float shading = 0.4f + 0.45f * Mathf.Max(0, Vector3.Dot(normal, light)) + noise * 0.17f;
                float ridges = Mathf.Abs(Mathf.Sin(noise * 18f + r * 5f));
                shading *= 0.75f + 0.25f * ridges;
                pixels[y * size + x] = new Color(shading * 0.98f, shading, shading * 1.03f, density);
            }
        string texturePath = Root + "/Art/" + name + ".png";
        WriteTexture(texturePath, size, pixels);
        AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceSynchronousImport);
        TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(texturePath);
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 128f;
        importer.mipmapEnabled = true;
        importer.alphaIsTransparency = true;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.SaveAndReimport();
        Sprite sprite = AssetDatabase.LoadAssetAtPath<Sprite>(texturePath);
        string materialPath = Root + "/Art/" + name + ".mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null)
        {
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, materialPath);
        }
        material.shader = shader;
        material.mainTexture = sprite.texture;
        material.SetColor("_Color", Color.white);
        EditorUtility.SetDirty(material);
        GameObject child = new GameObject(name);
        child.transform.SetParent(root.transform, false);
        SpriteRenderer renderer = child.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = material;
    }

    private static float Fractal(float x, float y)
    {
        return Mathf.PerlinNoise(x, y) * 0.55f + Mathf.PerlinNoise(x * 2.07f, y * 2.07f) * 0.28f
            + Mathf.PerlinNoise(x * 4.13f, y * 4.13f) * 0.12f + Mathf.PerlinNoise(x * 8.21f, y * 8.21f) * 0.05f;
    }

    private static void BuildScorchedTextures()
    {
        string folder = Path.Combine(ModRoot, @"Content\Textures\Common\WorldArcaneStrike");
        Directory.CreateDirectory(folder);
        const int size = 512;
        for (int kind = 0; kind < 2; kind++)
        {
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)size * 9f, v = y / (float)size * 9f;
                    float noise = Fractal(u + 31f, v + 51f);
                    float nearest = 10f, second = 10f;
                    int gx = Mathf.FloorToInt(u), gy = Mathf.FloorToInt(v);
                    for (int cy = gy - 1; cy <= gy + 1; cy++)
                        for (int cx = gx - 1; cx <= gx + 1; cx++)
                        {
                            int wx = (cx + 9) % 9, wy = (cy + 9) % 9;
                            float rx = Mathf.Repeat(Mathf.Sin(wx * 127.1f + wy * 311.7f) * 43758.5453f, 1f);
                            float ry = Mathf.Repeat(Mathf.Sin(wx * 269.5f + wy * 183.3f) * 43758.5453f, 1f);
                            float dx = cx + 0.15f + rx * 0.7f - u, dy = cy + 0.15f + ry * 0.7f - v;
                            float distance = Mathf.Sqrt(dx * dx + dy * dy);
                            if (distance < nearest) { second = nearest; nearest = distance; }
                            else if (distance < second) second = distance;
                        }
                    float crack = Mathf.Exp(-(second - nearest) * 50f);
                    float charcoal = (kind == 0 ? 0.15f : 0.24f) + noise * (kind == 0 ? 0.11f : 0.14f);
                    charcoal *= 1f - crack * 0.7f;
                    float ember = kind == 0 ? crack * Mathf.Clamp01((noise - 0.57f) * 5f) * 0.2f : 0;
                    pixels[y * size + x] = new Color(charcoal + ember, charcoal * 0.95f + ember * 0.3f,
                        charcoal * 1.05f + ember * 0.05f, 1);
                }
            WriteTexture(Path.Combine(folder, kind == 0 ? "ScorchedWorld.png" : "ScorchedGround.png"), size, pixels);
        }
    }

    private static void WriteTexture(string path, int size, Color[] pixels)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        try
        {
            texture.SetPixels(pixels);
            texture.Apply();
            File.WriteAllBytes(path, texture.EncodeToPNG());
        }
        finally { UnityEngine.Object.DestroyImmediate(texture); }
    }

    private static void BuildSound()
    {
        string folder = Path.Combine(ModRoot, @"Content\Sounds\WorldArcaneStrike");
        Directory.CreateDirectory(folder);
        const int rate = 44100, length = 9, samples = rate * length;
        using (BinaryWriter writer = new BinaryWriter(File.Create(Path.Combine(folder, "ArcaneImpact.wav"))))
        {
            writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); writer.Write(36 + samples * 2);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16); writer.Write((short)1); writer.Write((short)1);
            writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
            writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(samples * 2);
            System.Random random = new System.Random(271828);
            double low = 0, mid = 0;
            for (int i = 0; i < samples; i++)
            {
                double t = i / (double)rate;
                double noise = random.NextDouble() * 2 - 1;
                low = low * 0.992 + noise * 0.008;
                mid = mid * 0.91 + noise * 0.09;
                double punch = Math.Sin(2 * Math.PI * (36 * t + 10 * (1 - Math.Exp(-t * 12)))) * Math.Exp(-t * 3.5);
                double cracking = noise * Math.Exp(-t * 15) * 0.3;
                double rumble = (low * 4 + mid * 0.7 + 0.14 * Math.Sin(2 * Math.PI * 31 * t)) * Math.Exp(-t * 0.32);
                double echo = t < 0.3 ? 0 : Math.Sin(2 * Math.PI * 46 * (t - 0.3)) * Math.Exp(-(t - 0.3) * 1.3) * 0.18;
                double fade = Math.Min(1, t * 250) * Math.Min(1, (length - t) * 2);
                double signal = Math.Tanh((punch * 0.75 + cracking + rumble + echo) * 1.2) * fade * 0.92;
                writer.Write((short)(signal * short.MaxValue));
            }
        }
    }
}
#endif

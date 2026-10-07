#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

// Reproducible authoring source; builds only the ArcaneCircle bundle.
public static class ArcaneCircleAutomation
{
    private const string ModRoot = @"E:\SteamLibrary\steamapps\common\RimWorld\Mods\MiliraXian_NeiyuLaw";
    private const string Root = "Assets/ArcaneCircle";
    private const float Length = 60f;
    private const string BundleName = "miliraxian_arcane_circle";
    private static readonly Color Ice = new Color(0.12f, 0.62f, 1f, 1f);
    private static readonly Color Gold = new Color(1f, 0.68f, 0.12f, 1f);
    private static readonly Color Violet = new Color(0.63f, 0.22f, 1f, 1f);
    private static readonly Color Teal = new Color(0.12f, 1f, 0.64f, 1f);
    private static readonly Color Rose = new Color(1f, 0.20f, 0.47f, 1f);

    [MenuItem("Tools/MiliraXian/Build ArcaneCircle Bundle")]
    public static void BuildFromCommandLine()
    {
        Directory.CreateDirectory(Root + "/Art");
        Directory.CreateDirectory(Root + "/Animations");
        Directory.CreateDirectory(Root + "/Prefabs");
        File.Copy(Path.Combine(ModRoot, @"Tools\UnityVfx\ArcaneCircle.shader"), Root + "/ArcaneCircle.shader", true);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Shader shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "/ArcaneCircle.shader");
        if (shader == null)
        {
            throw new InvalidOperationException("ArcaneCircle shader was not imported.");
        }
        Dictionary<string, Sprite> art = BuildArt();
        BuildSounds();
        Material light = SaveMaterial("ArcaneLight", shader, null);
        Material spark = SaveMaterial("Spark", shader, art["Spark"].texture);
        Material rune = SaveMaterial("RuneParticle", shader, art["RuneParticle"].texture);
        GameObject root = new GameObject("ArcaneCircle");
        try
        {
            AnimationClip clip = CreateClip();
            Transform circle = Child(root.transform, "Circle").transform;
            SetCurve(clip, "Circle", typeof(Transform), "m_LocalScale.x", Keys(0, 1, 0.96f, 1, 0.9725f, 1, 0.981f, 0.82f, 0.991f, 0.24f, 0.998f, 0.01f, 1, 0));
            SetCurve(clip, "Circle", typeof(Transform), "m_LocalScale.y", Keys(0, 1, 0.96f, 1, 0.9725f, 1, 0.981f, 0.82f, 0.991f, 0.24f, 0.998f, 0.01f, 1, 0));
            Transform ground = Child(circle, "Ground").transform;
            // Readable radial bands replace the overlapping web in the first version.
            Layer(ground, "BaseCircle", art["BaseCircle"], light, clip, 12.4f, Gold, 0, 0.15f, 0, 0, 0.80f);
            Layer(ground, "OuterRing", art["OuterRing"], light, clip, 12.0f, Gold, 0.15f, 14f/60, 8, 1);
            Layer(ground, "RuneRing01", art["RuneRing"], light, clip, 10.7f, Ice, 12f/60, 18f/60, -12, 2);
            Layer(ground, "InnerRing", art["InnerRing"], light, clip, 8.3f, Teal, 17f/60, 0.35f, 18, 3);
            Layer(ground, "Geometry01", art["Triangle"], light, clip, 7.2f, Violet, 0.35f, 0.45f, 9, 4);
            Layer(ground, "Geometry02", art["Octagram"], light, clip, 6.6f, Rose, 26f/60, 31f/60, -12, 5);
            Layer(ground, "RuneRing02", art["RuneRing"], light, clip, 5.4f, Gold, 30f/60, 35f/60, -16, 6);
            Layer(ground, "RadialStructure", art["Radial"], light, clip, 12.8f, Teal, 29f/60, 0.60f, 0, 7, 0.40f);
            Layer(ground, "InnerSigil", art["Sigil"], light, clip, 3.4f, Teal, 33f/60, 0.60f, 20, 8);
            Layer(ground, "AmbientGlow", art["Halo"], light, clip, 13.0f, Ice, 0.03f, 0.20f, 0, 0, 0.10f);
            ConstructionPulse(ground, art["Pulse"], light, clip);

            Transform satellite = Child(circle, "Satellite").transform;
            SetCurve(clip, "Circle/Satellite", typeof(Transform), "localEulerAnglesRaw.z", Rotation(3.5f, 0.60f, 0.85f));
            Layer(satellite, "Connections", art["Connections"], light, clip, 17.8f, Gold, 0.60f, 0.85f, 0, 8, 0.60f);
            Color[] palette = { Gold, Ice, Violet, Rose };
            for (int i = 0; i < 8; i++)
            {
                string name = "Satellite" + (i + 1).ToString("00");
                Transform group = Child(satellite, name).transform;
                float angle = Mathf.PI / 2 - Mathf.PI / 8 - i * Mathf.PI / 4f;
                group.localPosition = new Vector3(Mathf.Cos(angle) * 8.2f, Mathf.Sin(angle) * 8.2f, 0);
                float begin = (36f + i * 1.45f) / Length;
                float end = begin + 2.4f / Length;
                Color color = palette[i % 4];
                float direction = i % 2 == 0 ? 1 : -1;
                Layer(group, "Boundary", art["SatelliteBoundary"], light, clip, 3.3f, Teal, begin, end, -direction * 8, 9, 0.65f);
                Layer(group, "OuterRing", art["Satellite" + i % 4], light, clip, 2.8f, color, begin, end, direction * 14, 10);
                Layer(group, "CounterRunes", art["SatelliteRunes"], light, clip, 2.2f, palette[(i + 2) % 4], begin + 0.5f/Length, end + 0.8f/Length, -direction * 19, 11, 0.65f);
                Layer(group, "InnerSigil", art["SatelliteCore" + i % 4], light, clip, 1.4f, palette[(i + 1) % 4], begin + 0.9f/Length, end + 1.5f/Length, direction * 24, 12);
                Layer(group, "Glow", art["Halo"], light, clip, 3.7f, color, begin, end, 0, 8, 0.16f);
                Particle(group, "NodeSpark", spark, 6, 0.7f, 0.10f, 0.13f, color, 0.9f,
                    begin, 0.28f, 0.1f, true);
            }

            // A second constellation counter-orbits the primary eight seals.
            Transform outer = Child(circle, "OuterConstellation").transform;
            SetCurve(clip, "Circle/OuterConstellation", typeof(Transform), "localEulerAnglesRaw.z", Rotation(-2.5f, 0.65f, 0.85f));
            Layer(outer, "OrbitRuneBelt", art["OrbitRunes"], light, clip, 22.4f, Ice, 0.62f, 0.82f, 4, 8, 0.68f);
            Layer(outer, "OrbitCounterBelt", art["OrbitTicks"], light, clip, 23.0f, Gold, 0.68f, 0.85f, -6, 9, 0.55f);
            Layer(outer, "Connections", art["OuterConnections"], light, clip, 26.0f, Violet, 0.70f, 0.85f, 0, 9, 0.50f);
            for (int i = 0; i < 8; i++)
            {
                Transform group = Child(outer, "OuterSeal" + (i + 1).ToString("00")).transform;
                float angle = Mathf.PI / 2 - i * Mathf.PI / 4f;
                group.localPosition = new Vector3(Mathf.Cos(angle) * 11.8f, Mathf.Sin(angle) * 11.8f, 0);
                float begin = (43f + i * 0.7f) / Length;
                Color color = palette[(i + 2) % 4];
                Layer(group, "OuterRing", art["Satellite" + (i + 1) % 4], light, clip, 1.95f, color, begin, begin + 1.5f/Length, -16, 10);
                Layer(group, "Core", art["SatelliteCore" + (i + 2) % 4], light, clip, 0.95f, Teal, begin + 0.6f/Length, begin + 2.8f/Length, 22, 11);
                Layer(group, "Glow", art["Halo"], light, clip, 2.4f, color, begin, begin + 1.5f/Length, 0, 9, 0.18f);
            }

            Transform particleGroup = Child(circle, "Particle").transform;
            Particle(particleGroup, "Spark", spark, 64, 5.0f, 0.20f, 0.16f, Gold, 1.1f, 0.34f, 0.30f, 0.05f, true);
            Particle(particleGroup, "EnergyDust", spark, 48, 4.0f, 0.10f, 0.10f, Ice, 1.8f, 0.40f, -0.12f, 0.02f);
            Particle(particleGroup, "RuneParticles", rune, 48, 3.8f, 0.10f, 0.32f, Violet, 1.5f, 0.50f, 0.18f, 0.02f);
            Particle(satellite, "OrbitSparks", spark, 64, 8.2f, 0, 0.12f, Gold, 1.4f, 0.60f, 0.45f, 0, true);
            Particle(outer, "OuterOrbitSparks", spark, 48, 10.6f, 0, 0.10f, Ice, 1.6f, 0.70f, -0.30f, 0, true);
            Particle(particleGroup, "InwardWisps", spark, 32, 5.7f, 0, 0.18f, Teal, 1.2f, 0.85f, 0.25f, -0.7f, true);
            AttachAnimator(root, clip);
            string prefabPath = Root + "/Prefabs/ArcaneCircle.prefab";
            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            AssetDatabase.SaveAssets();

            string output = Path.Combine(ModRoot, @".arcane-strike-chant-build\UnityBundle");
            Directory.CreateDirectory(output);
            AssetBundleBuild build = new AssetBundleBuild
            {
                assetBundleName = BundleName,
                assetNames = new[] { prefabPath },
                addressableNames = new[] { "arcane_circle" }
            };
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(output, new[] { build },
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode,
                BuildTarget.StandaloneWindows64);
            if (manifest == null)
            {
                throw new InvalidOperationException("ArcaneCircle AssetBundle build failed.");
            }
            File.Copy(Path.Combine(output, BundleName), Path.Combine(ModRoot, @"1.6\AssetBundles\Windows", BundleName), true);
            Debug.Log("ArcaneCircle AssetBundle built: " + BundleName);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static Dictionary<string, Sprite> BuildArt()
    {
        Dictionary<string, Canvas> canvases = new Dictionary<string, Canvas>();
        Canvas foundation = new Canvas();
        foundation.Circle(0.475f, 5f);
        foundation.Circle(0.46f, 2.5f);
        canvases.Add("BaseCircle", foundation);

        Canvas outer = new Canvas();
        outer.Circle(0.478f, 4.5f);
        outer.Circle(0.440f, 3f);
        for (int i = 0; i < 48; i++)
        {
            float a = i * Mathf.PI * 2 / 48;
            outer.Line(Polar(a, 0.468f), Polar(a, i % 4 == 0 ? 0.443f : 0.455f), 2.5f);
            if (i % 4 == 0)
            {
                Vector2 point = Polar(a, 0.423f);
                outer.Diamond(point, 0.009f, a, 3f);
            }
        }
        canvases.Add("OuterRing", outer);

        Canvas runes = new Canvas();
        runes.Circle(0.475f, 3f);
        runes.Circle(0.39f, 3f);
        for (int i = 0; i < 32; i++)
        {
            float a = i * Mathf.PI * 2 / 32;
            runes.Rune(Polar(a, 0.432f), 0.035f, a + Mathf.PI / 2, i, 4f);
        }
        canvases.Add("RuneRing", runes);

        Canvas inner = new Canvas();
        inner.Circle(0.472f, 4f);
        inner.Circle(0.435f, 2.5f);
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2 / 12;
            inner.Diamond(Polar(a, 0.405f), 0.018f, a, 3.5f);
        }
        canvases.Add("InnerRing", inner);

        Canvas triangle = new Canvas();
        triangle.Polygon(3, 0.46f, Mathf.PI / 2, 5.5f);
        triangle.Polygon(3, 0.46f, -Mathf.PI / 2, 3.5f);
        for (int i = 0; i < 6; i++)
        {
            triangle.CircleAt(Polar(i * Mathf.PI / 3, 0.46f), 0.017f, 4f);
        }
        canvases.Add("Triangle", triangle);

        Canvas octagram = new Canvas();
        octagram.Star(8, 3, 0.462f, Mathf.PI / 8, 4.5f);
        octagram.Polygon(8, 0.46f, Mathf.PI / 8, 2.5f);
        canvases.Add("Octagram", octagram);

        Canvas radial = new Canvas();
        for (int i = 0; i < 12; i++)
        {
            float a = i * Mathf.PI * 2 / 12;
            radial.Line(Polar(a, 0.30f), Polar(a, 0.405f), 3f);
            radial.Diamond(Polar(a, 0.33f), 0.012f, a, 3f);
        }
        canvases.Add("Radial", radial);

        Canvas sigil = new Canvas(512);
        sigil.Circle(0.46f, 3f);
        sigil.Star(5, 2, 0.37f, Mathf.PI / 2, 4f);
        sigil.Circle(0.142f, 2f);
        sigil.Diamond(Vector2.zero, 0.078f, 0, 3f);
        canvases.Add("Sigil", sigil);

        // Four original occult/astral/alchemical/rosette designs, two of each.
        for (int style = 0; style < 4; style++)
        {
            Canvas satellite = new Canvas(512);
            satellite.Circle(0.47f, 4f);
            satellite.Circle(0.35f, 2.5f);
            for (int i = 0; i < 12; i++)
            {
                float a = i * Mathf.PI / 6;
                satellite.Rune(Polar(a, 0.413f), 0.041f, a + Mathf.PI / 2, i + style * 3, 3f);
            }
            Canvas core = new Canvas(512);
            core.Circle(0.47f, 3.5f);
            switch (style)
            {
                case 0:
                    satellite.Star(6, 2, 0.32f, Mathf.PI / 2, 3.5f);
                    core.Star(6, 2, 0.40f, 0, 4f);
                    core.Circle(0.17f, 3f);
                    break;
                case 1:
                    satellite.Star(8, 3, 0.32f, Mathf.PI / 8, 3.5f);
                    core.Star(8, 3, 0.40f, 0, 4f);
                    core.Diamond(Vector2.zero, 0.20f, 0, 3f);
                    break;
                case 2:
                    satellite.Polygon(3, 0.32f, Mathf.PI / 2, 4f);
                    core.Polygon(3, 0.40f, -Mathf.PI / 2, 4f);
                    core.Circle(0.22f, 3f);
                    for (int i = 0; i < 3; i++)
                        satellite.CircleAt(Polar(i * Mathf.PI * 2 / 3 + Mathf.PI / 2, 0.32f), 0.055f, 3f);
                    break;
                default:
                    for (int i = 0; i < 7; i++)
                    {
                        satellite.CircleAt(Polar(i * Mathf.PI * 2 / 7, 0.20f), 0.115f, 3f);
                        core.CircleAt(Polar(i * Mathf.PI * 2 / 7, 0.22f), 0.15f, 3.5f);
                    }
                    core.Circle(0.09f, 3f);
                    break;
            }
            canvases.Add("Satellite" + style, satellite);
            canvases.Add("SatelliteCore" + style, core);
        }

        Canvas connections = new Canvas();
        for (int i = 0; i < 8; i++)
        {
            float angle = Mathf.PI / 2 - Mathf.PI / 8 - i * Mathf.PI / 4;
            connections.Line(Polar(angle, 0.34f), Polar(angle, 0.377f), 4f);
            connections.Diamond(Polar(angle, 0.358f), 0.009f, angle, 3f);
        }
        canvases.Add("Connections", connections);
        Canvas boundary = new Canvas(512);
        boundary.Circle(0.478f, 3.5f);
        for (int i = 0; i < 8; i++)
        {
            float angle = i * Mathf.PI / 4;
            boundary.Diamond(Polar(angle, 0.443f), 0.021f, angle, 3f);
            boundary.Line(Polar(angle, 0.39f), Polar(angle, 0.423f), 2.5f);
        }
        canvases.Add("SatelliteBoundary", boundary);
        Canvas localRunes = new Canvas(512);
        localRunes.Circle(0.47f, 2.5f);
        for (int i = 0; i < 12; i++)
        {
            float angle = i * Mathf.PI / 6;
            localRunes.Rune(Polar(angle, 0.40f), 0.05f, angle + Mathf.PI / 2, i + 5, 3f);
        }
        canvases.Add("SatelliteRunes", localRunes);
        Canvas orbitRunes = new Canvas();
        orbitRunes.Circle(0.475f, 3f);
        orbitRunes.Circle(0.438f, 2f);
        for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI / 32;
            orbitRunes.Rune(Polar(angle, 0.456f), 0.015f, angle + Mathf.PI/2, i+2, 2.5f);
        }
        canvases.Add("OrbitRunes", orbitRunes);
        Canvas orbitTicks = new Canvas();
        orbitTicks.Circle(0.478f, 3f);
        for (int i = 0; i < 64; i++)
        {
            float angle = i * Mathf.PI / 32;
            orbitTicks.Line(Polar(angle, 0.466f), Polar(angle, i % 4 == 0 ? 0.443f : 0.456f), 2.5f);
            if (i % 8 == 0) orbitTicks.Diamond(Polar(angle, 0.431f), 0.008f, angle, 3f);
        }
        canvases.Add("OrbitTicks", orbitTicks);
        Canvas outerConnections = new Canvas();
        for (int i = 0; i < 8; i++)
        {
            float angle = Mathf.PI / 2 - i * Mathf.PI / 4;
            outerConnections.Line(Polar(angle, 10.5f / 26), Polar(angle, 10.86f / 26), 3.5f);
            outerConnections.Diamond(Polar(angle, 10.7f / 26), 0.008f, angle, 3f);
        }
        canvases.Add("OuterConnections", outerConnections);
        Canvas pulse = new Canvas(512);
        pulse.Circle(0.47f, 3f);
        canvases.Add("Pulse", pulse);

        Canvas halo = new Canvas();
        halo.Halo();
        canvases.Add("Halo", halo);
        Canvas spark = new Canvas(128);
        spark.Spark();
        canvases.Add("Spark", spark);
        Canvas particle = new Canvas(128);
        particle.Rune(Vector2.zero, 0.32f, 0, 3, 3.5f);
        canvases.Add("RuneParticle", particle);

        foreach (KeyValuePair<string, Canvas> pair in canvases)
        {
            pair.Value.Save(Root + "/Art/" + pair.Key + ".png");
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        Dictionary<string, Sprite> result = new Dictionary<string, Sprite>();
        foreach (string name in canvases.Keys)
        {
            string path = Root + "/Art/" + name + ".png";
            TextureImporter importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = canvases[name].Size;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.filterMode = FilterMode.Trilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            TextureImporterSettings settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            result.Add(name, AssetDatabase.LoadAssetAtPath<Sprite>(path));
        }
        return result;
    }

    private static SpriteRenderer Layer(Transform parent, string name, Sprite sprite, Material material,
        AnimationClip clip, float diameter, Color tint, float begin, float end, float speed, int order, float opacity = 0.78f)
    {
        GameObject node = Child(parent, name);
        SpriteRenderer renderer = node.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        // A material per texture is shared across prefab instances, never cloned at runtime.
        renderer.sharedMaterial = SaveMaterial(sprite.name, material.shader, sprite.texture);
        renderer.sortingOrder = order;
        renderer.color = tint;
        node.transform.localScale = new Vector3(diameter, diameter, 1);
        Transform reveal = Child(node.transform, "Reveal").transform;
        reveal.localScale = new Vector3(0, 1, 1);
        string path = AnimationUtility.CalculateTransformPath(node.transform, node.transform.root);
        AnimationCurve construction = Keys(0, 0, begin, 0, end, 1, 1, 1);
        for (int i = 0; i < construction.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(construction, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(construction, i, AnimationUtility.TangentMode.Linear);
        }
        SetCurve(clip, path + "/Reveal", typeof(Transform), "m_LocalScale.x", construction);
        SetCurve(clip, path, typeof(SpriteRenderer), "m_Color.a",
            Keys(0, 0, begin, 0, Mathf.Min(end, begin + 0.35f/Length), opacity * 0.70f, end, opacity * 0.90f,
                0.85f, opacity * 0.85f, 0.96f, opacity, 0.9725f, opacity, 0.990f, opacity, 0.998f, 0, 1, 0));
        SetCurve(clip, path, typeof(Transform), "localEulerAnglesRaw.z", Rotation(speed, begin, end));
        foreach (string axis in new[] { "x", "y" })
            SetCurve(clip, path, typeof(Transform), "m_LocalScale." + axis,
                Keys(0, diameter * 0.98f, begin, diameter * 0.98f, end, diameter, 1, diameter));
        string[] channels = { "r", "g", "b" };
        for (int i = 0; i < channels.Length; i++)
        {
            float c = tint[i];
            SetCurve(clip, path, typeof(SpriteRenderer), "m_Color." + channels[i],
                Keys(0, c * 0.8f, begin, c * 0.8f, end, c, 0.85f, c, 0.96f, c * 1.16f,
                    0.9725f, c * 1.16f, 0.989f, c * 1.24f, 1, c));
        }
        return renderer;
    }

    private static void ConstructionPulse(Transform parent, Sprite sprite, Material material, AnimationClip clip)
    {
        SpriteRenderer renderer = Layer(parent, "ConstructionPulse", sprite, material, clip, 1, Ice, 0, 0.01f, 0, 11);
        string path = AnimationUtility.CalculateTransformPath(renderer.transform, renderer.transform.root);
        List<Keyframe> alpha = new List<Keyframe> { new Keyframe(0, 0) };
        List<Keyframe> radius = new List<Keyframe> { new Keyframe(0, 1.8f) };
        for (int second = 0; second < 57; second++)
        {
            float diameter = second < 21 ? 12.4f : second < 36 ? 8.0f : 13.0f;
            alpha.Add(new Keyframe(second + 0.12f, 0));
            alpha.Add(new Keyframe(second + 0.32f, second < 51 ? 0.14f : 0.22f));
            alpha.Add(new Keyframe(second + 0.88f, 0));
            radius.Add(new Keyframe(second + 0.12f, 1.8f));
            radius.Add(new Keyframe(second + 0.88f, diameter));
        }
        alpha.Add(new Keyframe(Length * 0.96f, 0));
        alpha.Add(new Keyframe(Length, 0));
        radius.Add(new Keyframe(Length, 13));
        SetCurve(clip, path, typeof(SpriteRenderer), "m_Color.a", new AnimationCurve(alpha.ToArray()));
        SetCurve(clip, path, typeof(Transform), "m_LocalScale.x", new AnimationCurve(radius.ToArray()));
        SetCurve(clip, path, typeof(Transform), "m_LocalScale.y", new AnimationCurve(radius.ToArray()));
        SetCurve(clip, path + "/Reveal", typeof(Transform), "m_LocalScale.x", Keys(0, 1, 1, 1));
    }

    private static void BuildSounds()
    {
        // Original PCM assets, generated only while authoring. No synthesis in game.
        string folder = Path.Combine(ModRoot, @"Content\Sounds\WorldArcaneStrike");
        Directory.CreateDirectory(folder);
        string[] names = { "ArcaneHum", "ArcaneEtch", "ArcaneStage", "ArcaneSeal", "ArcaneConverge" };
        double[] lengths = { 4, 0.28, 1.7, 1.0, 1.6 };
        const int rate = 22050;
        for (int kind = 0; kind < names.Length; kind++)
        {
            int frames = (int)(lengths[kind] * rate);
            using (BinaryWriter writer = new BinaryWriter(File.Create(Path.Combine(folder, names[kind] + ".wav"))))
            {
                writer.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                writer.Write(36 + frames * 2);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
                writer.Write(16); writer.Write((short)1); writer.Write((short)1);
                writer.Write(rate); writer.Write(rate * 2); writer.Write((short)2); writer.Write((short)16);
                writer.Write(System.Text.Encoding.ASCII.GetBytes("data")); writer.Write(frames * 2);
                System.Random random = new System.Random(1703 + kind);
                double noise = 0;
                for (int i = 0; i < frames; i++)
                {
                    double t = i / (double)rate;
                    double u = t / lengths[kind];
                    double twoPi = Math.PI * 2;
                    noise = noise * 0.82 + (random.NextDouble() * 2 - 1) * 0.18;
                    double value;
                    if (kind == 0)
                        value = (0.18 * Math.Sin(twoPi * 110 * t) + 0.10 * Math.Sin(twoPi * 130.75 * t)
                            + 0.08 * Math.Sin(twoPi * 165 * t) + 0.03 * Math.Sin(twoPi * 660 * t))
                            * (0.85 + 0.15 * Math.Sin(twoPi * 0.25 * t));
                    else if (kind == 1)
                        value = (0.27 * Math.Sin(twoPi * 1320 * t) + 0.13 * Math.Sin(twoPi * 1760 * t) + noise * 0.18)
                            * Math.Exp(-t * 15) * Math.Min(1, t * 160) * (1-u);
                    else if (kind == 2)
                        value = (0.24 * Math.Sin(twoPi * 440 * t) + 0.17 * Math.Sin(twoPi * 523.25 * t)
                            + 0.12 * Math.Sin(twoPi * 659.25 * t) + 0.05 * Math.Sin(twoPi * 1320 * t))
                            * Math.Exp(-t * 3) * Math.Min(1, t * 100) * (1-u);
                    else if (kind == 3)
                        value = (0.4 * Math.Sin(twoPi * 82.5 * t) + 0.18 * Math.Sin(twoPi * 880 * t) + noise * 0.12)
                            * Math.Exp(-t * 5) * Math.Min(1, t * 100) * (1-u);
                    else
                    {
                        double phase = twoPi * (1900 * (t - t*t / lengths[kind] + t*t*t / (3*lengths[kind]*lengths[kind])) + 60*t);
                        value = (0.27 * Math.Sin(phase) + noise * 0.22) * Math.Pow(Math.Sin(Math.PI*u), 0.7);
                    }
                    writer.Write((short)(Math.Max(-0.98, Math.Min(0.98, value)) * short.MaxValue));
                }
            }
        }
    }

    private static void Particle(Transform parent, string name, Material material, int maximum,
        float radius, float speed, float size, Color tint, float lifetime,
        float begin = 0.34f, float orbit = 0, float radial = 0, bool stretch = false)
    {
        ParticleSystem system = Child(parent, name).AddComponent<ParticleSystem>();
        var main = system.main;
        main.duration = Length;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = lifetime;
        main.startSpeed = speed;
        main.startSize = new ParticleSystem.MinMaxCurve(size * 0.7f, size * 1.3f);
        main.startColor = tint;
        main.maxParticles = maximum;
        var emission = system.emission;
        AnimationCurve rate = new AnimationCurve(new Keyframe(0, 0), new Keyframe(begin, 0),
            new Keyframe(Mathf.Min(begin + 0.045f, 0.90f), maximum * 0.28f),
            new Keyframe(Mathf.Max(begin + 0.05f, 0.85f), maximum * 0.52f),
            new Keyframe(0.96f, maximum * 0.85f), new Keyframe(0.98f, 0), new Keyframe(1, 0));
        emission.rateOverTime = new ParticleSystem.MinMaxCurve(1, rate);
        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = radius;
        shape.radiusThickness = stretch ? 0.025f : 0.75f;
        var velocity = system.velocityOverLifetime;
        velocity.enabled = orbit != 0 || radial != 0;
        velocity.orbitalZ = orbit;
        velocity.radial = radial;
        var color = system.colorOverLifetime;
        color.enabled = true;
        Gradient fade = new Gradient();
        fade.SetKeys(new[] { new GradientColorKey(Color.white, 0), new GradientColorKey(Color.white, 1) },
            new[] { new GradientAlphaKey(0, 0), new GradientAlphaKey(0.95f, 0.15f), new GradientAlphaKey(0.5f, 0.7f), new GradientAlphaKey(0, 1) });
        color.color = fade;
        ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = stretch ? ParticleSystemRenderMode.Stretch : ParticleSystemRenderMode.Billboard;
        renderer.lengthScale = stretch ? 3.5f : 1f;
        renderer.velocityScale = stretch ? 0.15f : 0;
        system.useAutoRandomSeed = false;
        system.randomSeed = (uint)(name.Length * 1147);
    }

    private static AnimationClip CreateClip()
    {
        string path = Root + "/Animations/ArcaneCircle.anim";
        AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
        if (clip == null)
        {
            clip = new AnimationClip();
            AssetDatabase.CreateAsset(clip, path);
        }
        clip.ClearCurves();
        clip.name = "ArcaneCircle";
        clip.frameRate = 60;
        return clip;
    }

    private static void AttachAnimator(GameObject root, AnimationClip clip)
    {
        AnimationClipSettings settings = AnimationUtility.GetAnimationClipSettings(clip);
        settings.loopTime = false;
        settings.startTime = 0;
        settings.stopTime = Length;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
        string path = Root + "/Animations/ArcaneCircle.controller";
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
        if (controller == null)
        {
            controller = AnimatorController.CreateAnimatorControllerAtPath(path);
        }
        AnimatorStateMachine machine = controller.layers[0].stateMachine;
        AnimatorState state = null;
        foreach (ChildAnimatorState child in machine.states)
        {
            if (child.state.name == "Play")
            {
                state = child.state;
                break;
            }
        }
        if (state == null)
        {
            state = machine.AddState("Play");
        }
        state.motion = clip;
        machine.defaultState = state;
        Animator animator = root.AddComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = false;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
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
        material.SetFloat("_Reveal", 1);
        material.SetFloat("_Outline", name == "Halo" || name == "Pulse" ? 0 : 0.65f);
        EditorUtility.SetDirty(material);
        return material;
    }

    private static GameObject Child(Transform parent, string name)
    {
        GameObject result = new GameObject(name);
        result.transform.SetParent(parent, false);
        return result;
    }

    private static AnimationCurve Rotation(float speed, float begin, float complete)
    {
        List<Keyframe> keys = new List<Keyframe>();
        float angle = 0;
        // Ten keys per second give linear rotation without a large per-tick clip.
        int ticks = (int)(Length * 10);
        for (int tick = 0; tick <= ticks; tick++)
        {
            float progress = tick / (float)ticks;
            if (tick > 0 && progress >= begin && progress <= 0.96f)
            {
                angle += speed / 10 * Mathf.Lerp(0.15f, 1, Mathf.InverseLerp(begin, complete, progress))
                    * (1 + Mathf.Clamp01((progress - 0.85f) / 0.11f) * 0.75f);
            }
            keys.Add(new Keyframe(progress * Length, angle));
        }
        AnimationCurve curve = new AnimationCurve(keys.ToArray());
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.Linear);
        }
        return curve;
    }

    private static AnimationCurve Keys(params float[] pairs)
    {
        List<Keyframe> result = new List<Keyframe>();
        for (int i = 0; i < pairs.Length; i += 2)
        {
            float time = pairs[i] * Length;
            // Some construction layers begin at zero; keep a single key at each time.
            if (result.Count > 0 && Mathf.Abs(result[result.Count - 1].time - time) < 0.0001f)
            {
                result[result.Count - 1] = new Keyframe(time, pairs[i + 1]);
            }
            else
            {
                result.Add(new Keyframe(time, pairs[i + 1]));
            }
        }
        AnimationCurve curve = new AnimationCurve(result.ToArray());
        for (int i = 0; i < curve.length; i++)
        {
            AnimationUtility.SetKeyLeftTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
            AnimationUtility.SetKeyRightTangentMode(curve, i, AnimationUtility.TangentMode.ClampedAuto);
        }
        return curve;
    }

    private static void SetCurve(AnimationClip clip, string path, Type type, string property, AnimationCurve curve)
    {
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, type, property), curve);
    }

    private static Vector2 Polar(float angle, float radius)
    {
        return new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
    }

    private sealed class Canvas
    {
        public readonly int Size;
        private readonly Color32[] pixels;
        private static readonly float[][] Glyphs =
        {
            new float[] { 0,-1, 0,1 },
            new float[] { 0,-1, 0,1, 0,0.6f, 0.8f,1, 0,0, 0.8f,0.4f },
            new float[] { -0.5f,-1, -0.5f,1, -0.5f,1, 0.6f,0.3f, 0.6f,0.3f, -0.5f,0, -0.5f,0, 0.6f,-1 },
            new float[] { -0.7f,-1, 0.7f,1, -0.7f,1, 0.7f,-1, -0.7f,-1, -0.7f,1, 0.7f,-1, 0.7f,1 },
            new float[] { 0,-1, 0,1, -0.7f,0.8f, 0,0.2f, 0,0.2f, 0.7f,0.8f },
            new float[] { -0.7f,1, 0.7f,-1, 0,-1, 0,1 },
            new float[] { -0.6f,-1, -0.6f,1, 0.6f,-1, 0.6f,1, -0.6f,0, 0.6f,0 },
            new float[] { -0.7f,0, 0,1, 0,1, 0.7f,0, 0.7f,0, 0,-1, 0,-1, -0.7f,0 },
            new float[] { 0,-1, 0,1, -0.7f,0.3f, 0,1, 0,1, 0.7f,0.3f },
            new float[] { -0.7f,-0.7f, 0.7f,0.7f, -0.7f,0.7f, 0.7f,-0.7f },
            new float[] { -0.5f,-1, -0.5f,1, -0.5f,0.9f, 0.6f,0.3f, 0.6f,0.3f, -0.5f,-0.3f },
            new float[] { -0.6f,1, 0.5f,0.2f, 0.5f,0.2f, -0.5f,-0.2f, -0.5f,-0.2f, 0.6f,-1 }
        };

        public Canvas(int size = 1024)
        {
            Size = size;
            pixels = new Color32[size * size];
        }

        public void Line(Vector2 a, Vector2 b, float width)
        {
            a = (a + Vector2.one * 0.5f) * (Size - 1);
            b = (b + Vector2.one * 0.5f) * (Size - 1);
            float radius = width * 0.5f;
            int minX = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - radius - 1));
            int maxX = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + radius + 1));
            int minY = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - radius - 1));
            int maxY = Mathf.Min(Size - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + radius + 1));
            Vector2 direction = b - a;
            float lengthSquared = direction.sqrMagnitude;
            for (int y = minY; y <= maxY; y++)
            {
                for (int x = minX; x <= maxX; x++)
                {
                    Vector2 point = new Vector2(x, y);
                    float t = lengthSquared > 0 ? Mathf.Clamp01(Vector2.Dot(point - a, direction) / lengthSquared) : 0;
                    float distance = (point - a - direction * t).magnitude;
                    byte alpha = (byte)(255 * Mathf.Clamp01(radius + 0.75f - distance));
                    int index = y * Size + x;
                    if (alpha > pixels[index].a)
                    {
                        pixels[index] = new Color32(255, 255, 255, alpha);
                    }
                }
            }
        }

        public void Circle(float radius, float width)
        {
            CircleAt(Vector2.zero, radius, width);
        }

        public void CircleAt(Vector2 center, float radius, float width)
        {
            int count = Mathf.Max(24, Mathf.CeilToInt(radius * Size * 6));
            for (int i = 0; i < count; i++)
            {
                Line(center + Polar(i * Mathf.PI * 2 / count, radius),
                    center + Polar((i + 1) * Mathf.PI * 2 / count, radius), width);
            }
        }

        public void Polygon(int sides, float radius, float offset, float width)
        {
            Star(sides, 1, radius, offset, width);
        }

        public void Star(int sides, int stride, float radius, float offset, float width)
        {
            for (int i = 0; i < sides; i++)
            {
                Line(Polar(offset + i * Mathf.PI * 2 / sides, radius),
                    Polar(offset + (i + stride) * Mathf.PI * 2 / sides, radius), width);
            }
        }

        public void Diamond(Vector2 center, float radius, float angle, float width)
        {
            for (int i = 0; i < 4; i++)
            {
                Line(center + Polar(angle + i * Mathf.PI / 2, radius),
                    center + Polar(angle + (i + 1) * Mathf.PI / 2, radius), width);
            }
        }

        public void Rune(Vector2 center, float size, float angle, int glyph, float width)
        {
            float[] lines = Glyphs[glyph % Glyphs.Length];
            Vector2 x = Polar(angle, size * 0.55f);
            Vector2 y = Polar(angle + Mathf.PI / 2, size);
            for (int i = 0; i < lines.Length; i += 4)
            {
                Line(center + x * lines[i] + y * lines[i + 1], center + x * lines[i + 2] + y * lines[i + 3], width);
            }
        }

        public void Halo()
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float r = new Vector2(x / (float)(Size - 1) - 0.5f, y / (float)(Size - 1) - 0.5f).magnitude;
                    float band = Mathf.Exp(-Mathf.Pow((r - 0.37f) * 23, 2));
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(band * 110));
                }
            }
        }

        public void Spark()
        {
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    Vector2 p = new Vector2(x / (float)(Size - 1) - 0.5f, y / (float)(Size - 1) - 0.5f);
                    float core = Mathf.Exp(-p.sqrMagnitude * 90);
                    float rays = Mathf.Exp(-Mathf.Abs(p.x) * 100 - Mathf.Abs(p.y) * 9)
                        + Mathf.Exp(-Mathf.Abs(p.y) * 100 - Mathf.Abs(p.x) * 9);
                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(Mathf.Clamp01(core + rays * 0.35f) * 255));
                }
            }
        }

        public void Save(string path)
        {
            Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            try
            {
                texture.SetPixels32(pixels);
                texture.Apply();
                File.WriteAllBytes(path, texture.EncodeToPNG());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(texture);
            }
        }
    }
}
#endif

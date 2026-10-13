#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Xml.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Rendering;

// Exports the existing authored layers and curves, without depending on a runtime Animator.
public static class PortableVfxAutomation
{
    private const string ModRoot = @"E:\SteamLibrary\steamapps\common\RimWorld\Mods\MiliraXian_NeiyuLaw";
    private static readonly Dictionary<string, string> TexturePaths = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> ExistingTextures = new Dictionary<string, string>();
    private static readonly Dictionary<string, string> MaterialPaths = new Dictionary<string, string>();
    private const string ParticleAsset = "Assets/Editor/MiliraXian_ArcaneParticles.txt";
    private static string F(float value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static XElement V(string name, Vector3 v) => new XElement(name, "(" + F(v.x) + "," + F(v.y) + "," + F(v.z) + ")");
    private static XElement C(string name, Color c) => new XElement(name, "(" + F(c.r) + "," + F(c.g) + "," + F(c.b) + "," + F(c.a) + ")");

    public static void ExportFromCommandLine() => ExportAll(true);
    public static void ExportCurvesFromCommandLine() => ExportAll(false);

    private static void ExportAll(bool buildResources)
    {
        TexturePaths.Clear();
        ExistingTextures.Clear();
        MaterialPaths.Clear();
        if (buildResources) BakeParticles();
        string textureRoot = Path.Combine(ModRoot, "Content/Textures");
        foreach (string file in Directory.GetFiles(textureRoot, "*.png", SearchOption.AllDirectories).OrderBy(x => x, StringComparer.Ordinal))
            ExistingTextures[Hash(file)] = file.Substring(textureRoot.Length + 1).Replace('\\', '/').Replace(".png", "");

        XElement defs = new XElement("Defs");
        string[] names = { "ZhaoliMinghuo", "ZhaoliGuiyi", "ZhaoliDeathField", "ZhaoliMinshen", "ZhaoliMinshenImpact",
            "NeiyuFlowerCircle", "NeiyuSkyfallTakeoff", "NeiyuSkyfallWarning", "NeiyuSkyfallImpact", "NeiyuHalo", "ZhaoliHalo" };
        string[] addresses = { "zhaoli_minghuo_vfx", "zhaoli_guiyi_vfx", "zhaoli_deathfield_vfx", "zhaoli_minshen_vfx",
            "zhaoli_minshen_impact_vfx", "neiyu_flower_circle_vfx", "neiyu_skyfall_takeoff_vfx", "neiyu_skyfall_warning_vfx",
            "neiyu_skyfall_impact_vfx", "neiyu_halo_vfx", "zhaoli_halo_vfx" };
        for (int i = 0; i < names.Length; i++)
            defs.Add(Export(addresses[i], "Assets/CharacterVfx/Prefabs/" + names[i] + ".prefab", "Assets/CharacterVfx/Animations/" + names[i] + ".anim"));
        defs.Add(Export("arcane_circle", "Assets/ArcaneCircle/Prefabs/ArcaneCircle.prefab", "Assets/ArcaneCircle/Animations/ArcaneCircle.anim"));
        defs.Add(Export("arcane_launch", "Assets/ArcaneLaunch/Prefabs/ArcaneLaunch.prefab", null));
        defs.Add(Export("arcane_impact", "Assets/ArcaneImpact/Prefabs/ArcaneImpact.prefab", null));
        foreach (string state in new[] { "Idle", "Twitch", "Alert" })
            defs.Add(Export("neiyu_ear_" + state.ToLowerInvariant(), "Assets/NeiyuEar/Prefabs/NeiyuEarDriver_Runtime.prefab", "Assets/NeiyuEar/Animations/Ear_" + state + ".anim"));
        string destination = Path.Combine(ModRoot, "1.6/Defs/MiscDefs/MiliraXian_CharacterVfx.xml");
        new XDocument(new XDeclaration("1.0", "utf-8", null), defs).Save(destination);
        if (buildResources) BuildMaterials();
        Debug.Log("[MiliraXian] Exported authored VFX layers and animation curves to " + destination);
    }

    private static XElement Export(string address, string prefabPath, string clipPath)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) throw new FileNotFoundException("Missing authored prefab", prefabPath);
        AnimationClip clip = clipPath == null ? null : AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
        if (clipPath != null && clip == null) throw new FileNotFoundException("Missing authored animation", clipPath);
        Transform[] nodes = prefab.GetComponentsInChildren<Transform>(true);
        XElement parts = new XElement("parts");
        for (int i = 0; i < nodes.Length; i++)
        {
            Transform node = nodes[i];
            XElement part = new XElement("li", new XElement("name", node.name),
                new XElement("parentIndex", Array.IndexOf(nodes, node.parent)), V("position", node.localPosition),
                V("rotation", node.localEulerAngles), V("scale", node.localScale), new XElement("active", node.gameObject.activeSelf));
            SpriteRenderer sprite = node.GetComponent<SpriteRenderer>();
            if (sprite != null && sprite.sprite != null)
            {
                Vector3 size = sprite.sprite.bounds.size;
                part.Add(new XElement("texture", TexturePath(sprite.sprite.texture, address)),
                    new XElement("size", "(" + F(size.x) + "," + F(size.y) + ")"), C("color", sprite.color),
                    new XElement("sortingOrder", sprite.sortingOrder), new XElement("flipX", sprite.flipX),
                    new XElement("flipY", sprite.flipY), new XElement("visible", sprite.enabled),
                    new XElement("glow", node.name.IndexOf("Glow", StringComparison.OrdinalIgnoreCase) >= 0));
                if (address.StartsWith("arcane_", StringComparison.Ordinal))
                {
                    string materialPath = AssetDatabase.GetAssetPath(sprite.sharedMaterial);
                    if (!MaterialPaths.TryGetValue(materialPath, out string materialAddress))
                    {
                        materialAddress = address + "_" + sprite.sharedMaterial.name.ToLowerInvariant();
                        MaterialPaths.Add(materialPath, materialAddress);
                    }
                    part.Add(new XElement("material", materialAddress));
                }
            }
            ParticleSystem ps = node.GetComponent<ParticleSystem>();
            if (ps != null) part.Add(Emitter(ps, address, Array.IndexOf(prefab.GetComponentsInChildren<ParticleSystem>(true), ps)));
            parts.Add(part);
        }
        XElement tracks = new XElement("tracks");
        XElement frames = new XElement("spriteFrames");
        if (clip != null)
        {
            foreach (EditorCurveBinding binding in AnimationUtility.GetCurveBindings(clip))
            {
                Transform node = binding.path.Length == 0 ? prefab.transform : prefab.transform.Find(binding.path);
                if (node == null) throw new InvalidOperationException("Missing curve target: " + binding.path);
                tracks.Add(new XElement("li", new XElement("part", Array.IndexOf(nodes, node)),
                    new XElement("property", binding.propertyName), Curve("curve", AnimationUtility.GetEditorCurve(clip, binding))));
            }
            foreach (EditorCurveBinding binding in AnimationUtility.GetObjectReferenceCurveBindings(clip))
            {
                if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite")
                    throw new InvalidOperationException("Unsupported authored object curve: " + binding.propertyName);
                Transform node = binding.path.Length == 0 ? prefab.transform : prefab.transform.Find(binding.path);
                foreach (ObjectReferenceKeyframe key in AnimationUtility.GetObjectReferenceCurve(clip, binding))
                {
                    Sprite sprite = key.value as Sprite;
                    frames.Add(new XElement("li", new XElement("part", Array.IndexOf(nodes, node)), new XElement("time", F(key.time)),
                        new XElement("texture", sprite == null ? "" : TexturePath(sprite.texture, address)),
                        new XElement("size", sprite == null ? "(0,0)" : "(" + F(sprite.bounds.size.x) + "," + F(sprite.bounds.size.y) + ")")));
                }
            }
        }
        XElement result = new XElement("MiliraXian.Characters.Common.Vfx.CharacterVfxDef", new XElement("defName", "MX_Vfx_" + address),
            new XElement("duration", F(clip == null ? 1f : clip.length)), parts, tracks, frames);
        if (address.StartsWith("neiyu_ear_", StringComparison.Ordinal))
        {
            AnimatorController controller = prefab.GetComponent<Animator>().runtimeAnimatorController as AnimatorController;
            AnimatorStateTransition transition = controller.layers[0].stateMachine.anyStateTransitions.FirstOrDefault(t => t.destinationState.motion == clip);
            if (transition != null) result.Add(new XElement("blendInDuration", F(transition.duration)));
        }
        return result;
    }

    private static XElement Curve(string name, AnimationCurve curve)
    {
        XElement keys = new XElement("keys");
        foreach (Keyframe key in curve.keys)
        {
            string data = F(key.time) + "," + F(key.value) + "," + F(key.inTangent) + "," + F(key.outTangent);
            if (key.weightedMode != WeightedMode.None)
                data += "," + F(key.inWeight) + "," + F(key.outWeight) + "," + (int)key.weightedMode;
            keys.Add(new XElement("li", "(" + data + ")"));
        }
        return new XElement(name, keys);
    }

    private static XElement Emitter(ParticleSystem ps, string address, int index)
    {
        var renderer = ps.GetComponent<ParticleSystemRenderer>();
        return new XElement("emitter", new XElement("texture", TexturePath((Texture2D)renderer.sharedMaterial.mainTexture, address)),
            new XElement("bakedIndex", index),
            new XElement("stretch", renderer.renderMode == ParticleSystemRenderMode.Stretch),
            new XElement("lengthScale", F(renderer.lengthScale)), new XElement("velocityScale", F(renderer.velocityScale)));
    }

    // Unity computes the authored orbital/radial motion and total velocity that Waylith does not implement.
    // One deterministic frame per game tick, with the same runtime seeds and local simulation space.
    private static void BakeParticles()
    {
        GameObject root = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/ArcaneCircle/Prefabs/ArcaneCircle.prefab"));
        try
        {
            Animator animator = root.GetComponent<Animator>();
            if (animator != null) animator.enabled = false;
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ArcaneCircle/Animations/ArcaneCircle.anim");
            ParticleSystem[] systems = root.GetComponentsInChildren<ParticleSystem>(true);
            var buffers = systems.Select(ps => new ParticleSystem.Particle[ps.main.maxParticles]).ToArray();
            for (int i = 0; i < systems.Length; i++)
            {
                systems[i].useAutoRandomSeed = false;
                systems[i].randomSeed = (uint)(1147 + i * 79);
                systems[i].Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                systems[i].Simulate(0, false, true, false);
            }
            using (var compressed = new MemoryStream())
            {
                using (var gzip = new GZipStream(compressed, System.IO.Compression.CompressionLevel.Optimal, true))
                using (var writer = new BinaryWriter(gzip))
                {
                    writer.Write(0x4D585031); // MXP1
                    writer.Write(60);
                    writer.Write(Mathf.RoundToInt(clip.length * 60f) + 1);
                    writer.Write(systems.Length);
                    for (int frame = 0; frame <= Mathf.RoundToInt(clip.length * 60f); frame++)
                    {
                        clip.SampleAnimation(root, frame / 60f);
                        for (int i = 0; i < systems.Length; i++)
                        {
                            ParticleSystem ps = systems[i];
                            if (frame > 0) ps.Simulate(1f / 60f, false, false, false);
                            int count = ps.GetParticles(buffers[i]);
                            writer.Write(count);
                            for (int p = 0; p < count; p++)
                            {
                                ParticleSystem.Particle particle = buffers[i][p];
                                Write(writer, particle.position);
                                Write(writer, particle.totalVelocity);
                                writer.Write(particle.rotation);
                                writer.Write(particle.GetCurrentSize(ps));
                                Color32 color = particle.GetCurrentColor(ps);
                                writer.Write(color.r); writer.Write(color.g); writer.Write(color.b); writer.Write(color.a);
                            }
                        }
                    }
                }
                File.WriteAllText(ParticleAsset, Convert.ToBase64String(compressed.ToArray()));
            }
            AssetDatabase.ImportAsset(ParticleAsset, ImportAssetOptions.ForceSynchronousImport);
            MaterialPaths.Add(ParticleAsset, "arcane_circle_particles");
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }

    private static void Write(BinaryWriter writer, Vector3 vector)
    {
        writer.Write(vector.x); writer.Write(vector.y); writer.Write(vector.z);
    }

    private static string Hash(string path)
    {
        using (SHA256 sha = SHA256.Create()) return Convert.ToBase64String(sha.ComputeHash(File.ReadAllBytes(path)));
    }

    private static void BuildMaterials()
    {
        const string bundleName = "miliraxian_vfx_materials";
        string output = Path.Combine(ModRoot, ".compat17-fix/Materials");
        Directory.CreateDirectory(output);
        GraphicsDeviceType[] previous = PlayerSettings.GetGraphicsAPIs(BuildTarget.StandaloneWindows64);
        bool automatic = PlayerSettings.GetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64);
        try
        {
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, false);
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, new[] { GraphicsDeviceType.Direct3D11, GraphicsDeviceType.Vulkan });
            AssetBundleBuild build = new AssetBundleBuild
            {
                assetBundleName = bundleName, assetNames = MaterialPaths.Keys.ToArray(), addressableNames = MaterialPaths.Values.ToArray()
            };
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(output, new[] { build },
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode, BuildTarget.StandaloneWindows64);
            if (manifest == null) throw new InvalidOperationException("Authored VFX material export failed.");
            File.Copy(Path.Combine(output, bundleName), Path.Combine(ModRoot, "1.6/AssetBundles/Windows/" + bundleName), true);
        }
        finally
        {
            PlayerSettings.SetGraphicsAPIs(BuildTarget.StandaloneWindows64, previous);
            PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.StandaloneWindows64, automatic);
        }
    }

    private static string TexturePath(Texture2D texture, string address)
    {
        string path = AssetDatabase.GetAssetPath(texture);
        if (TexturePaths.TryGetValue(path, out string result)) return result;
        string hash = Hash(path);
        if (!ExistingTextures.TryGetValue(hash, out result))
        {
            result = "Common/WorldArcaneStrike/" + address + "/" + texture.name;
            string destination = Path.Combine(ModRoot, "Content/Textures/" + result + ".png");
            Directory.CreateDirectory(Path.GetDirectoryName(destination));
            File.Copy(path, destination, true);
            ExistingTextures[hash] = result;
        }
        TexturePaths.Add(path, result);
        return result;
    }
}
#endif

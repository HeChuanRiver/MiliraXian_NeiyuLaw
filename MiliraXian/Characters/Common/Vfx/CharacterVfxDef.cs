using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Xml;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx
{
    // Portable authoring data for the existing shared VFX runtime, not gameplay state.
    public sealed class CharacterVfxDef : Def
    {
        public float duration = 1f;
        public float blendInDuration;
        public List<CharacterVfxPart> parts = new();
        public List<CharacterVfxTrack> tracks = new();
        public List<CharacterVfxSpriteFrame> spriteFrames = new();
        private readonly Dictionary<string, Texture2D> textures = new();
        private readonly Dictionary<string, Material> materials = new();
        private bool prepared;
        private bool available;

        internal bool Prepare()
        {
            if (prepared) return available;
            prepared = true;
            try
            {
                available = parts.Count > 0;
                foreach (CharacterVfxPart part in parts)
                {
                    available &= LoadTexture(part.texture);
                    if (!part.material.NullOrEmpty() && !materials.ContainsKey(part.material))
                    {
                        Material material = CharacterUnityVfxRuntime.LoadAuthoredMaterial(part.material);
                        materials.Add(part.material, material);
                        available &= material != null;
                    }
                    if (part.emitter != null) available &= LoadTexture(part.emitter.texture) && CharacterVfxParticles.Prepare();
                }
                foreach (CharacterVfxSpriteFrame frame in spriteFrames) available &= LoadTexture(frame.texture);
            }
            catch (Exception exception)
            {
                available = false;
                Log.ErrorOnce("[MiliraXian] Authored VFX data could not be prepared: " + defName + "\n" + exception,
                    GenText.StableStringHash("MX_Vfx_Prepare_" + defName));
            }
            return available;
        }

        private bool LoadTexture(string path)
        {
            if (path.NullOrEmpty()) return true;
            if (textures.ContainsKey(path)) return textures[path] != null;
            Texture2D texture = ContentFinder<Texture2D>.Get(path, false);
            textures.Add(path, texture);
            if (texture == null) Log.ErrorOnce("[MiliraXian] VFX texture is missing: " + path, GenText.StableStringHash(path));
            return texture != null;
        }

        internal Texture2D Texture(string path) => !path.NullOrEmpty() && textures.TryGetValue(path, out Texture2D texture) ? texture : null;
        internal Material Material(string address) => !address.NullOrEmpty() && materials.TryGetValue(address, out Material material) ? material : null;
        internal CharacterVfxAnimation Instantiate() => Prepare() ? new CharacterVfxAnimation(this) : null;

        internal Material MaterialAt(string path)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                string candidate = parts[i].name;
                int parent = parts[i].parentIndex;
                while (parent > 0) { candidate = parts[parent].name + "/" + candidate; parent = parts[parent].parentIndex; }
                if (candidate == path) return Material(parts[i].material) ?? CharacterUnityVfxRuntime.GetMaterial(Texture(parts[i].texture), parts[i].glow);
            }
            return null;
        }
    }

    public sealed class CharacterVfxKey
    {
        public float time, value, inTangent, outTangent, inWeight = 1f / 3f, outWeight = 1f / 3f;
        public WeightedMode weightedMode;

        public void LoadDataFromXmlCustom(XmlNode root)
        {
            string[] values = root.InnerText.Trim().Trim('(', ')').Split(',');
            time = float.Parse(values[0], CultureInfo.InvariantCulture);
            value = float.Parse(values[1], CultureInfo.InvariantCulture);
            inTangent = float.Parse(values[2], CultureInfo.InvariantCulture);
            outTangent = float.Parse(values[3], CultureInfo.InvariantCulture);
            if (values.Length == 7)
            {
                inWeight = float.Parse(values[4], CultureInfo.InvariantCulture);
                outWeight = float.Parse(values[5], CultureInfo.InvariantCulture);
                weightedMode = (WeightedMode)int.Parse(values[6], CultureInfo.InvariantCulture);
            }
        }
    }

    public sealed class CharacterVfxCurve
    {
        public List<CharacterVfxKey> keys = new();
        internal float Evaluate(float time)
        {
            if (keys.Count == 0) return 0f;
            if (time <= keys[0].time) return keys[0].value;
            if (time >= keys[keys.Count - 1].time) return keys[keys.Count - 1].value;
            int low = 0, high = keys.Count - 1;
            while (high - low > 1)
            {
                int middle = (low + high) / 2;
                if (time < keys[middle].time) high = middle; else low = middle;
            }
            CharacterVfxKey left = keys[low], right = keys[high];
            if (float.IsInfinity(left.outTangent) || float.IsInfinity(right.inTangent)) return left.value;
            float duration = right.time - left.time;
            float t = (time - left.time) / duration;
            float outgoing = (left.weightedMode & WeightedMode.Out) != 0 ? left.outWeight : 1f / 3f;
            float incoming = (right.weightedMode & WeightedMode.In) != 0 ? right.inWeight : 1f / 3f;
            // Weighted Unity keys are a cubic Bezier in time and value. UnityStubs ignores the weights.
            if (outgoing != 1f / 3f || incoming != 1f / 3f)
            {
                float minimum = 0f, maximum = 1f;
                for (int i = 0; i < 24; i++)
                {
                    float candidate = (minimum + maximum) * 0.5f;
                    if (Bezier(0f, outgoing, 1f - incoming, 1f, candidate) < t) minimum = candidate;
                    else maximum = candidate;
                }
                t = (minimum + maximum) * 0.5f;
            }
            return Bezier(left.value, left.value + left.outTangent * duration * outgoing,
                right.value - right.inTangent * duration * incoming, right.value, t);
        }

        private static float Bezier(float a, float b, float c, float d, float t)
        {
            float u = 1f - t;
            return u * u * u * a + 3f * u * u * t * b + 3f * u * t * t * c + t * t * t * d;
        }
    }

    public sealed class CharacterVfxTrack
    {
        public int part;
        public string property;
        public CharacterVfxCurve curve;
    }

    public sealed class CharacterVfxSpriteFrame
    {
        public int part;
        public float time;
        public string texture;
        public Vector2 size;
    }

    public sealed class CharacterVfxPart
    {
        public string name, texture, material;
        public int parentIndex = -1, sortingOrder;
        public Vector3 position, rotation, scale = Vector3.one;
        public Vector2 size = Vector2.one;
        public Color color = Color.white;
        public bool active = true, visible = true, flipX, flipY, glow;
        public CharacterVfxEmitter emitter;
    }

    public sealed class CharacterVfxEmitter
    {
        public string texture;
        public int bakedIndex;
        public float lengthScale = 1f, velocityScale;
        public bool stretch;
    }

    internal struct CharacterVfxParticle
    {
        internal Vector3 Position, Velocity;
        internal float Rotation, Size;
        internal Color32 Color;
    }

    internal sealed class CharacterVfxParticleLayer
    {
        internal Transform Transform;
        internal CharacterVfxEmitter Emitter;
    }

    // Shared, immutable samples from Unity's original particle simulation; no second simulation runs in-game.
    internal static class CharacterVfxParticles
    {
        private static CharacterVfxParticle[][][] frames;
        private static int sampleRate;
        private static bool attempted;

        internal static bool Prepare()
        {
            if (attempted) return frames != null;
            attempted = true;
            try
            {
                TextAsset asset = CharacterUnityVfxRuntime.LoadAuthoredAsset<TextAsset>("arcane_circle_particles");
                if (asset == null) throw new InvalidDataException("Particle animation asset is missing.");
                using var stream = new MemoryStream(Convert.FromBase64String(asset.text));
                using var gzip = new GZipStream(stream, CompressionMode.Decompress);
                using var reader = new BinaryReader(gzip);
                if (reader.ReadInt32() != 0x4D585031) throw new InvalidDataException("Unknown particle animation format.");
                sampleRate = reader.ReadInt32();
                int frameCount = reader.ReadInt32(), layerCount = reader.ReadInt32();
                var data = new CharacterVfxParticle[frameCount][][];
                for (int frame = 0; frame < frameCount; frame++)
                {
                    data[frame] = new CharacterVfxParticle[layerCount][];
                    for (int layer = 0; layer < layerCount; layer++)
                    {
                        var particles = new CharacterVfxParticle[reader.ReadInt32()];
                        for (int p = 0; p < particles.Length; p++)
                            particles[p] = new CharacterVfxParticle
                            {
                                Position = ReadVector(reader), Velocity = ReadVector(reader), Rotation = reader.ReadSingle(),
                                Size = reader.ReadSingle(), Color = new Color32(reader.ReadByte(), reader.ReadByte(), reader.ReadByte(), reader.ReadByte())
                            };
                        data[frame][layer] = particles;
                    }
                }
                frames = data;
            }
            catch (Exception exception)
            {
                Log.ErrorOnce("[MiliraXian] Authored particle animation could not be loaded: " + exception, 197631207);
            }
            return frames != null;
        }

        private static Vector3 ReadVector(BinaryReader reader) => new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        internal static CharacterVfxParticle[] Sample(int layer, float time) =>
            frames[Mathf.Clamp(Mathf.RoundToInt(time * sampleRate), 0, frames.Length - 1)][layer];
    }

    internal sealed class CharacterVfxSprite
    {
        internal Transform transform;
        internal string name;
        internal Texture2D texture;
        internal Vector2 size;
        internal Color color;
        internal int sortingOrder;
        internal bool flipX, flipY, visible, glow;
        internal Material authoredMaterial;
        internal Material sharedMaterial => authoredMaterial ?? CharacterUnityVfxRuntime.GetMaterial(texture, glow);
    }

    internal sealed class CharacterVfxAnimation
    {
        internal readonly CharacterVfxDef Def;
        internal readonly GameObject Root;
        internal readonly Transform[] Nodes;
        internal readonly CharacterVfxSprite[] Sprites;
        internal readonly CharacterVfxParticleLayer[] Particles;
        private readonly CharacterVfxSprite[] spritesByPart;
        private readonly Vector3[] positions, scales, euler;
        private readonly Quaternion[] rotations;
        private readonly bool[] quaternionAnimated;

        internal CharacterVfxAnimation(CharacterVfxDef def)
        {
            Def = def;
            Root = new GameObject(def.defName);
            Root.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(Root);
            Nodes = new Transform[def.parts.Count]; spritesByPart = new CharacterVfxSprite[def.parts.Count];
            positions = new Vector3[Nodes.Length]; scales = new Vector3[Nodes.Length]; euler = new Vector3[Nodes.Length];
            rotations = new Quaternion[Nodes.Length]; quaternionAnimated = new bool[Nodes.Length];
            List<CharacterVfxSprite> sprites = new(); List<CharacterVfxParticleLayer> particles = new();
            try
            {
                for (int i = 0; i < Nodes.Length; i++)
                {
                    CharacterVfxPart part = def.parts[i];
                    GameObject node = new GameObject(part.name); Nodes[i] = node.transform;
                    if (part.parentIndex >= 0) Nodes[i].SetParent(Nodes[part.parentIndex], false);
                    else Nodes[i].SetParent(Root.transform, false);
                    Nodes[i].localPosition = part.position; Nodes[i].localEulerAngles = part.rotation; Nodes[i].localScale = part.scale;
                    node.SetActive(part.active);
                    if (!part.texture.NullOrEmpty())
                    {
                        CharacterVfxSprite sprite = new CharacterVfxSprite
                        {
                            transform = Nodes[i], name = part.name, texture = def.Texture(part.texture), size = part.size,
                            color = part.color, sortingOrder = part.sortingOrder, flipX = part.flipX, flipY = part.flipY,
                            visible = part.visible, glow = part.glow, authoredMaterial = def.Material(part.material)
                        };
                        spritesByPart[i] = sprite; sprites.Add(sprite);
                    }
                    if (part.emitter != null) particles.Add(new CharacterVfxParticleLayer { Transform = Nodes[i], Emitter = part.emitter });
                }
                Sprites = sprites.ToArray(); Particles = particles.ToArray();
                Sample(0f);
            }
            catch { UnityEngine.Object.Destroy(Root); throw; }
        }

        internal Transform Find(string path) => Nodes[0].Find(path);
        internal CharacterVfxSprite SpriteAt(string path)
        {
            Transform target = Find(path);
            for (int i = 0; i < Sprites.Length; i++) if (Sprites[i].transform == target) return Sprites[i];
            return null;
        }

        internal void Sample(float normalized, CharacterVfxDef clip = null)
        {
            CharacterVfxDef source = clip ?? Def;
            float time = Mathf.Clamp01(normalized) * source.duration;
            for (int i = 0; i < Nodes.Length; i++)
            {
                CharacterVfxPart part = source.parts[i];
                positions[i] = part.position; euler[i] = part.rotation; scales[i] = part.scale;
                rotations[i] = Quaternion.Euler(part.rotation); quaternionAnimated[i] = false;
                Nodes[i].gameObject.SetActive(part.active);
                if (spritesByPart[i] != null)
                {
                    spritesByPart[i].color = part.color; spritesByPart[i].visible = part.visible;
                    spritesByPart[i].texture = source.Texture(part.texture);
                    spritesByPart[i].size = part.size;
                }
            }
            foreach (CharacterVfxTrack track in source.tracks) Apply(track, track.curve.Evaluate(time));
            for (int i = 0; i < Nodes.Length; i++)
            {
                Nodes[i].localPosition = positions[i]; Nodes[i].localScale = scales[i];
                Nodes[i].localRotation = quaternionAnimated[i] ? rotations[i].normalized : Quaternion.Euler(euler[i]);
            }
            foreach (CharacterVfxSpriteFrame frame in source.spriteFrames)
                if (frame.time <= time && spritesByPart[frame.part] != null)
                {
                    spritesByPart[frame.part].texture = source.Texture(frame.texture);
                    spritesByPart[frame.part].size = frame.size;
                }
        }

        private void Apply(CharacterVfxTrack track, float value)
        {
            Transform node = Nodes[track.part]; CharacterVfxSprite sprite = spritesByPart[track.part];
            string property = track.property;
            if (property == "m_IsActive") { node.gameObject.SetActive(value >= 0.5f); return; }
            if (property == "m_Enabled") { if (sprite != null) sprite.visible = value >= 0.5f; return; }
            int axis = property.EndsWith(".x") || property.EndsWith(".r") ? 0 : property.EndsWith(".y") || property.EndsWith(".g") ? 1
                : property.EndsWith(".z") || property.EndsWith(".b") ? 2 : 3;
            if (property.StartsWith("m_Color.") && sprite != null) { Color c = sprite.color; c[axis] = value; sprite.color = c; }
            else if (property.StartsWith("m_LocalPosition.")) positions[track.part][axis] = value;
            else if (property.StartsWith("m_LocalScale.")) scales[track.part][axis] = value;
            else if (property.StartsWith("localEulerAngles")) euler[track.part][axis] = value;
            else if (property.StartsWith("m_LocalRotation.")) { rotations[track.part][axis] = value; quaternionAnimated[track.part] = true; }
        }

        internal void Dispose() { if (Root != null) UnityEngine.Object.Destroy(Root); }
    }
}

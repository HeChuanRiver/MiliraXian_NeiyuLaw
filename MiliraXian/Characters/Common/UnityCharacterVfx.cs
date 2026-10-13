using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Verse;
using MiliraXian.Characters.Neiyu;
using MiliraXian.Characters.Common.Vfx;

namespace MiliraXian.Characters.Common
{
    internal enum CharacterUnityVfxKind
    {
        ZhaoliMinghuo,
        ZhaoliGuiyi,
        ZhaoliDeathField,
        ZhaoliMinshen,
        ZhaoliMinshenImpact,
        NeiyuFlowerCircle,
        NeiyuSkyfallTakeoff,
        NeiyuSkyfallWarning,
        NeiyuSkyfallImpact,
        NeiyuHalo,
        ZhaoliHalo,
        ArcaneCircle,
        ArcaneLaunch,
        ArcaneImpact
    }

    public sealed class GameComponent_CharacterUnityVfx : GameComponent
    {
        public GameComponent_CharacterUnityVfx(Game game)
        {
        }

        public override void StartedNewGame()
        {
            ArcaneProjectileVfx.Reset();
            ArcaneImpactVfx.Reset();
            SpecialHaloAnimationRuntime.Reset();
            CharacterUnityVfxRuntime.Reset();
        }

        public override void LoadedGame()
        {
            ArcaneProjectileVfx.Reset();
            ArcaneImpactVfx.Reset();
            SpecialHaloAnimationRuntime.Reset();
            CharacterUnityVfxRuntime.Reset();
            ArcaneCircleVFXController.RebuildAfterLoad();
        }

        public override void GameComponentUpdate()
        {
            SpecialHaloAnimationRuntime.Update();
            CharacterUnityVfxRuntime.Update();
        }
    }

    [StaticConstructorOnStartup]
    internal static class CharacterUnityVfxRuntime
    {
        private const int MaintainGraceTicks = 3;

        private sealed class VfxInstance
        {
            public CharacterUnityVfxKind Kind;
            public GameObject Root;
            public CharacterVfxAnimation Animation;
            public CharacterVfxSprite[] Renderers;
            public Map Map;
            public Pawn FollowPawn;
            public Vector3 Position;
            public float Scale;
            public float Rotation;
            public int StartTick;
            public int DurationTicks;
            public bool Loop;
            public int LastMaintainTick;
            public bool Directional;
            public Rot4 Facing;
            public bool FlipX;
            public float AlphaMultiplier;
            public float PlaybackRate;
            public float LoopPhase;
            public int LastPhaseTick;
        }

        private static readonly HashSet<CharacterUnityVfxKind> MissingPrefabs = new();

        private static readonly List<VfxInstance> OneShots = new List<VfxInstance>();
        private static readonly Dictionary<long, VfxInstance> Persistent = new Dictionary<long, VfxInstance>();
        private static readonly List<long> PersistentRemovalBuffer = new List<long>();
        private static readonly Dictionary<int, Material> Materials = new Dictionary<int, Material>();
        private static readonly MaterialPropertyBlock PropertyBlock = new MaterialPropertyBlock();

        private static AssetBundle authoredMaterials;
        private static bool materialsLoadAttempted;

        static CharacterUnityVfxRuntime()
        {
            foreach (CharacterVfxDef def in DefDatabase<CharacterVfxDef>.AllDefsListForReading) def.Prepare();
        }

        public static bool IsAvailable(CharacterUnityVfxKind kind)
        {
            return TryGetAnimation(kind, out _);
        }

        public static bool TryPlayAttached(
            CharacterUnityVfxKind kind,
            Pawn pawn,
            float scale,
            int durationTicks,
            float rotation = 0f)
        {
            if (pawn == null || !pawn.Spawned || pawn.MapHeld == null)
            {
                return false;
            }

            VfxInstance instance = CreateInstance(kind, pawn.MapHeld, pawn.DrawPos, scale, rotation, durationTicks, false);
            if (instance == null)
            {
                return false;
            }

            instance.FollowPawn = pawn;
            OneShots.Add(instance);
            return true;
        }

        public static bool TryPlayWorld(
            CharacterUnityVfxKind kind,
            Map map,
            IntVec3 cell,
            float scale,
            int durationTicks,
            float rotation = 0f)
        {
            return TryPlayWorld(kind, map, cell.ToVector3Shifted(), scale, durationTicks, rotation);
        }

        public static bool TryPlayWorld(
            CharacterUnityVfxKind kind,
            Map map,
            Vector3 position,
            float scale,
            int durationTicks,
            float rotation = 0f)
        {
            VfxInstance instance = CreateInstance(kind, map, position, scale, rotation, durationTicks, false);
            if (instance == null)
            {
                return false;
            }

            OneShots.Add(instance);
            return true;
        }

        public static bool TryMaintainAttached(
            CharacterUnityVfxKind kind,
            Pawn pawn,
            float scale,
            int loopTicks,
            float rotation = 0f)
        {
            if (pawn == null || !pawn.Spawned || pawn.MapHeld == null)
            {
                return false;
            }

            long key = PersistentKey(kind, pawn.thingIDNumber);
            int now = CurrentTick;
            if (!Persistent.TryGetValue(key, out VfxInstance instance))
            {
                instance = CreateInstance(kind, pawn.MapHeld, pawn.DrawPos, scale, rotation, loopTicks, true);
                if (instance == null)
                {
                    return false;
                }

                Persistent.Add(key, instance);
            }

            instance.Map = pawn.MapHeld;
            instance.FollowPawn = pawn;
            AdvanceLoopPhase(instance, now);
            instance.Scale = Mathf.Max(0.01f, scale);
            instance.Rotation = rotation;
            instance.DurationTicks = Mathf.Max(1, loopTicks);
            instance.PlaybackRate = 1f;
            instance.LastMaintainTick = now;
            return true;
        }

        public static bool TryMaintainDirectionalAttached(
            CharacterUnityVfxKind kind,
            Pawn pawn,
            float scale,
            int loopTicks,
            float playbackRate,
            float alphaMultiplier)
        {
            if (pawn == null || !pawn.Spawned || pawn.MapHeld == null)
            {
                return false;
            }

            long key = PersistentKey(kind, pawn.thingIDNumber);
            int now = CurrentTick;
            if (!Persistent.TryGetValue(key, out VfxInstance instance))
            {
                instance = CreateInstance(kind, pawn.MapHeld, pawn.DrawPos, scale, 0f, loopTicks, true);
                if (instance == null)
                {
                    return false;
                }

                Persistent.Add(key, instance);
            }

            AdvanceLoopPhase(instance, now);
            instance.Map = pawn.MapHeld;
            instance.FollowPawn = pawn;
            instance.Scale = Mathf.Max(0.01f, scale);
            instance.Rotation = 0f;
            instance.DurationTicks = Mathf.Max(1, loopTicks);
            instance.PlaybackRate = Mathf.Max(0.01f, playbackRate);
            instance.AlphaMultiplier = Mathf.Max(0f, alphaMultiplier);
            instance.Directional = true;
            instance.Facing = pawn.Rotation;
            instance.FlipX = pawn.Rotation == Rot4.West;
            instance.LastMaintainTick = now;
            return true;
        }

        public static bool TryMaintainWorld(
            CharacterUnityVfxKind kind,
            Thing owner,
            Map map,
            IntVec3 cell,
            float scale,
            int loopTicks,
            float rotation = 0f)
        {
            if (owner == null || map == null || !cell.IsValid)
            {
                return false;
            }

            long key = PersistentKey(kind, owner.thingIDNumber);
            int now = CurrentTick;
            if (!Persistent.TryGetValue(key, out VfxInstance instance))
            {
                instance = CreateInstance(kind, map, cell.ToVector3Shifted(), scale, rotation, loopTicks, true);
                if (instance == null)
                {
                    return false;
                }

                Persistent.Add(key, instance);
            }

            instance.Map = map;
            instance.FollowPawn = null;
            AdvanceLoopPhase(instance, now);
            instance.Position = cell.ToVector3Shifted();
            instance.Scale = Mathf.Max(0.01f, scale);
            instance.Rotation = rotation;
            instance.DurationTicks = Mathf.Max(1, loopTicks);
            instance.PlaybackRate = 1f;
            instance.LastMaintainTick = now;
            return true;
        }

        public static void Update()
        {
            if (Current.ProgramState != ProgramState.Playing || Find.TickManager == null)
            {
                return;
            }

            int now = CurrentTick;
            for (int index = OneShots.Count - 1; index >= 0; index--)
            {
                VfxInstance instance = OneShots[index];
                if (InstanceInvalid(instance) || now - instance.StartTick > instance.DurationTicks)
                {
                    DestroyInstance(instance);
                    OneShots.RemoveAt(index);
                    continue;
                }

                UpdateAndDraw(instance, now);
            }

            PersistentRemovalBuffer.Clear();
            foreach (KeyValuePair<long, VfxInstance> pair in Persistent)
            {
                VfxInstance instance = pair.Value;
                if (InstanceInvalid(instance) || now - instance.LastMaintainTick > MaintainGraceTicks)
                {
                    PersistentRemovalBuffer.Add(pair.Key);
                    continue;
                }

                UpdateAndDraw(instance, now);
            }

            for (int index = 0; index < PersistentRemovalBuffer.Count; index++)
            {
                long key = PersistentRemovalBuffer[index];
                if (Persistent.TryGetValue(key, out VfxInstance instance))
                {
                    DestroyInstance(instance);
                    Persistent.Remove(key);
                }
            }
        }

        public static void Reset()
        {
            for (int index = 0; index < OneShots.Count; index++)
            {
                DestroyInstance(OneShots[index]);
            }

            foreach (VfxInstance instance in Persistent.Values)
            {
                DestroyInstance(instance);
            }

            OneShots.Clear();
            Persistent.Clear();
            PersistentRemovalBuffer.Clear();
        }

        private static VfxInstance CreateInstance(CharacterUnityVfxKind kind, Map map, Vector3 position,
            float scale, float rotation, int durationTicks, bool loop)
        {
            if (map == null || !TryGetAnimation(kind, out CharacterVfxDef def)) return null;
            CharacterVfxAnimation animation = null;
            try
            {
                animation = def.Instantiate();
                if (animation == null || animation.Sprites.Length == 0)
                {
                    animation?.Dispose();
                    MissingPrefabs.Add(kind);
                    return null;
                }
                int now = CurrentTick;
                return new VfxInstance
                {
                    Kind = kind, Root = animation.Root, Animation = animation, Renderers = animation.Sprites,
                    Map = map, Position = position, Scale = Mathf.Max(0.01f, scale), Rotation = rotation,
                    StartTick = now, DurationTicks = Mathf.Max(1, durationTicks), Loop = loop,
                    LastMaintainTick = now, AlphaMultiplier = 1f, PlaybackRate = 1f, LastPhaseTick = now
                };
            }
            catch (Exception exception)
            {
                animation?.Dispose();
                MissingPrefabs.Add(kind);
                Log.ErrorOnce("[MiliraXian] VFX could not be initialized: " + kind + "\n" + exception,
                    197631100 + (int)kind);
                return null;
            }
        }

        private static void UpdateAndDraw(VfxInstance instance, int now)
        {
            if (instance.Map != Find.CurrentMap || instance.Animation == null)
            {
                return;
            }

            float normalized;
            if (instance.Loop)
            {
                float pendingTicks = Mathf.Max(0, now - instance.LastPhaseTick);
                normalized = Mathf.Repeat(
                    instance.LoopPhase + pendingTicks * instance.PlaybackRate / Mathf.Max(1f, instance.DurationTicks),
                    1f);
            }
            else
            {
                float elapsed = Mathf.Max(0, now - instance.StartTick);
                normalized = Mathf.Clamp01(elapsed / Mathf.Max(1f, instance.DurationTicks));
            }
            instance.Animation.Sample(normalized);

            Vector3 anchor = instance.FollowPawn != null ? instance.FollowPawn.DrawPos : instance.Position;
            if (instance.Directional && instance.FollowPawn?.Drawer?.renderer != null)
            {
                anchor += instance.FollowPawn.Drawer.renderer.BaseHeadOffsetAt(instance.Facing);
            }
            anchor.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            for (int index = 0; index < instance.Renderers.Length; index++)
            {
                DrawRenderer(instance, instance.Renderers[index], anchor);
            }
        }

        private static void DrawRenderer(VfxInstance instance, CharacterVfxSprite renderer, Vector3 anchor)
        {
            if (!RendererMatchesFacing(instance, renderer) || IsStaticHaloRenderer(instance, renderer))
            {
                return;
            }

            DrawAnimatedSprite(renderer, anchor, Vector3.zero, instance.Scale,
                instance.Rotation, instance.FlipX, instance.AlphaMultiplier);
        }

        internal static void DrawAnimatedSprite(CharacterVfxSprite renderer, Vector3 anchor,
            Vector3 authoredOrigin, float visualScale, float rotation, bool mirror, float alphaMultiplier,
            Material overrideMaterial = null, float reveal = 1f, bool invertAnimatedRotation = false)
        {

            if (renderer == null || !renderer.visible || !renderer.transform.gameObject.activeInHierarchy
                || renderer.texture == null || renderer.color.a <= 0.001f || reveal <= 0.0001f) return;
            Texture2D texture = renderer.texture;

            Transform transform = renderer.transform;
            Vector3 animationPosition = transform.position - authoredOrigin;
            Vector3 scale = transform.localToWorldMatrix.lossyScale;
            Vector2 spriteSize = renderer.size;
            float width = Mathf.Abs(spriteSize.x * scale.x * visualScale);
            float height = Mathf.Abs(spriteSize.y * scale.y * visualScale);
            if (width <= 0.001f || height <= 0.001f)
            {
                return;
            }

            Vector3 drawPosition = anchor;
            drawPosition.x += animationPosition.x * visualScale * (mirror ? -1f : 1f);
            drawPosition.z += animationPosition.y * visualScale;
            drawPosition += Altitudes.AltIncVect * (0.01f + Mathf.Max(0, renderer.sortingOrder) * 0.004f);
            float animatedAngle = Mathf.DeltaAngle(0f, transform.eulerAngles.z);
            if (invertAnimatedRotation) animatedAngle = -animatedAngle;
            float angle = rotation + (mirror ? -animatedAngle : animatedAngle);
            bool flipX = mirror ^ renderer.flipX ^ (scale.x < 0f);
            bool flipY = renderer.flipY ^ (scale.y < 0f);
            Matrix4x4 matrix = Matrix4x4.TRS(
                drawPosition,
                Quaternion.AngleAxis(angle, Vector3.up),
                new Vector3(flipX ? -width : width, 1f, flipY ? -height : height));

            bool glow = renderer.glow;
            Material material = overrideMaterial ?? GetMaterial(texture, glow);
            if (material == null)
            {
                return;
            }

            PropertyBlock.Clear();
            Color drawColor = renderer.color;
            drawColor.a = Mathf.Clamp01(drawColor.a * alphaMultiplier);
            PropertyBlock.SetColor(ShaderPropertyIDs.Color, drawColor);
            if (overrideMaterial != null) PropertyBlock.SetFloat("_Reveal", reveal);
            Graphics.DrawMesh(MeshPool.plane10, matrix, material, 0, null, 0, PropertyBlock);
            PropertyBlock.Clear();
        }

        private static bool IsStaticHaloRenderer(VfxInstance instance, CharacterVfxSprite renderer)
        {
            if (!instance.Directional || renderer == null)
            {
                return false;
            }

            return renderer.name.StartsWith("HaloBaseGlow_", StringComparison.OrdinalIgnoreCase)
                || renderer.name.StartsWith("HaloPulseGlow_", StringComparison.OrdinalIgnoreCase);
        }

        internal static Material GetMaterial(Texture2D texture, bool glow)
        {
            if (texture == null) return null;
            int key = unchecked(texture.GetInstanceID() * 2 + (glow ? 1 : 0));
            if (Materials.TryGetValue(key, out Material material))
            {
                return material;
            }

            Shader shader = glow ? ShaderDatabase.MoteGlow : ShaderDatabase.Transparent;
            material = MaterialPool.MatFrom(texture, shader, Color.white);
            Materials[key] = material;
            return material;
        }

        internal static bool TryGetAnimation(CharacterUnityVfxKind kind, out CharacterVfxDef def)
        {
            def = null;
            if (MissingPrefabs.Contains(kind)) return false;
            def = DefDatabase<CharacterVfxDef>.GetNamedSilentFail("MX_Vfx_" + AddressFor(kind));
            if (def != null && def.Prepare()) return true;
            MissingPrefabs.Add(kind);
            Log.ErrorOnce("[MiliraXian] Authored VFX data is unavailable: " + kind, 197631100 + (int)kind);
            def = null;
            return false;
        }

        internal static Material LoadAuthoredMaterial(string address)
        {
            Material material = LoadAuthoredAsset<Material>(address);
            if (material == null || material.shader == null || !material.shader.isSupported)
            {
                Log.ErrorOnce("[MiliraXian] Authored VFX material is unavailable: " + address, GenText.StableStringHash(address));
                return null;
            }
            return material;
        }

        internal static T LoadAuthoredAsset<T>(string address) where T : UnityEngine.Object
        {
            if (!materialsLoadAttempted)
            {
                materialsLoadAttempted = true;
                const string name = "miliraxian_vfx_materials";
                ModContentPack content = NeiyuLawMod.Instance?.Content;
                List<AssetBundle> bundles = content?.assetBundles?.loadedAssetBundles;
                if (bundles != null)
                    foreach (AssetBundle bundle in bundles)
                        if (bundle != null && bundle.name == name) { authoredMaterials = bundle; break; }
                if (authoredMaterials == null && content != null)
                {
                    string path = Path.Combine(content.RootDir, "Content/AssetBundles/Windows/" + name);
                    if (File.Exists(path)) authoredMaterials = AssetBundle.LoadFromFile(path);
                }
            }
            return authoredMaterials?.LoadAsset<T>(address);
        }

        private static string AddressFor(CharacterUnityVfxKind kind)
        {
            switch (kind)
            {
                case CharacterUnityVfxKind.ZhaoliMinghuo:
                    return "zhaoli_minghuo_vfx";
                case CharacterUnityVfxKind.ZhaoliGuiyi:
                    return "zhaoli_guiyi_vfx";
                case CharacterUnityVfxKind.ZhaoliDeathField:
                    return "zhaoli_deathfield_vfx";
                case CharacterUnityVfxKind.ZhaoliMinshen:
                    return "zhaoli_minshen_vfx";
                case CharacterUnityVfxKind.ZhaoliMinshenImpact:
                    return "zhaoli_minshen_impact_vfx";
                case CharacterUnityVfxKind.NeiyuFlowerCircle:
                    return "neiyu_flower_circle_vfx";
                case CharacterUnityVfxKind.NeiyuSkyfallTakeoff:
                    return "neiyu_skyfall_takeoff_vfx";
                case CharacterUnityVfxKind.NeiyuSkyfallWarning:
                    return "neiyu_skyfall_warning_vfx";
                case CharacterUnityVfxKind.NeiyuSkyfallImpact:
                    return "neiyu_skyfall_impact_vfx";
                case CharacterUnityVfxKind.NeiyuHalo:
                    return "neiyu_halo_vfx";
                case CharacterUnityVfxKind.ZhaoliHalo:
                    return "zhaoli_halo_vfx";
                case CharacterUnityVfxKind.ArcaneCircle:
                    return "arcane_circle";
                case CharacterUnityVfxKind.ArcaneLaunch:
                    return "arcane_launch";
                case CharacterUnityVfxKind.ArcaneImpact:
                    return "arcane_impact";
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, null);
            }
        }

        private static bool InstanceInvalid(VfxInstance instance)
        {
            if (instance == null || instance.Root == null || instance.Map == null)
            {
                return true;
            }

            Pawn pawn = instance.FollowPawn;
            return pawn != null && (pawn.Destroyed || pawn.Dead || !pawn.Spawned || pawn.MapHeld != instance.Map);
        }

        private static void DestroyInstance(VfxInstance instance)
        {
            if (instance?.Root != null)
            {
                UnityEngine.Object.Destroy(instance.Root);
            }
        }

        private static void AdvanceLoopPhase(VfxInstance instance, int now)
        {
            if (instance == null || !instance.Loop)
            {
                return;
            }

            int deltaTicks = Mathf.Max(0, now - instance.LastPhaseTick);
            if (deltaTicks > 0)
            {
                instance.LoopPhase = Mathf.Repeat(
                    instance.LoopPhase + deltaTicks * instance.PlaybackRate / Mathf.Max(1f, instance.DurationTicks),
                    1f);
                instance.LastPhaseTick = now;
            }
        }

        private static bool RendererMatchesFacing(VfxInstance instance, CharacterVfxSprite renderer)
        {
            if (!instance.Directional || renderer == null)
            {
                return true;
            }

            string direction = instance.Facing == Rot4.North
                ? "_North"
                : instance.Facing == Rot4.South
                    ? "_South"
                    : "_East";
            return renderer.name.IndexOf(direction, StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static long PersistentKey(CharacterUnityVfxKind kind, int ownerId)
        {
            return ((long)(int)kind << 32) | (uint)ownerId;
        }

        private static int CurrentTick
        {
            get { return Find.TickManager != null ? Find.TickManager.TicksGame : 0; }
        }
    }
}

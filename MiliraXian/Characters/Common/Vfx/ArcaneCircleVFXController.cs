using System;
using MiliraXian.Characters.Common.Abilities;
using RimWorld.Planet;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.Common.Vfx
{
    public sealed class ArcaneCircleVFXProperties : DefModExtension
    {
        public float scale = 1.15f;
        public SoundDef humSound;
        public SoundDef etchSound;
        public SoundDef stageSound;
        public SoundDef sealSound;
        public SoundDef convergeSound;
    }

    // One Unity instance per active job. It reads game progress and never advances it.
    public sealed class ArcaneCircleVFXController : MonoBehaviour
    {
        private sealed class CircleLayer
        {
            public CharacterVfxSprite Renderer;
            public Transform Reveal;
            public float Seed, Profile;
        }

        private sealed class ParticleLayer
        {
            public CharacterVfxParticleLayer Source;
            public ArcaneParticleBatch Batch;
            public bool Stretch;
            public float LengthScale;
            public float VelocityScale;
        }

        private static bool invalidPrefab;
        private JobDriver_WorldArcaneStrike owner;
        private Game sourceGame;
        private CharacterVfxAnimation animation;
        private Transform circleTransform;
        private CircleLayer[] layers;
        private ParticleLayer[] particles;
        private MaterialPropertyBlock strokeProperties;
        private ArcaneParticleBatch sparkBatch, runeBatch;
        private float cachedParticleTime = -1f;
        private Vector3 cachedAnchor;
        private ArcaneVfxQuality cachedQuality;
        private ArcaneCircleVFXProperties properties;
        private Sustainer hum;
        private float visualDuration;
        private float lastAudioProgress;
        private int lastSoundSecond;
        private float scale;
        private float lastProgress = -1f;
        private float lastParticleTime = -1f;
        private bool disposed;

        public static ArcaneCircleVFXController Create(JobDriver_WorldArcaneStrike job)
        {
            if (!OwnerAvailable(job) || invalidPrefab
                || !CharacterUnityVfxRuntime.TryGetAnimation(CharacterUnityVfxKind.ArcaneCircle, out CharacterVfxDef definition))
            {
                return null;
            }

            GameObject root = null;
            try
            {
                CharacterVfxAnimation native = definition.Instantiate();
                if (native == null) return null;
                root = native.Root;
                root.name = "ArcaneCircle_" + job.Caster.thingIDNumber;
                root.hideFlags = HideFlags.HideAndDontSave;
                // Update still runs during menu/load transitions and disposes stale owners.
                UnityEngine.Object.DontDestroyOnLoad(root);
                ArcaneCircleVFXController controller = root.AddComponent<ArcaneCircleVFXController>();
                controller.animation = native;
                controller.Initialize(job);
                return controller;
            }
            catch (Exception exception)
            {
                if (root != null)
                {
                    root.SetActive(false);
                    UnityEngine.Object.Destroy(root);
                }
                invalidPrefab = true;
                Log.ErrorOnce("[WorldArcaneStrike] ArcaneCircle prefab initialization failed: " + exception, 197631201);
                return null;
            }
        }

        public static void RebuildAfterLoad()
        {
            // One traversal after loading, never a per-tick map/pawn scan.
            for (int mapIndex = 0; mapIndex < Find.Maps.Count; mapIndex++)
            {
                var pawns = Find.Maps[mapIndex].mapPawns.AllPawnsSpawned;
                for (int pawnIndex = 0; pawnIndex < pawns.Count; pawnIndex++)
                {
                    if (pawns[pawnIndex].jobs?.curDriver is JobDriver_WorldArcaneStrike job && job.IsChanting)
                    {
                        job.RefreshChantVisuals();
                    }
                }
            }
        }

        private void Initialize(JobDriver_WorldArcaneStrike job)
        {
            owner = job;
            sourceGame = Current.Game;
            properties = job.Caster.CurJob.ability.def.GetModExtension<ArcaneCircleVFXProperties>();
            scale = Mathf.Max(0.01f, properties?.scale ?? 1.15f);
            visualDuration = animation.Def.duration;
            circleTransform = animation.Find("Circle")
                ?? throw new InvalidOperationException("ArcaneCircle is missing its Circle animation node.");
            lastAudioProgress = job.ChantProgress > 0f ? job.ChantProgress : -1f;
            // Restoring a cast must not replay its earlier construction cues.
            lastSoundSecond = job.ChantProgress > 0f ? Mathf.FloorToInt(job.ChantProgress * visualDuration) : -1;

            CharacterVfxSprite[] renderers = animation.Sprites;
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException("ArcaneCircle has no independent visual layers.");
            }
            layers = new CircleLayer[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                CharacterVfxSprite renderer = renderers[i];
                layers[i] = new CircleLayer
                {
                    Renderer = renderer, Reveal = renderer.transform.Find("Reveal"), Seed = i * 0.381966f,
                    Profile = renderer.name == "Connections" || renderer.name == "RadialStructure" ? 1f : 0f
                };
            }

            CharacterVfxParticleLayer[] systems = animation.Particles;
            particles = new ParticleLayer[systems.Length];
            strokeProperties = new MaterialPropertyBlock();
            Shader energyShader = DefDatabase<ShaderDef>.GetNamed("MX_ArcaneEnergy").Shader;
            for (int i = 0; i < systems.Length; i++)
            {
                CharacterVfxParticleLayer system = systems[i];
                CharacterVfxEmitter emitter = system.Emitter;
                Texture2D texture = animation.Def.Texture(emitter?.texture);
                if (texture == null)
                {
                    throw new InvalidOperationException("ArcaneCircle particle layer has no texture: " + system.Transform.name);
                }
                bool rune = emitter.texture.EndsWith("RuneParticle", StringComparison.Ordinal);
                if (rune && runeBatch == null) runeBatch = new ArcaneParticleBatch(MaterialPool.MatFrom(texture, energyShader, Color.white));
                if (!rune && sparkBatch == null) sparkBatch = new ArcaneParticleBatch(MaterialPool.MatFrom(texture, energyShader, Color.white));
                particles[i] = new ParticleLayer
                {
                    Source = system,
                    Batch = rune ? runeBatch : sparkBatch,
                    Stretch = emitter.stretch,
                    LengthScale = emitter.lengthScale,
                    VelocityScale = emitter.velocityScale
                };
            }
            SampleProgress(job.ChantProgress);
        }

        private void LateUpdate()
        {
            if (disposed)
            {
                return;
            }
            if (Current.Game != sourceGame || Current.ProgramState != ProgramState.Playing || !OwnerAvailable(owner))
            {
                Cleanup();
                return;
            }

            Vector3 anchor = owner.Caster.DrawPos;
            transform.position = anchor;
            SampleProgress(owner.ChantProgress);
            UpdateSound(owner.ChantProgress);
            if (owner.SourceMap != Find.CurrentMap || WorldRendererUtility.WorldSelected
                || Find.UIRoot.HideMotes || Find.ScreenshotModeHandler.Active)
            {
                return;
            }
            anchor.y = AltitudeLayer.MoteOverhead.AltitudeFor();
            for (int i = 0; i < layers.Length; i++)
            {
                CircleLayer layer = layers[i];
                strokeProperties.Clear();
                float profile = layer.Renderer.name == "ConstructionPulse" ? 1f : layer.Profile;
                ArcaneVfxStyle.Properties(strokeProperties, lastParticleTime, layer.Seed, profile);
                CharacterUnityVfxRuntime.DrawAnimatedSprite(layer.Renderer, anchor, transform.position,
                    scale, 0f, false, ArcaneVfxStyle.Tuning.circleOpacity, layer.Renderer.sharedMaterial,
                    layer.Reveal != null ? layer.Reveal.localScale.x : 1f, invertAnimatedRotation: true,
                    properties: strokeProperties);
            }
            DrawParticles(anchor);
        }

        private void SampleProgress(float progress)
        {
            if (progress == lastProgress)
            {
                return;
            }
            animation.Sample(Mathf.Clamp01(progress));
            // The clip and particles share the 96%-97.25% sealing hold.
            lastProgress = progress;
            lastParticleTime = ArcaneVfxStyle.ChantSeconds(progress, visualDuration);
        }

        private void UpdateSound(float progress)
        {
            bool audible = owner.SourceMap == Find.CurrentMap && !WorldRendererUtility.WorldSelected
                && !Find.TickManager.Paused;
            if (progress < 0.96f && (hum == null || hum.Ended) && audible)
            {
                hum = properties?.humSound?.TrySpawnSustainer(SoundInfo.InMap(owner.Caster, MaintenanceType.PerFrame));
            }
            if (hum != null && !hum.Ended)
            {
                if (progress >= 0.96f)
                {
                    StopSound();
                }
                else
                {
                    hum.info.volumeFactor = Mathf.Lerp(0.25f, 1f, progress * progress);
                    hum.info.pitchFactor = Mathf.Lerp(0.8f, 1.1f, progress);
                    hum.Maintain();
                }
            }
            int second = Mathf.FloorToInt(progress * visualDuration);
            if (audible)
            {
                // At high speed use one current cue, never a burst of skipped seconds.
                if (second != lastSoundSecond && progress < 0.96f)
                {
                    bool stage = lastAudioProgress < 0f
                        || lastAudioProgress < 0.15f && progress >= 0.15f
                        || lastAudioProgress < 0.35f && progress >= 0.35f
                        || lastAudioProgress < 0.60f && progress >= 0.60f
                        || lastAudioProgress < 0.85f && progress >= 0.85f;
                    PlayCue(stage ? properties?.stageSound : properties?.etchSound,
                        stage ? 0.8f : Mathf.Lerp(0.25f, 0.55f, progress), 0.9f + second % 4 * 0.07f);
                }
                if (lastAudioProgress < 0.96f && progress >= 0.96f)
                    PlayCue(properties?.sealSound, 1f, 1f);
                if (lastAudioProgress < 0.9725f && progress >= 0.9725f)
                    PlayCue(properties?.convergeSound, 1f, 1f);
            }
            lastSoundSecond = second;
            lastAudioProgress = progress;
        }

        private void PlayCue(SoundDef sound, float volume, float pitch)
        {
            if (sound == null) return;
            SoundInfo info = SoundInfo.InMap(owner.Caster);
            info.volumeFactor = volume;
            info.pitchFactor = pitch;
            sound.PlayOneShot(info);
        }

        private void StopSound()
        {
            if (hum != null && !hum.Ended) hum.End();
            hum = null;
        }

        private void DrawParticles(Vector3 anchor)
        {
            if (cachedParticleTime != lastParticleTime || cachedAnchor != anchor || cachedQuality != ArcaneVfxStyle.Quality)
            {
                BuildParticles(anchor);
                cachedParticleTime = lastParticleTime;
                cachedAnchor = anchor;
                cachedQuality = ArcaneVfxStyle.Quality;
            }
            sparkBatch?.Draw(lastParticleTime, 0, null);
            runeBatch?.Draw(lastParticleTime, 0, null);
        }

        private void BuildParticles(Vector3 anchor)
        {
            sparkBatch?.Begin();
            runeBatch?.Begin();
            // Reserve a small, stable budget for the construction pen and seal nodes.
            DrawEtchingAndNodes(anchor);
            for (int layerIndex = 0; layerIndex < particles.Length; layerIndex++)
            {
                ParticleLayer layer = particles[layerIndex];
                CharacterVfxParticle[] frame = CharacterVfxParticles.Sample(layer.Source.Emitter.bakedIndex, lastParticleTime);
                Transform emitterTransform = layer.Source.Transform;
                float layerScale = emitterTransform.localToWorldMatrix.lossyScale.x * scale;
                float glow = Mathf.Lerp(1f, 1.35f, Mathf.InverseLerp(0.85f, 0.96f, lastProgress));
                for (int i = 0; i < frame.Length; i++)
                {
                    if (!ArcaneVfxStyle.KeepParticle(i, layerIndex, ArcaneVfxStyle.Density)) continue;
                    CharacterVfxParticle particle = frame[i];
                    Color color = particle.Color;
                    color.r = Mathf.Min(1f, color.r * glow);
                    color.g = Mathf.Min(1f, color.g * glow);
                    color.b = Mathf.Min(1f, color.b * glow);
                    color.a *= 0.72f;
                    Vector3 local = emitterTransform.TransformPoint(particle.Position) - transform.position;
                    Vector3 position = anchor + new Vector3(local.x * scale, local.z * 0.04f, local.y * scale);
                    float size = particle.Size * Mathf.Abs(layerScale);
                    if (size <= 0.001f || color.a <= 0.001f)
                    {
                        continue;
                    }
                    float angle = -particle.Rotation - emitterTransform.eulerAngles.z;
                    float length = size;
                    if (layer.Stretch)
                    {
                        Vector3 velocity = emitterTransform.TransformVector(particle.Velocity) * scale;
                        float speed = Mathf.Sqrt(velocity.x * velocity.x + velocity.y * velocity.y);
                        if (speed > 0.001f) angle = Mathf.Atan2(velocity.x, velocity.y) * Mathf.Rad2Deg;
                        length = Mathf.Min(1.8f * Mathf.Abs(layerScale),
                            size * layer.LengthScale + speed * layer.VelocityScale);
                    }
                    Quaternion rotation = Quaternion.AngleAxis(angle, Vector3.up);
                    layer.Batch.Add(position, rotation * Vector3.right, rotation * Vector3.forward, size, length, color);
                }
            }
            DrawGatheringDust(anchor);
            sparkBatch?.Upload();
            runeBatch?.Upload();
        }

        private void DrawEtchingAndNodes(Vector3 anchor)
        {
            if (sparkBatch == null) return;
            for (int i = 0; i < layers.Length; i++)
            {
                CircleLayer layer = layers[i];
                CharacterVfxSprite renderer = layer.Renderer;
                if (!renderer.visible || renderer.color.a < 0.01f) continue;
                float reveal = layer.Reveal?.localScale.x ?? 1f;
                bool ring = renderer.name == "BaseCircle" || renderer.name.Contains("Ring") || renderer.name.Contains("Belt");
                bool node = renderer.name == "Core" || renderer.name == "InnerSigil";
                if (renderer.name == "Connections")
                {
                    bool outer = renderer.transform.parent.name == "OuterConstellation";
                    for (int n = 0; n < 8; n++)
                    {
                        if (reveal < (n + 1f) / 8f) continue;
                        float age = Mathf.Repeat(lastParticleTime * 0.45f + n * 0.618034f, 1f);
                        float angle = Mathf.PI / 2f - (outer ? 0f : Mathf.PI / 8f) - n * Mathf.PI / 4f;
                        float radius = outer ? Mathf.Lerp(10.86f / 26f, 10.5f / 26f, age) : Mathf.Lerp(0.377f, 0.34f, age);
                        Vector3 local = renderer.transform.TransformPoint(new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0)) - transform.position;
                        Vector3 position = anchor + new Vector3(local.x * scale, 0.025f, local.y * scale);
                        Color color = renderer.color;
                        color.a *= Mathf.Sin(age * Mathf.PI) * 0.46f;
                        float size = 0.11f * circleTransform.localScale.x;
                        sparkBatch.Add(position, Vector3.right, Vector3.forward, size, size, color);
                    }
                }
                if ((!ring || reveal <= 0.002f || reveal >= 0.998f) && !node) continue;
                int count = node ? (ArcaneVfxStyle.Quality == ArcaneVfxQuality.Low ? 1 : 2) : 4;
                for (int p = 0; p < count; p++)
                {
                    float age = Mathf.Repeat(lastParticleTime / Mathf.Max(0.4f, ArcaneVfxStyle.Tuning.dustLifetime) + p * 0.25f + layer.Seed, 1f);
                    float angle = node ? lastParticleTime * (0.6f + i % 3 * 0.15f) + p * Mathf.PI
                        : (reveal - p * 0.0035f) * Mathf.PI * 2f;
                    float radius = node ? 0.27f + age * 0.08f : 0.475f;
                    Vector3 authored = renderer.transform.TransformPoint(new Vector3(Mathf.Sin(angle) * radius,
                        Mathf.Cos(angle) * radius, 0)) - transform.position;
                    Vector3 position = anchor + new Vector3(authored.x * scale, 0.035f + age * 0.07f, authored.y * scale);
                    Color color = renderer.color;
                    color.a *= node ? 0.58f * Mathf.Sin(age * Mathf.PI) : 0.72f * (1f - p * 0.21f);
                    float size = node ? 0.075f + age * 0.04f : p == 0 ? 0.14f : 0.075f;
                    float contraction = circleTransform.localScale.x;
                    sparkBatch.Add(position, Vector3.right, Vector3.forward, size * contraction, size * contraction, color);
                }
            }
        }

        private void DrawGatheringDust(Vector3 anchor)
        {
            if (sparkBatch == null) return;
            float t = lastParticleTime;
            float wake = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f, 18f, t));
            float climax = Mathf.InverseLerp(51f, 57.6f, t);
            float contraction = circleTransform.localScale.x;
            ArcaneVfxStyleDef tuning = ArcaneVfxStyle.Tuning;
            for (int i = 0; i < 84; i++)
            {
                if (!ArcaneVfxStyle.KeepParticle(i, 41, ArcaneVfxStyle.Density)) continue;
                float seed = Mathf.Repeat(i * 0.618034f, 1f);
                float age = Mathf.Repeat(t / Mathf.Max(0.4f, tuning.dustLifetime) + seed, 1f);
                float angle = i * 2.399963f + t * (i % 2 == 0 ? 0.12f : -0.16f);
                float orbit = 2.2f + i % 4 * 1.32f;
                float radius = orbit * Mathf.Lerp(1f, 1f - age * 0.88f, climax) * contraction * scale;
                Vector3 position = anchor + new Vector3(Mathf.Cos(angle) * radius, 0.03f + age * 0.11f, Mathf.Sin(angle) * radius);
                Color color = i % 5 == 0 ? new Color(0.96f, 0.76f, 0.39f) : new Color(0.48f, 0.84f, 1f);
                color.a = Mathf.Sin(age * Mathf.PI) * wake * (0.18f + climax * 0.28f);
                float size = Mathf.Lerp(tuning.particleSizeMin, tuning.particleSizeMax, seed) * contraction;
                Vector3 direction = new Vector3(-Mathf.Cos(angle), 0, -Mathf.Sin(angle));
                sparkBatch.Add(position, Vector3.Cross(Vector3.up, direction), direction, size, size * (1f + climax * 2.5f), color);
            }
        }

        private static bool OwnerAvailable(JobDriver_WorldArcaneStrike job)
        {
            Pawn caster = job?.Caster;
            return job != null && job.IsChanting && caster != null && !caster.Destroyed
                && !caster.Dead && !caster.Downed && !caster.InMentalState && !caster.Deathresting
                && caster.Spawned && caster.jobs?.curDriver == job
                && job.SourceMap != null && caster.Map == job.SourceMap && Find.Maps.Contains(job.SourceMap)
                && caster.Position == caster.CurJob.targetA.Cell;
        }

        public void Cleanup()
        {
            if (disposed)
            {
                return;
            }
            disposed = true;
            enabled = false;
            StopSound();
            owner = null;
            sourceGame = null;
            gameObject.SetActive(false);
            sparkBatch?.Dispose();
            runeBatch?.Dispose();
            sparkBatch = runeBatch = null;
            animation?.Dispose();
        }

        private void OnDestroy()
        {
            StopSound();
            sparkBatch?.Dispose();
            runeBatch?.Dispose();
            sparkBatch = runeBatch = null;
            disposed = true;
            owner = null;
            sourceGame = null;
            layers = null;
            particles = null;
            animation = null;
            circleTransform = null;
            strokeProperties = null;
            properties = null;
        }
    }
}

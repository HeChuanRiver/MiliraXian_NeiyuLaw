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
            public SpriteRenderer Renderer;
            public Transform Reveal;
        }

        private sealed class ParticleLayer
        {
            public ParticleSystem System;
            public ParticleSystem.Particle[] Buffer;
            public Material Material;
            public bool Stretch;
            public float LengthScale;
            public float VelocityScale;
        }

        private static bool invalidPrefab;
        private JobDriver_WorldArcaneStrike owner;
        private Game sourceGame;
        private Animator animator;
        private CircleLayer[] layers;
        private ParticleLayer[] particles;
        private MaterialPropertyBlock particleProperties;
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
                || !CharacterUnityVfxRuntime.TryGetPrefab(CharacterUnityVfxKind.ArcaneCircle, out GameObject prefab))
            {
                return null;
            }

            GameObject root = null;
            try
            {
                root = UnityEngine.Object.Instantiate(prefab);
                root.name = "ArcaneCircle_" + job.Caster.thingIDNumber;
                root.hideFlags = HideFlags.HideAndDontSave;
                // Update still runs during menu/load transitions and disposes stale owners.
                UnityEngine.Object.DontDestroyOnLoad(root);
                ArcaneCircleVFXController controller = root.AddComponent<ArcaneCircleVFXController>();
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
            animator = GetComponent<Animator>();
            if (animator == null || animator.runtimeAnimatorController == null || !animator.HasState(0, Animator.StringToHash("Play")))
            {
                throw new InvalidOperationException("ArcaneCircle requires an Animator with the Play state.");
            }
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.updateMode = AnimatorUpdateMode.Normal;
            animator.speed = 0f;
            visualDuration = animator.runtimeAnimatorController.animationClips[0].length;
            lastAudioProgress = job.ChantProgress > 0f ? job.ChantProgress : -1f;
            // Restoring a cast must not replay its earlier construction cues.
            lastSoundSecond = job.ChantProgress > 0f ? Mathf.FloorToInt(job.ChantProgress * visualDuration) : -1;

            SpriteRenderer[] renderers = GetComponentsInChildren<SpriteRenderer>(true);
            if (renderers.Length == 0)
            {
                throw new InvalidOperationException("ArcaneCircle has no independent visual layers.");
            }
            layers = new CircleLayer[renderers.Length];
            for (int i = 0; i < renderers.Length; i++)
            {
                SpriteRenderer renderer = renderers[i];
                renderer.enabled = false;
                layers[i] = new CircleLayer { Renderer = renderer, Reveal = renderer.transform.Find("Reveal") };
            }

            ParticleSystem[] systems = GetComponentsInChildren<ParticleSystem>(true);
            particles = new ParticleLayer[systems.Length];
            particleProperties = new MaterialPropertyBlock();
            for (int i = 0; i < systems.Length; i++)
            {
                ParticleSystem system = systems[i];
                ParticleSystemRenderer renderer = system.GetComponent<ParticleSystemRenderer>();
                Texture2D texture = renderer?.sharedMaterial?.mainTexture as Texture2D;
                if (texture == null)
                {
                    throw new InvalidOperationException("ArcaneCircle particle layer has no texture: " + system.name);
                }
                renderer.enabled = false;
                system.useAutoRandomSeed = false;
                system.randomSeed = (uint)(1147 + i * 79);
                system.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
                particles[i] = new ParticleLayer
                {
                    System = system,
                    Buffer = new ParticleSystem.Particle[Mathf.Clamp(system.main.maxParticles, 1, 128)],
                    Material = CharacterUnityVfxRuntime.GetMaterial(texture, true),
                    Stretch = renderer.renderMode == ParticleSystemRenderMode.Stretch,
                    LengthScale = renderer.lengthScale,
                    VelocityScale = renderer.velocityScale
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
                CharacterUnityVfxRuntime.DrawAnimatedSprite(layer.Renderer, anchor, transform.position,
                    scale, 0f, false, 1f, layer.Renderer.sharedMaterial,
                    layer.Reveal != null ? layer.Reveal.localScale.x : 1f, invertAnimatedRotation: true);
            }
            DrawParticles(anchor);
        }

        private void SampleProgress(float progress)
        {
            if (progress == lastProgress)
            {
                return;
            }
            animator.Play("Play", 0, Mathf.Clamp01(progress));
            animator.Update(0f);
            // The clip and particles share the 96%-97.25% sealing hold.
            float particleProgress = progress <= 0.96f ? progress
                : progress < 0.9725f ? 0.96f : progress - 0.0125f;
            float particleTime = particleProgress * visualDuration;
            bool restart = lastParticleTime < 0f || particleTime < lastParticleTime;
            float delta = restart ? particleTime : particleTime - lastParticleTime;
            if (delta > 0f || restart)
            {
                for (int i = 0; i < particles.Length; i++)
                {
                    ParticleSystem system = particles[i].System;
                    // A saved orbit needs small simulation steps when fast-forwarded.
                    system.Simulate(delta, false, restart, restart || delta > 0.1f);
                    system.Pause(false);
                }
            }
            lastProgress = progress;
            lastParticleTime = particleTime;
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
            for (int layerIndex = 0; layerIndex < particles.Length; layerIndex++)
            {
                ParticleLayer layer = particles[layerIndex];
                int count = layer.System.GetParticles(layer.Buffer);
                float layerScale = layer.System.transform.lossyScale.x * scale;
                float glow = Mathf.Lerp(1f, 1.35f, Mathf.InverseLerp(0.85f, 0.96f, lastProgress));
                for (int i = 0; i < count; i++)
                {
                    ParticleSystem.Particle particle = layer.Buffer[i];
                    Color color = particle.GetCurrentColor(layer.System);
                    color.r *= glow;
                    color.g *= glow;
                    color.b *= glow;
                    Vector3 local = layer.System.transform.TransformPoint(particle.position) - transform.position;
                    Vector3 position = anchor + new Vector3(local.x * scale, local.z * 0.04f, local.y * scale);
                    float size = particle.GetCurrentSize(layer.System) * Mathf.Abs(layerScale);
                    if (size <= 0.001f || color.a <= 0.001f)
                    {
                        continue;
                    }
                    particleProperties.Clear();
                    particleProperties.SetColor(ShaderPropertyIDs.Color, color);
                    float angle = -particle.rotation - layer.System.transform.eulerAngles.z;
                    float length = size;
                    if (layer.Stretch)
                    {
                        Vector3 velocity = layer.System.transform.TransformVector(particle.totalVelocity) * scale;
                        float speed = Mathf.Sqrt(velocity.x * velocity.x + velocity.y * velocity.y);
                        if (speed > 0.001f) angle = Mathf.Atan2(velocity.x, velocity.y) * Mathf.Rad2Deg;
                        length = Mathf.Min(1.8f * Mathf.Abs(layerScale),
                            size * layer.LengthScale + speed * layer.VelocityScale);
                    }
                    Matrix4x4 matrix = Matrix4x4.TRS(position, Quaternion.AngleAxis(angle, Vector3.up),
                        new Vector3(size, 1f, length));
                    Graphics.DrawMesh(MeshPool.plane10, matrix, layer.Material, 0, null, 0, particleProperties);
                }
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
            UnityEngine.Object.Destroy(gameObject);
        }

        private void OnDestroy()
        {
            StopSound();
            disposed = true;
            owner = null;
            sourceGame = null;
            layers = null;
            particles = null;
            animator = null;
            particleProperties = null;
            properties = null;
        }
    }
}

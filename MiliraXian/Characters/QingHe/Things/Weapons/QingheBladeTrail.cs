using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Common.Vfx.BladeMotion;
using MiliraXian.Characters.QingHe.Abilities.SakuraDance;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    /// <summary>
    /// Trail and sheathe flash of Qinghe's sword motions (weapon mod extension). The trail follows
    /// the blade: each layer is a strip whose centre line is a point on the blade at
    /// <see cref="SakuraTrailLayer.radialOffset"/> metres from <see cref="bandCentre"/> (a share of the
    /// blade from the grip), its width along the blade. Drawn by the Sakura Dance strip renderer.
    /// </summary>
    public class QingheBladeTrailExtension : DefModExtension
    {
        /// <summary>Blade length from the grip to the tip, metres.</summary>
        public float bladeLength = 0.976f;
        /// <summary>Share of the blade (from the grip) the layers' radial offsets are measured from.</summary>
        public float bandCentre = 0.65f;
        public float heightToScreen = 0.5f;
        public List<SakuraTrailLayer> layers = new();
        public string distortionNormalTexPath = "MiliraXianQinghe/Effect/SakuraDance/DistortNormal";
        public float distortionMetresPerUnit = 0.04f;
        public float shadeOpacity;
        public ScreenBloomProperties bloom;
        /// <summary>Tint of the additive layers per flower bell resonance (spring, summer, autumn, winter); white without one.</summary>
        public List<Color> resonanceThreadColors = new();

        public QingheSheatheFlashProps sheatheFlash;

        public Color ThreadColor(FlowerBellResonance resonance)
        {
            int i = (int)resonance;
            return i >= 0 && i < resonanceThreadColors.Count ? resonanceThreadColors[i] : Color.white;
        }
    }

    /// <summary>
    /// The "ting" at the scabbard mouth when the blade goes fully in: two crossed needles for one
    /// frame or two, a horizontal light bar and a halo.
    /// Sizes in metres, times in seconds.
    /// </summary>
    public class QingheSheatheFlashProps
    {
        public string texPath = "MiliraXianQinghe/Effect/SakuraDance/Glow";
        public Color color = new(0.88f, 0.93f, 0.96f, 1f);
        public float needleLength = 0.9f;
        public float needleWidth = 0.03f;
        public float needleSeconds = 0.067f;
        public float needleBrightness = 4f;
        /// <summary>Degrees between the two needles and the horizontal bar.</summary>
        public float needleAngle = 55f;
        public float barLength = 1.4f;
        public float barWidth = 0.07f;
        public float barSeconds = 0.2f;
        public float barBrightness = 2.5f;
        public float haloSize = 0.9f;
        public float haloEndSize = 0.5f;
        public float haloSeconds = 0.2f;
        public float haloBrightness = 1.5f;
        public SoundDef sound;
    }

    /// <summary>Spawns the trail and the flash for one attack of a Qinghe sword motion.</summary>
    public static class QingheBladeTrail
    {
        /// <summary>The weapon's motion projection (the trail and the flash are drawn exactly like the blade).</summary>
        private static BladeProjection ProjectionFor(Pawn pawn, QingheBladeTrailExtension ext)
        {
            return pawn.equipment?.Primary?.def.GetModExtension<BladeMotionSetExtension>() is { } set
                ? set.Projection
                : new BladeProjection(ext.heightToScreen, 1f, 0f);
        }

        /// <param name="startTick">Game tick of trail second 0 (clip second <paramref name="from"/>).</param>
        /// <param name="from">Clip second the trail starts at; below the clip's trail start = the whole trail.</param>
        /// <param name="projection">The motion's projection; null = the held weapon's.</param>
        public static void SpawnTrail(Pawn pawn, QingheBladeTrailExtension ext, BladeMotionClip clip, Vector3 aim, int startTick, float speed,
            FlowerBellResonance resonance, float from = -1f, float to = -1f, BladeProjection? projection = null)
        {
            MapComponent_SakuraDanceVisuals visuals = pawn?.Map?.GetComponent<MapComponent_SakuraDanceVisuals>();
            if (visuals == null || ext == null || clip == null || !clip.HasTrail || ext.layers.NullOrEmpty())
            {
                return;
            }
            float trailFrom = Mathf.Max(clip.trailStart, from);
            // `to` (clip second) ends the emission early, for a piece of the clip played at its own speed.
            float trailTo = to > 0f ? Mathf.Min(clip.trailEnd, to) : clip.trailEnd;
            if (trailFrom >= trailTo)
            {
                return;
            }
            // The pawn's place on the ground: the body offset of the motion is added per sample instead.
            Vector3 c = pawn.DrawPos - BladeMotionPlayer.BodyOffset(pawn);
            BladeTrailVisual v = new()
            {
                ext = ext,
                clip = clip,
                startTick = startTick,
                center = new Vector3(c.x, 0f, c.z),
                pawnAltitude = AltitudeLayer.Pawn.AltitudeFor(),
                groundZ = c.z,
                noisePhase = (startTick * 0.6180339f) % 1f * 100f,
                emitSeconds = trailTo - trailFrom,
                threadColor = ext.ThreadColor(resonance),
                pawn = pawn,
                trailFrom = trailFrom,
            };
            v.projection = projection ?? ProjectionFor(pawn, ext);
            float rate = Mathf.Max(0.01f, speed);
            v.clock = ticks => (ticks - startTick) / 60f * rate;
            v.aim = aim;
            BladeMotionUtility.Basis(aim, out _, out _, out v.forward);
            v.layers.AddRange(ext.layers);
            v.baseLayerCount = v.layers.Count;
            visuals.AddSource(v);
        }

        /// <param name="flashTick">Game tick of the flash.</param>
        /// <param name="stillOn">Checked until the flash starts; false (the warmup was interrupted) drops it.</param>
        /// <param name="projection">The motion's projection; null = the held weapon's.</param>
        /// <param name="flash">Replaces the weapon's sheathe flash (another colour); null = the weapon's.</param>
        public static void SpawnFlash(Pawn pawn, QingheBladeTrailExtension ext, BladeMotionClip clip, Vector3 aim, float flashTick,
            System.Func<bool> stillOn, BladeProjection? projection = null, QingheSheatheFlashProps flash = null)
        {
            QingheSheatheFlashProps f = flash ?? ext?.sheatheFlash;
            if (ext == null || f == null || clip == null || clip.flashTime < 0f
                || !BladeMotionUtility.ScabbardMouth(clip, clip.flashTime, aim, out Vector3 mouth))
            {
                return;
            }
            BladeProjection proj = projection ?? ProjectionFor(pawn, ext);
            Vector3 bodyAt = BladeMotionUtility.BodyOffsetWorld(clip, clip.flashTime, aim);
            bodyAt.z /= Mathf.Max(1e-3f, proj.depthToScreen);
            SpawnFlashAt(pawn, f, ext.bloom, mouth + bodyAt, flashTick, stillOn, proj);
        }

        /// <summary>The same flash anywhere on the motion: <paramref name="offset"/> in world metres from the pawn's place on the ground (north-south unscaled).</summary>
        public static void SpawnFlashAt(Pawn pawn, QingheSheatheFlashProps f, ScreenBloomProperties bloom, Vector3 offset, float flashTick,
            System.Func<bool> stillOn, BladeProjection proj)
        {
            MapComponent_SakuraDanceVisuals visuals = pawn?.Map?.GetComponent<MapComponent_SakuraDanceVisuals>();
            if (visuals == null || f == null)
            {
                return;
            }
            SheatheFlashVisual v = new()
            {
                flash = f,
                projection = proj,
                bloom = bloom,
                startTick = Mathf.RoundToInt(flashTick),
                offset = offset,
                pawnAltitude = AltitudeLayer.Pawn.AltitudeFor() + 0.02f,
                lifeSeconds = Mathf.Max(f.needleSeconds, Mathf.Max(f.barSeconds, f.haloSeconds)),
                stillOn = stillOn,
                pawn = pawn,
                played = false,
            };
            float start = flashTick;
            v.clock = ticks => (ticks - start) / 60f;
            v.Follow();
            visuals.AddSource(v);
        }
    }

    internal sealed class BladeTrailVisual : MapComponent_SakuraDanceVisuals.StripSource
    {
        public QingheBladeTrailExtension ext;
        public BladeMotionClip clip;
        public Vector3 center;
        public Vector3 aim;
        public Vector3 forward;
        public Color threadColor = Color.white;
        public BladeProjection projection;
        public Pawn pawn;
        /// <summary>Clip second of trail second 0.</summary>
        public float trailFrom;

        public override float HeightToScreen => projection.heightToScreen;
        public override float DepthToScreen => projection.depthToScreen;
        public override float HeightOrigin => projection.heightOrigin;

        /// <summary>
        /// The trail is cut by the drawn body, which can still be sliding into the cell (a step into
        /// the strike), so it follows the pawn's current place on the ground.
        /// </summary>
        public override void BeforeDraw()
        {
            if (pawn == null || !pawn.Spawned)
            {
                return;
            }
            Vector3 c = pawn.DrawPos - BladeMotionPlayer.BodyOffset(pawn);
            center = new Vector3(c.x, 0f, c.z);
            groundZ = c.z;
        }

        public override string DistortionNormalTexPath => ext.distortionNormalTexPath;
        public override float DistortionMetresPerUnit => ext.distortionMetresPerUnit;
        public override float ShadeOpacity => ext.shadeOpacity;
        public override ScreenBloomProperties BloomFor(int layerIndex) => ext.bloom;

        public override Color TintFor(int layerIndex, SakuraTrailLayer layer)
        {
            return layer.additive ? layer.color * threadColor : layer.color;
        }

        public override void Sample(int layerIndex, float te, float radialOffset, out Vector3 centre, out Vector3 side)
        {
            float s = trailFrom + te;
            float inner = Mathf.Clamp01(ext.bandCentre - 0.5f);
            BladeMotionUtility.TrailPoints(clip, s, aim, ext.bladeLength, inner, out Vector3 a, out Vector3 tip);
            // The blade is drawn on the moving body: the trail follows the body offset of that second.
            Vector3 body = BladeMotionUtility.BodyOffsetWorld(clip, s, aim);
            // The body shift is a plain map offset (DrawPos); the strip renderer scales north-south
            // offsets by the depth factor, so it is given unscaled.
            body.z /= Mathf.Max(1e-3f, projection.depthToScreen);
            a += body;
            tip += body;
            // a = inner end, tip = outer end of the blade's outer part; the band centre sits between.
            Vector3 along = tip - a;
            float len = along.magnitude;
            side = len > 1e-5f ? along / len : forward;
            float share = (ext.bandCentre - inner) / Mathf.Max(1e-4f, 1f - inner);
            centre = center + a + along * share + side * radialOffset;
        }
    }

    internal sealed class SheatheFlashVisual : MapComponent_SakuraDanceVisuals.StripSource
    {
        public QingheSheatheFlashProps flash;
        public BladeProjection projection;
        public ScreenBloomProperties bloom;
        /// <summary>Scabbard mouth relative to the pawn's place on the ground.</summary>
        public Vector3 offset;
        public Vector3 at;
        public System.Func<bool> stillOn;
        public Pawn pawn;
        public bool played;
        private Mesh mesh;

        public override float HeightToScreen => projection.heightToScreen;
        public override float DepthToScreen => projection.depthToScreen;
        public override float HeightOrigin => projection.heightOrigin;
        public override ScreenBloomProperties BloomFor(int layerIndex) => bloom;

        public override bool Alive(float ticks)
        {
            if (ticks < startTick)
            {
                return stillOn == null || stillOn();
            }
            return true;
        }

        /// <summary>
        /// Puts the flash on the pawn's current place on the ground. The drawn position still lags
        /// behind the cell right after a walk, so a place taken once at the warmup start drifts.
        /// </summary>
        public void Follow()
        {
            if (pawn == null || !pawn.Spawned)
            {
                return;
            }
            Vector3 c = pawn.DrawPos - BladeMotionPlayer.BodyOffset(pawn);
            at = new Vector3(c.x, 0f, c.z) + offset;
            groundZ = c.z;
        }

        public override void Sample(int layerIndex, float te, float radialOffset, out Vector3 centre, out Vector3 side)
        {
            centre = at;
            side = Vector3.right;
        }

        public override void BeforeDraw() => Follow();

        public override void DrawExtra(float t, float dt)
        {
            if (!played)
            {
                played = true;
                if (flash.sound != null && pawn?.Map != null)
                {
                    flash.sound.PlayOneShot(new TargetInfo(pawn.Position, pawn.Map));
                }
            }
            Material mat = MapComponent_SakuraDanceVisuals.Material(flash.texPath, "MX_QH_SakuraTrailAdd");
            if (mat == null)
            {
                return;
            }
            Vector3 c = MapComponent_SakuraDanceVisuals.ToMap(this, at, projection.heightToScreen);
            MapComponent_SakuraDanceVisuals.ClearBuffers();
            Color col = flash.color;
            // Halo: one soft quad shrinking and fading.
            if (t < flash.haloSeconds)
            {
                float k = t / flash.haloSeconds;
                float size = Mathf.Lerp(flash.haloSize, flash.haloEndSize, k);
                Color hc = new(col.r, col.g, col.b, Mathf.Clamp01((1f - k) * flash.haloBrightness / 4f));
                MapComponent_SakuraDanceVisuals.AddQuad(c, size, 0f, Vector2.zero, Vector2.one, hc);
            }
            // Horizontal bar: long and thin, thinning to nothing.
            if (t < flash.barSeconds)
            {
                float k = t / flash.barSeconds;
                float width = flash.barWidth * (1f - k);
                float alpha = Mathf.Clamp01((k < 0.33f ? 1f : 1f - (k - 0.33f) / 0.67f) * flash.barBrightness / 4f);
                AddBar(c, flash.barLength, width, 0f, new Color(col.r, col.g, col.b, alpha));
            }
            // Needles: two crossed streaks for the first frames.
            if (t < flash.needleSeconds)
            {
                float k = t / flash.needleSeconds;
                float len = flash.needleLength * Mathf.Lerp(1f, 0.5f, k);
                Color nc = new(1f, 1f, 1f, Mathf.Clamp01((1f - k) * flash.needleBrightness / 4f));
                AddBar(c, len, flash.needleWidth, flash.needleAngle, nc);
                AddBar(c, len, flash.needleWidth, -flash.needleAngle, nc);
            }
            if (MapComponent_SakuraDanceVisuals.BuffersEmpty)
            {
                return;
            }
            mesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
            MapComponent_SakuraDanceVisuals.Upload(mesh);
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetFloat(MapComponent_SakuraDanceVisuals.BrightnessId, 4f);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AlphaScaleId, 1f);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AdditiveId, 1f);
            if (bloom != null)
            {
                ScreenBloom.Submit(bloom, mesh, Matrix4x4.identity, mat, block);
            }
            Graphics.DrawMesh(mesh, Matrix4x4.identity, mat, 0, null, 0, block);
            block.Clear();
        }

        /// <summary>A map-space rectangle through <paramref name="c"/>, the texture's soft round spot stretched along it.</summary>
        private static void AddBar(Vector3 c, float length, float width, float angleDeg, Color col)
        {
            float r = angleDeg * Mathf.Deg2Rad;
            Vector3 d = new(Mathf.Cos(r) * length * 0.5f, 0f, Mathf.Sin(r) * length * 0.5f);
            Vector3 s = new(-Mathf.Sin(r) * width * 0.5f, 0f, Mathf.Cos(r) * width * 0.5f);
            MapComponent_SakuraDanceVisuals.AddStripPair(c - d - s, c - d + s, 0f, 0f, 1f, col, false);
            MapComponent_SakuraDanceVisuals.AddStripPair(c + d - s, c + d + s, 1f, 0f, 1f, col, true);
        }

        public override void Dispose()
        {
            base.Dispose();
            if (mesh != null)
            {
                Object.Destroy(mesh);
            }
        }
    }
}

using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Common.Vfx.BladeBody;
using MiliraXian.Characters.Common.Vfx.BladeMotion;
using MiliraXian.Characters.QingHe.Abilities.SakuraDance;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace MiliraXian.Characters.QingHe.Things.Weapons
{
    /// <summary>
    /// One tier of Qinghe's sword impact effect (block, parry, counter, posture break, execution),
    /// drawn with a soft glow texture: a flash with optional needles, sparks thrown towards the attacker that
    /// slow down and fall, an expanding light ring and a grey dust ring. Map units and seconds;
    /// alphas above 1 add more light (additive parts).
    /// </summary>
    public class QingheImpactVfxProps
    {
        /// <summary>A straight cut line through the point along the spark direction (a slash mark).</summary>
        public float slashLength;
        public float slashWidth = 0.08f;
        public float slashSeconds = 0.3f;
        public float slashAlpha = 3f;
        public Color slashColor = new(1f, 0.92f, 0.85f, 1f);
        /// <summary>Degrees the slash line turns from the spark direction.</summary>
        public float slashAngle;

        public float flashSize;
        public float flashSeconds = 0.15f;
        public float flashAlpha = 2f;
        public Color flashColor = new(1f, 0.75f, 0.45f, 1f);

        public float needleLength;
        public float needleWidth = 0.035f;
        public float needleSeconds = 0.07f;
        public float needleAlpha = 3f;
        /// <summary>Degrees of each needle from the line across the attack.</summary>
        public float needleAngle = 30f;

        public int sparks;
        public FloatRange sparkSpeed = new(2f, 4.5f);
        public FloatRange sparkLife = new(0.15f, 0.35f);
        /// <summary>Cone the sparks fly in, degrees, centred on the direction towards the attacker.</summary>
        public float sparkSpread = 135f;
        /// <summary>Speed loss per second (exponential).</summary>
        public float sparkDrag = 3f;
        /// <summary>Downward pull on the screen, map units per second squared.</summary>
        public float sparkGravity = 5f;
        /// <summary>Seconds of flight each streak spans.</summary>
        public float sparkStreak = 0.035f;
        public float sparkWidth = 0.025f;
        public float sparkAlpha = 3f;
        public Color sparkHot = new(1f, 0.95f, 0.8f, 1f);
        public Color sparkCool = new(1f, 0.45f, 0.12f, 1f);

        public float ringStart;
        public float ringEnd;
        public float ringWidth = 0.1f;
        public float ringSeconds = 0.25f;
        public float ringAlpha = 1.5f;
        public Color ringColor = Color.white;
        /// <summary>Screen height of the ring against its width (1 = a circle).</summary>
        public float ringSquash = 1f;

        public float dustStart;
        public float dustEnd;
        public float dustWidth = 0.25f;
        public float dustSeconds = 0.6f;
        public float dustAlpha = 0.4f;
        public Color dustColor = new(0.75f, 0.75f, 0.75f, 1f);
        public float dustSquash = 0.6f;

        /// <summary>
        /// Seconds the flash, needles, slash, sparks, rings and petals wait after the effect is added:
        /// the cuts show first and burst open with them. A caller that times a blow to the burst adds
        /// the effect this much earlier without its sound and plays the sound at the burst.
        /// </summary>
        public float burstDelay;
        /// <summary>
        /// Crossing cut lines through the point (the marks of several cuts hidden in one). Each draws
        /// itself thin along its length over <see cref="cutGrowSeconds"/> after its delay, waits for the
        /// burst (<see cref="burstDelay"/>), flares there and thins and fades over <see cref="cutFadeSeconds"/>.
        /// </summary>
        public List<QingheImpactCut> cuts;
        public float cutGrowSeconds = 0.08f;
        /// <summary>Width and alpha share of a cut while it waits for the burst.</summary>
        public float cutWaitShare = 0.35f;
        public float cutFadeSeconds = 0.3f;
        public float cutWidth = 0.08f;
        public float cutAlpha = 3f;
        public Color cutColor = new(1f, 0.92f, 0.85f, 1f);
        /// <summary>The thin bright line inside each cut.</summary>
        public Color cutCoreColor = Color.white;
        /// <summary>At the burst each cut stretches to this many times its length over <see cref="cutStretchSeconds"/>.</summary>
        public float cutBurstStretch = 1f;
        public float cutStretchSeconds = 0.08f;
        /// <summary>Glow of the cuts (their own pass); null = the sword's impact bloom.</summary>
        public ScreenBloomProperties cutBloom;
        /// <summary>
        /// A refraction band along each cut from the burst on, as the sword trail's distortion band:
        /// <see cref="cutDistortWidth"/> wide, its strength falling over <see cref="cutDistortSeconds"/>.
        /// </summary>
        public float cutDistortWidth;
        public float cutDistortSeconds = 0.5f;
        public float cutDistortIntensity = 5f;
        public float cutDistortMetresPerUnit = 0.04f;
        public string cutDistortTexPath = "MiliraXianQinghe/Effect/SakuraDance/SakuraBand";
        public string cutDistortNormalTexPath = "MiliraXianQinghe/Effect/SakuraDance/SakuraNormal";

        /// <summary>
        /// Sakura petals thrown out with the burst (alpha blended, from the Sakura Dance 8 x 8 atlas):
        /// they slow down quickly, then drift down the screen swaying and flutter through the atlas.
        /// </summary>
        public int petals;
        public FloatRange petalSpeed = new(1.5f, 4f);
        public FloatRange petalLife = new(0.8f, 1.6f);
        /// <summary>Cone the petals fly in, degrees, centred on the impact direction.</summary>
        public float petalSpread = 360f;
        public float petalDrag = 4f;
        /// <summary>Speed down the screen they settle to, map units per second.</summary>
        public float petalFall = 0.35f;
        public float petalSway = 0.12f;
        public FloatRange petalSize = new(0.22f, 0.34f);
        public string petalTexPath = "MiliraXianQinghe/Effect/SakuraDance/SakuraPetals";
        public Color petalColor = new(1f, 0.8f, 0.86f, 1f);
        public float petalBrightness = 1.25f;

        public SoundDef sound;

        internal QingheImpactVfxProps WithCut(float length, float delay = 0f, float width = 1f)
        {
            QingheImpactVfxProps copy = (QingheImpactVfxProps)MemberwiseClone();
            copy.cuts = new List<QingheImpactCut> { new() { angle = 0f, length = length, delay = delay, width = width } };
            return copy;
        }

        public float LifeSeconds
        {
            get
            {
                float life = burstDelay + Mathf.Max(Mathf.Max(Mathf.Max(flashSeconds, needleSeconds), slashLength > 0f ? slashSeconds : 0f),
                    Mathf.Max(sparks > 0 ? sparkLife.max + sparkStreak : 0f, Mathf.Max(ringEnd > 0f ? ringSeconds : 0f, dustEnd > 0f ? dustSeconds : 0f)));
                if (petals > 0)
                {
                    life = Mathf.Max(life, burstDelay + petalLife.max);
                }
                if (!cuts.NullOrEmpty())
                {
                    foreach (QingheImpactCut cut in cuts)
                    {
                        life = Mathf.Max(life, Mathf.Max(cut.delay + cutGrowSeconds, burstDelay) + cutFadeSeconds);
                    }
                    if (cutDistortWidth > 0f)
                    {
                        life = Mathf.Max(life, burstDelay + cutDistortSeconds);
                    }
                }
                return life;
            }
        }
    }

    /// <summary>One line of <see cref="QingheImpactVfxProps.cuts"/>.</summary>
    public class QingheImpactCut
    {
        /// <summary>Degrees from the impact direction.</summary>
        public float angle;
        public float length = 1.8f;
        /// <summary>Map units the line is moved sideways off the point.</summary>
        public float offset;
        /// <summary>Seconds after the impact the line starts to draw.</summary>
        public float delay;
        /// <summary>Width against the props' cutWidth.</summary>
        public float width = 1f;
    }

    internal static class QingheImpactVfx
    {
        /// <summary>
        /// Map draw position of the point on Qinghe's drawn blade, <paramref name="share"/> of its length from
        /// the grip; without a drawn blade a point between her and <paramref name="toward"/>.
        /// </summary>
        internal static Vector3 BladeContact(Pawn pawn, Thing toward, float share)
        {
            Vector3 fallback = toward != null && toward.Spawned && toward.Map == pawn.Map
                ? Vector3.Lerp(pawn.DrawPos, toward.DrawPos, 0.35f)
                : pawn.DrawPos;
            if (!BladeMotionPlayer.TryGetBladePose(pawn, out BladePose pose))
            {
                return fallback;
            }
            float length = BladeMotionPlayer.DrawnBlade(pawn)?.length ?? 1.1f;
            Vector3 offset = pose.grip + pose.dir.normalized * (length * share);
            return BladeRenderer.BodyOffsetToMap(pawn.DrawPos, offset, pose);
        }

        /// <summary>Plays the tier's sound and adds its effect at map position <paramref name="at"/>.</summary>
        /// <param name="toward">Map direction the sparks fly in (towards the attacker).</param>
        internal static void Spawn(Map map, QingheImpactVfxProps props, Vector3 at, Vector3 toward, IntVec3 soundCell, bool sound = true)
        {
            if (props == null || map == null)
            {
                return;
            }
            try
            {
                if (sound)
                {
                    props.sound?.PlayOneShot(new TargetInfo(soundCell, map));
                }
                map.GetComponent<MapComponent_QingheImpactVfx>()?.Add(props, at, toward);
            }
            catch (System.Exception ex)
            {
                Log.ErrorOnce("[MiliraXian] Qinghe impact effect failed: " + ex, 0x51c7a4);
            }
        }
    }

    /// <summary>
    /// Draws the impact effects of the map. Visual only and not saved: each lives well under a second.
    /// Time is the interpolated game clock (freezes when paused, follows the game speed). Every impact is
    /// rebuilt each frame into one additive mesh (with bloom) and one alpha mesh (the dust).
    /// </summary>
    public sealed class MapComponent_QingheImpactVfx : MapComponent
    {
        private const int MaxImpacts = 32;
        private const int RingSegments = 28;
        /// <summary>Vertex alpha of the additive mesh is stored divided by this (alphas up to 4).</summary>
        private const float AlphaScale = 4f;

        private sealed class Impact
        {
            public QingheImpactVfxProps props;
            public Vector3 at;
            public float angle;
            public float startTicks;
            public int seed;
        }

        private readonly List<Impact> impacts = new();
        private const int PetalAtlasColumns = 8;
        private const int PetalAtlasFrames = 64;

        private Mesh additiveMesh;
        private Mesh alphaMesh;
        private Mesh petalMesh;
        private Mesh cutMesh;
        private Mesh distortMesh;

        private static readonly int NormalTexId = Shader.PropertyToID("_NormalTex");
        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int ShadeId = Shader.PropertyToID("_Shade");
        private static readonly int DistortScaleId = Shader.PropertyToID("_DistortScale");
        private const int DistortSegments = 12;
        private bool warned;

        public MapComponent_QingheImpactVfx(Map map) : base(map)
        {
        }

        internal void Add(QingheImpactVfxProps props, Vector3 at, Vector3 toward)
        {
            if (impacts.Count >= MaxImpacts)
            {
                impacts.RemoveAt(0);
            }
            int now = Find.TickManager.TicksGame;
            impacts.Add(new Impact
            {
                props = props,
                at = at,
                angle = toward.sqrMagnitude > 1e-6f ? Mathf.Atan2(toward.z, toward.x) * Mathf.Rad2Deg : 90f,
                startTicks = now,
                seed = Gen.HashCombineInt(now, impacts.Count * 7919 + Mathf.RoundToInt(at.x * 131f + at.z * 977f)),
            });
        }

        public override void MapComponentDraw()
        {
            if (impacts.Count == 0 || Find.CurrentMap != map)
            {
                return;
            }
            float ticks = VisualClock.Ticks;
            for (int i = impacts.Count - 1; i >= 0; i--)
            {
                if ((ticks - impacts[i].startTicks) / 60f > impacts[i].props.LifeSeconds)
                {
                    impacts.RemoveAt(i);
                }
            }
            if (impacts.Count == 0)
            {
                return;
            }
            try
            {
                QingheSwordGuardDef c = QingheSwordGuard.Config;
                float altitude = AltitudeLayer.MoteOverhead.AltitudeFor();

                MapComponent_SakuraDanceVisuals.ClearBuffers();
                foreach (Impact impact in impacts)
                {
                    float t = (ticks - impact.startTicks) / 60f;
                    if (t >= 0f)
                    {
                        BuildDust(impact, t, altitude);
                    }
                }
                if (!MapComponent_SakuraDanceVisuals.BuffersEmpty)
                {
                    Material mat = MapComponent_SakuraDanceVisuals.Material(c.vfxTexPath, "MX_QH_SakuraTrailAlpha");
                    alphaMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
                    MapComponent_SakuraDanceVisuals.Upload(alphaMesh);
                    Draw(alphaMesh, mat, 1f, additive: false, null);
                }

                // Petals: alpha blended like the Sakura Dance petals, one mesh for every impact (they
                // share the props' texture; the execution is the only impact with petals).
                MapComponent_SakuraDanceVisuals.ClearBuffers();
                QingheImpactVfxProps petalProps = null;
                foreach (Impact impact in impacts)
                {
                    float t = (ticks - impact.startTicks) / 60f - impact.props.burstDelay;
                    if (t >= 0f && impact.props.petals > 0)
                    {
                        petalProps ??= impact.props;
                        BuildPetals(impact, t, altitude + 0.005f);
                    }
                }
                if (petalProps != null && !MapComponent_SakuraDanceVisuals.BuffersEmpty)
                {
                    Material mat = MapComponent_SakuraDanceVisuals.Material(petalProps.petalTexPath, "MX_QH_SakuraPetal");
                    if (mat != null)
                    {
                        petalMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
                        MapComponent_SakuraDanceVisuals.Upload(petalMesh);
                        MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
                        block.Clear();
                        block.SetFloat(MapComponent_SakuraDanceVisuals.BrightnessId, petalProps.petalBrightness);
                        Graphics.DrawMesh(petalMesh, Matrix4x4.identity, mat, 0, null, 0, block);
                        block.Clear();
                    }
                }

                // Refraction along the cuts: its shader grabs the scene at its own (earlier) queue,
                // so it never covers the light drawn below.
                MapComponent_SakuraDanceVisuals.ClearBuffers();
                QingheImpactVfxProps distortProps = null;
                foreach (Impact impact in impacts)
                {
                    float t = (ticks - impact.startTicks) / 60f;
                    if (t >= 0f && impact.props.cutDistortWidth > 0f && !impact.props.cuts.NullOrEmpty())
                    {
                        distortProps ??= impact.props;
                        BuildCutDistortion(impact, t, altitude);
                    }
                }
                if (distortProps != null && !MapComponent_SakuraDanceVisuals.BuffersEmpty)
                {
                    DrawDistortion(distortProps);
                }

                // The cuts, with their own glow.
                MapComponent_SakuraDanceVisuals.ClearBuffers();
                ScreenBloomProperties cutBloom = null;
                foreach (Impact impact in impacts)
                {
                    float t = (ticks - impact.startTicks) / 60f;
                    if (t >= 0f && !impact.props.cuts.NullOrEmpty())
                    {
                        cutBloom ??= impact.props.cutBloom ?? c.bloom;
                        BuildCuts(impact, t, new Vector3(impact.at.x, altitude + 0.012f, impact.at.z));
                    }
                }
                if (!MapComponent_SakuraDanceVisuals.BuffersEmpty)
                {
                    Material mat = MapComponent_SakuraDanceVisuals.Material(c.vfxTexPath, "MX_QH_SakuraTrailAdd");
                    cutMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
                    MapComponent_SakuraDanceVisuals.Upload(cutMesh);
                    Draw(cutMesh, mat, AlphaScale, additive: true, cutBloom);
                }

                MapComponent_SakuraDanceVisuals.ClearBuffers();
                foreach (Impact impact in impacts)
                {
                    float t = (ticks - impact.startTicks) / 60f;
                    if (t >= 0f)
                    {
                        BuildLight(impact, t, altitude + 0.01f);
                    }
                }
                if (!MapComponent_SakuraDanceVisuals.BuffersEmpty)
                {
                    Material mat = MapComponent_SakuraDanceVisuals.Material(c.vfxTexPath, "MX_QH_SakuraTrailAdd");
                    additiveMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
                    MapComponent_SakuraDanceVisuals.Upload(additiveMesh);
                    Draw(additiveMesh, mat, AlphaScale, additive: true, c.bloom);
                }
            }
            catch (System.Exception ex)
            {
                if (!warned)
                {
                    warned = true;
                    Log.Error("[MiliraXian] Qinghe impact visuals failed: " + ex);
                }
                impacts.Clear();
            }
        }

        private static void Draw(Mesh mesh, Material mat, float alphaScale, bool additive, ScreenBloomProperties bloom)
        {
            if (mat == null)
            {
                return;
            }
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            block.SetFloat(MapComponent_SakuraDanceVisuals.BrightnessId, 1f);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AlphaScaleId, alphaScale);
            block.SetFloat(MapComponent_SakuraDanceVisuals.AdditiveId, additive ? 1f : 0f);
            if (bloom != null)
            {
                ScreenBloom.Submit(bloom, mesh, Matrix4x4.identity, mat, block);
            }
            Graphics.DrawMesh(mesh, Matrix4x4.identity, mat, 0, null, 0, block);
            block.Clear();
        }

        private void DrawDistortion(QingheImpactVfxProps p)
        {
            Material mat = MapComponent_SakuraDanceVisuals.Material(p.cutDistortTexPath, "MX_QH_SakuraDistort");
            Camera cam = Find.Camera;
            if (mat == null || cam == null || cam.orthographicSize <= 0f)
            {
                return;
            }
            float h = 2f * cam.orthographicSize;
            Vector4 scale = new(1f / (h * Mathf.Max(0.1f, cam.aspect)), 1f / h, 0f, 0f);
            distortMesh ??= MapComponent_SakuraDanceVisuals.NewMesh();
            MapComponent_SakuraDanceVisuals.Upload(distortMesh);
            MaterialPropertyBlock block = MX_RenderStatics.SharedPropertyBlock;
            block.Clear();
            Texture2D normal = ContentFinder<Texture2D>.Get(p.cutDistortNormalTexPath, reportFailure: false);
            if (normal != null)
            {
                block.SetTexture(NormalTexId, normal);
            }
            // The strength falls with each band's vertex alpha (see BuildCutDistortion).
            block.SetFloat(IntensityId, p.cutDistortIntensity * p.cutDistortMetresPerUnit);
            block.SetFloat(ShadeId, 0f);
            block.SetVector(DistortScaleId, scale);
            Graphics.DrawMesh(distortMesh, Matrix4x4.identity, mat, 0, null, 0, block);
            block.Clear();
        }

        private static Color Light(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha / AlphaScale));
        }

        private static void BuildLight(Impact impact, float t, float altitude)
        {
            QingheImpactVfxProps p = impact.props;
            Vector3 c = new(impact.at.x, altitude, impact.at.z);

            t -= p.burstDelay;
            if (t < 0f)
            {
                return;
            }

            if (p.flashSize > 0f && t < p.flashSeconds)
            {
                float k = t / p.flashSeconds;
                float size = p.flashSize * Mathf.Lerp(0.6f, 1.1f, Mathf.Sqrt(k));
                MapComponent_SakuraDanceVisuals.AddQuad(c, size, 0f, Vector2.zero, Vector2.one,
                    Light(p.flashColor, p.flashAlpha * (1f - k) * (1f - k)));
            }

            if (p.slashLength > 0f && t < p.slashSeconds)
            {
                // Full length at once, then thins to a hair and fades: the mark of a cut already made.
                float k = t / p.slashSeconds;
                float width = p.slashWidth * (1f - k) * (1f - 0.5f * k);
                Bar(c, impact.angle + p.slashAngle, p.slashLength * Mathf.Lerp(1f, 1.08f, k), width,
                    Light(p.slashColor, p.slashAlpha * (k < 0.25f ? 1f : 1f - (k - 0.25f) / 0.75f)));
                Bar(c, impact.angle + p.slashAngle, p.slashLength * 0.9f, width * 0.3f, Light(Color.white, p.slashAlpha * (1f - k)));
            }

            if (p.needleLength > 0f && t < p.needleSeconds)
            {
                float k = t / p.needleSeconds;
                float len = p.needleLength * Mathf.Lerp(1f, 0.6f, k);
                Color col = Light(Color.white, p.needleAlpha * (1f - k));
                float across = impact.angle + 90f;
                Bar(c, across + p.needleAngle, len, p.needleWidth, col);
                Bar(c, across - p.needleAngle, len * 0.7f, p.needleWidth, col);
            }

            if (p.ringEnd > 0f && t < p.ringSeconds)
            {
                float k = t / p.ringSeconds;
                float ease = 1f - (1f - k) * (1f - k);
                Ring(c, Mathf.Lerp(p.ringStart, p.ringEnd, ease), p.ringWidth * (1f - 0.6f * k), p.ringSquash,
                    Light(p.ringColor, p.ringAlpha * (1f - k)));
            }

            for (int i = 0; i < p.sparks; i++)
            {
                Rand.PushState(Gen.HashCombineInt(impact.seed, i));
                float angle = (impact.angle + Rand.Range(-0.5f, 0.5f) * p.sparkSpread) * Mathf.Deg2Rad;
                float speed = p.sparkSpeed.RandomInRange;
                float life = p.sparkLife.RandomInRange;
                float heat = Rand.Value;
                Rand.PopState();
                if (t > life)
                {
                    continue;
                }
                float k = t / life;
                Vector3 dir = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 head = c + SparkOffset(p, dir, speed, t);
                Vector3 tail = c + SparkOffset(p, dir, speed, Mathf.Max(0f, t - p.sparkStreak));
                Color col = Color.Lerp(p.sparkHot, p.sparkCool, Mathf.Clamp01(k * 1.3f + heat * 0.3f));
                Streak(tail, head, p.sparkWidth * (1f - 0.5f * k), Light(col, p.sparkAlpha * (1f - k)));
            }
        }

        private static void BuildDust(Impact impact, float t, float altitude)
        {
            QingheImpactVfxProps p = impact.props;
            t -= p.burstDelay;
            if (p.dustEnd <= 0f || t < 0f || t >= p.dustSeconds)
            {
                return;
            }
            float k = t / p.dustSeconds;
            float ease = 1f - (1f - k) * (1f - k);
            Vector3 c = new(impact.at.x, altitude, impact.at.z);
            Color col = p.dustColor;
            col.a = Mathf.Clamp01(p.dustAlpha * (k < 0.1f ? k / 0.1f : 1f - (k - 0.1f) / 0.9f));
            Ring(c, Mathf.Lerp(p.dustStart, p.dustEnd, ease), p.dustWidth * Mathf.Lerp(0.6f, 1f, ease), p.dustSquash, col);
        }

        /// <summary>
        /// The crossing cuts, <paramref name="t"/> seconds after the effect was added: each draws itself
        /// thin from one end, waits faint, and at the burst flares wide and thins away.
        /// </summary>
        private static void BuildCuts(Impact impact, float t, Vector3 c)
        {
            QingheImpactVfxProps p = impact.props;
            if (p.cuts.NullOrEmpty())
            {
                return;
            }
            foreach (QingheImpactCut cut in p.cuts)
            {
                float age = t - cut.delay;
                if (age < 0f)
                {
                    continue;
                }
                float grow = p.cutGrowSeconds > 1e-4f ? Mathf.Clamp01(age / p.cutGrowSeconds) : 1f;
                grow = 1f - (1f - grow) * (1f - grow);
                float width = p.cutWidth * cut.width;
                float alpha;
                float length = cut.length;
                float burst = t - p.burstDelay;
                if (burst < 0f)
                {
                    width *= p.cutWaitShare;
                    alpha = p.cutAlpha * p.cutWaitShare;
                }
                else
                {
                    float k = p.cutFadeSeconds > 1e-4f ? burst / p.cutFadeSeconds : 1f;
                    if (k >= 1f)
                    {
                        continue;
                    }
                    // Wide at once, then down to a hair: the wound opening.
                    width *= Mathf.Lerp(2.2f, 0.1f, Mathf.Sqrt(k));
                    alpha = p.cutAlpha * (1f - k);
                    length *= CutStretch(p, burst);
                }
                CutLine(impact, cut, c, length, grow, out Vector3 a, out Vector3 b, out Vector3 d);
                Streak(a, b, width, Light(p.cutColor, alpha));
                Streak(a + d * ((b - a).magnitude * 0.05f), b - d * ((b - a).magnitude * 0.05f), width * 0.3f, Light(p.cutCoreColor, alpha));
            }
        }

        /// <summary>Length factor of a cut <paramref name="burst"/> seconds after the burst: out fast, then a slow creep.</summary>
        private static float CutStretch(QingheImpactVfxProps p, float burst)
        {
            float s = p.cutStretchSeconds > 1e-4f ? Mathf.Clamp01(burst / p.cutStretchSeconds) : 1f;
            s = 1f - (1f - s) * (1f - s) * (1f - s);
            float creep = 1f + 0.05f * Mathf.Clamp01(p.cutFadeSeconds > 1e-4f ? burst / p.cutFadeSeconds : 1f);
            return Mathf.Lerp(1f, Mathf.Max(1f, p.cutBurstStretch), s) * creep;
        }

        /// <summary>
        /// Ends of one cut <paramref name="length"/> long, centred on its point (it stretches both
        /// ways), drawn to <paramref name="grow"/> of its length from its first end.
        /// </summary>
        private static void CutLine(Impact impact, QingheImpactCut cut, Vector3 c, float length, float grow,
            out Vector3 a, out Vector3 b, out Vector3 d)
        {
            float r = (impact.angle + cut.angle) * Mathf.Deg2Rad;
            d = new Vector3(Mathf.Cos(r), 0f, Mathf.Sin(r));
            Vector3 side = new Vector3(-d.z, 0f, d.x) * cut.offset;
            a = c + side - d * (length * 0.5f);
            b = a + d * (length * grow);
        }

        /// <summary>
        /// The refraction band along each cut from the burst on, its strength (vertex alpha) falling
        /// over <see cref="QingheImpactVfxProps.cutDistortSeconds"/>; it fades out along both ends.
        /// </summary>
        private static void BuildCutDistortion(Impact impact, float t, float altitude)
        {
            QingheImpactVfxProps p = impact.props;
            float burst = t - p.burstDelay;
            if (burst < 0f || burst >= p.cutDistortSeconds)
            {
                return;
            }
            float k = burst / Mathf.Max(1e-4f, p.cutDistortSeconds);
            float strength = (1f - k) * (1f - k);
            Vector3 c = new(impact.at.x, altitude, impact.at.z);
            foreach (QingheImpactCut cut in p.cuts)
            {
                CutLine(impact, cut, c, cut.length * CutStretch(p, burst), 1f, out Vector3 a, out Vector3 b, out Vector3 d);
                Vector3 side = new Vector3(-d.z, 0f, d.x) * (p.cutDistortWidth * cut.width * 0.5f);
                for (int i = 0; i <= DistortSegments; i++)
                {
                    float x = i / (float)DistortSegments;
                    float edge = Mathf.Sin(x * Mathf.PI);
                    Vector3 m = Vector3.Lerp(a, b, x);
                    // Vertex red is the shadow colour (white: no shadow); alpha the strength.
                    Color col = new(1f, 1f, 1f, Mathf.Clamp01(strength * edge));
                    MapComponent_SakuraDanceVisuals.AddStripPair(m - side, m + side, x * 0.5f, 0f, 1f, col, i > 0);
                }
            }
        }

        /// <summary>
        /// The petals, <paramref name="t"/> seconds after the burst: thrown out and quickly slowed, then
        /// drifting down the screen with a sway, turning and fluttering through the atlas, fading at the end.
        /// </summary>
        private static void BuildPetals(Impact impact, float t, float altitude)
        {
            QingheImpactVfxProps p = impact.props;
            float cell = 1f / PetalAtlasColumns;
            for (int i = 0; i < p.petals; i++)
            {
                Rand.PushState(Gen.HashCombineInt(impact.seed, 0x5ea7 + i));
                float angle = (impact.angle + Rand.Range(-0.5f, 0.5f) * p.petalSpread) * Mathf.Deg2Rad;
                float speed = p.petalSpeed.RandomInRange;
                float life = p.petalLife.RandomInRange;
                float size = p.petalSize.RandomInRange;
                float frame0 = Rand.Range(0f, PetalAtlasFrames);
                float phase = Rand.Range(0f, Mathf.PI * 2f);
                float spin = Rand.Range(-180f, 180f);
                float spin0 = Rand.Range(0f, 360f);
                Rand.PopState();
                if (t > life)
                {
                    continue;
                }
                float x = t / life;
                Vector3 dir = new(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                float distance = p.petalDrag > 1e-4f ? speed * (1f - Mathf.Exp(-p.petalDrag * t)) / p.petalDrag : speed * t;
                Vector3 pos = new Vector3(impact.at.x, altitude, impact.at.z) + dir * distance;
                // The fall and sway set in as the throw dies down.
                float settle = Mathf.Clamp01(t * p.petalDrag * 0.5f);
                pos.z -= p.petalFall * t * settle;
                pos.x += p.petalSway * Mathf.Sin(t * 5f + phase) * settle;
                int frame = Mathf.FloorToInt(frame0 + (PetalAtlasFrames - 1) * x);
                frame = ((frame % PetalAtlasFrames) + PetalAtlasFrames) % PetalAtlasFrames;
                float u0 = frame % PetalAtlasColumns * cell;
                // Row 0 of the atlas is the top of the image; UV v grows upward.
                float v1 = 1f - frame / PetalAtlasColumns * cell;
                float fade = x < 0.05f ? x / 0.05f : x < 0.7f ? 1f : 1f - (x - 0.7f) / 0.3f;
                Color col = new(p.petalColor.r, p.petalColor.g, p.petalColor.b, p.petalColor.a * fade);
                MapComponent_SakuraDanceVisuals.AddQuad(pos, size * Mathf.Lerp(1.3f, 1f, Mathf.Min(1f, x * 3f)), spin0 + spin * t,
                    new Vector2(u0, v1 - cell), new Vector2(u0 + cell, v1), col);
            }
        }

        /// <summary>Map offset of a spark after <paramref name="t"/> seconds: thrown out, slowed by drag, pulled down the screen.</summary>
        private static Vector3 SparkOffset(QingheImpactVfxProps p, Vector3 dir, float speed, float t)
        {
            float distance = p.sparkDrag > 1e-4f ? speed * (1f - Mathf.Exp(-p.sparkDrag * t)) / p.sparkDrag : speed * t;
            Vector3 v = dir * distance;
            v.z -= 0.5f * p.sparkGravity * t * t;
            return v;
        }

        /// <summary>A soft line from <paramref name="a"/> to <paramref name="b"/> (the glow's round spot stretched along it).</summary>
        private static void Streak(Vector3 a, Vector3 b, float width, Color col)
        {
            Vector3 d = b - a;
            d.y = 0f;
            float len = d.magnitude;
            if (len < 1e-4f)
            {
                return;
            }
            // Extend the ends by half the width so the soft caps do not shorten a short streak.
            d /= len;
            Vector3 s = new Vector3(-d.z, 0f, d.x) * (width * 0.5f);
            Vector3 a2 = a - d * (width * 0.5f);
            Vector3 b2 = b + d * (width * 0.5f);
            MapComponent_SakuraDanceVisuals.AddStripPair(a2 - s, a2 + s, 0f, 0f, 1f, col, false);
            MapComponent_SakuraDanceVisuals.AddStripPair(b2 - s, b2 + s, 1f, 0f, 1f, col, true);
        }

        private static void Bar(Vector3 c, float angleDeg, float length, float width, Color col)
        {
            float r = angleDeg * Mathf.Deg2Rad;
            Vector3 d = new(Mathf.Cos(r) * length * 0.5f, 0f, Mathf.Sin(r) * length * 0.5f);
            Streak(c - d, c + d, width, col);
        }

        /// <summary>A ring of soft band (the glow's middle column across its width).</summary>
        private static void Ring(Vector3 c, float radius, float width, float squash, Color col)
        {
            if (radius <= 0f || width <= 0f || col.a <= 0.002f)
            {
                return;
            }
            float inner = Mathf.Max(0f, radius - width * 0.5f);
            float outer = radius + width * 0.5f;
            for (int i = 0; i <= RingSegments; i++)
            {
                float a = i * Mathf.PI * 2f / RingSegments;
                Vector3 u = new(Mathf.Cos(a), 0f, Mathf.Sin(a) * squash);
                MapComponent_SakuraDanceVisuals.AddStripPair(c + u * inner, c + u * outer, 0.5f, 0f, 1f, col, i > 0);
            }
        }

        public override void MapRemoved()
        {
            base.MapRemoved();
            impacts.Clear();
            if (additiveMesh != null)
            {
                Object.Destroy(additiveMesh);
            }
            if (alphaMesh != null)
            {
                Object.Destroy(alphaMesh);
            }
            if (petalMesh != null)
            {
                Object.Destroy(petalMesh);
            }
            if (cutMesh != null)
            {
                Object.Destroy(cutMesh);
            }
            if (distortMesh != null)
            {
                Object.Destroy(distortMesh);
            }
        }
    }
}

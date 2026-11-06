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
    /// modelled on Sekiro's guard and deflect FFX (201001 / 252001 / 253001 / 261001) and drawn with
    /// a soft glow texture: a flash with optional needles, sparks thrown towards the attacker that
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

        public SoundDef sound;

        public float LifeSeconds => Mathf.Max(Mathf.Max(Mathf.Max(flashSeconds, needleSeconds), slashLength > 0f ? slashSeconds : 0f),
            Mathf.Max(sparks > 0 ? sparkLife.max + sparkStreak : 0f, Mathf.Max(ringSeconds, dustSeconds)));
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
            float length = pawn.equipment?.Primary?.def.GetModExtension<BladeGraphicExtension>()?.length ?? 1.1f;
            Vector3 offset = pose.grip + pose.dir.normalized * (length * share);
            return BladeRenderer.BodyOffsetToMap(pawn.DrawPos, offset, pose);
        }

        /// <summary>Plays the tier's sound and adds its effect at map position <paramref name="at"/>.</summary>
        /// <param name="toward">Map direction the sparks fly in (towards the attacker).</param>
        internal static void Spawn(Map map, QingheImpactVfxProps props, Vector3 at, Vector3 toward, IntVec3 soundCell)
        {
            if (props == null || map == null)
            {
                return;
            }
            try
            {
                props.sound?.PlayOneShot(new TargetInfo(soundCell, map));
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
        private Mesh additiveMesh;
        private Mesh alphaMesh;
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

        private static Color Light(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha / AlphaScale));
        }

        private static void BuildLight(Impact impact, float t, float altitude)
        {
            QingheImpactVfxProps p = impact.props;
            Vector3 c = new(impact.at.x, altitude, impact.at.z);

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
            if (p.dustEnd <= 0f || t >= p.dustSeconds)
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
        }
    }
}

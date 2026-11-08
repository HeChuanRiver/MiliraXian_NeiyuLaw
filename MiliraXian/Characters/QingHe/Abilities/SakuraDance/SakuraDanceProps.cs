using System.Collections.Generic;
using MiliraXian.Characters.Common.SweepDamage;
using MiliraXian.Characters.Common.Vfx;
using MiliraXian.Characters.Common.Vfx.BladeBody;
using MiliraXian.Characters.Common.Vfx.BladeMotion;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.QingHe.Abilities.SakuraDance
{
    /// <summary>
    /// One tracer layer of a cut's ring (a strip at <see cref="radialOffset"/> from the ring radius).
    /// Curves are evaluated at the cut's local time in seconds; a missing curve uses the constant.
    /// </summary>
    public class SakuraTrailLayer
    {
        public string texPath;
        /// <summary>Additive (bright strands) or normal alpha blend (grey veil).</summary>
        public bool additive;
        public bool bloom;
        public float radialOffset;
        public float width = 0.45f;
        public SimpleCurve widthCurve;
        /// <summary>Seconds a trail segment lives after it was emitted.</summary>
        public float segmentDuration = 1f;
        /// <summary>Linear fade length at the head end of the visible trail, percent of its length.</summary>
        public float startFade;
        public SimpleCurve startFadeCurve;
        /// <summary>Linear fade length at the tail end, percent.</summary>
        public float endFade;
        public SimpleCurve endFadeCurve;
        public float alpha = 1f;
        public SimpleCurve alphaCurve;
        /// <summary>Brightness; may exceed 1 (feeds the bloom).</summary>
        public float brightness = 1f;
        public SimpleCurve brightnessCurve;
        /// <summary>Texture width used per segment (per whole trail with <see cref="attachedUV"/>).</summary>
        public float textureFraction = 0.1f;
        /// <summary>Seconds between segments; 0 = one per frame (1/60 s).</summary>
        public float segmentInterval;
        public float speedU;
        public SimpleCurve speedUCurve;
        public bool attachedUV;
        public bool flipV;
        /// <summary>Width at the oldest end of the trail relative to the head (1 = even width).</summary>
        public float tailWidth = 1f;
        /// <summary>Which edge stays put when the strip narrows (tailWidth, width noise): 0 = centre, 1 = outer, -1 = inner.</summary>
        public float taperAnchor;
        /// <summary>Opacity at the oldest end relative to the head (1 = even).</summary>
        public float tailAlpha = 1f;
        /// <summary>Extra opacity at the head (the blade tip end): 1 = twice as bright at the very head.</summary>
        public float headBoost;
        /// <summary>Length of the head boost, share of the emitted trail.</summary>
        public float headLength = 0.12f;
        /// <summary>Low-frequency variation of width / opacity along the trail (0 = none, 0.3 = +-30 %).</summary>
        public float widthNoise;
        public float alphaNoise;
        /// <summary>
        /// Head / tail fades measured against the part of the trail still alive, and no per-sample age
        /// fade. Off = fades against the whole emitted trail.
        /// </summary>
        public bool liveFade;
        /// <summary>Clamp the final opacity to 1, also for additive layers.</summary>
        public bool clampAlpha;
        /// <summary>Strip tint, written to the vertex colour (keep channels at or below 1; use brightness to go brighter).</summary>
        public Color color = Color.white;
        /// <summary>Frames stacked downward in the texture (row 0 = top); the whole strip shows one frame at a time.</summary>
        public int frames = 1;
        /// <summary>Frames per second when <see cref="frames"/> is above 1.</summary>
        public float frameRate = 30f;

        // Distortion band (normal-map refraction + faint shadow) instead of a colour strip.
        public bool distortion;
        public float distortionIntensity;
        public SimpleCurve distortionIntensityCurve;
        /// <summary>Normal map for this band; null = the props' <c>distortionNormalTexPath</c>.</summary>
        public string normalTexPath;
        /// <summary>Darkness of the band colour, 0 = white (no shadow), 1 = black.</summary>
        public float shadeDarkness = 1f;

        public static float Eval(SimpleCurve curve, float constant, float t)
        {
            return curve != null && curve.PointsCount > 0 ? curve.Evaluate(t) : constant;
        }

        /// <summary>Integral of the U speed from 0 to <paramref name="t"/>.</summary>
        public float ScrollU(float t)
        {
            if (speedUCurve == null || speedUCurve.PointsCount == 0)
            {
                return speedU * t;
            }
            const int steps = 24;
            float sum = 0f;
            float dt = t / steps;
            for (int i = 0; i < steps; i++)
            {
                sum += speedUCurve.Evaluate((i + 0.5f) * dt) * dt;
            }
            return sum;
        }
    }

    /// <summary>One petal emitter: equal-distance emission along the ring head while the cut spins.</summary>
    public class SakuraPetalEmitter
    {
        public float lifetime = 0.5f;
        /// <summary>Metres of path between two petals.</summary>
        public float spacing = 0.225f;
        public float speed = 15.6f;
        /// <summary>Largest random turn applied every 0.12 s.</summary>
        public float turnDegrees = 22.5f;
    }

    /// <summary>
    /// One cut: a ring in a tilted plane spun once. Angles are in the body frame
    /// (local x = right, y = up, z = forward, rotation = Rx * Rz in degrees).
    /// </summary>
    public class SakuraCutProps
    {
        public float time;
        public Vector3 offset;
        public float tiltX;
        public float tiltZ;
        public float startAngle = 90f;
        public float angularSpeed = 1800f;
        public float spinSeconds = 0.1667f;
        public float radius = 2.5f;
        public float damage = 40f;
        public float armorPenetration = 0.5f;
        public float petalStartAngle = 67.5f;
        public float petalRadius = 2.75f;
        public List<SakuraTrailLayer> layers = new();
        public List<SakuraPetalEmitter> petals = new();
        /// <summary>Extra electric layers on the same ring, drawn only when the blade carries a caught thunder.</summary>
        public List<SakuraTrailLayer> thunderLayers = new();
        /// <summary>The finishing cut of the Lightning Reversal: with the thunder it uses the stunning sweep.</summary>
        public bool thunderRelease;
        /// <summary>Radius and damage factors of this cut when it carries the thunder.</summary>
        public float thunderRadiusFactor = 1f;
        public float thunderDamageFactor = 1f;
        /// <summary>Posture each hit of this cut adds to the enemy, and the extra when it carries the thunder.</summary>
        public float posture;
        public float thunderPosture;
        /// <summary>Lightning shot out of the charged blade while this cut spins.</summary>
        public SakuraDischargeProps thunderDischarge;
        /// <summary>Rising whirlwind spawned with this cut (the take-off of the third cut).</summary>
        public SakuraUpdraftProps updraft;

        public int SpinTicks => Mathf.Max(1, Mathf.RoundToInt(spinSeconds * 60f));
    }

    /// <summary>
    /// Bolts discharged from the charged blade during a cut. Each bolt starts at a random point of
    /// the blade, shoots out between the blade direction and the swing direction, then stays where
    /// it was fired and fades; together they trail the swing.
    /// </summary>
    public class SakuraDischargeProps
    {
        /// <summary>Seconds after the cut starts before the first bolt.</summary>
        public float delay;
        /// <summary>Seconds of emission. Keep it inside the cut's spin: the blade keeps turning after it.</summary>
        public float duration = 0.17f;
        /// <summary>
        /// Emission dies out over <see cref="duration"/>: strength = (1 - progress)^falloff scales the
        /// number, length, width and brightness of the bolts. 0 = constant.
        /// </summary>
        public float falloff = 1.5f;
        public float interval = 0.033f;
        public int perBurst = 2;
        /// <summary>Bolt length, metres.</summary>
        public FloatRange length = new(1f, 2f);
        /// <summary>Core width, metres; the halo is wider (see the comp's thunderHaloWidth).</summary>
        public float width = 0.05f;
        public float life = 0.15f;
        /// <summary>Random spread of the bolt direction, degrees.</summary>
        public float spreadDegrees = 50f;
        /// <summary>1 = bolts leave along the blade, 0 = along the swing.</summary>
        public float outward = 0.6f;
        public int branches = 1;
        /// <summary>Zigzag amplitude, share of the bolt length.</summary>
        public float jitter = 0.25f;
    }

    /// <summary>
    /// The whirlwind of a jump: emitters spinning about a forward-tilted axis
    /// that rises from the feet, each dragging trail layers into a helix, plus a ring of dust on
    /// the ground. Offsets are in the body frame (right, up, forward), metres.
    /// </summary>
    public class SakuraUpdraftProps
    {
        /// <summary>Seconds relative to the cut; negative = before it (at the take-off crouch).</summary>
        public float delay;
        public Vector3 startOffset = new(0f, -0.6f, 0f);
        public Vector3 endOffset = new(0f, 2.6f, 1f);
        /// <summary>Seconds for the spin centre to travel from start to end offset.</summary>
        public float riseSeconds = 1f;
        public float angularSpeed = 540f;
        public float spiralRadius = 0.75f;
        public int strands = 2;
        /// <summary>Seconds the emitters keep emitting trail.</summary>
        public float emitSeconds = 0.8f;
        public List<SakuraTrailLayer> layers = new();

        public string dustTexPath;
        public Vector3 dustOffset = new(0f, -0.6f, 0f);
        public int dustCount = 18;
        /// <summary>Seconds between two dust puffs.</summary>
        public float dustInterval = 0.011f;
        public float dustLife = 1f;
        public float dustStartSize = 1f;
        public float dustEndSize = 4f;
        public float dustAlpha = 0.5f;
        /// <summary>Screen-vertical scale of the dust (it lies on the ground, seen at a slant); 1 = round.</summary>
        public float dustSquash = 1f;
        public Color dustColor = new(0.85f, 0.85f, 0.85f, 1f);
    }

    /// <summary>
    /// The body motion of one variant (ground or airborne cast). Curves are interpolated with a
    /// monotone cubic, so keys mark the poses and the motion eases between them without overshoot.
    /// </summary>
    public class SakuraDanceMotion : BladeMotionClip
    {
        public SakuraDanceMotion()
        {
            durationSeconds = 2.3f;
        }

        /// <summary>
        /// The dance starts this far into the curves (motion seconds); everything before it is the
        /// aim pose, already shown by the warmup. All times here stay in motion seconds.
        /// </summary>
        public float skipSeconds;
        /// <summary>Spawn time of each cut, seconds. Missing entries fall back to the cut's own time.</summary>
        public List<float> cutTimes = new();
        // Body height (height), body turn (turn) and the held blade relative to the turned body
        // (blade) are the shared clip tracks.
        /// <summary>Airborne cast only: when vanilla flight is ended. Negative = the pawn stays flying.</summary>
        public float landTime = -1f;
        /// <summary>Rain only: when the blade catches the thunder (motion seconds). Negative = never. See <see cref="thunderCatchTimes"/>.</summary>
        public float thunderCatchTime = -1f;
        /// <summary>
        /// Rain only: hit-stop at the catch. The next <see cref="thunderSlowSeconds"/> of the motion are
        /// stretched over <see cref="thunderSlowPower"/> times as long: the motion stops dead when the
        /// bolt lands and eases back to full speed (motion = slow * u^power over the window), so there is
        /// no jerk where it rejoins. The rest of the dance is pushed back. 0 = no hit-stop.
        /// </summary>
        public float thunderSlowSeconds;
        public float thunderSlowPower = 4f;
        /// <summary>Rain only: one of these is picked at random per cast; overrides <see cref="thunderCatchTime"/>.</summary>
        public List<float> thunderCatchTimes = new();

        public int DurationTicks => Mathf.Max(1, Mathf.RoundToInt(durationSeconds * 60f));

        public float CutTime(SakuraCutProps cut, int index)
        {
            return cutTimes != null && index < cutTimes.Count ? cutTimes[index] : cut.time;
        }
    }

    /// <summary>
    /// The aim (ability warmup): the turn into the stance and the blade, written over
    /// <see cref="durationSeconds"/> and stretched over the actual warmup. It should end in the
    /// pose the dance starts from.
    /// </summary>
    public class SakuraWarmupMotion : BladeMotionClip
    {
        // turn should end at the dance's first value.
        public SakuraWarmupMotion()
        {
            durationSeconds = 0.4f;
        }
    }

    public class CompProperties_AbilitySakuraDance : CompProperties_AbilityEffect
    {
        public SweepDamageDef sweepDamage;
        /// <summary>Named damage of the cuts and of the charged cuts (shown in wounds and the combat log); null = the sweep def's.</summary>
        public DamageDef cutDamageDef;
        public DamageDef thunderCutDamageDef;
        public SakuraWarmupMotion warmup;
        public SakuraDanceMotion groundMotion;
        /// <summary>Sword pressure spent when the aim starts; the cast needs at least this much.</summary>
        public float swordPressureCost;
        /// <summary>Qinghe's own posture meter empties when the aim starts.</summary>
        public bool clearOwnPosture;
        /// <summary>Used when the pawn is flying at cast time; falls back to the ground motion.</summary>
        public SakuraDanceMotion airMotion;
        /// <summary>Screen offset to the north per metre of height.</summary>
        public float heightToScreen = 0.5f;
        /// <summary>Screen scale of north-south offsets of the held blade from the body (not the rings); see BladeMotionSetExtension.</summary>
        public float depthToScreen = 1f;
        /// <summary>Height of the motion origin above the drawn body centre, metres (blade, rings, petals, lightning).</summary>
        public float heightOrigin;
        public BladeProjection Projection => new(heightToScreen, depthToScreen, heightOrigin);
        /// <summary>Height of the ring origin above the pawn's drawn body centre, metres.</summary>
        public float ringHeightOffset = 0.6f;
        public List<SakuraCutProps> cuts = new();

        public float petalKeepFraction = 0.3f;
        public float petalSpeedScale = 0.25f;
        public float petalSpreadDegrees = 12f;
        public float petalSize = 0.14f;
        public string petalTexPath = "MiliraXianQinghe/Effect/SakuraDance/Petals";
        /// <summary>Petal tint. Vertex colours are 8-bit, so keep every channel at or below 1; use petalBrightness to go brighter.</summary>
        public Color petalColor = new(1f, 0.55f, 0.7f, 1f);
        public float petalBrightness = 1f;

        public string distortionNormalTexPath = "MiliraXianQinghe/Effect/SakuraDance/DistortNormal";
        /// <summary>Metres of refraction offset per unit of distortion intensity.</summary>
        public float distortionMetresPerUnit = 0.04f;
        /// <summary>Opacity of the dark band left after the strands fade.</summary>
        public float shadeOpacity = 0.22f;

        public ScreenBloomProperties bloom;
        public SoundDef castSound;
        public SoundDef cutSound;

        // Lightning Reversal: cast in the rain under the open sky, the blade catches a thunder at a
        // random one of the motion's catch times; every cut after it carries the thunder (its own
        // thunder layers, discharge and damage / radius factors), and the release cut also stuns.
        /// <summary>Damage rules of the release cut (with the stun). Missing = no thunder at all.</summary>
        public SweepDamageDef thunderSweepDamage;
        /// <summary>Minimum map rain rate (vanilla rainy weathers are 1; it ramps over the 4000-tick weather change).</summary>
        public float thunderMinRainRate = 1f;
        /// <summary>
        /// Weather with lightning events (vanilla thunderstorms, rainy or dry) counts as a catch once it
        /// has fully set in, rain or not.
        /// </summary>
        public bool thunderInStorms = true;
        /// <summary>Chance of a catch under the open sky in any other weather (a bolt from the clear sky).</summary>
        public float thunderClearSkyChance;
        public SoundDef thunderCatchSound;
        public SoundDef thunderReleaseSound;
        /// <summary>Halo of the electric layers; falls back to <see cref="bloom"/>.</summary>
        public ScreenBloomProperties thunderBloom;
        /// <summary>Outer colour of the electricity (vertex colour, channels at or below 1).</summary>
        public Color thunderColor = new(1f, 0.86f, 0.28f, 1f);
        public Color thunderCoreColor = Color.white;
        public float thunderBrightness = 3f;
        public string thunderArcTexPath = "MiliraXianQinghe/Effect/SakuraDance/Arc";
        public int thunderArcFrames = 16;
        public string thunderSparkTexPath = "MiliraXianQinghe/Effect/SakuraDance/Sparks";
        public string thunderBoltTexPath = "MiliraXianQinghe/Effect/SakuraDance/Bolt";
        public string thunderGlowTexPath = "MiliraXianQinghe/Effect/SakuraDance/Glow";
        /// <summary>Sky bolt striking the blade when the thunder is caught.</summary>
        public float thunderBoltSeconds = 0.3f;
        public float thunderBoltWidth = 0.18f;
        public float thunderGlowSize = 1.4f;
        public float thunderGlowBrightness = 0.8f;
        /// <summary>Bolts are a thin bright core (with bloom) over a soft halo this many times wider.</summary>
        public float thunderHaloWidth = 4f;
        public float thunderHaloBrightness = 0.35f;
        public float thunderCoreBrightness = 1.6f;
        /// <summary>Sparks crawling on the charged blade.</summary>
        public float thunderSparkInterval = 0.05f;
        public int thunderSparksPerBurst = 3;
        public float thunderSparkSize = 0.35f;
        /// <summary>The blade stops sparking this long after the release cut has finished spinning.</summary>
        public float thunderSparkAfterRelease = 0.5f;

        public CompProperties_AbilitySakuraDance()
        {
            compClass = typeof(CompAbilityEffect_SakuraDance);
        }

        public override IEnumerable<string> ConfigErrors(AbilityDef parentDef)
        {
            foreach (string error in base.ConfigErrors(parentDef))
            {
                yield return error;
            }
            if (sweepDamage == null)
            {
                yield return "sweepDamage is not set";
            }
            if (cuts.NullOrEmpty())
            {
                yield return "no cuts defined";
            }
            if (groundMotion == null)
            {
                yield return "groundMotion is not set";
            }

        }
    }
}

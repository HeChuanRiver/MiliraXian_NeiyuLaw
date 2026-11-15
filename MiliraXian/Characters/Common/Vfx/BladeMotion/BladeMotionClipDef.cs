using System.Collections.Generic;
using MiliraXian.Characters.Common.Vfx.BladeBody;
using RimWorld;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx.BladeMotion
{
    /// <summary>
    /// A blade motion clip as a Def. The def only names the clip; its data (keyframe tracks,
    /// strikes, trail/flash timing) lives in the JSON file at dataPath, relative to the mod root
    /// (reloadable via the "Reload JSON assets" debug action). Missing scalar fields keep the
    /// defaults of <see cref="BladeMotionClip"/>; a missing curve or track stays null. JSON shape:
    /// { "durationSeconds": 3.17, "windupSeconds": 1.45, "loop": false,
    ///   "damageDef": "MX_QH_CrossSlash", "counterFrom": 1.64, "flashTime": 0.783,
    ///   "trailStart": 1.45, "trailEnd": 1.8, "drawPushStart": 1.45, "drawPushEnd": 1.5,
    ///   "turn": [[t, v], ...], "height": [...], "bodyForward": [...], "bodySide": [...],
    ///   "tilt": [...], "tiltSide": [...], "sheathed": [...],
    ///   "strikes": [{ "start": 1.45, "end": 1.633, "damageFactor": 2.3, "resourceFactor": 0.5 }, ...],
    ///   "scabbardKeys": [{ "time": 0, "mouth": [x, y, z], "end": [x, y, z] }, ...],
    ///   "blade": { "shoulder": [x, y, z], "armLength": 0.55, "armPitchShare": 0.5,
    ///     "keys": [{ "time": 0, "yaw": -12, "pitch": 65, "roll": -90, "reach": 0, "offset": [x, y, z] }, ...] } }
    /// </summary>
    public class BladeMotionClipDef : Def
    {
        /// <summary>JSON mirror of <see cref="BladeMotionClip"/>; subclasses add their extra fields.</summary>
        protected class ClipData
        {
            public float durationSeconds { get; set; } = 1f;
            public bool loop { get; set; }
            public float windupSeconds { get; set; }
            public StrikeData[] strikes { get; set; }
            public BladeData blade { get; set; }
            public float[][] turn { get; set; }
            public bool turnCarriesBlade { get; set; } = true;
            public float[][] height { get; set; }
            public float[][] bodyForward { get; set; }
            public float[][] bodySide { get; set; }
            public float[][] tilt { get; set; }
            public float[][] tiltSide { get; set; }
            public float trailStart { get; set; } = -1f;
            public float trailEnd { get; set; } = -1f;
            public float drawPushStart { get; set; } = -1f;
            public float drawPushEnd { get; set; } = -1f;
            public float flashTime { get; set; } = -1f;
            public float[][] sheathed { get; set; }
            public ScabbardKeyData[] scabbardKeys { get; set; }
            public float counterFrom { get; set; } = -1f;
            public string damageDef { get; set; }
            public string counterDamageDef { get; set; }

            // Sakura Dance extras, read by SakuraDanceClipDef only.
            public float skipSeconds { get; set; }
            public float[] cutTimes { get; set; }
            public float landTime { get; set; } = -1f;
            public float thunderCatchTime { get; set; } = -1f;
            public float[] thunderCatchTimes { get; set; }
            public float thunderSlowSeconds { get; set; }
            public float thunderSlowPower { get; set; } = 4f;
        }

        protected sealed class StrikeData
        {
            public float start { get; set; }
            public float end { get; set; }
            public float damageFactor { get; set; } = 1f;
            public float resourceFactor { get; set; } = 1f;
        }

        protected sealed class BladeData
        {
            public float[] shoulder { get; set; }
            public float armLength { get; set; } = 0.55f;
            public float armPitchShare { get; set; } = 0.5f;
            public KeyData[] keys { get; set; }
        }

        protected sealed class KeyData
        {
            public float time { get; set; }
            public float yaw { get; set; }
            public float pitch { get; set; }
            public float roll { get; set; }
            public float reach { get; set; } = 1f;
            public float[] offset { get; set; }
        }

        protected sealed class ScabbardKeyData
        {
            public float time { get; set; }
            public float[] mouth { get; set; }
            public float[] end { get; set; }
        }

        public string dataPath;

        private JsonModAsset<ClipData> asset;
        private BladeMotionClip clip;
        // Volatile: Sync's steady state reads them lock-free (a reload publishes the new clip by
        // writing dataVersion last).
        private volatile int dataVersion = -1;
        // Def ConfigErrors run in parallel over def types, and the first Sync of a def's clip may
        // race with another def's (e.g. the sword guard reading the weapon's attack clip): serialize
        // the whole check-map-assign so a concurrent reader never sees a half-built clip.
        private readonly object syncLock = new();

        /// <summary>
        /// The current clip data, rebuilt in place after every successful (re)load of the JSON
        /// (consumers re-read it; a playing motion may jump to the reloaded pose).
        /// </summary>
        public BladeMotionClip Clip
        {
            get
            {
                Sync();
                return clip;
            }
        }

        /// <summary>Changes whenever the clip data is (re)loaded.</summary>
        public int DataVersion
        {
            get
            {
                Sync();
                return dataVersion;
            }
        }

        /// <summary>The clip instance the data is mapped into; subclasses carry their extra fields.</summary>
        protected virtual BladeMotionClip NewClip()
        {
            return new BladeMotionClip();
        }

        /// <summary>Maps fields beyond the base clip (see <see cref="NewClip"/>).</summary>
        protected virtual void MapExtra(BladeMotionClip target, ClipData data)
        {
        }

        public override IEnumerable<string> ConfigErrors()
        {
            foreach (string error in base.ConfigErrors())
            {
                yield return error;
            }
            if (dataPath.NullOrEmpty())
            {
                yield return "dataPath is empty";
            }
            else if (Clip == null)
            {
                yield return $"clip data at '{dataPath}' failed to load (see the JSON error above)";
            }
        }

        private void Sync()
        {
            if (dataPath.NullOrEmpty())
            {
                return;
            }
            // Lock-free steady state: once loaded, the version only changes on a reload.
            JsonModAsset<ClipData> current = asset;
            if (current != null && dataVersion == current.Version)
            {
                return;
            }
            lock (syncLock)
            {
                asset ??= new JsonModAsset<ClipData>(dataPath, ValidateData);
                ClipData data = asset.Data;
                if (data == null || dataVersion == asset.Version)
                {
                    return;
                }
                BladeMotionClip built = NewClip();
                MapCommon(built, data);
                MapExtra(built, data);
                clip = built;
                dataVersion = asset.Version;
            }
        }

        private void MapCommon(BladeMotionClip c, ClipData d)
        {
            c.durationSeconds = d.durationSeconds;
            c.loop = d.loop;
            c.windupSeconds = d.windupSeconds;
            c.strikes = new List<BladeStrike>(d.strikes?.Length ?? 0);
            if (d.strikes != null)
            {
                foreach (StrikeData s in d.strikes)
                {
                    c.strikes.Add(new BladeStrike
                    {
                        start = s.start,
                        end = s.end,
                        damageFactor = s.damageFactor,
                        resourceFactor = s.resourceFactor,
                    });
                }
            }
            c.blade = BuildBlade(d.blade);
            c.turn = Curve(d.turn);
            c.turnCarriesBlade = d.turnCarriesBlade;
            c.height = Curve(d.height);
            c.bodyForward = Curve(d.bodyForward);
            c.bodySide = Curve(d.bodySide);
            c.tilt = Curve(d.tilt);
            c.tiltSide = Curve(d.tiltSide);
            c.trailStart = d.trailStart;
            c.trailEnd = d.trailEnd;
            c.drawPushStart = d.drawPushStart;
            c.drawPushEnd = d.drawPushEnd;
            c.flashTime = d.flashTime;
            c.sheathed = Curve(d.sheathed);
            c.scabbardKeys = new List<BladeScabbardKey>(d.scabbardKeys?.Length ?? 0);
            if (d.scabbardKeys != null)
            {
                foreach (ScabbardKeyData k in d.scabbardKeys)
                {
                    c.scabbardKeys.Add(new BladeScabbardKey { time = k.time, mouth = Vec3(k.mouth), end = Vec3(k.end) });
                }
            }
            c.counterFrom = d.counterFrom;
            c.damageDef = DamageNamed(d.damageDef);
            c.counterDamageDef = DamageNamed(d.counterDamageDef);
        }

        private static BladeKeyframes BuildBlade(BladeData d)
        {
            if (d == null)
            {
                return null;
            }
            BladeKeyframes blade = new()
            {
                armLength = d.armLength,
                armPitchShare = d.armPitchShare,
                keys = new List<BladeKey>(d.keys?.Length ?? 0),
            };
            if (d.shoulder != null)
            {
                blade.shoulder = Vec3(d.shoulder);
            }
            if (d.keys != null)
            {
                foreach (KeyData k in d.keys)
                {
                    blade.keys.Add(new BladeKey
                    {
                        time = k.time,
                        yaw = k.yaw,
                        pitch = k.pitch,
                        roll = k.roll,
                        reach = k.reach,
                        offset = k.offset != null ? Vec3(k.offset) : Vector3.zero,
                    });
                }
            }
            return blade;
        }

        private static SimpleCurve Curve(float[][] points)
        {
            if (points == null || points.Length == 0)
            {
                return null;
            }
            var curve = new SimpleCurve();
            foreach (float[] p in points)
            {
                curve.Add(p[0], p[1], sort: false);
            }
            curve.SortPoints();
            return curve;
        }

        private static Vector3 Vec3(float[] a)
        {
            return new Vector3(a[0], a[1], a[2]);
        }

        private DamageDef DamageNamed(string name)
        {
            if (name.NullOrEmpty())
            {
                return null;
            }
            DamageDef def = DefDatabase<DamageDef>.GetNamedSilentFail(name);
            // A name that does not resolve falls back to the verb's own damage (null), like a
            // missing field; warn rather than reject the whole clip.
            if (def == null)
            {
                Log.Warning($"[MiliraXian] Blade motion clip '{defName}' references unknown DamageDef '{name}'.");
            }
            return def;
        }

        private static string ValidateData(ClipData d)
        {
            if (d.durationSeconds <= 0f)
            {
                return "durationSeconds must be positive";
            }
            foreach (float[][] curve in new[] { d.turn, d.height, d.bodyForward, d.bodySide, d.tilt, d.tiltSide, d.sheathed })
            {
                if (curve == null)
                {
                    continue;
                }
                foreach (float[] p in curve)
                {
                    if (p == null || p.Length != 2)
                    {
                        return "a curve point is not 2 numbers";
                    }
                }
            }
            if (d.blade != null)
            {
                if (d.blade.shoulder != null && d.blade.shoulder.Length != 3)
                {
                    return "blade shoulder is not 3 numbers";
                }
                if (d.blade.keys != null)
                {
                    foreach (KeyData k in d.blade.keys)
                    {
                        if (k == null || (k.offset != null && k.offset.Length != 3))
                        {
                            return "a blade key offset is not 3 numbers";
                        }
                    }
                }
            }
            if (d.scabbardKeys != null)
            {
                foreach (ScabbardKeyData k in d.scabbardKeys)
                {
                    if (k == null || k.mouth == null || k.mouth.Length != 3 || k.end == null || k.end.Length != 3)
                    {
                        return "a scabbard key needs mouth and end of 3 numbers";
                    }
                }
            }
            if (d.strikes != null)
            {
                foreach (StrikeData s in d.strikes)
                {
                    if (s == null || s.end <= s.start)
                    {
                        return "a strike needs end > start";
                    }
                }
            }
            return null;
        }
    }
}

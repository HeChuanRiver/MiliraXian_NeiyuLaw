using System;
using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Skeletal2D
{
    /// <summary>
    /// One Bezier keyframe of a bone channel, mirroring a Blender F-curve keyframe.
    /// Handles are absolute (tick, value) control points; a handle equal to (0, 0) is
    /// treated as unset and collapses onto the key point.
    /// </summary>
    public class Skeletal2DBezierKey
    {
        public float tick;
        public float value;
        public Vector2 inHandle;
        public Vector2 outHandle;
    }

    /// <summary>
    /// Per-bone animation channels. rotationKeys are in-plane degrees; scale keys are
    /// bone-local (along the bone axis / perpendicular). Missing channels rest at 0 / 1.
    /// </summary>
    public class Skeletal2DBoneTrack
    {
        public string bone;
        public List<Skeletal2DBezierKey> rotationKeys;
        public List<Skeletal2DBezierKey> scaleAlongKeys;
        public List<Skeletal2DBezierKey> scaleAcrossKeys;
    }

    /// <summary>
    /// A skeletal animation clip: sparse Bezier keyframe tracks per bone, sampled at
    /// arbitrary fractional ticks so clips can be interrupted and retriggered freely.
    /// Tick 0 is the first frame of the source Blender action (60 ticks = 1 second).
    /// The Def only names the clip; its data (length, looping, tracks) lives in the JSON file
    /// at dataPath, relative to the mod root (reloadable via the "Reload Skeletal2D assets"
    /// debug action). JSON shape:
    /// { "durationTicks": 119, "loop": true, "tracks": [ { "bone": "PetalC_root",
    ///   "rotation": [[tick, value, inTick, inValue, outTick, outValue], ...],
    ///   "scaleAlong": [...], "scaleAcross": [...] }, ... ] } (missing channels may be omitted).
    /// </summary>
    public class Skeletal2DClipDef : Def
    {
        private sealed class ClipData
        {
            public int durationTicks { get; set; }
            public bool loop { get; set; }
            public TrackData[] tracks { get; set; }
        }

        private sealed class TrackData
        {
            public string bone { get; set; }
            public float[][] rotation { get; set; }
            public float[][] scaleAlong { get; set; }
            public float[][] scaleAcross { get; set; }
        }

        private const int KeyFloats = 6;
        private static readonly List<Skeletal2DBoneTrack> NoTracks = new();

        public string dataPath;

        private Skeletal2DJsonAsset<ClipData> asset;
        private List<Skeletal2DBoneTrack> tracks;
        private int durationTicks = 60;
        private bool loop = true;
        private int dataVersion = -1;

        public int DurationTicks
        {
            get
            {
                Sync();
                return durationTicks;
            }
        }

        public bool Loop
        {
            get
            {
                Sync();
                return loop;
            }
        }

        public List<Skeletal2DBoneTrack> Tracks
        {
            get
            {
                Sync();
                return tracks ?? NoTracks;
            }
        }

        /// <summary>Changes whenever the clip data is (re)loaded; players re-resolve tracks on change.</summary>
        public int DataVersion
        {
            get
            {
                Sync();
                return dataVersion;
            }
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
        }

        private void Sync()
        {
            if (dataPath.NullOrEmpty())
            {
                return;
            }
            asset ??= new Skeletal2DJsonAsset<ClipData>(dataPath, Validate);
            ClipData data = asset.Data;
            if (data == null || dataVersion == asset.Version)
            {
                return;
            }
            durationTicks = Mathf.Max(1, data.durationTicks);
            loop = data.loop;
            tracks = new List<Skeletal2DBoneTrack>(data.tracks.Length);
            foreach (TrackData track in data.tracks)
            {
                tracks.Add(new Skeletal2DBoneTrack
                {
                    bone = track.bone,
                    rotationKeys = ToKeys(track.rotation),
                    scaleAlongKeys = ToKeys(track.scaleAlong),
                    scaleAcrossKeys = ToKeys(track.scaleAcross),
                });
            }
            dataVersion = asset.Version;
        }

        private static string Validate(ClipData data)
        {
            if (data.tracks == null)
            {
                return "missing tracks";
            }
            foreach (TrackData track in data.tracks)
            {
                if (track == null || track.bone.NullOrEmpty())
                {
                    return "a track has no bone name";
                }
                foreach (float[][] keys in new[] { track.rotation, track.scaleAlong, track.scaleAcross })
                {
                    if (keys == null)
                    {
                        continue;
                    }
                    foreach (float[] key in keys)
                    {
                        if (key == null || key.Length != KeyFloats)
                        {
                            return $"track '{track.bone}' has a key that is not {KeyFloats} numbers";
                        }
                    }
                }
            }
            return null;
        }

        private static List<Skeletal2DBezierKey> ToKeys(float[][] keys)
        {
            if (keys == null || keys.Length == 0)
            {
                return null;
            }
            var result = new List<Skeletal2DBezierKey>(keys.Length);
            foreach (float[] k in keys)
            {
                result.Add(new Skeletal2DBezierKey
                {
                    tick = k[0],
                    value = k[1],
                    inHandle = new Vector2(k[2], k[3]),
                    outHandle = new Vector2(k[4], k[5]),
                });
            }
            return result;
        }

        public static float Sample(List<Skeletal2DBezierKey> keys, float tick, float defaultValue)
        {
            if (keys == null || keys.Count == 0)
            {
                return defaultValue;
            }
            if (tick <= keys[0].tick)
            {
                return keys[0].value;
            }
            int last = keys.Count - 1;
            if (tick >= keys[last].tick)
            {
                return keys[last].value;
            }
            int i = 0;
            while (i < last && keys[i + 1].tick <= tick)
            {
                i++;
            }
            Skeletal2DBezierKey k0 = keys[i];
            Skeletal2DBezierKey k1 = keys[i + 1];
            Vector2 p0 = new(k0.tick, k0.value);
            Vector2 p3 = new(k1.tick, k1.value);
            Vector2 p1 = k0.outHandle == default ? p0 : k0.outHandle;
            Vector2 p2 = k1.inHandle == default ? p3 : k1.inHandle;
            float u = SolveU(p0.x, p1.x, p2.x, p3.x, tick);
            float v = 1f - u;
            return v * v * v * p0.y + 3f * v * v * u * p1.y + 3f * v * u * u * p2.y + u * u * u * p3.y;
        }

        private static float SolveU(float x0, float x1, float x2, float x3, float x)
        {
            float u = Mathf.Clamp01((x - x0) / Mathf.Max(0.0001f, x3 - x0));
            for (int iter = 0; iter < 6; iter++)
            {
                float v = 1f - u;
                float fx = v * v * v * x0 + 3f * v * v * u * x1 + 3f * v * u * u * x2 + u * u * u * x3 - x;
                if (Mathf.Abs(fx) < 0.001f)
                {
                    break;
                }
                float dfx = 3f * v * v * (x1 - x0) + 6f * v * u * (x2 - x1) + 3f * u * u * (x3 - x2);
                if (Mathf.Abs(dfx) < 0.0001f)
                {
                    break;
                }
                u = Mathf.Clamp01(u - fx / dfx);
            }
            return u;
        }
    }
}

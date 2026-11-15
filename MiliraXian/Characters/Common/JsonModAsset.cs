using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LudeonTK;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Common
{
    /// <summary>
    /// Registry of every JSON data asset under the mod root (skeletal meshes and clips, blade
    /// motion clips), so a debug action can reload them all.
    /// </summary>
    internal abstract class JsonModAsset
    {
        private static readonly List<JsonModAsset> All = new();

        protected JsonModAsset()
        {
            // Def ConfigErrors run in parallel over def types, so assets are created concurrently.
            lock (All)
            {
                All.Add(this);
            }
        }

        /// <summary>Re-parses the file; returns false (keeping the previous data) on failure.</summary>
        protected abstract bool Reload();

        [DebugAction("Milira Xian - Common", "Reload JSON assets (meshes, skeletal/blade clips)", false, false, false, false, false, 0, false,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ReloadAll()
        {
            int reloaded = 0;
            int failed = 0;
            JsonModAsset[] snapshot;
            lock (All)
            {
                snapshot = All.ToArray();
            }
            foreach (JsonModAsset asset in snapshot)
            {
                if (asset.Reload())
                {
                    reloaded++;
                }
                else
                {
                    failed++;
                }
            }
            string text = $"JSON assets: reloaded {reloaded}" + (failed > 0 ? $", {failed} failed (see log)" : "") + ".";
            Messages.Message(text, failed > 0 ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.NeutralEvent, false);
        }
    }

    /// <summary>
    /// A JSON data file under the mod root, parsed on first use. Reloaded only through the
    /// "Reload JSON assets" debug action. Version increments on every successful load so
    /// consumers can rebuild derived data; a failed reload keeps the previous data.
    /// </summary>
    internal sealed class JsonModAsset<T> : JsonModAsset where T : class
    {
        private readonly string relativePath;
        private readonly string fullPath;
        private readonly Func<T, string> validate;
        // Def ConfigErrors run in parallel over def types, and the first read of a def's clip may
        // race with another def's (e.g. the sword guard reading the weapon's attack clip): the load
        // must be serialized or the second reader sees the half-loaded state. The steady-state read
        // stays lock-free (double-checked on the volatile flags).
        private readonly object loadLock = new();
        private volatile T data;
        private volatile bool attempted;

        /// <param name="validate">Returns an error message for malformed data, or null if valid.</param>
        public JsonModAsset(string relativePath, Func<T, string> validate)
        {
            this.relativePath = relativePath;
            this.validate = validate;
            fullPath = Path.Combine(NeiyuLawMod.Instance?.Content?.RootDir ?? "", relativePath);
        }

        /// <summary>Incremented on every successful load; 0 until the first load succeeds.</summary>
        public int Version { get; private set; }

        /// <summary>The parsed data, or null if it has never loaded successfully.</summary>
        public T Data
        {
            get
            {
                if (!attempted)
                {
                    lock (loadLock)
                    {
                        if (!attempted)
                        {
                            TryLoad();
                            attempted = true;
                        }
                    }
                }
                return data;
            }
        }

        protected override bool Reload()
        {
            lock (loadLock)
            {
                bool ok = TryLoad();
                attempted = true;
                return ok;
            }
        }

        private bool TryLoad()
        {
            try
            {
                if (!File.Exists(fullPath))
                {
                    throw new FileNotFoundException("file not found");
                }
                T parsed = JsonSerializer.Deserialize<T>(File.ReadAllText(fullPath));
                string error = parsed == null ? "file is empty" : validate?.Invoke(parsed);
                if (error != null)
                {
                    throw new InvalidDataException(error);
                }
                data = parsed;
                Version++;
                return true;
            }
            catch (Exception ex)
            {
                if (data == null)
                {
                    Log.Error($"[MiliraXian] Failed to load JSON asset {fullPath}: {ex.Message}");
                }
                else
                {
                    Log.Warning($"[MiliraXian] Reload of JSON asset {relativePath} failed, keeping previous data: {ex.Message}");
                }
                return false;
            }
        }
    }
}

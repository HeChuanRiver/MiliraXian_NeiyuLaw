using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using LudeonTK;
using RimWorld;
using Verse;

namespace MiliraXian.Characters.Common.Skeletal2D
{
    /// <summary>
    /// Registry of every Skeletal2D JSON asset, so a debug action can reload them all.
    /// </summary>
    internal abstract class Skeletal2DJsonAsset
    {
        private static readonly List<Skeletal2DJsonAsset> All = new();

        protected Skeletal2DJsonAsset()
        {
            All.Add(this);
        }

        /// <summary>Re-parses the file; returns false (keeping the previous data) on failure.</summary>
        protected abstract bool Reload();

        [DebugAction("Milira Xian - Common", "Reload Skeletal2D assets (mesh/clip JSON)", false, false, false, false, false, 0, false,
            allowedGameStates = AllowedGameStates.PlayingOnMap)]
        private static void ReloadAll()
        {
            int reloaded = 0;
            int failed = 0;
            foreach (Skeletal2DJsonAsset asset in All)
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
            string text = $"Skeletal2D: reloaded {reloaded} asset(s)" + (failed > 0 ? $", {failed} failed (see log)" : "") + ".";
            Messages.Message(text, failed > 0 ? MessageTypeDefOf.RejectInput : MessageTypeDefOf.NeutralEvent, false);
        }
    }

    /// <summary>
    /// A JSON data file under the mod root, parsed on first use. Reloaded only through the
    /// "Reload Skeletal2D assets" debug action. Version increments on every successful load so
    /// consumers can rebuild derived data; a failed reload keeps the previous data.
    /// </summary>
    internal sealed class Skeletal2DJsonAsset<T> : Skeletal2DJsonAsset where T : class
    {
        private readonly string relativePath;
        private readonly string fullPath;
        private readonly Func<T, string> validate;
        private T data;
        private bool attempted;

        /// <param name="validate">Returns an error message for malformed data, or null if valid.</param>
        public Skeletal2DJsonAsset(string relativePath, Func<T, string> validate)
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
                    attempted = true;
                    TryLoad();
                }
                return data;
            }
        }

        protected override bool Reload()
        {
            attempted = true;
            return TryLoad();
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
                    Log.Error($"[MiliraXian] Failed to load Skeletal2D asset {fullPath}: {ex.Message}");
                }
                else
                {
                    Log.Warning($"[MiliraXian] Reload of Skeletal2D asset {relativePath} failed, keeping previous data: {ex.Message}");
                }
                return false;
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace MiliraXian.Characters.Common.Vfx
{
    /// <summary>
    /// Game-scoped owner of the "pawn snapshot" textures used by ghosts (afterimages, evade
    /// flashes, mirror reflections).
    ///
    /// Capture is synchronous and stays on the GPU: the snapshot is a temporary RenderTexture
    /// handed out by Unity's own pool, baked in the frame that asks for it, so a ghost can be
    /// drawn on its first frame. There is deliberately no readback to a Texture2D — that stalls
    /// the GPU and made captures an order of magnitude more expensive than the pawn render itself.
    ///
    /// What the service adds on top of the old per-instance copies is ownership: the textures
    /// belong to this game-scoped component instead of a map-level object, so no cleanup hook is
    /// needed on map teardown. Consumers only hold an int handle; a handle is reference counted
    /// (several ghosts of the same pose share one capture) and any lease that is never returned is
    /// released by the age-out sweep below.
    /// </summary>
    public sealed class GameComponent_MxVfxCaptures : GameComponent
    {
        private const int SnapshotSize = 512;
        private const float SnapshotZoom = 0.5f;

        /// <summary>Hard ceiling on simultaneous snapshots; beyond it requests are declined.</summary>
        private const int MaxLiveCaptures = 32;

        /// <summary>A ghost that lives longer than this is a bug; release the texture anyway.</summary>
        private const int MaxCaptureAgeTicks = 600;

        private sealed class Entry
        {
            internal RenderTexture texture;
            internal int references;
            internal int capturedTick;
            internal (int pawnId, Rot4 facing, int tick) pose;
        }

        private readonly Dictionary<int, Entry> entries = new();

        /// <summary>Pose → handle, so several ghosts of one moment share a single capture.</summary>
        private readonly Dictionary<(int pawnId, Rot4 facing, int tick), int> byPose = new();

        /// <summary>Reused by the sweep so it never allocates while enumerating.</summary>
        private readonly List<int> expired = new();

        private int nextId = 1;

        public GameComponent_MxVfxCaptures(Game game)
        {
        }

        private static GameComponent_MxVfxCaptures Instance =>
            Current.Game?.GetComponent<GameComponent_MxVfxCaptures>();

        /// <summary>
        /// True while a snapshot is being baked. Body-hiding render prefixes must let the real body
        /// draw during this window, otherwise a pawn standing in for a ghost would be captured as
        /// an empty picture.
        /// </summary>
        public static bool IsCapturing { get; private set; }

        /// <summary>
        /// Captures <paramref name="pawn"/> now and returns a handle, or 0 when the pawn cannot be
        /// captured. Snapshots of the same (pawn, facing, tick) are shared, so several ghosts of one
        /// moment cost a single render.
        /// </summary>
        public static int Request(Pawn pawn, Rot4 facing)
        {
            GameComponent_MxVfxCaptures service = Instance;
            if (service == null || pawn == null || pawn.Destroyed || !pawn.Spawned)
            {
                return 0;
            }
            return service.Capture(pawn, facing, share: true);
        }

        /// <summary>
        /// Captures a snapshot that must not be shared with anyone else, for callers that keep
        /// re-rendering into the same texture while their effect is alive (the mirror's stance).
        /// </summary>
        public static int RequestExclusive(Pawn pawn, Rot4 facing)
        {
            GameComponent_MxVfxCaptures service = Instance;
            if (service == null || pawn == null || pawn.Destroyed || !pawn.Spawned)
            {
                return 0;
            }
            return service.Capture(pawn, facing, share: false);
        }

        /// <summary>
        /// Re-renders into the texture already owned by <paramref name="handle"/>. Used by effects
        /// that refresh their snapshot in place instead of asking for a new one.
        /// </summary>
        public static void Recapture(int handle, Pawn pawn, Rot4 facing)
        {
            GameComponent_MxVfxCaptures service = Instance;
            if (service == null || handle <= 0 || pawn == null || pawn.Destroyed || !pawn.Spawned)
            {
                return;
            }
            service.RecaptureInternal(handle, pawn, facing);
        }

        /// <summary>The snapshot for a handle, or false once the handle has been released.</summary>
        public static bool TryGetTexture(int handle, out RenderTexture texture)
        {
            texture = null;
            GameComponent_MxVfxCaptures service = Instance;
            if (service == null || handle <= 0)
            {
                return false;
            }
            Entry entry = service.entries.TryGetValue(handle, out Entry found) ? found : null;
            if (entry == null || entry.texture == null)
            {
                return false;
            }
            texture = entry.texture;
            return true;
        }

        /// <summary>Drops one reference; the texture returns to Unity's pool with the last one.</summary>
        public static void Release(int handle)
        {
            GameComponent_MxVfxCaptures service = Instance;
            if (service == null || handle <= 0)
            {
                return;
            }
            service.ReleaseInternal(handle);
        }

        private int Capture(Pawn pawn, Rot4 facing, bool share)
        {
            int tick = Find.TickManager?.TicksGame ?? 0;
            var pose = (pawn.thingIDNumber, facing, tick);
            if (share && byPose.TryGetValue(pose, out int existing)
                && entries.TryGetValue(existing, out Entry entry) && entry.texture != null)
            {
                entry.references++;
                return existing;
            }

            if (entries.Count >= MaxLiveCaptures)
            {
                return 0;
            }

            if (pawn.Drawer?.renderer == null || Find.PawnCacheRenderer == null)
            {
                return 0;
            }

            RenderTexture texture = RenderTexture.GetTemporary(
                SnapshotSize, SnapshotSize, 24, RenderTextureFormat.ARGB32);
            texture.name = "MX_PawnCapture";
            IsCapturing = true;
            try
            {
                Find.PawnCacheRenderer.RenderPawn(pawn, texture, Vector3.zero, SnapshotZoom, 0f, facing,
                    renderHead: true, renderHeadgear: true, renderClothes: true);
            }
            catch (System.Exception exception)
            {
                Log.ErrorOnce("[MiliraXian] Pawn capture failed: " + exception, 0x4d0c17);
                IsCapturing = false;
                RenderTexture.ReleaseTemporary(texture);
                return 0;
            }
            finally
            {
                IsCapturing = false;
            }

            int id = nextId++;
            entries.Add(id, new Entry { texture = texture, references = 1, capturedTick = tick, pose = pose });
            if (share)
            {
                byPose[pose] = id;
            }
            return id;
        }

        private void RecaptureInternal(int handle, Pawn pawn, Rot4 facing)
        {
            if (!entries.TryGetValue(handle, out Entry entry) || entry.texture == null)
            {
                return;
            }
            if (pawn.Drawer?.renderer == null || Find.PawnCacheRenderer == null)
            {
                return;
            }

            IsCapturing = true;
            try
            {
                Find.PawnCacheRenderer.RenderPawn(pawn, entry.texture, Vector3.zero, SnapshotZoom, 0f, facing,
                    renderHead: true, renderHeadgear: true, renderClothes: true);
                // Refresh the age stamp: a stance that keeps re-rendering is not a stale lease.
                entry.capturedTick = Find.TickManager?.TicksGame ?? entry.capturedTick;
            }
            catch (System.Exception exception)
            {
                Log.ErrorOnce("[MiliraXian] Pawn recapture failed: " + exception, 0x4d0c18);
            }
            finally
            {
                IsCapturing = false;
            }
        }

        private void ReleaseInternal(int handle)
        {
            if (!entries.TryGetValue(handle, out Entry entry))
            {
                return;
            }
            entry.references--;
            if (entry.references > 0)
            {
                return;
            }
            Drop(handle, entry);
        }

        private void Drop(int handle, Entry entry)
        {
            if (entry.texture != null)
            {
                RenderTexture.ReleaseTemporary(entry.texture);
                entry.texture = null;
            }
            entries.Remove(handle);
            if (byPose.TryGetValue(entry.pose, out int mapped) && mapped == handle)
            {
                byPose.Remove(entry.pose);
            }
        }

        public override void GameComponentUpdate()
        {
            int now = Find.TickManager?.TicksGame ?? 0;
            if (entries.Count == 0)
            {
                return;
            }

            // Collect first: the dictionary cannot be mutated while it is being enumerated.
            expired.Clear();
            foreach (KeyValuePair<int, Entry> pair in entries)
            {
                if (now - pair.Value.capturedTick > MaxCaptureAgeTicks)
                {
                    expired.Add(pair.Key);
                }
            }
            for (int i = 0; i < expired.Count; i++)
            {
                if (entries.TryGetValue(expired[i], out Entry entry))
                {
                    Drop(expired[i], entry);
                }
            }
            expired.Clear();
        }
    }
}

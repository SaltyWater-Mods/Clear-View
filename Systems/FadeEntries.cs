using System;
using System.Collections.Generic;

namespace ClearView.Systems
{
    internal sealed class FadeEntries
    {
        private const float FadeOutSpeed = 4.8f;
        private const float FadeInSpeed = 5.5f;
        private const long FadeOutGraceMs = 90;

        private readonly Dictionary<BlockKey, FadeEntry> entries = new Dictionary<BlockKey, FadeEntry>();
        private readonly HashSet<BlockKey> wanted = new HashSet<BlockKey>();
        private readonly object entriesLock = new object();

        public void SetWanted(HashSet<BlockKey> newWanted, long now, List<BlockKey> redrawNow)
        {
            // camera decides what should hide, terrain redraw follows after
            redrawNow.Clear();

            lock (entriesLock)
            {
                wanted.Clear();

                foreach (BlockKey key in newWanted)
                {
                    wanted.Add(key);
                    if (!entries.TryGetValue(key, out FadeEntry entry))
                    {
                        entry = new FadeEntry();
                        entries[key] = entry;
                        redrawNow.Add(key);
                    }
                    else if (!entry.HiddenInTerrain)
                    {
                        entry.HiddenInTerrain = true;
                        entry.MeshReady = false;
                        redrawNow.Add(key);
                    }

                    entry.Wanted = true;
                    entry.LastWantedMs = now;
                }

                foreach (KeyValuePair<BlockKey, FadeEntry> pair in entries)
                {
                    if (!wanted.Contains(pair.Key))
                    {
                        pair.Value.Wanted = false;
                    }
                }
            }
        }

        public void Update(float dt, float targetOpacity, long now, List<BlockKey> restoreNow)
        {
            // wait for captured mesh before fading so the block does not pop away
            restoreNow.Clear();

            lock (entriesLock)
            {
                foreach (KeyValuePair<BlockKey, FadeEntry> pair in entries)
                {
                    FadeEntry entry = pair.Value;
                    if (entry.Wanted)
                    {
                        if (!entry.MeshReady)
                        {
                            entry.Opacity = 1f;
                            continue;
                        }

                        entry.Opacity = Math.Max(targetOpacity, entry.Opacity - FadeOutSpeed * dt);
                        continue;
                    }

                    if (now - entry.LastWantedMs < FadeOutGraceMs)
                    {
                        continue;
                    }

                    if (!entry.MeshReady)
                    {
                        if (entry.HiddenInTerrain)
                        {
                            entry.HiddenInTerrain = false;
                            restoreNow.Add(pair.Key);
                        }

                        continue;
                    }

                    entry.Opacity = Math.Min(1f, entry.Opacity + FadeInSpeed * dt);
                    if (entry.Opacity >= 0.995f && entry.HiddenInTerrain)
                    {
                        entry.HiddenInTerrain = false;
                        restoreNow.Add(pair.Key);
                    }
                }
            }
        }

        public bool IsHiddenInTerrain(BlockKey key)
        {
            lock (entriesLock)
            {
                return entries.TryGetValue(key, out FadeEntry entry) && entry.HiddenInTerrain;
            }
        }

        public float OpacityFor(BlockKey key)
        {
            lock (entriesLock)
            {
                return entries.TryGetValue(key, out FadeEntry entry) ? entry.Opacity : 1f;
            }
        }

        public HashSet<BlockKey> HiddenKeysSnapshot()
        {
            lock (entriesLock)
            {
                HashSet<BlockKey> keys = new HashSet<BlockKey>();
                foreach (KeyValuePair<BlockKey, FadeEntry> pair in entries)
                {
                    if (pair.Value.HiddenInTerrain)
                    {
                        keys.Add(pair.Key);
                    }
                }

                return keys;
            }
        }

        public bool AcceptCapturedMesh(BlockKey key)
        {
            lock (entriesLock)
            {
                return entries.TryGetValue(key, out FadeEntry entry) && entry.HiddenInTerrain;
            }
        }

        public void MarkMeshReady(BlockKey key)
        {
            lock (entriesLock)
            {
                if (entries.TryGetValue(key, out FadeEntry entry) && entry.HiddenInTerrain)
                {
                    entry.MeshReady = true;
                }
            }
        }

        public bool TryRemoveAfterRestore(BlockKey key)
        {
            lock (entriesLock)
            {
                if (!entries.TryGetValue(key, out FadeEntry entry) || entry.Wanted || entry.HiddenInTerrain)
                {
                    return false;
                }

                entries.Remove(key);
                return true;
            }
        }

        public void Clear()
        {
            lock (entriesLock)
            {
                entries.Clear();
                wanted.Clear();
            }
        }
    }
}

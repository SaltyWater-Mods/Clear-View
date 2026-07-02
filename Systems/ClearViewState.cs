using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using ClearView.Rendering;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.Client.NoObf;

namespace ClearView.Systems
{
    internal sealed class ClearViewState : IDisposable
    {
        private readonly ICoreClientAPI capi;
        private readonly ClientMain game;
        private readonly FadedBlockRenderer renderer;
        private readonly ConcurrentQueue<CapturedBlockMeshData> pendingMeshes = new ConcurrentQueue<CapturedBlockMeshData>();
        private readonly HashSet<BlockKey> replacedThisUpload = new HashSet<BlockKey>();
        private readonly List<BlockKey> redrawNow = new List<BlockKey>();
        private readonly List<BlockKey> restoreNow = new List<BlockKey>();
        private readonly ClearViewSettings settings;
        private readonly IgnoredMaterials ignoredMaterials;
        private readonly FadeEntries fadeEntries = new FadeEntries();

        public float TargetOpacity
        {
            get { return settings.TargetOpacity; }
            set { settings.TargetOpacity = value; }
        }

        public float HorizontalAreaMultiplier
        {
            get { return settings.HorizontalAreaMultiplier; }
            set { settings.HorizontalAreaMultiplier = value; }
        }

        public float VerticalAreaMultiplier
        {
            get { return settings.VerticalAreaMultiplier; }
            set { settings.VerticalAreaMultiplier = value; }
        }

        public bool AllowCameraInsideBlocks
        {
            get { return settings.AllowCameraInsideBlocks; }
            set { settings.AllowCameraInsideBlocks = value; }
        }

        public ClearViewState(ICoreClientAPI capi)
        {
            this.capi = capi;

            settings = capi.LoadModConfig<ClearViewSettings>("clearview.json") ?? new ClearViewSettings();
            ignoredMaterials = new IgnoredMaterials(settings);
            SaveSettings();
            game = (ClientMain)capi.World;

            // register faded blocks in oit
            renderer = new FadedBlockRenderer(capi, this);
            capi.Event.RegisterRenderer(renderer, EnumRenderStage.OIT, "clearview-faded-blocks");
            capi.Event.LeaveWorld += OnLeaveWorld;
        }

        public void SaveSettings()
        {
            ignoredMaterials.WriteToSettings();
            capi.StoreModConfig(settings, "clearview.json");
        }

        public bool IsMaterialIgnored(EnumBlockMaterial material)
        {
            return ignoredMaterials.IsIgnored(material);
        }

        public bool IsMaterialIgnored(Block block)
        {
            return ignoredMaterials.IsIgnored(block);
        }

        public void SetMaterialIgnored(EnumBlockMaterial material, bool ignored)
        {
            ignoredMaterials.SetIgnored(material, ignored);
            SaveSettings();
        }

        public bool ToggleMaterialIgnored(EnumBlockMaterial material)
        {
            bool ignored = ignoredMaterials.ToggleIgnored(material);
            SaveSettings();
            return ignored;
        }

        public string IgnoredMaterialsText()
        {
            return ignoredMaterials.Text();
        }

        public bool IsHiddenInTerrain(BlockKey key)
        {
            return fadeEntries.IsHiddenInTerrain(key);
        }

        public void SetWanted(HashSet<BlockKey> newWanted)
        {
            fadeEntries.SetWanted(newWanted, capi.InWorldEllapsedMilliseconds, redrawNow);
            for (int i = 0; i < redrawNow.Count; i++)
            {
                RedrawAround(redrawNow[i], true);
            }
        }

        public void OnRenderFrame(float dt)
        {
            UploadPendingMeshes();
            UpdateFades(dt);
        }

        public float OpacityFor(BlockKey key)
        {
            return fadeEntries.OpacityFor(key);
        }

        public HashSet<BlockKey> HiddenKeysSnapshot()
        {
            return fadeEntries.HiddenKeysSnapshot();
        }

        public void EnqueueCapturedMesh(CapturedBlockMeshData data)
        {
            pendingMeshes.Enqueue(data);
        }

        private void RedrawAround(BlockKey key, bool priority)
        {
            // redraw neighbour chunks too when the hidden block is on a chunk border
            QueueChunkRedraw(key, null, priority);

            if ((key.X & 31) == 0) QueueChunkRedraw(new BlockKey(key.X - 1, key.Y, key.Z, key.Dimension), null, priority);
            if ((key.X & 31) == 31) QueueChunkRedraw(new BlockKey(key.X + 1, key.Y, key.Z, key.Dimension), null, priority);
            if ((key.Y & 31) == 0) QueueChunkRedraw(new BlockKey(key.X, key.Y - 1, key.Z, key.Dimension), null, priority);
            if ((key.Y & 31) == 31) QueueChunkRedraw(new BlockKey(key.X, key.Y + 1, key.Z, key.Dimension), null, priority);
            if ((key.Z & 31) == 0) QueueChunkRedraw(new BlockKey(key.X, key.Y, key.Z - 1, key.Dimension), null, priority);
            if ((key.Z & 31) == 31) QueueChunkRedraw(new BlockKey(key.X, key.Y, key.Z + 1, key.Dimension), null, priority);
        }

        private void UpdateFades(float dt)
        {
            fadeEntries.Update(dt, TargetOpacity, capi.InWorldEllapsedMilliseconds, restoreNow);

            for (int i = 0; i < restoreNow.Count; i++)
            {
                BlockKey key = restoreNow[i];
                QueueChunkRedraw(key, () => RemoveAfterRestore(key), false);
            }
        }

        private void UploadPendingMeshes()
        {
            // mesh upload needs to happen from the render thread
            replacedThisUpload.Clear();

            while (pendingMeshes.TryDequeue(out CapturedBlockMeshData data))
            {
                if (!fadeEntries.AcceptCapturedMesh(data.Key))
                {
                    data.Mesh.Dispose();
                    continue;
                }

                if (replacedThisUpload.Add(data.Key))
                {
                    renderer.Remove(data.Key);
                }

                renderer.Add(data);
                fadeEntries.MarkMeshReady(data.Key);
            }
        }

        private void RemoveAfterRestore(BlockKey key)
        {
            if (fadeEntries.TryRemoveAfterRestore(key))
            {
                renderer.Remove(key);
            }
        }

        private void QueueChunkRedraw(BlockKey key, Action onRetesselated, bool priority)
        {
            game.WorldMap.MarkChunkDirty(key.ChunkX, key.ChunkY, key.ChunkZ, priority, false, onRetesselated, false);
        }

        private void OnLeaveWorld()
        {
            fadeEntries.Clear();
            renderer.Clear();
        }

        public void Dispose()
        {
            capi.Event.UnregisterRenderer(renderer, EnumRenderStage.OIT);
            capi.Event.LeaveWorld -= OnLeaveWorld;
            renderer.Dispose();
        }
    }
}

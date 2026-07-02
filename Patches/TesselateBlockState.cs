using ClearView.Systems;
using Vintagestory.API.Client;

namespace ClearView.Patches
{
    internal readonly struct TesselateBlockState
    {
        public readonly bool Active;
        public readonly BlockKey Key;
        public readonly MeshData[][][] OriginalPools;
        public readonly MeshData[][][] ScratchPools;
        public readonly int[] TextureIds;

        public TesselateBlockState(BlockKey key, MeshData[][][] originalPools, MeshData[][][] scratchPools, int[] textureIds)
        {
            Active = true;
            Key = key;
            OriginalPools = originalPools;
            ScratchPools = scratchPools;
            TextureIds = textureIds;
        }
    }
}

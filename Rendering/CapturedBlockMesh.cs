using Vintagestory.API.Client;

namespace ClearView.Rendering
{
    internal sealed class CapturedBlockMesh
    {
        // gpu mesh for a block currently rendered by clear view
        public readonly int AtlasTextureId;
        public readonly MeshDataPoolMasterManager MasterPool;
        public readonly MeshDataPoolManager Pool;

        public CapturedBlockMesh(int atlasTextureId, MeshDataPoolMasterManager masterPool, MeshDataPoolManager pool)
        {
            AtlasTextureId = atlasTextureId;
            MasterPool = masterPool;
            Pool = pool;
        }

        public void Dispose(ICoreClientAPI capi)
        {
            MasterPool.DisposeAllPools(capi);
        }
    }
}

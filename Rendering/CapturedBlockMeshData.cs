using ClearView.Systems;
using Vintagestory.API.Client;

namespace ClearView.Rendering
{
    internal sealed class CapturedBlockMeshData
    {
        // cpu mesh passed from tessellation to render upload
        public readonly BlockKey Key;
        public readonly int AtlasTextureId;
        public readonly MeshData Mesh;

        public CapturedBlockMeshData(BlockKey key, int atlasTextureId, MeshData mesh)
        {
            Key = key;
            AtlasTextureId = atlasTextureId;
            Mesh = mesh;
        }
    }
}

using System;
using System.Collections.Generic;
using ClearView.Systems;
using Vintagestory.API.Client;
using Vintagestory.API.Config;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace ClearView.Rendering
{
    internal sealed class FadedBlockRenderer : IRenderer
    {
        private readonly ICoreClientAPI capi;
        private readonly ClientMain game;
        private readonly ClearViewState state;
        private readonly Dictionary<BlockKey, List<CapturedBlockMesh>> meshesByBlock = new Dictionary<BlockKey, List<CapturedBlockMesh>>();
        private static readonly HashSet<BlockKey> EmptyWanted = new HashSet<BlockKey>();

        public double RenderOrder => 0.375;
        public int RenderRange => 128;

        public FadedBlockRenderer(ICoreClientAPI capi, ClearViewState state)
        {
            this.capi = capi;
            this.state = state;
            game = (ClientMain)capi.World;
        }

        public void Add(CapturedBlockMeshData data)
        {
            // one mesh pool per faded block so removal stays simple
            if (!meshesByBlock.TryGetValue(data.Key, out List<CapturedBlockMesh> meshes))
            {
                meshes = new List<CapturedBlockMesh>();
                meshesByBlock[data.Key] = meshes;
            }
            else
            {
                RemoveAtlas(meshes, data.AtlasTextureId);
            }

            MeshDataPoolMasterManager masterPool = new MeshDataPoolMasterManager(capi);
            MeshDataPoolManager pool = new MeshDataPoolManager(
                masterPool,
                capi.Render.DefaultFrustumCuller,
                capi,
                Math.Max(1, data.Mesh.VerticesCount),
                Math.Max(1, data.Mesh.IndicesCount),
                1,
                null,
                null,
                null,
                new CustomMeshDataPartInt()
                {
                    InterleaveOffsets = new[] { 0 },
                    InterleaveSizes = new[] { 1 },
                    InterleaveStride = 4,
                    Conversion = DataConversion.Integer
                }
            );

            Vec3i origin = ChunkOrigin(data.Key);
            pool.AddModel(data.Mesh, origin, data.Key.Dimension, Sphere.BoundingSphereForCube(data.Key.X, data.Key.InternalY, data.Key.Z, 1));
            data.Mesh.Dispose();
            meshes.Add(new CapturedBlockMesh(data.AtlasTextureId, masterPool, pool));
        }

        public void Remove(BlockKey key)
        {
            if (!meshesByBlock.TryGetValue(key, out List<CapturedBlockMesh> meshes))
            {
                return;
            }

            for (int i = 0; i < meshes.Count; i++)
            {
                meshes[i].Dispose(capi);
            }

            meshesByBlock.Remove(key);
        }

        private void RemoveAtlas(List<CapturedBlockMesh> meshes, int atlasTextureId)
        {
            for (int i = meshes.Count - 1; i >= 0; i--)
            {
                if (meshes[i].AtlasTextureId == atlasTextureId)
                {
                    meshes[i].Dispose(capi);
                    meshes.RemoveAt(i);
                }
            }
        }

        public void Clear()
        {
            foreach (List<CapturedBlockMesh> meshes in meshesByBlock.Values)
            {
                for (int i = 0; i < meshes.Count; i++)
                {
                    meshes[i].Dispose(capi);
                }
            }

            meshesByBlock.Clear();
        }

        public void OnRenderFrame(float deltaTime, EnumRenderStage stage)
        {
            if (capi.Render.CameraType == EnumCameraMode.FirstPerson)
            {
                state.SetWanted(EmptyWanted);
            }

            state.OnRenderFrame(deltaTime);
            if (meshesByBlock.Count == 0)
            {
                return;
            }

            IRenderAPI rapi = capi.Render;
            Vec3d camPos = game.EntityPlayer.CameraPos;
            // reuse vanilla transparent chunk shader here
            ShaderProgramChunktransparent prog = ShaderPrograms.Chunktransparent;

            game.GlPushMatrix();
            game.GlLoadMatrix(game.MainCamera.CameraMatrixOrigin);

            rapi.GlDisableCullFace();
            rapi.GLEnableDepthTest();

            // render transparent mesh without writing new depth
            rapi.GLDepthMask(false);

            prog.Use();
            ApplyChunkTransparentUniforms(prog);

            foreach (KeyValuePair<BlockKey, List<CapturedBlockMesh>> pair in meshesByBlock)
            {
                // state stores opacity, shader uses transparency
                float forcedTransparency = 1f - state.OpacityFor(pair.Key);
                List<CapturedBlockMesh> meshes = pair.Value;

                for (int i = 0; i < meshes.Count; i++)
                {
                    prog.ForcedTransparency = forcedTransparency;
                    prog.TerrainTex2D = meshes[i].AtlasTextureId;
                    meshes[i].Pool.Render(camPos, "origin", EnumFrustumCullMode.NoCull);
                }
            }

            prog.ForcedTransparency = 0f;
            prog.Stop();

            rapi.GLDepthMask(true);
            rapi.GlEnableCullFace();
            game.GlPopMatrix();
        }

        private void ApplyChunkTransparentUniforms(ShaderProgramChunktransparent prog)
        {
            prog.RgbaFogIn = game.AmbientManager.BlendedFogColor;
            prog.RgbaAmbientIn = game.AmbientManager.BlendedAmbientColor;
            prog.FogDensityIn = game.AmbientManager.BlendedFogDensity;
            prog.FogMinIn = game.AmbientManager.BlendedFogMin;
            prog.ProjectionMatrix = game.CurrentProjectionMatrix;
            prog.ModelViewMatrix = game.CurrentModelViewMatrix;
            prog.SubpixelPaddingX = game.BlockAtlasManager.SubPixelPaddingX;
            prog.SubpixelPaddingY = game.BlockAtlasManager.SubPixelPaddingY;
            prog.LightPosition = game.shUniforms.LightPosition3D;
            prog.ShadowIntensity = game.shUniforms.DropShadowIntensity;
            prog.ShadowRangeFar = game.shUniforms.ShadowRangeFar;
            prog.ShadowRangeNear = game.shUniforms.ShadowRangeNear;
            prog.ToShadowMapSpaceMatrixFar = game.shUniforms.ToShadowMapSpaceMatrixFar;
            prog.ToShadowMapSpaceMatrixNear = game.shUniforms.ToShadowMapSpaceMatrixNear;
            prog.WindWaveCounter = game.shUniforms.WindWaveCounter;
            prog.WindWaveCounterHighFreq = game.shUniforms.WindWaveCounterHighFreq;
            prog.WindSpeed = game.shUniforms.WindSpeed;
            prog.Playerpos = game.shUniforms.PlayerPos;
            prog.GlobalWarpIntensity = game.shUniforms.GlobalWorldWarp;
            prog.GlitchWaviness = game.shUniforms.GlitchWaviness;
            prog.WindWaveIntensity = game.shUniforms.WindWaveIntensity;
            prog.WaterWaveIntensity = game.shUniforms.WaterWaveIntensity;
            prog.PerceptionEffectId = game.shUniforms.PerceptionEffectId;
            prog.PerceptionEffectIntensity = game.shUniforms.PerceptionEffectIntensity;
            prog.ColorMapRectsArray(GlobalConstants.MaxColorMaps, game.shUniforms.ColorMapRects4);
            prog.SeasonRel = game.shUniforms.SeasonRel;
            prog.SeaLevel = game.shUniforms.SeaLevel;
            prog.AtlasHeight = game.shUniforms.BlockAtlasHeight;
            prog.SeasonTemperature = game.shUniforms.SeasonTemperature;
        }

        public void Dispose()
        {
            Clear();
        }

        private static Vec3i ChunkOrigin(BlockKey key)
        {
            return new Vec3i(
                key.ChunkX << 5,
                (key.ChunkY << 5) - key.Dimension * BlockPos.DimensionBoundary,
                key.ChunkZ << 5
            );
        }
    }
}

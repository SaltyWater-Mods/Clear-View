using System;
using System.Reflection;
using ClearView.Rendering;
using ClearView.Systems;
using HarmonyLib;
using Vintagestory.API.Client;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.Common;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace ClearView.Patches
{
    // capture the block while vanilla is already tessellating the chunk
    [HarmonyPatch]
    internal static class ChunkTesselatorTesselateBlockPatch
    {
        private static readonly AccessTools.FieldRef<ChunkTesselator, TCTCache> Vars = AccessTools.FieldRefAccess<ChunkTesselator, TCTCache>("vars");
        private static readonly AccessTools.FieldRef<ChunkTesselator, MeshData[][][]> CurrentPools = AccessTools.FieldRefAccess<ChunkTesselator, MeshData[][][]>("currentModeldataByRenderPassByLodLevel");
        private static readonly AccessTools.FieldRef<ChunkTesselator, int> QuantityAtlasses = AccessTools.FieldRefAccess<ChunkTesselator, int>("quantityAtlasses");
        private static readonly AccessTools.FieldRef<ChunkTesselator, int[]> TextureIds = AccessTools.FieldRefAccess<ChunkTesselator, int[]>("TextureIdToReturnNum");
        private static readonly EnumChunkRenderPass[] Passes = (EnumChunkRenderPass[])Enum.GetValues(typeof(EnumChunkRenderPass));

        [ThreadStatic]
        private static MeshData[][][] scratchPools;

        [ThreadStatic]
        private static int scratchAtlases;

        public static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ChunkTesselator), "TesselateBlock", new[]
            {
                typeof(Block), typeof(int), typeof(int), typeof(int), typeof(int), typeof(int)
            });
        }

        public static void Prefix(ChunkTesselator __instance, Block block, int lX, int faceflags, int posX, int posZ, int drawType, ref TesselateBlockState __state)
        {
            ClearViewState state = ClearViewModSystem.State;
            if (state == null || !FadeableBlock.CanBeCaptured(block, state))
            {
                return;
            }

            TCTCache vars = Vars(__instance);
            BlockKey key = new BlockKey(posX, vars.posY, posZ, vars.dimension);
            if (!state.IsHiddenInTerrain(key))
            {
                return;
            }

            // send this block to scratch pools and let vanilla build the mesh
            MeshData[][][] scratch = GetScratchPools(QuantityAtlasses(__instance));
            Clear(scratch);

            __state = new TesselateBlockState(key, CurrentPools(__instance), scratch, TextureIds(__instance));
            CurrentPools(__instance) = scratch;
        }

        public static void Postfix(ChunkTesselator __instance, TesselateBlockState __state)
        {
            // restore vanilla pools first then keep the captured mesh
            if (!__state.Active)
            {
                return;
            }

            CurrentPools(__instance) = __state.OriginalPools;
            Capture(__state);
        }

        public static void Finalizer(ChunkTesselator __instance, TesselateBlockState __state)
        {
            // always restore the pools even if tessellation fails
            if (__state.Active)
            {
                CurrentPools(__instance) = __state.OriginalPools;
            }
        }

        private static MeshData[][][] GetScratchPools(int atlasCount)
        {
            if (scratchPools != null && scratchAtlases == atlasCount)
            {
                return scratchPools;
            }

            scratchAtlases = atlasCount;
            scratchPools = new MeshData[ChunkTesselator.LODPOOLS][][];
            for (int lod = 0; lod < ChunkTesselator.LODPOOLS; lod++)
            {
                scratchPools[lod] = new MeshData[Passes.Length][];
                for (int pass = 0; pass < Passes.Length; pass++)
                {
                    scratchPools[lod][pass] = new MeshData[atlasCount];
                    for (int atlas = 0; atlas < atlasCount; atlas++)
                    {
                        scratchPools[lod][pass][atlas] = NewScratchMesh((EnumChunkRenderPass)pass);
                    }
                }
            }

            return scratchPools;
        }

        private static MeshData NewScratchMesh(EnumChunkRenderPass pass)
        {
            MeshData mesh = new MeshData(128, 192, false, true, true, true);

            if (pass == EnumChunkRenderPass.Liquid)
            {
                mesh.CustomFloats = new CustomMeshDataPartFloat(256);
                mesh.CustomInts = new CustomMeshDataPartInt(256);
                return mesh;
            }

            mesh.CustomInts = new CustomMeshDataPartInt(128);
            if (pass == EnumChunkRenderPass.TopSoil)
            {
                mesh.CustomShorts = new CustomMeshDataPartShort(256);
            }

            return mesh;
        }

        private static void Clear(MeshData[][][] pools)
        {
            for (int lod = 0; lod < pools.Length; lod++)
            {
                for (int pass = 0; pass < pools[lod].Length; pass++)
                {
                    for (int atlas = 0; atlas < pools[lod][pass].Length; atlas++)
                    {
                        pools[lod][pass][atlas].Clear();
                    }
                }
            }
        }

        private static void Capture(TesselateBlockState state)
        {
            // here one block can write into more than one atlas or render pass
            ClearViewState clearView = ClearViewModSystem.State;
            if (clearView == null)
            {
                return;
            }

            for (int atlas = 0; atlas < state.TextureIds.Length; atlas++)
            {
                MeshData combined = null;

                for (int lod = 0; lod <= (int)EnumLodPool.EverywhereExceptFar; lod++)
                {
                    for (int pass = 0; pass < Passes.Length; pass++)
                    {
                        if (!FadeableBlock.CanRenderCapturedPass((EnumChunkRenderPass)pass))
                        {
                            continue;
                        }

                        MeshData mesh = state.ScratchPools[lod][pass][atlas];
                        if (mesh.VerticesCount == 0)
                        {
                            continue;
                        }

                        if (combined == null)
                        {
                            combined = mesh.Clone();
                        }
                        else
                        {
                            combined.AddMeshData(mesh);
                        }
                    }
                }

                if (combined != null)
                {
                    clearView.EnqueueCapturedMesh(new CapturedBlockMeshData(state.Key, state.TextureIds[atlas], combined));
                }
            }
        }
    }
}

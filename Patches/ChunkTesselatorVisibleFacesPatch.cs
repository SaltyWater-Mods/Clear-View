using System.Collections.Generic;
using ClearView.Systems;
using HarmonyLib;
using Vintagestory.API.Client.Tesselation;
using Vintagestory.API.MathTools;
using Vintagestory.Client.NoObf;

namespace ClearView.Patches
{
    // when a faded block leaves terrain, neighbour faces may need to come back
    [HarmonyPatch(typeof(ChunkTesselator), nameof(ChunkTesselator.CalculateVisibleFaces))]
    internal static class ChunkTesselatorVisibleFacesPatch
    {
        private static readonly AccessTools.FieldRef<ChunkTesselator, TCTCache> Vars = AccessTools.FieldRefAccess<ChunkTesselator, TCTCache>("vars");
        private static readonly AccessTools.FieldRef<ChunkTesselator, byte[]> CurrentChunkDraw = AccessTools.FieldRefAccess<ChunkTesselator, byte[]>("currentChunkDraw32");

        public static void Postfix(ChunkTesselator __instance, int baseX, int baseY, int baseZ, ref bool __result)
        {
            ClearViewState state = ClearViewModSystem.State;
            if (state == null)
            {
                return;
            }

            TCTCache vars = Vars(__instance);
            byte[] draw = CurrentChunkDraw(__instance);
            HashSet<BlockKey> keys = state.HiddenKeysSnapshot();

            foreach (BlockKey key in keys)
            {
                if (key.Dimension != vars.dimension)
                {
                    continue;
                }

                int lx = key.X - baseX;
                int ly = key.Y - baseY;
                int lz = key.Z - baseZ;

                for (int side = 0; side < TileSideEnum.SideCount; side++)
                {
                    FastVec3i offset = TileSideEnum.OffsetByTileSide[side];
                    int nx = lx + offset.X;
                    int ny = ly + offset.Y;
                    int nz = lz + offset.Z;
                    if (!InsideChunk(nx, ny, nz) || keys.Contains(new BlockKey(key.X + offset.X, key.Y + offset.Y, key.Z + offset.Z, key.Dimension)))
                    {
                        continue;
                    }

                    // only force the face next to the hidden block
                    draw[Index(nx, ny, nz)] |= (byte)TileSideEnum.ToFlags(TileSideEnum.GetOpposite(side));
                    __result = true;
                }
            }
        }

        private static bool InsideChunk(int x, int y, int z)
        {
            return x >= 0 && x < 32 && y >= 0 && y < 32 && z >= 0 && z < 32;
        }

        private static int Index(int x, int y, int z)
        {
            return (y * 32 + z) * 32 + x;
        }
    }
}

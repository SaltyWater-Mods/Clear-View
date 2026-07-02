using System;
using Vintagestory.API.MathTools;

namespace ClearView.Systems
{
    internal readonly struct BlockKey : IEquatable<BlockKey>
    {
        public readonly int X;
        public readonly int Y;
        public readonly int Z;
        public readonly int Dimension;

        // dimension is stored inside the internal y value
        public int InternalY => Y + Dimension * BlockPos.DimensionBoundary;
        public int ChunkX => X >> 5; // chunk coords from 32 block chunks
        public int ChunkY => InternalY >> 5;
        public int ChunkZ => Z >> 5;

        public BlockKey(int x, int y, int z, int dimension)
        {
            X = x;
            Y = y;
            Z = z;
            Dimension = dimension;
        }

        public BlockKey(BlockPos pos)
        {
            X = pos.X;
            Y = pos.Y;
            Z = pos.Z;
            Dimension = pos.dimension;
        }

        public bool Equals(BlockKey other)
        {
            return X == other.X && Y == other.Y && Z == other.Z && Dimension == other.Dimension;
        }

        public override bool Equals(object obj)
        {
            return obj is BlockKey other && Equals(other);
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(X, Y, Z, Dimension);
        }
    }
}

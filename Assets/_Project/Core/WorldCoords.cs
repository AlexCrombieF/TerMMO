using UnityEngine;

namespace Doodgy.Core
{
    /// <summary>
    /// Pure coordinate conversions between the three spaces we work in:
    ///   - world space   : Unity world position (Vector3, floats)
    ///   - tile space    : integer tile grid (Vector2Int), 1 tile == TileSize units
    ///   - chunk space   : integer chunk grid (Vector2Int)
    /// plus the local index inside a chunk's flat array.
    ///
    /// All math is floor-based so it behaves correctly for NEGATIVE coordinates
    /// (the world extends left of and below the origin). Kept dependency-free so
    /// it can be unit-tested without a scene.
    /// </summary>
    public static class WorldCoords
    {
        private const int Size = WorldConstants.ChunkSize;

        /// <summary>Integer division that rounds toward negative infinity.</summary>
        public static int FloorDiv(int a, int b)
        {
            int q = a / b;
            // C# truncates toward zero; correct the result when the signs differ
            // and there is a remainder (i.e. we rounded the wrong way).
            if ((a % b != 0) && ((a < 0) != (b < 0))) q--;
            return q;
        }

        /// <summary>Which chunk a tile belongs to.</summary>
        public static Vector2Int TileToChunk(Vector2Int tile)
            => new Vector2Int(FloorDiv(tile.x, Size), FloorDiv(tile.y, Size));

        /// <summary>Tile's local position within its chunk, always in [0, Size).</summary>
        public static Vector2Int TileToLocal(Vector2Int tile)
        {
            int cx = FloorDiv(tile.x, Size);
            int cy = FloorDiv(tile.y, Size);
            return new Vector2Int(tile.x - cx * Size, tile.y - cy * Size);
        }

        /// <summary>Flat-array index for a local (lx, ly) inside a chunk.</summary>
        public static int LocalIndex(int lx, int ly) => ly * Size + lx;

        /// <summary>World position -> tile coordinate (floored).</summary>
        public static Vector2Int WorldToTile(Vector3 world)
            => new Vector2Int(
                Mathf.FloorToInt(world.x / WorldConstants.TileSize),
                Mathf.FloorToInt(world.y / WorldConstants.TileSize));

        /// <summary>Tile coordinate -> world position of its bottom-left corner.</summary>
        public static Vector3 TileToWorld(Vector2Int tile)
            => new Vector3(tile.x * WorldConstants.TileSize,
                           tile.y * WorldConstants.TileSize, 0f);
    }
}

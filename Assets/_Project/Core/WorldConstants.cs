namespace Doodgy.Core
{
    /// <summary>
    /// Global, compile-time world constants. Centralised so a single change here
    /// propagates everywhere (chunk arrays, coordinate math, save format).
    /// </summary>
    public static class WorldConstants
    {
        /// <summary>Tiles per chunk edge. A chunk is ChunkSize x ChunkSize tiles.</summary>
        public const int ChunkSize = 32;

        /// <summary>Total tiles in one chunk (cached to avoid repeat multiplies).</summary>
        public const int TilesPerChunk = ChunkSize * ChunkSize;

        /// <summary>Reserved id meaning "empty / air". Never assign to a real tile.</summary>
        public const ushort AirTileId = 0;

        /// <summary>World units per tile. Matches the Grid cell size (1 unit cells).</summary>
        public const float TileSize = 1f;
    }
}

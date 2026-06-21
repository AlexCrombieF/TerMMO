using UnityEngine;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Raised by <see cref="World"/> whenever a tile changes. Lets other systems
    /// (lighting, save, audio, networking) react without polling the grid. A
    /// readonly struct so subscribing is allocation-free.
    /// </summary>
    public readonly struct TileChangedEvent
    {
        public readonly Vector2Int Tile;
        public readonly ushort PreviousId;
        public readonly ushort NewId;

        public TileChangedEvent(Vector2Int tile, ushort previousId, ushort newId)
        {
            Tile = tile;
            PreviousId = previousId;
            NewId = newId;
        }

        public bool WasPlaced => PreviousId == Core.WorldConstants.AirTileId
                                 && NewId != Core.WorldConstants.AirTileId;

        public bool WasDestroyed => PreviousId != Core.WorldConstants.AirTileId
                                    && NewId == Core.WorldConstants.AirTileId;
    }
}

using UnityEngine;
using Doodgy.Core;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Pure data container for one chunk's tiles — a flat ushort array of
    /// length <see cref="WorldConstants.TilesPerChunk"/>. Deliberately NOT a
    /// MonoBehaviour: this is the unit we will serialize to disk (saves) and
    /// send over the network. Rendering lives in <see cref="ChunkRenderer"/>.
    ///
    /// Tiles are indexed [ly * ChunkSize + lx], row-major, local coords in
    /// [0, ChunkSize).
    /// </summary>
    public sealed class Chunk
    {
        /// <summary>Chunk-space coordinate (which chunk in the world grid).</summary>
        public Vector2Int Coord { get; }

        /// <summary>True if a tile changed since the last <see cref="ClearDirty"/>.
        /// The save system uses this to persist only modified chunks.</summary>
        public bool Dirty { get; private set; }

        private readonly ushort[] _tiles;

        public Chunk(Vector2Int coord)
        {
            Coord = coord;
            _tiles = new ushort[WorldConstants.TilesPerChunk];
        }

        public ushort GetLocal(int lx, int ly) => _tiles[WorldCoords.LocalIndex(lx, ly)];

        /// <summary>Sets a local tile; returns true if the value actually changed.</summary>
        public bool SetLocal(int lx, int ly, ushort id)
        {
            int i = WorldCoords.LocalIndex(lx, ly);
            if (_tiles[i] == id) return false;
            _tiles[i] = id;
            Dirty = true;
            return true;
        }

        /// <summary>Raw backing array — exposed for bulk fill / save / load only.</summary>
        public ushort[] Raw => _tiles;

        public void MarkDirty() => Dirty = true;
        public void ClearDirty() => Dirty = false;
    }
}

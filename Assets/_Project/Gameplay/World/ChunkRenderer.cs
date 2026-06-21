using UnityEngine;
using UnityEngine.Tilemaps;
using Doodgy.Core;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Presentation for a single <see cref="Chunk"/>: owns the Tilemap and pushes
    /// chunk data into it. Created at runtime by <see cref="World"/>. Uses ABSOLUTE
    /// tile coordinates as cell positions, so all chunk tilemaps share the parent
    /// Grid's coordinate space and line up seamlessly.
    /// </summary>
    [RequireComponent(typeof(Tilemap))]
    public sealed class ChunkRenderer : MonoBehaviour
    {
        private Tilemap _tilemap;

        private void Awake() => _tilemap = GetComponent<Tilemap>();

        /// <summary>
        /// Full (re)draw of the chunk in one batched <c>SetTiles</c> call — far
        /// cheaper than per-cell SetTile. Called once when the chunk is built.
        /// </summary>
        public void RenderAll(Chunk chunk, TileDatabase db, RuntimeTileResolver resolver)
        {
            int size = WorldConstants.ChunkSize;
            int baseX = chunk.Coord.x * size;
            int baseY = chunk.Coord.y * size;

            var positions = new Vector3Int[WorldConstants.TilesPerChunk];
            var tiles = new TileBase[WorldConstants.TilesPerChunk];

            for (int ly = 0; ly < size; ly++)
            {
                for (int lx = 0; lx < size; lx++)
                {
                    int idx = ly * size + lx;
                    ushort id = chunk.GetLocal(lx, ly);
                    positions[idx] = new Vector3Int(baseX + lx, baseY + ly, 0);
                    tiles[idx] = id == WorldConstants.AirTileId ? null : resolver.Resolve(db.Get(id));
                }
            }

            _tilemap.SetTiles(positions, tiles);
        }

        /// <summary>Updates a single absolute tile coordinate after an edit.</summary>
        public void SetTile(Vector2Int tileCoord, ushort id, TileDatabase db, RuntimeTileResolver resolver)
        {
            var pos = new Vector3Int(tileCoord.x, tileCoord.y, 0);
            TileBase tb = id == WorldConstants.AirTileId ? null : resolver.Resolve(db.Get(id));
            _tilemap.SetTile(pos, tb);
        }
    }
}

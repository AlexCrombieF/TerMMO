using UnityEngine;
using UnityEngine.Tilemaps;
using Doodgy.Core;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Runtime tile that picks its sprite from its horizontal neighbours: edge
    /// art when the tile beside it is air (grass tufts hanging over a cliff),
    /// normal art otherwise. Reads neighbours through the World rather than the
    /// Tilemap so it works across chunk boundaries (each chunk is its own
    /// Tilemap). Variant choice is a stable position hash, so it doesn't
    /// flicker on refresh.
    /// </summary>
    public sealed class EdgeAwareTile : TileBase
    {
        /// <summary>Set by World so tiles can query neighbours globally.</summary>
        public static World WorldRef;

        public Sprite Normal;
        public Sprite[] LeftEdges;
        public Sprite[] RightEdges;
        public Color Color = Color.white;
        public Tile.ColliderType ColliderType = Tile.ColliderType.Grid;

        public override void GetTileData(Vector3Int position, ITilemap tilemap,
                                         ref UnityEngine.Tilemaps.TileData tileData)
        {
            Sprite sprite = Normal;

            if (WorldRef != null)
            {
                // Edge art only on a REAL drop (2+ tiles of air beside us) —
                // single-tile stair-steps keep the normal sprite, otherwise the
                // whole surface turns into a ragged mess of overhanging tufts.
                if (IsCliff(position.x - 1, position.y) && LeftEdges != null && LeftEdges.Length > 0)
                    sprite = Pick(LeftEdges, position);
                else if (IsCliff(position.x + 1, position.y) && RightEdges != null && RightEdges.Length > 0)
                    sprite = Pick(RightEdges, position);
            }

            tileData.sprite = sprite;
            tileData.color = Color;
            tileData.colliderType = ColliderType;
        }

        // Air beside us at our level AND below it => a 2+ tile drop, a true edge.
        private static bool IsCliff(int x, int y)
            => WorldRef.GetTile(new Vector2Int(x, y)) == WorldConstants.AirTileId
            && WorldRef.GetTile(new Vector2Int(x, y - 1)) == WorldConstants.AirTileId;

        private static Sprite Pick(Sprite[] options, Vector3Int p)
        {
            if (options.Length == 1) return options[0];
            unchecked
            {
                int h = Mathf.Abs(p.x * 73856093 ^ p.y * 19349663);
                return options[h % options.Length];
            }
        }
    }
}

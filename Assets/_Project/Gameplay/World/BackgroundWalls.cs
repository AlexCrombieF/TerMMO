using UnityEngine;
using UnityEngine.Tilemaps;
using Doodgy.Core;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Background wall layer: fills everything below the surface with a darkened
    /// cave-wall tile on its own Tilemap, rendered behind the terrain. Mining a
    /// block underground reveals the wall behind it instead of sky — the world
    /// reads as solid ground you're digging INTO. Rebuilt on world generation;
    /// tile edits don't affect it (walls sit behind everything).
    /// </summary>
    public sealed class BackgroundWalls : MonoBehaviour
    {
        [SerializeField] private World world;
        [SerializeField] private Sprite wallSprite;
        [Tooltip("Darkening tint so walls read as background, not floating blocks.")]
        [SerializeField] private Color tint = new Color(0.42f, 0.42f, 0.48f, 1f);
        [Tooltip("Walls start this many tiles below the surface tile, so mining " +
                 "the very top block still shows sky behind it.")]
        [Min(0)] [SerializeField] private int startDepth = 1;
        [SerializeField] private int sortingOrder = -10; // behind chunk tilemaps (0)

        private Tilemap _map;

        private void OnEnable()
        {
            if (world != null) world.OnWorldGenerated += Build;
        }

        private void OnDisable()
        {
            if (world != null) world.OnWorldGenerated -= Build;
        }

        private void Build()
        {
            if (wallSprite == null) return; // no wall art yet

            if (_map == null)
            {
                var go = new GameObject("BackgroundWalls");
                go.transform.SetParent(transform, false); // under the World's Grid
                _map = go.AddComponent<Tilemap>();
                var rend = go.AddComponent<TilemapRenderer>();
                rend.sortingOrder = sortingOrder;
            }
            _map.ClearAllTiles();

            // One runtime tile, tinted; no collider — it's scenery.
            Rect r = wallSprite.rect;
            var normalized = Sprite.Create(wallSprite.texture, r, new Vector2(0.5f, 0.5f),
                                           Mathf.Max(r.width, r.height), 0, SpriteMeshType.FullRect);
            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = normalized;
            tile.color = tint;
            tile.colliderType = Tile.ColliderType.None;

            int w = world.WidthInTiles;
            int h = world.HeightInTiles;
            var positions = new System.Collections.Generic.List<Vector3Int>(w * 64);

            for (int x = 0; x < w; x++)
            {
                // Topmost solid tile in the column = the surface.
                int surface = -1;
                for (int y = h - 1; y >= 0; y--)
                {
                    if (world.GetTile(new Vector2Int(x, y)) != WorldConstants.AirTileId)
                    {
                        surface = y;
                        break;
                    }
                }
                if (surface < 0) continue;

                for (int y = surface - startDepth; y >= 0; y--)
                    positions.Add(new Vector3Int(x, y, 0));
            }

            var tiles = new TileBase[positions.Count];
            for (int i = 0; i < tiles.Length; i++) tiles[i] = tile;
            _map.SetTiles(positions.ToArray(), tiles);
        }
    }
}

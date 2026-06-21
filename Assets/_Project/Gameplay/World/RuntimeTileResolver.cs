using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using Doodgy.Data;
// UnityEngine.Tilemaps also defines a 'TileData' struct; alias ours to avoid the clash.
using TileData = Doodgy.Data.TileData;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Resolves a <see cref="TileData"/> to the <see cref="TileBase"/> the
    /// Tilemap actually draws, caching one result per tile id.
    ///
    /// If the TileData has an authored <c>TileAsset</c> (a real Tile/RuleTile),
    /// that is used. Otherwise a solid-colour placeholder Tile is generated from
    /// the tile's <c>Tint</c> — so the world renders with ZERO art during early
    /// development. Drop in real tile assets later and they take over with no
    /// code change.
    /// </summary>
    public sealed class RuntimeTileResolver
    {
        private const int PlaceholderPixels = 16; // 16px sprite @ 16 PPU == 1 world unit

        private readonly Dictionary<ushort, TileBase> _cache = new Dictionary<ushort, TileBase>();

        public TileBase Resolve(TileData data)
        {
            if (data == null) return null;
            if (_cache.TryGetValue(data.Id, out TileBase cached)) return cached;

            TileBase result = data.TileAsset != null ? data.TileAsset : CreatePlaceholder(data);
            _cache[data.Id] = result;
            return result;
        }

        private static Tile CreatePlaceholder(TileData data)
        {
            int px = PlaceholderPixels;
            var tex = new Texture2D(px, px, TextureFormat.RGBA32, mipChain: false)
            {
                filterMode = FilterMode.Point,   // crisp pixel-art look
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[px * px];
            Color32 c = data.Tint;
            for (int i = 0; i < pixels.Length; i++) pixels[i] = c;
            tex.SetPixels32(pixels);
            tex.Apply();

            // Center pivot + PPU == pixel size makes one sprite exactly fill one cell.
            var sprite = Sprite.Create(tex, new Rect(0, 0, px, px), new Vector2(0.5f, 0.5f), px);

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = sprite;
            tile.colliderType = data.IsSolid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
            return tile;
        }
    }
}

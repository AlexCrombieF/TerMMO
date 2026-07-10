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

            TileBase result;
            if (data.TileAsset != null) result = data.TileAsset;          // authored Tile/RuleTile
            else if (data.AnimationSprites != null && data.AnimationSprites.Length > 1)
                result = CreateAnimatedTile(data);                         // frame loop (torch)
            else if (data.HasEdgeSprites && data.Sprite != null)
                result = CreateEdgeTile(data);                             // neighbour-aware (grass)
            else if (data.Sprite != null) result = CreateSpriteTile(data); // raw sprite -> runtime tile
            else result = CreatePlaceholder(data);                         // colour fallback

            _cache[data.Id] = result;
            return result;
        }

        /// <summary>
        /// Builds an animated tile from the TileData's frames. Frames are rebuilt
        /// pixel-true at 16 px = 1 tile (a 12x20 torch slightly overhangs its cell,
        /// which reads nicely for flames).
        /// </summary>
        private static AnimatedRuntimeTile CreateAnimatedTile(TileData data)
        {
            Sprite[] src = data.AnimationSprites;
            var frames = new Sprite[src.Length];
            for (int i = 0; i < src.Length; i++)
            {
                Sprite s = src[i];
                frames[i] = Sprite.Create(s.texture, s.rect, new Vector2(0.5f, 0.5f),
                                          PlaceholderPixels, 0, SpriteMeshType.FullRect);
                frames[i].name = $"{data.DisplayName}_f{i}";
            }

            var tile = ScriptableObject.CreateInstance<AnimatedRuntimeTile>();
            tile.Frames = frames;
            tile.Fps = data.AnimationFps;
            tile.Color = data.Tint;
            tile.ColliderType = data.IsSolid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
            return tile;
        }

        /// <summary>Builds a neighbour-aware tile (grass edges over cliffs).</summary>
        private static EdgeAwareTile CreateEdgeTile(TileData data)
        {
            var tile = ScriptableObject.CreateInstance<EdgeAwareTile>();
            tile.Normal = Normalize(data.Sprite, data.DisplayName + "_tile");
            tile.LeftEdges = NormalizeAll(data.EdgeLeftSprites, data.DisplayName + "_L");
            tile.RightEdges = NormalizeAll(data.EdgeRightSprites, data.DisplayName + "_R");
            tile.Color = data.Tint;
            tile.ColliderType = data.IsSolid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
            return tile;
        }

        private static Sprite[] NormalizeAll(Sprite[] src, string prefix)
        {
            if (src == null) return System.Array.Empty<Sprite>();
            var result = new Sprite[src.Length];
            for (int i = 0; i < src.Length; i++)
                if (src[i] != null) result[i] = Normalize(src[i], $"{prefix}{i}");
            return result;
        }

        /// <summary>Drops cached tiles so reassigned sprites are picked up on the next render.</summary>
        public void Clear() => _cache.Clear();

        /// <summary>
        /// Rebuilds a sprite centred with PPU == its longest side, so it sits
        /// centred in the cell and its longest edge spans exactly one tile.
        /// </summary>
        private static Sprite Normalize(Sprite src, string name)
        {
            Rect r = src.rect; // sub-rect within the (possibly atlas) texture, in pixels
            float ppu = Mathf.Max(r.width, r.height);
            var normalized = Sprite.Create(
                src.texture, r, new Vector2(0.5f, 0.5f), ppu, 0, SpriteMeshType.FullRect);
            normalized.name = name;
            return normalized;
        }

        /// <summary>
        /// Wraps a raw sprite in a runtime Tile. The source sprite may be any size
        /// with any pivot/PPU (e.g. an auto-sliced atlas sub-sprite).
        /// </summary>
        private static Tile CreateSpriteTile(TileData data)
        {
            Sprite normalized = Normalize(data.Sprite, data.DisplayName + "_tile");

            var tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = normalized;
            tile.color = data.Tint;
            tile.colliderType = data.IsSolid ? Tile.ColliderType.Grid : Tile.ColliderType.None;
            return tile;
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

using UnityEngine;
using Doodgy.Core;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Spawns trees on the surface after the world generates. Trees are sprite
    /// objects (not tiles) so the canopy can be larger than one cell; they don't
    /// block digging/movement. Placement is seeded off the world seed so it's
    /// reproducible. Each tree is <see cref="Choppable"/> with an axe for wood.
    /// </summary>
    public sealed class TreeSpawner : MonoBehaviour
    {
        [SerializeField] private World world;

        [Header("Sprites")]
        [SerializeField] private Sprite trunkSprite;
        [SerializeField] private Sprite canopySprite;
        [SerializeField] private Sprite stumpSprite;

        [Header("Placement")]
        [SerializeField] private int minSpacing = 5;
        [SerializeField] private int maxSpacing = 13;
        [SerializeField] private int minHeight = 4;
        [SerializeField] private int maxHeight = 11;
        [Range(0f, 1f)] [SerializeField] private float spawnChance = 0.7f;
        [SerializeField] private int sortingOrder = 5; // above tiles (0), below player (10)

        [Header("Drops")]
        [SerializeField] private ItemData woodItem;
        [SerializeField] private int woodPerSegment = 1;
        [Tooltip("Optional bonus drop when a tree falls (apple).")]
        [SerializeField] private ItemData bonusItem;
        [Range(0f, 1f)] [SerializeField] private float bonusChance = 0.3f;

        private Transform _container;

        private void OnEnable()
        {
            if (world != null) world.OnWorldGenerated += Spawn;
        }

        private void OnDisable()
        {
            if (world != null) world.OnWorldGenerated -= Spawn;
        }

        private void Spawn()
        {
            if (_container != null) Destroy(_container.gameObject);
            _container = new GameObject("Trees").transform;
            _container.SetParent(transform, false);

            int w = world.WidthInTiles;
            int h = world.HeightInTiles;
            var rng = new System.Random(world.Seed ^ 0x7EE5);

            int x = 2;
            while (x < w - 2)
            {
                // The stump is 3 tiles wide, so the tree needs locally flat ground —
                // otherwise the base visually sinks into a slope.
                if (rng.NextDouble() <= spawnChance && TryGetSurface(x, h, out int surfaceY)
                    && IsFlat3(x, surfaceY, h))
                {
                    int height = rng.Next(minHeight, maxHeight + 1);
                    BuildTree(x, surfaceY, height);
                }
                x += rng.Next(minSpacing, maxSpacing + 1);
            }
        }

        private bool IsFlat3(int x, int surfaceY, int h)
            => TryGetSurface(x - 1, h, out int l) && l == surfaceY
            && TryGetSurface(x + 1, h, out int r) && r == surfaceY;

        // Topmost solid tile in a column = the surface.
        private bool TryGetSurface(int x, int h, out int surfaceY)
        {
            for (int y = h - 1; y >= 0; y--)
            {
                if (world.GetTile(new Vector2Int(x, y)) != WorldConstants.AirTileId)
                {
                    surfaceY = y;
                    return true;
                }
            }
            surfaceY = -1;
            return false;
        }

        private void BuildTree(int x, int surfaceY, int heightTiles)
        {
            var tree = new GameObject($"Tree_{x}");
            tree.transform.SetParent(_container, false);

            float cx = x + 0.5f;              // centre column (1-wide trunk)
            float bottom = surfaceY + 1f;     // first tile above the ground
            float y = bottom;

            // Each piece is auto-sized from its pixels (16 px = 1 tile), so the
            // stump (48x16) is 3x1, the trunk (16x16) is 1x1, the canopy (48x48) 3x3.
            if (stumpSprite != null) y += AddPiece(tree.transform, stumpSprite, cx, y);

            float targetTop = bottom + heightTiles;
            int guard = 0;
            while (trunkSprite != null && y < targetTop && guard++ < 64)
                y += AddPiece(tree.transform, trunkSprite, cx, y);

            float canopyH = 0f;
            if (canopySprite != null)
                canopyH = AddPiece(tree.transform, canopySprite, cx, y - 0.5f); // slight overlap

            // Trigger collider over the whole tree for click-to-chop (doesn't block movement).
            float top = (y - 0.5f) + canopyH;
            float totalH = Mathf.Max(1f, top - bottom);
            var box = tree.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(3f, totalH);
            box.offset = new Vector2(cx, bottom + totalH * 0.5f);

            var chop = tree.AddComponent<Choppable>();
            chop.Configure(woodItem, Mathf.Max(1, heightTiles / 2 * woodPerSegment),
                           heightTiles, new Vector3(cx, bottom + 1f, 0f));
            chop.SetBonusDrop(bonusItem, bonusChance);
        }

        // 16 px of source art == 1 world tile (all our sprites use this scale).
        private const float PixelsPerTile = 16f;

        /// <summary>
        /// Adds one sprite piece at its natural size (pixels/16 tiles), centred on
        /// <paramref name="centerX"/> with its bottom edge at <paramref name="bottomY"/>
        /// (pivot-independent). Returns the piece's height in tiles.
        /// </summary>
        private float AddPiece(Transform parent, Sprite sprite, float centerX, float bottomY)
        {
            float wTiles = sprite.rect.width / PixelsPerTile;
            float hTiles = sprite.rect.height / PixelsPerTile;
            Vector2 size = sprite.bounds.size;
            if (size.x <= 0f || size.y <= 0f) return 0f;

            float sx = wTiles / size.x;
            float sy = hTiles / size.y;

            var go = new GameObject("piece");
            go.transform.SetParent(parent, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            go.transform.localScale = new Vector3(sx, sy, 1f);

            Vector3 slotCenter = new Vector3(centerX, bottomY + hTiles * 0.5f, 0f);
            Vector3 c = sprite.bounds.center;
            go.transform.position = slotCenter - new Vector3(c.x * sx, c.y * sy, 0f);
            return hTiles;
        }
    }
}

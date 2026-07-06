using UnityEngine;
using Doodgy.Core;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Scatters decorative plant life (bushes, flowers, grass tufts) along the
    /// surface after world generation. Purely cosmetic: no collision with the
    /// player, breakable with any tool (instantly cleared when building). Sprites
    /// are auto-sized at 16 px = 1 tile; placement is seeded off the world seed.
    /// </summary>
    public sealed class DecorationSpawner : MonoBehaviour
    {
        private const float PixelsPerTile = 16f;

        [SerializeField] private World world;
        [Tooltip("Pool of decoration sprites; one is picked at random per spot.")]
        [SerializeField] private Sprite[] sprites;
        [Range(0f, 1f)] [SerializeField] private float chancePerSpot = 0.4f;
        [SerializeField] private int minSpacing = 1;
        [SerializeField] private int maxSpacing = 4;
        [Tooltip("Behind trees (5) and the player (10), above tiles.")]
        [SerializeField] private int sortingOrder = 4;

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
            if (sprites == null || sprites.Length == 0) return; // no decor art yet

            _container = new GameObject("Decorations").transform;
            _container.SetParent(transform, false);

            int w = world.WidthInTiles;
            var rng = new System.Random(world.Seed ^ 0x0DEC0);

            int x = 1;
            while (x < w - 1)
            {
                if (rng.NextDouble() <= chancePerSpot && TryGetSurface(x, out int surfaceY))
                {
                    Sprite sprite = sprites[rng.Next(sprites.Length)];
                    if (sprite != null) Place(sprite, x, surfaceY);
                }
                x += rng.Next(minSpacing, maxSpacing + 1);
            }
        }

        private bool TryGetSurface(int x, out int surfaceY)
        {
            for (int y = world.HeightInTiles - 1; y >= 0; y--)
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

        private void Place(Sprite sprite, int x, int surfaceY)
        {
            float wTiles = sprite.rect.width / PixelsPerTile;
            float hTiles = sprite.rect.height / PixelsPerTile;
            Vector2 size = sprite.bounds.size;
            if (size.x <= 0f || size.y <= 0f) return;

            float sx = wTiles / size.x;
            float sy = hTiles / size.y;

            var go = new GameObject($"Decor_{x}");
            go.transform.SetParent(_container, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;
            go.transform.localScale = new Vector3(sx, sy, 1f);

            // Bottom sits on the ground, centred on the column (pivot-independent).
            Vector3 slotCenter = new Vector3(x + 0.5f, surfaceY + 1f + hTiles * 0.5f, 0f);
            Vector3 c = sprite.bounds.center;
            go.transform.position = slotCenter - new Vector3(c.x * sx, c.y * sy, 0f);

            // Click-to-clear with any tool; drops nothing.
            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(size.x, size.y); // local size; scaled by transform

            var chop = go.AddComponent<Choppable>();
            chop.Configure(null, 1, 0.3f, go.transform.position, ToolType.None);
        }
    }
}

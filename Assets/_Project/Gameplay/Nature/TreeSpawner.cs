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
                if (rng.NextDouble() <= spawnChance && TryGetSurface(x, h, out int surfaceY))
                {
                    int height = rng.Next(minHeight, maxHeight + 1);
                    BuildTree(x, surfaceY, height);
                }
                x += rng.Next(minSpacing, maxSpacing + 1);
            }
        }

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

        private void BuildTree(int x, int surfaceY, int height)
        {
            var tree = new GameObject($"Tree_{x}");
            tree.transform.SetParent(_container, false);

            float cx = x + 0.5f;
            float baseY = surfaceY + 1; // first tile above the ground

            if (stumpSprite != null)
                AddPiece(tree.transform, stumpSprite, new Vector3(cx, baseY + 0.5f, 0f), 1f, 1f);

            for (int i = 1; i <= height; i++)
                if (trunkSprite != null)
                    AddPiece(tree.transform, trunkSprite, new Vector3(cx, baseY + i + 0.5f, 0f), 1f, 1f);

            if (canopySprite != null)
                AddPiece(tree.transform, canopySprite, new Vector3(cx, baseY + height + 1f, 0f), 3f, 3f);

            // Trigger collider over the trunk for click-to-chop (doesn't block movement).
            float totalH = height + 3f;
            var box = tree.AddComponent<BoxCollider2D>();
            box.isTrigger = true;
            box.size = new Vector2(1.2f, totalH);
            box.offset = new Vector2(cx, baseY + totalH * 0.5f);

            var chop = tree.AddComponent<Choppable>();
            chop.Configure(woodItem, Mathf.Max(1, height * woodPerSegment), height);
        }

        private void AddPiece(Transform parent, Sprite sprite, Vector3 worldCenter, float wTiles, float hTiles)
        {
            var go = new GameObject("piece");
            go.transform.SetParent(parent, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;

            // Scale so the sprite spans the requested number of tiles (1 tile = 1 unit).
            Vector2 size = sprite.bounds.size;
            go.transform.localScale = new Vector3(
                size.x > 0f ? wTiles / size.x : 1f,
                size.y > 0f ? hTiles / size.y : 1f, 1f);
            go.transform.position = worldCenter;
        }
    }
}

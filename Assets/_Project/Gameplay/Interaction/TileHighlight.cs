using UnityEngine;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Draws a highlight over the tile under the cursor whenever it's a valid
    /// target (mineable, or placeable-into while holding a block). Uses the
    /// custom highlight sprite when assigned; otherwise generates a thin white
    /// outline so the feature works before the art exists. Purely visual.
    /// </summary>
    [RequireComponent(typeof(WorldEditController))]
    public sealed class TileHighlight : MonoBehaviour
    {
        private const float PixelsPerTile = 16f;

        [Tooltip("Custom highlight art (16x16, transparent centre). Null = generated outline.")]
        [SerializeField] private Sprite highlightSprite;
        [Range(0f, 1f)] [SerializeField] private float opacity = 0.85f;

        private WorldEditController _editor;
        private SpriteRenderer _sr;

        private void Start()
        {
            _editor = GetComponent<WorldEditController>();

            var go = new GameObject("TileHighlight");
            _sr = go.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = 19; // above tiles, below the mining cracks (20)
            _sr.color = new Color(1f, 1f, 1f, opacity);
            _sr.enabled = false;

            Sprite sprite = highlightSprite != null ? highlightSprite : GenerateOutline();
            _sr.sprite = sprite;
            float s = sprite.pixelsPerUnit / PixelsPerTile; // pixel-true, 16 px = 1 tile
            go.transform.localScale = new Vector3(s, s, 1f);
        }

        private void LateUpdate()
        {
            if (_editor == null || _sr == null) return;
            _sr.enabled = _editor.HoverValid;
            if (!_editor.HoverValid) return;

            Vector2Int t = _editor.HoveredTile;
            Vector3 tileCenter = new Vector3(t.x + 0.5f, t.y + 0.5f, 0f);
            Vector3 c = _sr.sprite.bounds.center; // pivot-agnostic centring
            float s = _sr.transform.localScale.x;
            _sr.transform.position = tileCenter - c * s;
        }

        // 16x16 one-pixel white border, transparent centre.
        private static Sprite GenerateOutline()
        {
            const int size = 16;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    bool border = x == 0 || y == 0 || x == size - 1 || y == size - 1;
                    pixels[y * size + x] = border ? new Color32(255, 255, 255, 255)
                                                  : new Color32(0, 0, 0, 0);
                }
            tex.SetPixels32(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size),
                                 new Vector2(0.5f, 0.5f), size, 0, SpriteMeshType.FullRect);
        }
    }
}

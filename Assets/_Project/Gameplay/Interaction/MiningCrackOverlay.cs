using UnityEngine;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Purely visual mining feedback: draws a crack sprite over the tile being
    /// mined, choosing the frame from mining progress (light cracks -> shattered).
    /// Reads <see cref="WorldEditController"/>; changes nothing about gameplay.
    /// </summary>
    public sealed class MiningCrackOverlay : MonoBehaviour
    {
        [SerializeField] private WorldEditController editor;
        [Tooltip("Crack stages, light -> heavy. Sliced from the Cracks sheet.")]
        [SerializeField] private Sprite[] crackFrames;
        [SerializeField] private int sortingOrder = 20;

        private SpriteRenderer _sr;

        private void Awake()
        {
            var go = new GameObject("MiningCrack");
            _sr = go.AddComponent<SpriteRenderer>();
            _sr.sortingOrder = sortingOrder;
            _sr.enabled = false;
        }

        private void LateUpdate()
        {
            if (editor == null || crackFrames == null || crackFrames.Length == 0)
            {
                _sr.enabled = false;
                return;
            }

            // Cracks show for either mining a tile or chopping a tree.
            float p;
            Vector2Int t;
            if (editor.IsMiningTile) { p = editor.MiningProgress01; t = editor.MiningTile; }
            else if (editor.IsChopping) { p = editor.ChopProgress01; t = editor.ChopTile; }
            else { _sr.enabled = false; return; }

            if (p <= 0f) { _sr.enabled = false; return; }

            int idx = Mathf.Clamp(Mathf.FloorToInt(p * crackFrames.Length), 0, crackFrames.Length - 1);
            Sprite frame = crackFrames[idx];
            if (frame == null) { _sr.enabled = false; return; }

            _sr.sprite = frame;
            _sr.enabled = true;

            // Scale to fill one cell, then position so the sprite's CENTER lands on
            // the tile centre regardless of its pivot/trim (prevents drift between frames).
            Vector2 size = frame.bounds.size;
            float sx = size.x > 0f ? 1f / size.x : 1f;
            float sy = size.y > 0f ? 1f / size.y : 1f;
            _sr.transform.localScale = new Vector3(sx, sy, 1f);

            Vector3 tileCenter = new Vector3(t.x + 0.5f, t.y + 0.5f, 0f);
            Vector3 c = frame.bounds.center; // offset of geometric centre from pivot
            _sr.transform.position = tileCenter - new Vector3(c.x * sx, c.y * sy, 0f);
        }
    }
}

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
            if (editor == null || crackFrames == null || crackFrames.Length == 0 || !editor.IsMiningTile)
            {
                _sr.enabled = false;
                return;
            }

            float p = editor.MiningProgress01;
            if (p <= 0f)
            {
                _sr.enabled = false;
                return;
            }

            int idx = Mathf.Clamp(Mathf.FloorToInt(p * crackFrames.Length), 0, crackFrames.Length - 1);
            Sprite frame = crackFrames[idx];
            if (frame == null) { _sr.enabled = false; return; }

            _sr.sprite = frame;
            _sr.enabled = true;

            // Center on the tile and scale the sprite to fill one cell.
            Vector2Int t = editor.MiningTile;
            _sr.transform.position = new Vector3(t.x + 0.5f, t.y + 0.5f, 0f);
            Vector2 size = frame.bounds.size;
            _sr.transform.localScale = new Vector3(
                size.x > 0f ? 1f / size.x : 1f,
                size.y > 0f ? 1f / size.y : 1f, 1f);
        }
    }
}

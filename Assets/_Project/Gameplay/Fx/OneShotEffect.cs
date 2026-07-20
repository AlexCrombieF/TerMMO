using UnityEngine;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Plays a short sprite animation once at a world position, then removes
    /// itself. Used for transient VFX (sword slash, hit puff). Pixel-true
    /// (16 px = 1 tile). Purely visual.
    /// </summary>
    public sealed class OneShotEffect : MonoBehaviour
    {
        private const float PixelsPerTile = 16f;

        private Sprite[] _frames;
        private float _fps;
        private SpriteRenderer _sr;
        private float _t;

        public static void Spawn(Sprite[] frames, float fps, Vector3 pos,
                                 int sortingOrder = 18, bool flipX = false, Color? tint = null)
        {
            if (frames == null || frames.Length == 0 || frames[0] == null) return;

            var go = new GameObject("FX");
            go.transform.position = pos;
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = sortingOrder;
            sr.flipX = flipX;
            sr.color = tint ?? Color.white;
            sr.sprite = frames[0];
            float s = frames[0].pixelsPerUnit / PixelsPerTile;
            go.transform.localScale = new Vector3(s, s, 1f);

            var fx = go.AddComponent<OneShotEffect>();
            fx._frames = frames;
            fx._fps = fps;
            fx._sr = sr;
        }

        private void Update()
        {
            _t += Time.deltaTime;
            int idx = (int)(_t * _fps);
            if (idx >= _frames.Length) { Destroy(gameObject); return; }
            _sr.sprite = _frames[idx];
        }
    }
}

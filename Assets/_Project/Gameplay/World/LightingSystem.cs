using UnityEngine;
using Doodgy.Core;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Tile-based lighting (Terraria-style). Maintains a per-tile light level
    /// [0..1] from skylight (floods open-to-sky air, attenuates through tiles)
    /// plus light-emitting tiles, then draws a black "darkness" overlay sprite
    /// whose alpha = 1 - light. Surface is lit; enclosed/underground areas are
    /// dark (you can't see through the floor) until you dig down or place a torch.
    ///
    /// MVP scope: monochrome light over the fixed loaded rectangle, full recompute
    /// when tiles change (cheap at this size). Coloured light, per-chunk incremental
    /// updates, and a day/night sun multiplier are future work.
    /// </summary>
    [RequireComponent(typeof(World))]
    public sealed class LightingSystem : MonoBehaviour
    {
        [Header("Propagation")]
        [Tooltip("Sky brightness. Lower this for night/day cycle later.")]
        [Range(0f, 1f)] [SerializeField] private float skyLight = 1f;
        [Tooltip("Light lost per tile through air. Lower = light reaches further.")]
        [SerializeField] private float airAttenuation = 0.06f;
        [Tooltip("Light lost per tile through solid blocks. Higher = darker underground.")]
        [SerializeField] private float solidAttenuation = 0.22f;
        [Min(1)] [SerializeField] private int iterations = 4;
        [Tooltip("Floor on visibility (0 = pitch black, ~0.03 = a faint hint in the dark).")]
        [Range(0f, 0.2f)] [SerializeField] private float minVisibility = 0f;

        [Header("Render")]
        [Tooltip("Draw order of the darkness overlay (above tiles and the player).")]
        [SerializeField] private int sortingOrder = 1000;

        private World _world;
        private SpriteRenderer _overlay;
        private Texture2D _tex;

        private int _w, _h;
        private float[] _light;
        private bool[] _blocks;
        private float[] _emit;
        private Color32[] _pixels;
        private bool _dirty;

        private void Awake() => _world = GetComponent<World>();

        private void OnEnable()
        {
            _world.OnTileChanged += OnTileChanged;
            _world.OnWorldGenerated += OnWorldGenerated;
        }

        private void OnDisable()
        {
            _world.OnTileChanged -= OnTileChanged;
            _world.OnWorldGenerated -= OnWorldGenerated;
        }

        private void OnWorldGenerated() => Rebuild();

        /// <summary>Light level [0..1] at a tile (1 = full daylight). 0 outside the map.</summary>
        public float GetLight(Vector2Int tile)
        {
            if (_light == null || tile.x < 0 || tile.y < 0 || tile.x >= _w || tile.y >= _h)
                return 0f;
            return Mathf.Clamp01(_light[tile.y * _w + tile.x]);
        }
        private void OnTileChanged(TileChangedEvent _) => _dirty = true;

        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            Recompute();
        }

        // Allocate buffers/texture for the current world size, then light it.
        private void Rebuild()
        {
            _w = _world.WidthInTiles;
            _h = _world.HeightInTiles;
            int n = _w * _h;
            _light = new float[n];
            _blocks = new bool[n];
            _emit = new float[n];
            _pixels = new Color32[n];

            EnsureOverlay();
            Recompute();
        }

        private void EnsureOverlay()
        {
            if (_overlay == null)
            {
                var go = new GameObject("LightingOverlay");
                go.transform.SetParent(transform, false);
                _overlay = go.AddComponent<SpriteRenderer>();
                _overlay.sortingOrder = sortingOrder;
            }

            if (_tex == null || _tex.width != _w || _tex.height != _h)
            {
                _tex = new Texture2D(_w, _h, TextureFormat.RGBA32, false)
                {
                    filterMode = FilterMode.Bilinear, // smooth gradients between tiles
                    wrapMode = TextureWrapMode.Clamp
                };
                // 1 pixel per tile, pivot bottom-left, positioned at world (0,0)
                // so pixel (x,y) maps onto tile (x,y).
                _overlay.sprite = Sprite.Create(
                    _tex, new Rect(0, 0, _w, _h), Vector2.zero, 1f, 0, SpriteMeshType.FullRect);
                _overlay.transform.position = new Vector3(0f, 0f, -1f); // in front of tiles
            }
        }

        private void Recompute()
        {
            if (_light == null) return;
            ReadTiles();
            SeedLight();
            Propagate();
            BakeTexture();
        }

        private void ReadTiles()
        {
            for (int y = 0; y < _h; y++)
            {
                for (int x = 0; x < _w; x++)
                {
                    int i = y * _w + x;
                    ushort id = _world.GetTile(new Vector2Int(x, y));
                    if (id == WorldConstants.AirTileId)
                    {
                        _blocks[i] = false;
                        _emit[i] = 0f;
                    }
                    else
                    {
                        TileData d = _world.Tiles.Get(id);
                        _blocks[i] = d == null || d.BlocksLight;
                        _emit[i] = (d != null && d.EmitsLight) ? d.LightIntensity : 0f;
                    }
                }
            }
        }

        private void SeedLight()
        {
            for (int i = 0; i < _light.Length; i++) _light[i] = _emit[i];

            // Skylight: walking down each column, open air sees the sun until the
            // first blocking tile; everything below that starts dark.
            for (int x = 0; x < _w; x++)
            {
                bool sky = true;
                for (int y = _h - 1; y >= 0; y--)
                {
                    int i = y * _w + x;
                    if (sky && !_blocks[i]) _light[i] = Mathf.Max(_light[i], skyLight);
                    else if (_blocks[i]) sky = false;
                }
            }
        }

        // Gauss-Seidel relaxation: alternate forward/backward sweeps so light
        // spreads in all directions and converges in a few iterations.
        private void Propagate()
        {
            for (int it = 0; it < iterations; it++)
            {
                for (int y = 0; y < _h; y++)
                    for (int x = 0; x < _w; x++) Relax(x, y);
                for (int y = _h - 1; y >= 0; y--)
                    for (int x = _w - 1; x >= 0; x--) Relax(x, y);
            }
        }

        private void Relax(int x, int y)
        {
            int i = y * _w + x;
            float atten = _blocks[i] ? solidAttenuation : airAttenuation;
            float best = _light[i];
            if (x > 0) best = Mathf.Max(best, _light[i - 1] - atten);
            if (x < _w - 1) best = Mathf.Max(best, _light[i + 1] - atten);
            if (y > 0) best = Mathf.Max(best, _light[i - _w] - atten);
            if (y < _h - 1) best = Mathf.Max(best, _light[i + _w] - atten);
            _light[i] = best;
        }

        private void BakeTexture()
        {
            for (int i = 0; i < _light.Length; i++)
            {
                float v = Mathf.Max(Mathf.Clamp01(_light[i]), minVisibility);
                byte darkness = (byte)((1f - v) * 255f);
                _pixels[i] = new Color32(0, 0, 0, darkness);
            }
            _tex.SetPixels32(_pixels);
            _tex.Apply(false);
        }
    }
}

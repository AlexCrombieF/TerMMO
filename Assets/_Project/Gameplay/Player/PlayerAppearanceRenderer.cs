using UnityEngine;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Renders the customizable player as tinted sprite layers:
    ///   body (skin tint) -> clothes (untinted, optional) -> eyes (eye tint)
    ///   -> hair (chosen style, hair tint).
    /// Layer art is authored in white/gray so multiplicative tinting yields the
    /// chosen colours. All layers flip with the controller's facing. If no body
    /// art exists yet, the old placeholder box is simply tinted to the skin
    /// colour so customization is testable before the art lands.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public sealed class PlayerAppearanceRenderer : MonoBehaviour
    {
        private const float PixelsPerTile = 16f;

        [Header("Layer art (white/gray where tinted). May be empty until drawn.")]
        [SerializeField] private Sprite bodySprite;
        [SerializeField] private Sprite clothesSprite;
        [Tooltip("Untinted eye base: black pupil/outline + white sclera, drawn in real colours.")]
        [SerializeField] private Sprite eyesBaseSprite;
        [Tooltip("Iris only, in white — tinted to the chosen eye colour.")]
        [SerializeField] private Sprite eyesSprite;
        [Tooltip("One sprite per hairstyle, same canvas as the body.")]
        [SerializeField] private Sprite[] hairStyles;

        [Header("Animation (body layer only; same canvas/pose as the idle body)")]
        [SerializeField] private Sprite[] walkFrames;
        [SerializeField] private float walkFps = 8f;

        private PlayerController _controller;
        private SpriteRenderer _fallback;   // the controller's placeholder box
        private Transform _visual;
        private SpriteRenderer _body, _clothes, _eyesBase, _eyes, _hair;

        public PlayerAppearance Current { get; private set; } = PlayerAppearance.Default;
        public int HairStyleCount => hairStyles != null ? hairStyles.Length : 0;
        public bool HasLayeredArt => bodySprite != null;

        // Read access for the creation screen's preview.
        public Sprite BodySprite => bodySprite;
        public Sprite ClothesSprite => clothesSprite;
        public Sprite EyesBaseSprite => eyesBaseSprite;
        public Sprite EyesSprite => eyesSprite;
        public Sprite HairSprite(int index)
            => hairStyles == null || hairStyles.Length == 0
               ? null
               : hairStyles[Mathf.Clamp(index, 0, hairStyles.Length - 1)];

        private Rigidbody2D _rb;

        private void Start()
        {
            _controller = GetComponent<PlayerController>();
            _rb = GetComponent<Rigidbody2D>();
            _fallback = GetComponent<SpriteRenderer>();
            Apply(Current);
        }

        /// <summary>Applies an appearance to the layers (or the fallback box).</summary>
        public void Apply(PlayerAppearance appearance)
        {
            Current = appearance;

            if (!HasLayeredArt)
            {
                if (_fallback != null) _fallback.color = appearance.skinColor;
                return;
            }

            EnsureLayers();
            if (_fallback != null) _fallback.enabled = false;

            _body.sprite = bodySprite;
            _body.color = appearance.skinColor;

            _clothes.sprite = clothesSprite;
            _clothes.enabled = clothesSprite != null;

            _eyesBase.sprite = eyesBaseSprite;
            _eyesBase.enabled = eyesBaseSprite != null; // untinted: white sclera + black pupil

            _eyes.sprite = eyesSprite;
            _eyes.enabled = eyesSprite != null;
            _eyes.color = appearance.eyeColor;

            Sprite hair = null;
            if (hairStyles != null && hairStyles.Length > 0)
                hair = hairStyles[Mathf.Clamp(appearance.hairStyle, 0, hairStyles.Length - 1)];
            _hair.sprite = hair;
            _hair.enabled = hair != null;
            _hair.color = appearance.hairColor;
        }

        private float _scale;
        private Transform _offset;

        private void EnsureLayers()
        {
            if (_visual != null) return;

            // The importer trims transparent pixels and shifts pivots, so we
            // can't trust pivots for placement. Layout:
            //   _visual  — at the character's centre-x / feet-aligned-y; its
            //              X scale is mirrored to face left (keeps layers rigid)
            //   _offset  — shifts the shared canvas so the BODY's content is
            //              centred on _visual's origin
            //   layers   — plain SpriteRenderers at local zero
            _scale = bodySprite.pixelsPerUnit / PixelsPerTile; // 16 px == 1 tile

            Bounds b = bodySprite.bounds; // pivot-relative, unscaled
            float feetY = ColliderBottom();

            _visual = new GameObject("Visual").transform;
            _visual.SetParent(transform, false);
            _visual.localScale = new Vector3(_scale, _scale, 1f);
            _visual.localPosition = new Vector3(0f, feetY + b.extents.y * _scale, 0f);

            _offset = new GameObject("Canvas").transform;
            _offset.SetParent(_visual, false);
            _offset.localPosition = -b.center;

            _body = MakeLayer("Body", 10);
            _clothes = MakeLayer("Clothes", 11);
            _eyesBase = MakeLayer("EyesBase", 12);
            _eyes = MakeLayer("Eyes", 13);
            _hair = MakeLayer("Hair", 14);
        }

        // Bottom of the physics shape, in local space — where the feet belong.
        private float ColliderBottom()
        {
            var capsule = GetComponent<CapsuleCollider2D>();
            if (capsule != null) return capsule.offset.y - capsule.size.y * 0.5f;
            var any = GetComponent<Collider2D>();
            return any != null ? any.bounds.min.y - transform.position.y : -1f;
        }

        private SpriteRenderer MakeLayer(string name, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(_offset, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = order;
            return sr;
        }

        private void LateUpdate()
        {
            if (_visual == null || _controller == null) return;
            // Mirror the whole layer group around the character's centre —
            // per-layer flipX would scatter trimmed sprites around their pivots.
            float x = _controller.Facing < 0 ? -_scale : _scale;
            if (!Mathf.Approximately(_visual.localScale.x, x))
                _visual.localScale = new Vector3(x, _scale, 1f);

            AnimateBody();
        }

        // Walk cycle on the body layer while moving on the ground; the frames
        // share the idle canvas/pivot, so they stay aligned with the other layers.
        private void AnimateBody()
        {
            if (_body == null || walkFrames == null || walkFrames.Length == 0) return;

            bool moving = _rb != null && Mathf.Abs(_rb.linearVelocity.x) > 0.15f
                          && _controller.IsGrounded;
            if (moving)
            {
                int idx = (int)(Time.time * walkFps) % walkFrames.Length;
                _body.sprite = walkFrames[idx];
            }
            else
            {
                _body.sprite = bodySprite; // idle pose
            }
        }
    }
}

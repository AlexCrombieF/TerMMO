using UnityEngine;
using UnityEngine.InputSystem;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Side-on 2D movement: run, jump, fall, with the four standard game-feel
    /// helpers (coyote time, jump buffering, variable jump height, faster fall).
    /// Reads input via the new Input System low-level API (Keyboard/devices), so
    /// it has no dependency on a specific .inputactions asset yet — rebindable
    /// actions are a polish-step concern.
    ///
    /// MOVEMENT vs STATS: moveSpeed / jumpHeight are authored here for now. In
    /// the stats step they become *derived* values — e.g. effective speed =
    /// baseMoveSpeed * (1 + dexterity bonus). The hook is <see cref="MoveSpeed"/>
    /// / <see cref="JumpHeight"/>; route those through the stat system later.
    ///
    /// NETWORKING: this is the local-prediction movement. Under the authoritative
    /// model the server will re-simulate from inputs; keep movement a pure
    /// function of (input, state, dt) so that port is clean.
    /// </summary>
    [RequireComponent(typeof(Rigidbody2D))]
    [RequireComponent(typeof(Collider2D))]
    public sealed class PlayerController : MonoBehaviour
    {
        [Header("Run")]
        [SerializeField] private float moveSpeed = 8f;
        [Tooltip("How fast horizontal velocity ramps toward the target (units/s^2).")]
        [SerializeField] private float groundAccel = 80f;
        [SerializeField] private float airAccel = 40f;

        [Header("Jump")]
        [Tooltip("Apex height of a full jump, in world units (tiles).")]
        [SerializeField] private float jumpHeight = 3.2f;
        [SerializeField] private float gravityScale = 3.5f;
        [Tooltip("Extra gravity while falling — gives weight and a snappy descent.")]
        [SerializeField] private float fallGravityMult = 1.6f;
        [Tooltip("Gravity while rising with jump released — caps low hops.")]
        [SerializeField] private float lowJumpGravityMult = 2.2f;
        [Tooltip("Grace period to still jump just after leaving a ledge.")]
        [SerializeField] private float coyoteTime = 0.1f;
        [Tooltip("Grace period to buffer a jump pressed just before landing.")]
        [SerializeField] private float jumpBufferTime = 0.1f;

        [Header("Ground check")]
        [Tooltip("Layers considered solid ground. Default Everything works since " +
                 "the probe box sits just below the player's own collider.")]
        [SerializeField] private LayerMask groundMask = ~0;
        [SerializeField] private float groundCheckDistance = 0.08f;

        [Header("Visual (placeholder generated if no sprite assigned)")]
        [SerializeField] private Color placeholderColor = new Color(0.2f, 0.6f, 1f);

        private Rigidbody2D _rb;
        private Collider2D _collider;
        private SpriteRenderer _sprite;

        private float _moveInput;
        private bool _jumpHeld;
        private float _coyoteCounter;
        private float _jumpBufferCounter;
        private float _knockbackLock;   // seconds during which input is ignored
        private bool _isGrounded;
        private int _facing = 1;

        /// <summary>Effective move speed (route through stats later).</summary>
        public float MoveSpeed { get => moveSpeed; set => moveSpeed = value; }
        /// <summary>Effective jump height (route through stats later).</summary>
        public float JumpHeight { get => jumpHeight; set => jumpHeight = value; }
        /// <summary>+1 facing right, -1 facing left. Used by combat/aim later.</summary>
        public int Facing => _facing;
        public bool IsGrounded => _isGrounded;

        private void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _collider = GetComponent<Collider2D>();
            _rb.gravityScale = gravityScale;
            _rb.freezeRotation = true;
            _rb.interpolation = RigidbodyInterpolation2D.Interpolate;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            EnsurePlaceholderSprite();
        }

        private void Update()
        {
            SampleInput();
            UpdateTimers();
            UpdateFacingAndGravity();
        }

        private void FixedUpdate()
        {
            _isGrounded = CheckGrounded();
            if (_isGrounded) _coyoteCounter = coyoteTime;

            ApplyHorizontalMovement();
            TryConsumeJump();
        }

        // --------------------------------------------------------------- input

        private void SampleInput()
        {
            Keyboard kb = Keyboard.current;
            if (kb == null) { _moveInput = 0f; return; }

            float right = (kb.dKey.isPressed || kb.rightArrowKey.isPressed) ? 1f : 0f;
            float left = (kb.aKey.isPressed || kb.leftArrowKey.isPressed) ? 1f : 0f;
            _moveInput = right - left;

            bool jumpPressed = kb.spaceKey.wasPressedThisFrame
                               || kb.wKey.wasPressedThisFrame
                               || kb.upArrowKey.wasPressedThisFrame;
            _jumpHeld = kb.spaceKey.isPressed || kb.wKey.isPressed || kb.upArrowKey.isPressed;

            if (jumpPressed) _jumpBufferCounter = jumpBufferTime;
        }

        private void UpdateTimers()
        {
            float dt = Time.deltaTime;
            if (_coyoteCounter > 0f) _coyoteCounter -= dt;
            if (_jumpBufferCounter > 0f) _jumpBufferCounter -= dt;
            if (_knockbackLock > 0f) _knockbackLock -= dt;
        }

        // ------------------------------------------------------------ movement

        private void ApplyHorizontalMovement()
        {
            if (_knockbackLock > 0f) return; // let knockback carry the body

            float targetX = _moveInput * moveSpeed;
            float accel = _isGrounded ? groundAccel : airAccel;
            float newX = Mathf.MoveTowards(_rb.linearVelocity.x, targetX, accel * Time.fixedDeltaTime);
            _rb.linearVelocity = new Vector2(newX, _rb.linearVelocity.y);
        }

        private void TryConsumeJump()
        {
            if (_jumpBufferCounter > 0f && _coyoteCounter > 0f)
            {
                float gravity = Mathf.Abs(Physics2D.gravity.y) * gravityScale;
                float jumpVelocity = CalculateJumpVelocity(jumpHeight, gravity);
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, jumpVelocity);
                _jumpBufferCounter = 0f;
                _coyoteCounter = 0f;
            }
        }

        private void UpdateFacingAndGravity()
        {
            if (_moveInput > 0.01f) _facing = 1;
            else if (_moveInput < -0.01f) _facing = -1;
            if (_sprite != null) _sprite.flipX = _facing < 0;

            // Variable gravity: heavier on the way down, and heavier on a rising
            // jump once the button is released (clips the hop short).
            float scale = gravityScale;
            if (_rb.linearVelocity.y < 0f) scale *= fallGravityMult;
            else if (_rb.linearVelocity.y > 0f && !_jumpHeld) scale *= lowJumpGravityMult;
            _rb.gravityScale = scale;
        }

        // -------------------------------------------------------- ground check

        private bool CheckGrounded()
        {
            Bounds b = _collider.bounds;
            // A thin box sitting just beneath the collider — never overlaps self.
            Vector2 center = new Vector2(b.center.x, b.min.y - groundCheckDistance * 0.5f);
            Vector2 size = new Vector2(b.size.x * 0.9f, groundCheckDistance);
            return Physics2D.OverlapBox(center, size, 0f, groundMask) != null;
        }

        // --------------------------------------------------------------- hooks

        /// <summary>
        /// Combat/explosions call this to fling the player. Briefly locks input so
        /// the impulse isn't immediately cancelled by the run controller.
        /// </summary>
        public void ApplyKnockback(Vector2 impulse, float controlLockSeconds = 0.2f)
        {
            _rb.linearVelocity = impulse;
            _knockbackLock = Mathf.Max(_knockbackLock, controlLockSeconds);
        }

        /// <summary>Initial upward velocity to reach <paramref name="height"/> under a given gravity.</summary>
        public static float CalculateJumpVelocity(float height, float gravityMagnitude)
            => Mathf.Sqrt(2f * Mathf.Max(0f, gravityMagnitude) * Mathf.Max(0f, height));

        // --------------------------------------------------------------- visual

        private void EnsurePlaceholderSprite()
        {
            _sprite = GetComponent<SpriteRenderer>();
            if (_sprite == null) _sprite = gameObject.AddComponent<SpriteRenderer>();
            if (_sprite.sprite != null) return; // real art already assigned

            // Size the placeholder to the collider so visuals match collision.
            Bounds b = _collider.bounds;
            int ppu = 16;
            int w = Mathf.Max(1, Mathf.RoundToInt(b.size.x * ppu));
            int h = Mathf.Max(1, Mathf.RoundToInt(b.size.y * ppu));

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var px = new Color32[w * h];
            Color32 c = placeholderColor;
            for (int i = 0; i < px.Length; i++) px[i] = c;
            tex.SetPixels32(px);
            tex.Apply();

            _sprite.sprite = Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), ppu);
            _sprite.sortingOrder = 10; // draw above tilemaps
        }
    }
}

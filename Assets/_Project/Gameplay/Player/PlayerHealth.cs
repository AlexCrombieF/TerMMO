using System;
using UnityEngine;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Player health: damage/heal API, Terraria-style regeneration (kicks in
    /// after a few seconds without taking damage), fall damage from landing
    /// speed, and death -> respawn at the spawn point with full health.
    ///
    /// STATS HOOK: maxHealth becomes a derived stat (Vitality) when the stat
    /// system lands — route it through <see cref="Max"/> then.
    /// AUTHORITY: the server owns health in multiplayer; all mutation goes
    /// through Damage/Heal so that port is a matter of moving the calls.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    [RequireComponent(typeof(Rigidbody2D))]
    public sealed class PlayerHealth : MonoBehaviour
    {
        [Header("Health")]
        [SerializeField] private float maxHealth = 100f;

        [Header("Regeneration")]
        [Tooltip("HP per second once regen is active.")]
        [SerializeField] private float regenPerSecond = 2f;
        [Tooltip("Seconds without damage before regen starts.")]
        [SerializeField] private float regenDelay = 5f;

        [Header("Fall damage")]
        [Tooltip("Landing speed (units/s) you can survive unharmed (~a 6-tile drop).")]
        [SerializeField] private float safeLandingSpeed = 25f;
        [Tooltip("Damage per unit/s of landing speed beyond the safe limit.")]
        [SerializeField] private float damagePerExcessSpeed = 3f;

        private PlayerController _controller;
        private Rigidbody2D _rb;
        private Vector3 _spawnPoint;
        private float _sinceDamage;
        private float _fastestFall;   // most negative vy while airborne
        private bool _wasGrounded = true;

        public float Current { get; private set; }
        public float Max => maxHealth;
        public float Fraction => maxHealth > 0f ? Mathf.Clamp01(Current / maxHealth) : 0f;

        /// <summary>Raised whenever health changes (UI listens).</summary>
        public event Action Changed;
        /// <summary>Raised on death, after the respawn has been applied.</summary>
        public event Action Died;

        private void Start()
        {
            _controller = GetComponent<PlayerController>();
            _rb = GetComponent<Rigidbody2D>();
            _spawnPoint = transform.position;
            Current = maxHealth;
            Changed?.Invoke();
        }

        private void Update()
        {
            TrackFallDamage();

            // Regeneration after a quiet period.
            _sinceDamage += Time.deltaTime;
            if (_sinceDamage >= regenDelay && Current < maxHealth && Current > 0f)
                Heal(regenPerSecond * Time.deltaTime);
        }

        private void TrackFallDamage()
        {
            bool grounded = _controller.IsGrounded;

            if (!grounded)
            {
                _fastestFall = Mathf.Min(_fastestFall, _rb.linearVelocity.y);
            }
            else if (!_wasGrounded)
            {
                // Just landed — hurt if the impact exceeded the safe speed.
                float impact = -_fastestFall;
                if (impact > safeLandingSpeed)
                    Damage((impact - safeLandingSpeed) * damagePerExcessSpeed);
                _fastestFall = 0f;
            }

            _wasGrounded = grounded;
        }

        public void Damage(float amount)
        {
            if (amount <= 0f || Current <= 0f) return;
            Current = Mathf.Max(0f, Current - amount);
            _sinceDamage = 0f;
            Changed?.Invoke();
            if (Current <= 0f) Die();
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || Current <= 0f) return;
            float before = Current;
            Current = Mathf.Min(maxHealth, Current + amount);
            if (!Mathf.Approximately(before, Current)) Changed?.Invoke();
        }

        private void Die()
        {
            Debug.Log("[Health] You died — respawning.");
            transform.position = _spawnPoint;
            _rb.linearVelocity = Vector2.zero;
            _fastestFall = 0f;
            Current = maxHealth;
            _sinceDamage = 0f;
            Changed?.Invoke();
            Died?.Invoke();
        }
    }
}

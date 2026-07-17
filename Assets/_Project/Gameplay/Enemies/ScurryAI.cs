using UnityEngine;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Ground-chaser brain (cave rat): idles until the player is within aggro
    /// range, then scurries at them, hopping up single-tile steps. Deals contact
    /// damage with a cooldown and knocks the player back.
    /// </summary>
    [RequireComponent(typeof(Enemy))]
    public sealed class ScurryAI : MonoBehaviour
    {
        private const float ContactRange = 0.9f;
        private const float AttackCooldown = 1.0f;
        private const float HopVelocity = 7f;
        private const float GroundProbe = 0.08f;

        private Enemy _enemy;
        private Rigidbody2D _rb;
        private Collider2D _col;
        private Transform _player;
        private PlayerHealth _playerHealth;
        private PlayerController _playerController;
        private float _attackTimer;
        private float _lastX;
        private bool _wasChasing;

        public void Init(Enemy enemy, GameObject player)
        {
            _enemy = enemy;
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<Collider2D>();
            if (player != null)
            {
                _player = player.transform;
                _playerHealth = player.GetComponent<PlayerHealth>();
                _playerController = player.GetComponent<PlayerController>();
            }
        }

        private void FixedUpdate()
        {
            if (_player == null || _enemy.Data == null) return;

            float dist = Vector2.Distance(transform.position, _player.position);
            if (dist > _enemy.Data.aggroRange)
            {
                // Out of aggro: stop scurrying (gravity still applies).
                _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
                _wasChasing = false;
                return;
            }

            float dir = Mathf.Sign(_player.position.x - transform.position.x);
            _rb.linearVelocity = new Vector2(dir * _enemy.Data.moveSpeed, _rb.linearVelocity.y);

            // Hop when running into a step: wanted to move last step but the
            // position barely changed (checking assigned velocity is useless —
            // it reads back what we just set).
            float moved = Mathf.Abs(transform.position.x - _lastX);
            if (_wasChasing && Grounded() && moved < _enemy.Data.moveSpeed * Time.fixedDeltaTime * 0.25f)
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, HopVelocity);

            _lastX = transform.position.x;
            _wasChasing = true;
        }

        private void Update()
        {
            _attackTimer -= Time.deltaTime;
            if (_attackTimer > 0f || _player == null || _playerHealth == null) return;

            if (Vector2.Distance(transform.position, _player.position) <= ContactRange)
            {
                _playerHealth.Damage(_enemy.Data.contactDamage);
                if (_playerController != null)
                {
                    Vector2 away = ((Vector2)(_player.position - transform.position)).normalized;
                    _playerController.ApplyKnockback(new Vector2(away.x * 7f, 6f));
                }
                _attackTimer = AttackCooldown;
            }
        }

        private bool Grounded()
        {
            Bounds b = _col.bounds;
            Vector2 center = new Vector2(b.center.x, b.min.y - GroundProbe * 0.5f);
            Vector2 size = new Vector2(b.size.x * 0.9f, GroundProbe);
            Collider2D hit = Physics2D.OverlapBox(center, size, 0f);
            return hit != null && hit != _col;
        }
    }
}

using System.Collections.Generic;
using UnityEngine;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Runtime enemy: health, knockback, death (grants XP), and frame animation
    /// driven by its <see cref="EnemyData"/>. Movement lives in an AI component
    /// (e.g. <see cref="ScurryAI"/>) chosen from the data's aiKind. A static
    /// registry lets melee attacks and the spawner query living enemies cheaply.
    /// </summary>
    public sealed class Enemy : MonoBehaviour
    {
        private const float PixelsPerTile = 16f;
        private const int EnemyLayer = 11; // unnamed user layer reserved for enemies

        public static readonly List<Enemy> All = new List<Enemy>();
        private static bool _layerConfigured;

        public EnemyData Data { get; private set; }

        private float _health;
        private SpriteRenderer _sr;
        private Rigidbody2D _rb;
        private PlayerXP _xpTarget;

        private void OnEnable() => All.Add(this);
        private void OnDisable() => All.Remove(this);

        /// <summary>Spawns a fully wired enemy at a world position.</summary>
        public static Enemy Spawn(EnemyData data, Vector3 pos, GameObject player)
        {
            var go = new GameObject($"Enemy_{data.displayName}");
            go.transform.position = pos;

            // Enemies collide with the world but never with each other; contact
            // damage is distance-based, so they shouldn't physically shove the
            // player either.
            go.layer = EnemyLayer;
            if (!_layerConfigured)
            {
                Physics2D.IgnoreLayerCollision(EnemyLayer, EnemyLayer, true);
                _layerConfigured = true;
            }

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 3f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            Sprite first = data.frames != null && data.frames.Length > 0 ? data.frames[0] : null;
            float wTiles = first != null ? first.rect.width / PixelsPerTile : 1f;
            float hTiles = first != null ? first.rect.height / PixelsPerTile : 1f;

            var col = go.AddComponent<BoxCollider2D>();
            col.size = new Vector2(Mathf.Max(0.3f, wTiles * 0.9f), Mathf.Max(0.3f, hTiles * 0.9f));

            if (player != null)
            {
                var playerCol = player.GetComponent<Collider2D>();
                if (playerCol != null) Physics2D.IgnoreCollision(col, playerCol, true);
            }

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 9; // above tiles, below the player
            sr.sprite = first;
            if (first != null)
            {
                float s = first.pixelsPerUnit / PixelsPerTile; // pixel-true
                go.transform.localScale = new Vector3(s, s, 1f);
                col.size /= s; // collider is in local space; counteract the scale
            }

            var enemy = go.AddComponent<Enemy>();
            enemy.Data = data;
            enemy._health = data.maxHealth;
            enemy._sr = sr;
            enemy._rb = rb;
            enemy._xpTarget = player != null ? player.GetComponent<PlayerXP>() : null;

            // Behaviour by data — new brains get a case here, new enemies don't.
            switch (data.aiKind)
            {
                default:
                case "Scurry":
                    go.AddComponent<ScurryAI>().Init(enemy, player);
                    break;
            }

            return enemy;
        }

        public void TakeDamage(float amount, Vector2 knockback)
        {
            _health -= amount;
            if (_rb != null) _rb.linearVelocity = knockback;

            if (_health <= 0f)
            {
                if (_xpTarget != null) _xpTarget.AddXP(Data.xpReward);
                DropLoot();
                Destroy(gameObject);
            }
        }

        private void DropLoot()
        {
            if (Data.dropItem == null || Random.value > Data.dropChance) return;
            int count = Random.Range(Data.dropMin, Data.dropMax + 1);
            if (count > 0)
                ItemPickup.Spawn(Data.dropItem, count, transform.position,
                                 _xpTarget != null ? _xpTarget.GetComponent<PlayerInventory>() : null);
        }

        private void Update()
        {
            if (Data == null || Data.frames == null || Data.frames.Length == 0 || _sr == null)
                return;

            float vx = _rb != null ? _rb.linearVelocity.x : 0f;
            if (Mathf.Abs(vx) > 0.1f)
            {
                int idx = (int)(Time.time * Data.animFps) % Data.frames.Length;
                _sr.sprite = Data.frames[idx];
                _sr.flipX = vx < 0f; // art faces right
            }
            else
            {
                _sr.sprite = Data.frames[0]; // frame 1 doubles as idle
            }
        }
    }
}

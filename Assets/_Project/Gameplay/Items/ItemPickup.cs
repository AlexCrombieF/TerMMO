using UnityEngine;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// A dropped item in the world: pops out, falls under gravity, and is pulled
    /// into the player (magnet) then collected on contact. Spawned when a tile is
    /// mined or a tree is chopped. Server-authoritative later: the server spawns
    /// and grants; for now it collects straight into the local inventory.
    /// </summary>
    public sealed class ItemPickup : MonoBehaviour
    {
        private const float CollectDelay = 0.35f; // grace so it visibly pops before magneting
        private const float MagnetRange = 2.5f;
        private const float CollectRange = 0.6f;
        private const float MagnetSpeed = 9f;

        private ItemData _item;
        private int _count;
        private PlayerInventory _target;
        private Rigidbody2D _rb;
        private float _age;

        public static void Spawn(ItemData item, int count, Vector3 pos, PlayerInventory target)
        {
            if (item == null || count <= 0) return;

            var go = new GameObject($"Pickup_{item.DisplayName}");
            go.transform.position = pos;

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 8;
            if (item.Icon != null)
            {
                sr.sprite = item.Icon;
                Vector2 s = item.Icon.bounds.size;
                float longest = Mathf.Max(s.x, s.y);
                float scale = longest > 0f ? 0.5f / longest : 1f; // ~half a tile
                go.transform.localScale = new Vector3(scale, scale, 1f);
            }

            var rb = go.AddComponent<Rigidbody2D>();
            rb.gravityScale = 2f;
            rb.freezeRotation = true;
            rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            rb.linearVelocity = new Vector2(Random.Range(-1.5f, 1.5f), Random.Range(2f, 4f));

            var col = go.AddComponent<CircleCollider2D>();
            col.radius = 0.2f;

            // Don't physically shove the player; we collect via distance instead.
            if (target != null)
            {
                var pcol = target.GetComponent<Collider2D>();
                if (pcol != null) Physics2D.IgnoreCollision(col, pcol);
            }

            var pickup = go.AddComponent<ItemPickup>();
            pickup._item = item;
            pickup._count = count;
            pickup._target = target;
            pickup._rb = rb;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            if (_target == null || _age < CollectDelay) return;

            Vector3 playerPos = _target.transform.position;
            float dist = Vector2.Distance(transform.position, playerPos);

            if (dist <= CollectRange)
            {
                int leftover = _target.Inventory.Add(_item, _count);
                if (leftover <= 0) Destroy(gameObject);
                else _count = leftover; // inventory full — keep the rest on the ground
                return;
            }

            if (dist <= MagnetRange)
                _rb.linearVelocity = ((Vector2)(playerPos - transform.position)).normalized * MagnetSpeed;
        }
    }
}

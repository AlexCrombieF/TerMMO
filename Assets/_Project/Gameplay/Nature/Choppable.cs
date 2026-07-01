using UnityEngine;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// A destructible world object (a tree) that yields an item when chopped down.
    /// Damage accumulates from axe hits; at zero health it drops its item at a
    /// stored world position and removes itself.
    /// </summary>
    public sealed class Choppable : MonoBehaviour
    {
        [SerializeField] private float health = 5f;
        [SerializeField] private ItemData drop;
        [SerializeField] private int dropAmount = 5;

        private float _maxHealth = 5f;
        private Vector3 _dropPos;

        /// <summary>Chop completion [0..1], for crack visuals.</summary>
        public float Progress01 => _maxHealth > 0f ? Mathf.Clamp01(1f - health / _maxHealth) : 0f;

        public void Configure(ItemData dropItem, int amount, float hp, Vector3 dropPos)
        {
            drop = dropItem;
            dropAmount = Mathf.Max(1, amount);
            health = Mathf.Max(0.1f, hp);
            _maxHealth = health;
            _dropPos = dropPos;
        }

        /// <summary>Applies chop damage; drops wood at the tree and destroys when depleted.</summary>
        public void Chop(float damage, PlayerInventory inventory)
        {
            health -= damage;
            if (health > 0f) return;

            if (drop != null)
                ItemPickup.Spawn(drop, dropAmount, _dropPos, inventory);
            Destroy(gameObject);
        }
    }
}

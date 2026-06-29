using UnityEngine;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// A destructible world object (a tree) that yields an item when chopped down.
    /// Damage accumulates from axe hits; at zero health it drops its item into the
    /// player's inventory and removes itself.
    /// </summary>
    public sealed class Choppable : MonoBehaviour
    {
        [SerializeField] private float health = 5f;
        [SerializeField] private ItemData drop;
        [SerializeField] private int dropAmount = 5;

        public void Configure(ItemData dropItem, int amount, float hp)
        {
            drop = dropItem;
            dropAmount = Mathf.Max(1, amount);
            health = Mathf.Max(0.1f, hp);
        }

        /// <summary>Applies chop damage; drops + destroys when depleted.</summary>
        public void Chop(float damage, PlayerInventory inventory)
        {
            health -= damage;
            if (health > 0f) return;

            if (inventory != null && drop != null)
                inventory.Inventory.Add(drop, dropAmount);
            Destroy(gameObject);
        }
    }
}

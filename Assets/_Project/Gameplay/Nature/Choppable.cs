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
        [Tooltip("Tool needed to damage this. None = anything works (decorations).")]
        [SerializeField] private Core.ToolType requiredTool = Core.ToolType.Axe;

        private float _maxHealth = 5f;
        private Vector3 _dropPos;
        private ItemData _bonusDrop;      // e.g. an apple from a felled tree
        private float _bonusChance;

        /// <summary>Chop completion [0..1], for crack visuals.</summary>
        public float Progress01 => _maxHealth > 0f ? Mathf.Clamp01(1f - health / _maxHealth) : 0f;

        public bool CanChopWith(Core.ToolType tool)
            => requiredTool == Core.ToolType.None || tool == requiredTool;

        public void Configure(ItemData dropItem, int amount, float hp, Vector3 dropPos,
                              Core.ToolType tool = Core.ToolType.Axe)
        {
            drop = dropItem;
            dropAmount = Mathf.Max(1, amount);
            health = Mathf.Max(0.1f, hp);
            _maxHealth = health;
            _dropPos = dropPos;
            requiredTool = tool;
        }

        /// <summary>Optional extra drop rolled once when felled (apple chance on trees).</summary>
        public void SetBonusDrop(ItemData item, float chance)
        {
            _bonusDrop = item;
            _bonusChance = Mathf.Clamp01(chance);
        }

        /// <summary>Applies chop damage; drops wood at the tree and destroys when depleted.</summary>
        public void Chop(float damage, PlayerInventory inventory)
        {
            health -= damage;
            if (health > 0f) return;

            if (drop != null)
                ItemPickup.Spawn(drop, dropAmount, _dropPos, inventory);
            if (_bonusDrop != null && Random.value < _bonusChance)
                ItemPickup.Spawn(_bonusDrop, 1, _dropPos + Vector3.up * 0.5f, inventory);
            Destroy(gameObject);
        }
    }
}

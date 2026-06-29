using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// A quantity of one item type in a single inventory slot. A value type so
    /// slot arrays are allocation-free; an empty slot is <c>default</c>
    /// (Item == null). Treated as immutable-ish — replace the slot rather than
    /// mutating in place from outside <see cref="Inventory"/>.
    /// </summary>
    public struct ItemStack
    {
        public ItemData Item;
        public int Count;

        public ItemStack(ItemData item, int count)
        {
            Item = item;
            Count = count;
        }

        public bool IsEmpty => Item == null || Count <= 0;

        public static readonly ItemStack Empty = default;
    }
}

using System;
using UnityEngine;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// A fixed array of item slots with stacking rules. Pure C# (no Unity scene
    /// dependency) so it is unit-testable and will serialize cleanly for saves /
    /// network sync later. Authority note: in multiplayer the server owns the
    /// inventory; clients mirror it. Keep mutations going through these methods.
    /// </summary>
    public sealed class Inventory
    {
        private readonly ItemStack[] _slots;

        /// <summary>Raised after any change so UI can refresh.</summary>
        public event Action Changed;

        public Inventory(int size)
        {
            _slots = new ItemStack[Mathf.Max(1, size)];
        }

        public int Size => _slots.Length;
        public ItemStack Get(int index) => _slots[index];

        /// <summary>Directly sets a slot (used by inventory UI move/swap) and notifies.</summary>
        public void SetSlot(int index, ItemStack stack)
        {
            _slots[index] = stack;
            Changed?.Invoke();
        }

        /// <summary>
        /// Adds up to <paramref name="count"/> of an item, filling existing stacks
        /// first, then empty slots, respecting each item's max stack size.
        /// Returns the amount that did NOT fit (0 if everything was added).
        /// </summary>
        public int Add(ItemData item, int count)
        {
            if (item == null || count <= 0) return 0;
            int max = Mathf.Max(1, item.MaxStackSize);
            int remaining = count;

            // Top up existing stacks of the same item.
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i].Item == item && _slots[i].Count < max)
                {
                    int add = Mathf.Min(max - _slots[i].Count, remaining);
                    _slots[i].Count += add;
                    remaining -= add;
                }
            }

            // Spill into empty slots.
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i].IsEmpty)
                {
                    int add = Mathf.Min(max, remaining);
                    _slots[i] = new ItemStack(item, add);
                    remaining -= add;
                }
            }

            if (remaining != count) Changed?.Invoke();
            return remaining;
        }

        /// <summary>Removes up to <paramref name="count"/> from a slot; returns true if any removed.</summary>
        public bool ConsumeFromSlot(int index, int count)
        {
            if (count <= 0 || _slots[index].IsEmpty) return false;
            int take = Mathf.Min(count, _slots[index].Count);
            _slots[index].Count -= take;
            if (_slots[index].Count <= 0) _slots[index] = ItemStack.Empty;
            Changed?.Invoke();
            return true;
        }

        /// <summary>Total count of a given item across all slots.</summary>
        public int CountOf(ItemData item)
        {
            if (item == null) return 0;
            int total = 0;
            for (int i = 0; i < _slots.Length; i++)
                if (_slots[i].Item == item) total += _slots[i].Count;
            return total;
        }

        public bool HasItems(ItemData item, int count) => CountOf(item) >= count;

        /// <summary>Removes <paramref name="count"/> of an item spread across slots; false if not enough.</summary>
        public bool Consume(ItemData item, int count)
        {
            if (item == null || count <= 0 || CountOf(item) < count) return false;
            int remaining = count;
            for (int i = 0; i < _slots.Length && remaining > 0; i++)
            {
                if (_slots[i].Item != item) continue;
                int take = Mathf.Min(_slots[i].Count, remaining);
                _slots[i].Count -= take;
                remaining -= take;
                if (_slots[i].Count <= 0) _slots[i] = ItemStack.Empty;
            }
            Changed?.Invoke();
            return true;
        }
    }
}

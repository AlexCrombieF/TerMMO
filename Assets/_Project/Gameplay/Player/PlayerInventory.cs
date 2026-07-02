using System;
using UnityEngine;
using UnityEngine.InputSystem;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// The player's inventory plus hotbar selection. For this first version every
    /// slot is a hotbar slot (a separate backpack grid comes later). Selection is
    /// driven by number keys (1-0) and the mouse scroll wheel.
    /// </summary>
    public sealed class PlayerInventory : MonoBehaviour
    {
        [System.Serializable]
        public struct StartItem
        {
            public ItemData item;
            public int count;
        }

        [Tooltip("Total inventory slots (hotbar + backpack).")]
        [Min(1)] [SerializeField] private int size = 20;

        [Tooltip("How many of the slots are the always-visible hotbar row.")]
        [Min(1)] [SerializeField] private int hotbarSize = 10;

        [Tooltip("Items granted at spawn (pickaxe, axe, torches...).")]
        [SerializeField] private StartItem[] startingItems;

        public Inventory Inventory { get; private set; }
        public int Size => size;
        public int HotbarSize => Mathf.Min(hotbarSize, size);
        public int Selected { get; private set; }

        /// <summary>The currently selected hotbar stack (what the player is holding).</summary>
        public ItemStack Held => Inventory.Get(Selected);

        public event Action SelectionChanged;

        private void Awake()
        {
            Inventory = new Inventory(size);
            if (startingItems != null)
                foreach (StartItem s in startingItems)
                    if (s.item != null) Inventory.Add(s.item, Mathf.Max(1, s.count));
        }

        private void Update()
        {
            Keyboard kb = Keyboard.current;
            if (kb != null)
            {
                int hotkeys = Mathf.Min(10, HotbarSize);
                for (int i = 0; i < hotkeys; i++)
                {
                    // Key.Digit1..Digit9 are contiguous, then Digit0 (slot 10).
                    if (kb[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame)
                        Select(i);
                }
            }

            Mouse m = Mouse.current;
            if (m != null)
            {
                float scroll = m.scroll.ReadValue().y;
                if (scroll > 0f) Select(Selected - 1);
                else if (scroll < 0f) Select(Selected + 1);
            }
        }

        /// <summary>Selects a hotbar slot, wrapping around the hotbar row.</summary>
        public void Select(int index)
        {
            int h = HotbarSize;
            int wrapped = ((index % h) + h) % h;
            if (wrapped == Selected) return;
            Selected = wrapped;
            SelectionChanged?.Invoke();
        }

        /// <summary>Consumes one unit from the selected slot (e.g. after placing a block).</summary>
        public void ConsumeSelected(int count = 1) => Inventory.ConsumeFromSlot(Selected, count);
    }
}

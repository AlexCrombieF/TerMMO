using UnityEngine;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// A working furnace: put a smeltable item in the INPUT slot and fuel (wood,
    /// coal...) in the FUEL slot; smelted results appear in the OUTPUT slot over
    /// time. What smelts into what, and what burns for how long, is data on
    /// ItemData (SmeltsInto / FuelSeconds) — no recipes hardcoded here.
    /// Runs even while its UI is closed.
    /// </summary>
    public sealed class FurnaceObject : MonoBehaviour, IHasInventory
    {
        public const int InputSlot = 0;
        public const int FuelSlot = 1;
        public const int OutputSlot = 2;

        public Inventory Inventory { get; } = new Inventory(3);

        /// <summary>Seconds of burn left from the last consumed fuel.</summary>
        public float BurnRemaining { get; private set; }
        /// <summary>Progress (seconds) toward smelting the current input unit.</summary>
        public float SmeltProgress { get; private set; }
        public bool IsBurning => BurnRemaining > 0f;

        /// <summary>Smelt completion [0..1] of the current input, for UI.</summary>
        public float SmeltProgress01
        {
            get
            {
                ItemStack input = Inventory.Get(InputSlot);
                if (input.IsEmpty || !input.Item.IsSmeltable) return 0f;
                return Mathf.Clamp01(SmeltProgress / input.Item.SmeltSeconds);
            }
        }

        private void Update()
        {
            ItemStack input = Inventory.Get(InputSlot);
            bool canSmelt = !input.IsEmpty && input.Item.IsSmeltable
                            && OutputHasRoom(input.Item.SmeltsInto);

            // Light (or re-light) from the fuel slot only when there is work to do.
            if (BurnRemaining <= 0f && canSmelt)
            {
                ItemStack fuel = Inventory.Get(FuelSlot);
                if (!fuel.IsEmpty && fuel.Item.IsFuel)
                {
                    BurnRemaining = fuel.Item.FuelSeconds;
                    Inventory.ConsumeFromSlot(FuelSlot, 1);
                }
            }

            if (BurnRemaining > 0f)
            {
                BurnRemaining -= Time.deltaTime;

                if (canSmelt)
                {
                    SmeltProgress += Time.deltaTime;
                    if (SmeltProgress >= input.Item.SmeltSeconds)
                    {
                        SmeltProgress = 0f;
                        ItemData product = input.Item.SmeltsInto;
                        Inventory.ConsumeFromSlot(InputSlot, 1);
                        AddToOutput(product);
                    }
                }
                else
                {
                    SmeltProgress = 0f; // burning but nothing to smelt
                }
            }
            else
            {
                SmeltProgress = 0f;
            }
        }

        private bool OutputHasRoom(ItemData product)
        {
            ItemStack output = Inventory.Get(OutputSlot);
            if (output.IsEmpty) return true;
            return output.Item == product && output.Count < product.MaxStackSize;
        }

        private void AddToOutput(ItemData product)
        {
            ItemStack output = Inventory.Get(OutputSlot);
            if (output.IsEmpty) Inventory.SetSlot(OutputSlot, new ItemStack(product, 1));
            else { output.Count += 1; Inventory.SetSlot(OutputSlot, output); }
        }
    }
}

using UnityEngine;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Storage attached to a placed chest: its own 30-slot <see cref="Inventory"/>.
    /// The InventoryUI opens it alongside the backpack so stacks move between the
    /// two with the same cursor-stack controls. Contents persist via SaveSystem.
    /// </summary>
    public sealed class ChestObject : MonoBehaviour
    {
        public const int Slots = 30;

        public Inventory Inventory { get; } = new Inventory(Slots);
    }
}

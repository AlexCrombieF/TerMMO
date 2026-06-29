using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Doodgy.Data;
using Doodgy.Gameplay;

namespace Doodgy.Tests
{
    /// <summary>
    /// EditMode tests for the pure Inventory stacking logic — no scene required.
    /// </summary>
    public class InventoryTests
    {
        // Item with a controllable max stack size (the field is private/serialized).
        private static ItemData MakeItem(int maxStack)
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            FieldInfo f = typeof(ItemData).GetField("maxStackSize",
                BindingFlags.Instance | BindingFlags.NonPublic);
            f.SetValue(item, maxStack);
            return item;
        }

        [Test]
        public void Add_StacksIntoExistingSlot()
        {
            var inv = new Inventory(3);
            var item = MakeItem(10);

            Assert.AreEqual(0, inv.Add(item, 5));
            Assert.AreEqual(0, inv.Add(item, 3));

            Assert.AreEqual(item, inv.Get(0).Item);
            Assert.AreEqual(8, inv.Get(0).Count);
            Assert.IsTrue(inv.Get(1).IsEmpty);

            Object.DestroyImmediate(item);
        }

        [Test]
        public void Add_SpillsAcrossSlots_AtMaxStack()
        {
            var inv = new Inventory(3);
            var item = MakeItem(10);

            int leftover = inv.Add(item, 25); // 10 + 10 + 5
            Assert.AreEqual(0, leftover);
            Assert.AreEqual(10, inv.Get(0).Count);
            Assert.AreEqual(10, inv.Get(1).Count);
            Assert.AreEqual(5, inv.Get(2).Count);

            Object.DestroyImmediate(item);
        }

        [Test]
        public void Add_ReturnsLeftover_WhenFull()
        {
            var inv = new Inventory(1);
            var item = MakeItem(10);

            int leftover = inv.Add(item, 25); // only 10 fit
            Assert.AreEqual(15, leftover);
            Assert.AreEqual(10, inv.Get(0).Count);

            Object.DestroyImmediate(item);
        }

        [Test]
        public void Consume_ReducesThenEmptiesSlot()
        {
            var inv = new Inventory(2);
            var item = MakeItem(10);
            inv.Add(item, 5);

            Assert.IsTrue(inv.ConsumeFromSlot(0, 3));
            Assert.AreEqual(2, inv.Get(0).Count);

            Assert.IsTrue(inv.ConsumeFromSlot(0, 10)); // clamps to remaining
            Assert.IsTrue(inv.Get(0).IsEmpty);

            Object.DestroyImmediate(item);
        }

        [Test]
        public void CountOf_SumsAcrossSlots()
        {
            var inv = new Inventory(3);
            var item = MakeItem(10);
            inv.Add(item, 25); // 10 + 10 + 5

            Assert.AreEqual(25, inv.CountOf(item));

            Object.DestroyImmediate(item);
        }
    }
}

using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Doodgy.Core;
using Doodgy.Data;

namespace Doodgy.Tests
{
    /// <summary>
    /// EditMode tests validating the TileData / ItemData ScriptableObjects in
    /// isolation — no scene, no play mode required. These also prove the
    /// Core -> Data assembly references compile and resolve correctly.
    ///
    /// Run via: Window > General > Test Runner > EditMode > Run All.
    /// </summary>
    public class ContentDataTests
    {
        // Helper: assign a private [SerializeField] for test setup. Reflection is
        // acceptable here because it is confined to tests and keeps the runtime
        // API read-only (no test-only public setters leaking into the game).
        private static void SetField(object target, string fieldName, object value)
        {
            FieldInfo f = target.GetType().GetField(
                fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(f, $"Field '{fieldName}' not found on {target.GetType().Name}");
            f.SetValue(target, value);
        }

        // ---------- ItemData ----------

        [Test]
        public void ItemData_Defaults_AreStackableAndNotPlaceable()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            Assert.IsTrue(item.IsStackable, "Default maxStackSize (999) should be stackable.");
            Assert.IsFalse(item.IsPlaceable, "No placesTile assigned => not placeable.");
            Assert.AreEqual(ToolType.None, item.ToolType);
            ScriptableObject.DestroyImmediate(item);
        }

        [Test]
        public void ItemData_MaxStackOne_IsNotStackable()
        {
            var item = ScriptableObject.CreateInstance<ItemData>();
            SetField(item, "maxStackSize", 1);
            Assert.IsFalse(item.IsStackable);
            ScriptableObject.DestroyImmediate(item);
        }

        [Test]
        public void ItemData_WithPlacesTile_IsPlaceable()
        {
            var tile = ScriptableObject.CreateInstance<TileData>();
            var item = ScriptableObject.CreateInstance<ItemData>();
            SetField(item, "placesTile", tile);
            Assert.IsTrue(item.IsPlaceable);
            ScriptableObject.DestroyImmediate(item);
            ScriptableObject.DestroyImmediate(tile);
        }

        // ---------- TileData ----------

        [Test]
        public void TileData_DefaultId_IsAir()
        {
            var tile = ScriptableObject.CreateInstance<TileData>();
            Assert.IsTrue(tile.IsAir, "Default id 0 must report as air.");
            ScriptableObject.DestroyImmediate(tile);
        }

        [Test]
        public void TileData_CanBeMinedBy_RespectsToolTypeAndTier()
        {
            var tile = ScriptableObject.CreateInstance<TileData>();
            SetField(tile, "requiredTool", ToolType.Pickaxe);
            SetField(tile, "minToolTier", 2);

            Assert.IsFalse(tile.CanBeMinedBy(ToolType.Axe, 5),  "Wrong tool type should fail.");
            Assert.IsFalse(tile.CanBeMinedBy(ToolType.Pickaxe, 1), "Tier below minimum should fail.");
            Assert.IsTrue(tile.CanBeMinedBy(ToolType.Pickaxe, 2),  "Correct tool at min tier should pass.");
            Assert.IsTrue(tile.CanBeMinedBy(ToolType.Pickaxe, 9),  "Higher tier should pass.");

            ScriptableObject.DestroyImmediate(tile);
        }

        [Test]
        public void TileData_RollDropCount_ZeroWhenNoDropItem()
        {
            var tile = ScriptableObject.CreateInstance<TileData>();
            var rng = new System.Random(12345);
            Assert.AreEqual(0, tile.RollDropCount(rng), "No dropItem => 0 drops.");
            ScriptableObject.DestroyImmediate(tile);
        }

        [Test]
        public void TileData_RollDropCount_IsDeterministicForSeed()
        {
            var tile = ScriptableObject.CreateInstance<TileData>();
            var drop = ScriptableObject.CreateInstance<ItemData>();
            SetField(tile, "dropItem", drop);
            SetField(tile, "dropMin", 1);
            SetField(tile, "dropMax", 5);

            // Same seed must yield identical results (matters for server replay).
            int a = tile.RollDropCount(new System.Random(999));
            int b = tile.RollDropCount(new System.Random(999));
            Assert.AreEqual(a, b);
            Assert.GreaterOrEqual(a, 1);
            Assert.LessOrEqual(a, 5);

            ScriptableObject.DestroyImmediate(drop);
            ScriptableObject.DestroyImmediate(tile);
        }
    }
}

using NUnit.Framework;
using UnityEngine;
using Doodgy.Core;
using Doodgy.Gameplay;

namespace Doodgy.Tests
{
    /// <summary>
    /// EditMode tests for the chunk coordinate math and Chunk storage — the
    /// foundation everything else builds on. No scene required. The negative-
    /// coordinate cases matter because the world extends left of / below origin.
    /// </summary>
    public class WorldGridTests
    {
        private const int Size = WorldConstants.ChunkSize; // 32

        [Test]
        public void FloorDiv_RoundsTowardNegativeInfinity()
        {
            Assert.AreEqual(0, WorldCoords.FloorDiv(0, Size));
            Assert.AreEqual(0, WorldCoords.FloorDiv(31, Size));
            Assert.AreEqual(1, WorldCoords.FloorDiv(32, Size));
            Assert.AreEqual(-1, WorldCoords.FloorDiv(-1, Size));   // not 0
            Assert.AreEqual(-1, WorldCoords.FloorDiv(-32, Size));
            Assert.AreEqual(-2, WorldCoords.FloorDiv(-33, Size));
        }

        [Test]
        public void TileToChunk_And_Local_AreConsistent_ForNegatives()
        {
            var tile = new Vector2Int(-1, -1);
            Assert.AreEqual(new Vector2Int(-1, -1), WorldCoords.TileToChunk(tile));
            // local must always be in [0, Size): tile -1 sits at local Size-1
            Assert.AreEqual(new Vector2Int(Size - 1, Size - 1), WorldCoords.TileToLocal(tile));
        }

        [Test]
        public void Local_IsAlwaysInRange()
        {
            for (int t = -70; t <= 70; t++)
            {
                int local = WorldCoords.TileToLocal(new Vector2Int(t, 0)).x;
                Assert.GreaterOrEqual(local, 0);
                Assert.Less(local, Size);
            }
        }

        [Test]
        public void WorldToTile_FloorsCorrectly()
        {
            Assert.AreEqual(new Vector2Int(0, 0), WorldCoords.WorldToTile(new Vector3(0.4f, 0.9f, 0f)));
            Assert.AreEqual(new Vector2Int(-1, -1), WorldCoords.WorldToTile(new Vector3(-0.1f, -0.1f, 0f)));
            Assert.AreEqual(new Vector2Int(5, 3), WorldCoords.WorldToTile(new Vector3(5.99f, 3.01f, 0f)));
        }

        [Test]
        public void Chunk_SetLocal_ReportsChangeAndDirty()
        {
            var chunk = new Chunk(new Vector2Int(2, -3));
            Assert.IsFalse(chunk.Dirty);

            Assert.IsTrue(chunk.SetLocal(0, 0, 5));   // changed
            Assert.AreEqual((ushort)5, chunk.GetLocal(0, 0));
            Assert.IsTrue(chunk.Dirty);

            Assert.IsFalse(chunk.SetLocal(0, 0, 5));  // no-op, same value
            chunk.ClearDirty();
            Assert.IsFalse(chunk.Dirty);
        }
    }
}

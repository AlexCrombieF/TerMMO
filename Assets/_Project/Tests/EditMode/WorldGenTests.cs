using NUnit.Framework;
using UnityEngine;
using Doodgy.Core;
using Doodgy.Data;
using Doodgy.Gameplay;

namespace Doodgy.Tests
{
    /// <summary>
    /// EditMode tests for ProceduralWorldGenerator. No scene required — the
    /// generator is pure data in / data out. These lock in the two properties we
    /// care most about: determinism (same seed == same world) and the structural
    /// invariants (no floating ground, ore only in stone).
    /// </summary>
    public class WorldGenTests
    {
        private static WorldGenSettings MakeSettings()
        {
            var s = ScriptableObject.CreateInstance<WorldGenSettings>();
            s.grassTileId = 4; s.dirtTileId = 1; s.stoneTileId = 2; s.oreTileId = 3;
            s.baseSurfaceHeight = 64; s.surfaceAmplitude = 14;
            s.terrainFrequency = 0.012f; s.terrainOctaves = 4;
            s.terrainLacunarity = 2f; s.terrainPersistence = 0.5f; s.terrainRidge = 0.35f;
            s.dirtDepth = 6; s.dirtDepthVariance = 3;
            s.cavesEnabled = true; s.caveFrequency = 0.05f; s.caveOctaves = 3;
            s.caveThreshold = 0.6f; s.caveMinDepthBelowSurface = 4;
            s.caveDepthBonus = 0.12f; s.caveDepthFadeTiles = 90;
            s.wormCavesEnabled = true; s.wormFrequency = 0.035f; s.wormWidth = 0.055f;
            s.oreEnabled = true; s.oreFrequency = 0.14f; s.oreOctaves = 2;
            s.oreThreshold = 0.82f; s.oreMinDepthBelowSurface = 8;
            s.oreDepthBonus = 0.14f; s.oreRichDepth = 120;
            return s;
        }

        [Test]
        public void Generation_IsDeterministic_ForSameSeed()
        {
            var s = MakeSettings();
            var gen = new ProceduralWorldGenerator(s, 777);

            var a = new Chunk(new Vector2Int(1, 1));
            var b = new Chunk(new Vector2Int(1, 1));
            gen.Generate(a);
            gen.Generate(b);

            Assert.AreEqual(a.Raw, b.Raw, "Same seed + coord must yield identical tiles.");
            Object.DestroyImmediate(s);
        }

        [Test]
        public void DifferentSeeds_ProduceDifferentTerrain()
        {
            var s = MakeSettings();
            var g1 = new ProceduralWorldGenerator(s, 1);
            var g2 = new ProceduralWorldGenerator(s, 2);

            bool anyDifference = false;
            for (int x = 0; x < 200 && !anyDifference; x++)
                if (g1.SurfaceHeightAt(x) != g2.SurfaceHeightAt(x)) anyDifference = true;

            Assert.IsTrue(anyDifference, "Two seeds should produce different surfaces.");
            Object.DestroyImmediate(s);
        }

        [Test]
        public void NoSolidTile_AboveSurface()
        {
            var s = MakeSettings();
            var gen = new ProceduralWorldGenerator(s, 42);
            int size = WorldConstants.ChunkSize;

            // Check a couple of chunks including negative coords.
            foreach (var coord in new[] { new Vector2Int(0, 2), new Vector2Int(-1, 1) })
            {
                var chunk = new Chunk(coord);
                gen.Generate(chunk);
                int baseX = coord.x * size, baseY = coord.y * size;

                for (int lx = 0; lx < size; lx++)
                {
                    int surface = gen.SurfaceHeightAt(baseX + lx);
                    for (int ly = 0; ly < size; ly++)
                    {
                        ushort id = chunk.GetLocal(lx, ly);
                        if (id != WorldConstants.AirTileId)
                            Assert.LessOrEqual(baseY + ly, surface,
                                "Solid tile found above the surface height.");
                    }
                }
            }
            Object.DestroyImmediate(s);
        }

        [Test]
        public void Ore_OnlyReplacesStone_NotDirt()
        {
            var s = MakeSettings();
            s.surfaceAmplitude = 0;          // flat surface for a deterministic check
            s.cavesEnabled = false;          // isolate the ore rule (no cavern carving)
            s.wormCavesEnabled = false;      // ...and no worm carving
            s.dirtDepthVariance = 0;         // fixed dirt/stone boundary
            s.oreThreshold = 0f;             // ore noise always passes -> all stone becomes ore
            s.oreDepthBonus = 0f;
            s.oreMinDepthBelowSurface = 0;
            s.dirtDepth = 10;
            s.grassTileId = s.dirtTileId;    // ignore the grass surface layer for this check
            int surface = s.baseSurfaceHeight;

            var gen = new ProceduralWorldGenerator(s, 99);

            // Shallow tiles (within dirt band) must stay dirt — ore must not touch them.
            for (int depth = 0; depth < s.dirtDepth; depth++)
            {
                ushort id = gen.ResolveTile(worldX: 0, worldY: surface - depth, surfaceHeight: surface);
                Assert.AreEqual(s.dirtTileId, id, $"Depth {depth} should be dirt, not ore.");
            }

            // Deep stone (oreThreshold 0) becomes ore.
            ushort deep = gen.ResolveTile(0, surface - s.dirtDepth, surface);
            Assert.AreEqual(s.oreTileId, deep, "Deep stone should convert to ore here.");

            Object.DestroyImmediate(s);
        }

        [Test]
        public void ResolveTile_AboveSurface_IsAir()
        {
            var s = MakeSettings();
            var gen = new ProceduralWorldGenerator(s, 5);
            Assert.AreEqual(WorldConstants.AirTileId, gen.ResolveTile(0, 200, 64));
            Object.DestroyImmediate(s);
        }
    }
}

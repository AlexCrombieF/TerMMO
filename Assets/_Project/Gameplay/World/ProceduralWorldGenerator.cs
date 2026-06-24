using UnityEngine;
using Doodgy.Core;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Seeded terrain + caves + ore generator. Every tile is a pure function of
    /// (seed, worldX, worldY), so chunks generate independently and seamlessly.
    ///
    /// Layer order per solid tile:
    ///   1. Base material: dirt band near the surface, stone below.
    ///   2. Caves carve material to air (below a min depth, keeping a surface crust).
    ///   3. Ore replaces stone (below a min depth).
    /// </summary>
    public sealed class ProceduralWorldGenerator : IWorldGenerator
    {
        // Distinct channels so terrain / cave / ore noise fields are independent.
        private const int TerrainChannel = 1;
        private const int CaveChannel = 2;
        private const int OreChannel = 3;

        private readonly WorldGenSettings _s;
        private readonly Vector2 _terrainOffset;
        private readonly Vector2 _caveOffset;
        private readonly Vector2 _oreOffset;

        public ProceduralWorldGenerator(WorldGenSettings settings, int seed)
        {
            _s = settings;
            _terrainOffset = NoiseUtil.SeedOffset(seed, TerrainChannel);
            _caveOffset = NoiseUtil.SeedOffset(seed, CaveChannel);
            _oreOffset = NoiseUtil.SeedOffset(seed, OreChannel);
        }

        /// <summary>Surface (top solid) tile height for a given world column.</summary>
        public int SurfaceHeightAt(int worldX)
        {
            float n = NoiseUtil.Fbm1D(worldX, _terrainOffset, _s.terrainFrequency,
                                      _s.terrainOctaves, _s.terrainLacunarity, _s.terrainPersistence);
            // Map noise [0,1] -> [-1,1] -> +/- amplitude around the base height.
            return _s.baseSurfaceHeight + Mathf.RoundToInt((n * 2f - 1f) * _s.surfaceAmplitude);
        }

        public void Generate(Chunk chunk)
        {
            int size = WorldConstants.ChunkSize;
            int baseX = chunk.Coord.x * size;
            int baseY = chunk.Coord.y * size;

            // Surface height only varies with x — compute once per column.
            for (int lx = 0; lx < size; lx++)
            {
                int worldX = baseX + lx;
                int surface = SurfaceHeightAt(worldX);

                for (int ly = 0; ly < size; ly++)
                {
                    int worldY = baseY + ly;
                    ushort id = ResolveTile(worldX, worldY, surface);
                    if (id != WorldConstants.AirTileId)
                        chunk.SetLocal(lx, ly, id);
                }
            }

            // Generated state is the baseline, not a player edit.
            chunk.ClearDirty();
        }

        /// <summary>
        /// Pure per-tile resolution. Public so it can be unit-tested directly and
        /// reused (e.g. a future minimap preview) without building a chunk.
        /// </summary>
        public ushort ResolveTile(int worldX, int worldY, int surfaceHeight)
        {
            // Above ground -> air.
            if (worldY > surfaceHeight) return WorldConstants.AirTileId;

            int depth = surfaceHeight - worldY; // 0 at the surface tile, grows downward

            // 1. Base material: grass on the very top, dirt band beneath, then stone.
            ushort id;
            if (depth == 0) id = _s.grassTileId;
            else if (depth < _s.dirtDepth) id = _s.dirtTileId;
            else id = _s.stoneTileId;

            // 2. Caves (carve air) — keep a crust near the surface.
            if (_s.cavesEnabled && depth >= _s.caveMinDepthBelowSurface)
            {
                float c = NoiseUtil.Fbm(worldX, worldY, _caveOffset, _s.caveFrequency,
                                        _s.caveOctaves, 2f, 0.5f);
                if (c > _s.caveThreshold) return WorldConstants.AirTileId;
            }

            // 3. Ore veins replace stone only, below a min depth.
            if (_s.oreEnabled && id == _s.stoneTileId && depth >= _s.oreMinDepthBelowSurface)
            {
                float o = NoiseUtil.Fbm(worldX, worldY, _oreOffset, _s.oreFrequency,
                                        _s.oreOctaves, 2f, 0.5f);
                if (o > _s.oreThreshold) id = _s.oreTileId;
            }

            return id;
        }
    }
}

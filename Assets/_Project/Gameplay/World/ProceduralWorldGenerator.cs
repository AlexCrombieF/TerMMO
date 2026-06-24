using UnityEngine;
using Doodgy.Core;
using Doodgy.Data;

namespace Doodgy.Gameplay
{
    /// <summary>
    /// Seeded terrain + caves + ore generator. Every tile is a pure function of
    /// (seed, worldX, worldY), so chunks generate independently and seamlessly.
    ///
    /// Per solid tile:
    ///   1. Base material: grass (depth 0) -> dirt band (noisy boundary) -> stone.
    ///   2. Carving: cavern (cheese) noise + winding worm tunnels, both opening
    ///      up with depth, below a solid surface crust.
    ///   3. Ore replaces stone, getting richer with depth.
    /// </summary>
    public sealed class ProceduralWorldGenerator : IWorldGenerator
    {
        // Distinct channels so each noise field is independent for the same seed.
        private const int TerrainChannel = 1;
        private const int RidgeChannel = 2;
        private const int DirtChannel = 3;
        private const int CaveChannel = 4;
        private const int Worm1Channel = 5;
        private const int Worm2Channel = 6;
        private const int OreChannel = 7;

        private readonly WorldGenSettings _s;
        private readonly Vector2 _terrainOffset;
        private readonly Vector2 _ridgeOffset;
        private readonly Vector2 _dirtOffset;
        private readonly Vector2 _caveOffset;
        private readonly Vector2 _worm1Offset;
        private readonly Vector2 _worm2Offset;
        private readonly Vector2 _oreOffset;

        public ProceduralWorldGenerator(WorldGenSettings settings, int seed)
        {
            _s = settings;
            _terrainOffset = NoiseUtil.SeedOffset(seed, TerrainChannel);
            _ridgeOffset = NoiseUtil.SeedOffset(seed, RidgeChannel);
            _dirtOffset = NoiseUtil.SeedOffset(seed, DirtChannel);
            _caveOffset = NoiseUtil.SeedOffset(seed, CaveChannel);
            _worm1Offset = NoiseUtil.SeedOffset(seed, Worm1Channel);
            _worm2Offset = NoiseUtil.SeedOffset(seed, Worm2Channel);
            _oreOffset = NoiseUtil.SeedOffset(seed, OreChannel);
        }

        /// <summary>Surface (top solid) tile height for a given world column.</summary>
        public int SurfaceHeightAt(int worldX)
        {
            float baseN = NoiseUtil.Fbm1D(worldX, _terrainOffset, _s.terrainFrequency,
                                          _s.terrainOctaves, _s.terrainLacunarity, _s.terrainPersistence);
            float height = (baseN * 2f - 1f) * _s.surfaceAmplitude;

            if (_s.terrainRidge > 0f)
            {
                // Ridged noise: 1 - |2n-1| produces sharp peaks (cliffs/ridges).
                float r = NoiseUtil.Fbm1D(worldX, _ridgeOffset, _s.terrainFrequency * 2f, 2, 2f, 0.5f);
                float ridge = 1f - Mathf.Abs(2f * r - 1f);
                height += (ridge - 0.5f) * _s.surfaceAmplitude * _s.terrainRidge;
            }

            return _s.baseSurfaceHeight + Mathf.RoundToInt(height);
        }

        /// <summary>Thickness of the dirt band for a column (noisy boundary).</summary>
        public int DirtDepthAt(int worldX)
        {
            if (_s.dirtDepthVariance <= 0) return Mathf.Max(0, _s.dirtDepth);
            float n = NoiseUtil.Fbm1D(worldX, _dirtOffset, 0.08f, 2, 2f, 0.5f);
            int d = _s.dirtDepth + Mathf.RoundToInt((n * 2f - 1f) * _s.dirtDepthVariance);
            return Mathf.Max(0, d);
        }

        public void Generate(Chunk chunk)
        {
            int size = WorldConstants.ChunkSize;
            int baseX = chunk.Coord.x * size;
            int baseY = chunk.Coord.y * size;

            // Column-constant values (surface height, dirt thickness) computed once.
            for (int lx = 0; lx < size; lx++)
            {
                int worldX = baseX + lx;
                int surface = SurfaceHeightAt(worldX);
                int dirtHere = DirtDepthAt(worldX);

                for (int ly = 0; ly < size; ly++)
                {
                    int worldY = baseY + ly;
                    ushort id = ResolveTileCore(worldX, worldY, surface, dirtHere);
                    if (id != WorldConstants.AirTileId)
                        chunk.SetLocal(lx, ly, id);
                }
            }

            chunk.ClearDirty();
        }

        /// <summary>
        /// Pure per-tile resolution. Public for unit tests / previews; recomputes
        /// the column's dirt depth so it can be called for any single tile.
        /// </summary>
        public ushort ResolveTile(int worldX, int worldY, int surfaceHeight)
            => ResolveTileCore(worldX, worldY, surfaceHeight, DirtDepthAt(worldX));

        private ushort ResolveTileCore(int worldX, int worldY, int surfaceHeight, int dirtHere)
        {
            if (worldY > surfaceHeight) return WorldConstants.AirTileId; // above ground

            int depth = surfaceHeight - worldY; // 0 at the surface tile, grows downward

            // 1. Base material.
            ushort id;
            if (depth == 0) id = _s.grassTileId;
            else if (depth < dirtHere) id = _s.dirtTileId;
            else id = _s.stoneTileId;

            // 2. Carving (keep a crust near the surface).
            if (depth >= _s.caveMinDepthBelowSurface && IsCarved(worldX, worldY, depth))
                return WorldConstants.AirTileId;

            // 3. Ore veins replace stone, richer with depth.
            if (_s.oreEnabled && id == _s.stoneTileId && depth >= _s.oreMinDepthBelowSurface)
            {
                float fade = Mathf.Clamp01((float)(depth - _s.oreMinDepthBelowSurface) / Mathf.Max(1, _s.oreRichDepth));
                float threshold = _s.oreThreshold - _s.oreDepthBonus * fade;
                float o = NoiseUtil.Fbm(worldX, worldY, _oreOffset, _s.oreFrequency, _s.oreOctaves, 2f, 0.5f);
                if (o > threshold) id = _s.oreTileId;
            }

            return id;
        }

        /// <summary>True if cavern or worm-tunnel carving removes this tile.</summary>
        private bool IsCarved(int worldX, int worldY, int depth)
        {
            // Cavern ("cheese") caves: open up with depth.
            if (_s.cavesEnabled)
            {
                float fade = Mathf.Clamp01((float)depth / Mathf.Max(1, _s.caveDepthFadeTiles));
                float threshold = _s.caveThreshold - _s.caveDepthBonus * fade;
                float c = NoiseUtil.Fbm(worldX, worldY, _caveOffset, _s.caveFrequency, _s.caveOctaves, 2f, 0.5f);
                if (c > threshold) return true;
            }

            // Worm tunnels: carve where two independent fields both sit near 0.5.
            // The intersection of two iso-surfaces traces winding 1D-ish tunnels.
            if (_s.wormCavesEnabled)
            {
                float w1 = NoiseUtil.Fbm(worldX, worldY, _worm1Offset, _s.wormFrequency, 2, 2f, 0.5f);
                float w2 = NoiseUtil.Fbm(worldX, worldY, _worm2Offset, _s.wormFrequency, 2, 2f, 0.5f);
                if (Mathf.Abs(w1 - 0.5f) < _s.wormWidth && Mathf.Abs(w2 - 0.5f) < _s.wormWidth)
                    return true;
            }

            return false;
        }
    }
}

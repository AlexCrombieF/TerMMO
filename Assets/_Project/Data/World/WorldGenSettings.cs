using UnityEngine;

namespace Doodgy.Data
{
    /// <summary>
    /// All tunable parameters for procedural world generation. One asset; edit in
    /// the inspector and regenerate — no recompile. Public fields (rather than the
    /// private+property style used for protected content like TileData) because
    /// this is a pure designer-facing tuning board.
    ///
    /// Tile ids reference entries in the TileDatabase. 0 == air.
    /// </summary>
    [CreateAssetMenu(menuName = "Doodgy/World/World Gen Settings", fileName = "WorldGenSettings")]
    public sealed class WorldGenSettings : ScriptableObject
    {
        [Header("Tiles (ids must exist in the TileDatabase)")]
        [Tooltip("Topmost surface tile (depth 0).")]
        public ushort grassTileId = 4;
        public ushort dirtTileId = 1;
        public ushort stoneTileId = 2;
        public ushort oreTileId = 3;

        [Header("Surface terrain")]
        [Tooltip("Average ground level, in tiles above y=0.")]
        public int baseSurfaceHeight = 64;
        [Tooltip("Maximum +/- variation of the surface from the base height.")]
        public int surfaceAmplitude = 14;
        [Tooltip("Lower = wider, smoother hills; higher = rugged.")]
        public float terrainFrequency = 0.012f;
        [Min(1)] public int terrainOctaves = 4;
        public float terrainLacunarity = 2f;
        [Range(0f, 1f)] public float terrainPersistence = 0.5f;
        [Tooltip("Extra sharp ridged detail for cliffs/ridges (0 = smooth hills).")]
        [Range(0f, 1f)] public float terrainRidge = 0.35f;

        [Header("Dirt / stone layering")]
        [Tooltip("Average thickness of the dirt band below the surface; stone beneath.")]
        [Min(0)] public int dirtDepth = 6;
        [Tooltip("Random +/- variation of the dirt/stone boundary depth per column, " +
                 "so the layers interlock instead of a flat cut.")]
        [Min(0)] public int dirtDepthVariance = 3;

        [Header("Caves — caverns (cheese)")]
        public bool cavesEnabled = true;
        [Tooltip("Higher = smaller, more frequent caverns.")]
        public float caveFrequency = 0.05f;
        [Min(1)] public int caveOctaves = 3;
        [Tooltip("Carve to air where cavern noise exceeds this. Higher = fewer caves.")]
        [Range(0f, 1f)] public float caveThreshold = 0.6f;
        [Tooltip("Keep a solid crust this many tiles below the surface.")]
        [Min(0)] public int caveMinDepthBelowSurface = 4;
        [Tooltip("Caves open up with depth: the threshold is eased by this much...")]
        [Range(0f, 0.5f)] public float caveDepthBonus = 0.12f;
        [Tooltip("...reaching the full bonus this many tiles down.")]
        [Min(1)] public int caveDepthFadeTiles = 90;

        [Header("Caves — winding tunnels (worms)")]
        public bool wormCavesEnabled = true;
        [Tooltip("Lower = longer, smoother tunnels.")]
        public float wormFrequency = 0.035f;
        [Tooltip("Half-width of the tunnel carve band. Bigger = wider tunnels.")]
        [Range(0.01f, 0.2f)] public float wormWidth = 0.055f;

        [Header("Coal ore (common, shallower)")]
        public bool coalEnabled = true;
        public ushort coalTileId = 7;
        public float coalFrequency = 0.13f;
        [Range(0f, 1f)] public float coalThreshold = 0.78f;
        [Min(0)] public int coalMinDepthBelowSurface = 4;
        [Min(1)] public int coalMaxDepthBelowSurface = 70;

        [Header("Iron ore (deeper, richer with depth)")]
        public bool oreEnabled = true;
        [Tooltip("Higher = smaller, more scattered veins.")]
        public float oreFrequency = 0.14f;
        [Min(1)] public int oreOctaves = 2;
        [Tooltip("Base ore threshold near the top (higher = rarer).")]
        [Range(0f, 1f)] public float oreThreshold = 0.82f;
        [Tooltip("Ore only spawns at least this deep below the surface.")]
        [Min(0)] public int oreMinDepthBelowSurface = 8;
        [Tooltip("Ore gets richer with depth: threshold eased by this much...")]
        [Range(0f, 0.5f)] public float oreDepthBonus = 0.14f;
        [Tooltip("...reaching full richness this many tiles down.")]
        [Min(1)] public int oreRichDepth = 120;
    }
}

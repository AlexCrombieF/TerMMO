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
        [Tooltip("Thickness of the dirt band below the surface; stone beneath.")]
        [Min(0)] public int dirtDepth = 5;

        [Header("Caves")]
        public bool cavesEnabled = true;
        [Tooltip("Higher = smaller, more frequent caves.")]
        public float caveFrequency = 0.07f;
        [Min(1)] public int caveOctaves = 3;
        [Tooltip("Carve to air where cave noise exceeds this. Higher = fewer caves.")]
        [Range(0f, 1f)] public float caveThreshold = 0.55f;
        [Tooltip("Don't carve within this many tiles of the surface (keeps a crust).")]
        [Min(0)] public int caveMinDepthBelowSurface = 4;

        [Header("Ore veins")]
        public bool oreEnabled = true;
        [Tooltip("Higher = smaller, more scattered veins.")]
        public float oreFrequency = 0.14f;
        [Min(1)] public int oreOctaves = 2;
        [Tooltip("Place ore where ore noise exceeds this. Higher = rarer ore.")]
        [Range(0f, 1f)] public float oreThreshold = 0.74f;
        [Tooltip("Ore only spawns at least this deep below the surface.")]
        [Min(0)] public int oreMinDepthBelowSurface = 8;
    }
}

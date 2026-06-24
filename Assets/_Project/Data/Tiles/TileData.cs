using UnityEngine;
using UnityEngine.Tilemaps;
using Doodgy.Core;

namespace Doodgy.Data
{
    /// <summary>
    /// Immutable, designer-authored definition of a single tile TYPE
    /// (dirt, stone, copper ore, wood plank...). One asset per tile type.
    ///
    /// Runtime chunks store only the numeric <see cref="Id"/> in a compact
    /// array; this asset holds the heavy data (sprite, hardness, drops) and is
    /// looked up by id through the TileDatabase (added in Build Order step 2).
    ///
    /// Server-authoritative note: hardness, required tool, and drops are read on
    /// the SERVER when validating a mine request. The client uses the same data
    /// for prediction and rendering, but the server's copy is the source of
    /// truth — never let the client decide what a tile drops.
    /// </summary>
    [CreateAssetMenu(menuName = "Doodgy/World/Tile Data", fileName = "Tile_New")]
    public sealed class TileData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable numeric id written into chunk arrays, save files, and " +
                 "network packets. 0 is RESERVED for 'air/empty'. Never reuse or " +
                 "reorder ids once a world has been saved with them.")]
        [SerializeField] private ushort id = 0;

        [SerializeField] private string displayName = "New Tile";

        [Header("Rendering")]
        [Tooltip("Authored Tilemap tile asset (Tile, RuleTile, etc.). Highest " +
                 "priority. Leave null to use Sprite or the colour placeholder.")]
        [SerializeField] private TileBase tileAsset;

        [Tooltip("A raw Sprite (e.g. a sliced sub-sprite from an atlas). If set and " +
                 "TileAsset is null, the resolver wraps it into a runtime tile that " +
                 "is re-centred and scaled to fill one cell. No Tile asset needed.")]
        [SerializeField] private Sprite sprite;

        [Tooltip("Multiplicative colour tint. Also the fill colour of the " +
                 "placeholder tile when no TileAsset/Sprite is assigned.")]
        [SerializeField] private Color tint = Color.white;

        [Header("Physical")]
        [Tooltip("If true the tile has collision and blocks movement/projectiles.")]
        [SerializeField] private bool isSolid = true;

        [Tooltip("Mining effort. Higher = slower to break. Combined with " +
                 "RequiredTool/MinToolTier to gate what can break it.")]
        [Min(0f)]
        [SerializeField] private float hardness = 1f;

        [Tooltip("Which tool category is needed to mine this tile efficiently.")]
        [SerializeField] private ToolType requiredTool = ToolType.Pickaxe;

        [Tooltip("Minimum tool tier that can break this tile at all. A tool below " +
                 "this tier makes no mining progress (e.g. copper pick vs obsidian).")]
        [Min(0)]
        [SerializeField] private int minToolTier = 0;

        [Header("Drops")]
        [Tooltip("Item granted when the tile is destroyed — usually the placeable " +
                 "item form of this tile. Null = drops nothing.")]
        [SerializeField] private ItemData dropItem;

        [Min(0)] [SerializeField] private int dropMin = 1;
        [Min(0)] [SerializeField] private int dropMax = 1;

        [Header("Lighting (URP 2D)")]
        [Tooltip("If true this tile blocks light propagation through caves.")]
        [SerializeField] private bool blocksLight = true;

        [Tooltip("If true this tile emits light (torch, lava, glowing ore).")]
        [SerializeField] private bool emitsLight = false;

        [SerializeField] private Color lightColor = Color.white;
        [Range(0f, 1f)] [SerializeField] private float lightIntensity = 0f;

        // --- Read-only public API. Data is authored, never mutated at runtime. ---
        public ushort Id => id;
        public string DisplayName => displayName;
        public TileBase TileAsset => tileAsset;
        public Sprite Sprite => sprite;
        public Color Tint => tint;
        public bool IsSolid => isSolid;
        public float Hardness => hardness;
        public ToolType RequiredTool => requiredTool;
        public int MinToolTier => minToolTier;
        public ItemData DropItem => dropItem;
        public int DropMin => dropMin;
        public int DropMax => dropMax;
        public bool BlocksLight => blocksLight;
        public bool EmitsLight => emitsLight;
        public Color LightColor => lightColor;
        public float LightIntensity => lightIntensity;

        /// <summary>True when id 0 (the reserved "air/empty" tile).</summary>
        public bool IsAir => id == 0;

        /// <summary>
        /// Returns true if the supplied tool can break this tile. Pure function,
        /// server-side authority. A required tool of <see cref="ToolType.None"/>
        /// means "any tool (including hands) works".
        /// </summary>
        public bool CanBeMinedBy(ToolType toolType, int toolTier)
        {
            if (requiredTool != ToolType.None && toolType != requiredTool)
                return false;
            return toolTier >= minToolTier;
        }

        /// <summary>
        /// Rolls a random drop count in [DropMin, DropMax]. SERVER-SIDE ONLY —
        /// takes an explicit seeded <see cref="System.Random"/> so results are
        /// deterministic and reproducible. Returns 0 when there is no drop item.
        /// </summary>
        public int RollDropCount(System.Random rng)
        {
            if (dropItem == null || dropMax <= 0) return 0;
            int min = Mathf.Min(dropMin, dropMax);
            int max = Mathf.Max(dropMin, dropMax);
            return rng.Next(min, max + 1); // upper bound is exclusive, so +1
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (dropMax < dropMin) dropMax = dropMin;
            if (string.IsNullOrWhiteSpace(displayName)) displayName = name;
        }
#endif
    }
}

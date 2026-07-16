using UnityEngine;
using Doodgy.Core;

namespace Doodgy.Data
{
    /// <summary>
    /// Base designer-authored definition of an inventory item. One asset per
    /// item type.
    ///
    /// Specialised items (weapons, armour, tools with full combat stats) will
    /// subclass this ONE level deep in later build steps — e.g.
    /// WeaponItemData : ItemData. We keep the hierarchy shallow on purpose and
    /// put behaviour in systems, not in the data. The common fields here
    /// (stacking, placement link, basic tool stats) are enough to drive
    /// inventory and tile place/break in step 2 without any subclasses yet.
    ///
    /// Note this class is NOT sealed (TileData is) precisely because it is the
    /// designed extension point.
    /// </summary>
    [CreateAssetMenu(menuName = "Doodgy/Items/Item Data", fileName = "Item_New")]
    public class ItemData : ScriptableObject
    {
        [Header("Identity")]
        [Tooltip("Stable numeric id for save files and network packets. " +
                 "0 is RESERVED for 'no item'. Never reuse ids.")]
        [SerializeField] private int id = 0;

        [SerializeField] private string displayName = "New Item";
        [TextArea] [SerializeField] private string description = "";
        [SerializeField] private Sprite icon;

        [Header("Classification")]
        [SerializeField] private ItemCategory category = ItemCategory.Material;
        [SerializeField] private Rarity rarity = Rarity.Common;

        [Header("Stacking")]
        [Min(1)]
        [Tooltip("Max units per inventory slot. 1 = non-stackable (gear/tools).")]
        [SerializeField] private int maxStackSize = 999;

        [Header("Placement (optional)")]
        [Tooltip("If set, placing this item puts this tile into the world. This is " +
                 "how the 'dirt block' item and the dirt tile are linked. " +
                 "Typically set on TileBlock-category items.")]
        [SerializeField] private TileData placesTile;

        [Header("Tool stats (optional — used when Category is Tool)")]
        [SerializeField] private ToolType toolType = ToolType.None;
        [Min(0)] [SerializeField] private int toolTier = 0;

        [Tooltip("Mining speed multiplier — higher breaks tiles faster.")]
        [Min(0f)] [SerializeField] private float miningPower = 1f;

        [Tooltip("How far (in tiles) the player can reach to use this tool.")]
        [Min(0f)] [SerializeField] private float reach = 4f;

        [Header("Placeable object (optional; alternative to Places Tile)")]
        [Tooltip("If set (with a Kind), placing spawns a multi-tile world object " +
                 "(e.g. a workbench) instead of a single tile.")]
        [SerializeField] private Sprite objectSprite;
        [SerializeField] private Vector2Int objectSize = Vector2Int.one;
        [Tooltip("Identifier used by systems that look for this object, e.g. \"Workbench\".")]
        [SerializeField] private string objectKind = "";

        [Tooltip("Optional alternate-state sprite for the placed object " +
                 "(e.g. the door's OPEN art). Same canvas size as the main sprite.")]
        [SerializeField] private Sprite objectAltSprite;

        [Header("Consumable (optional)")]
        [Tooltip("HP restored when eaten/drunk (right-click while held). 0 = not edible.")]
        [Min(0f)] [SerializeField] private float healAmount = 0f;

        [Header("Furnace (optional)")]
        [Tooltip("Seconds of burn time this item provides as furnace fuel. 0 = not fuel.")]
        [Min(0f)] [SerializeField] private float fuelSeconds = 0f;
        [Tooltip("Item produced when this is smelted in a furnace. Null = not smeltable.")]
        [SerializeField] private ItemData smeltsInto;
        [Tooltip("Seconds of burn needed to smelt one unit.")]
        [Min(0.1f)] [SerializeField] private float smeltSeconds = 4f;

        // --- Read-only public API. Data is authored, never mutated at runtime. ---
        public int Id => id;
        public string DisplayName => displayName;
        public string Description => description;
        public Sprite Icon => icon;
        public ItemCategory Category => category;
        public Rarity Rarity => rarity;
        public int MaxStackSize => maxStackSize;
        public bool IsStackable => maxStackSize > 1;
        public TileData PlacesTile => placesTile;
        public bool IsPlaceable => placesTile != null;
        public ToolType ToolType => toolType;
        public int ToolTier => toolTier;
        public float MiningPower => miningPower;
        public float Reach => reach;
        public Sprite ObjectSprite => objectSprite;
        public Vector2Int ObjectSize => objectSize;
        public string ObjectKind => objectKind;
        public Sprite ObjectAltSprite => objectAltSprite;
        public bool IsPlaceableObject => objectSprite != null && !string.IsNullOrEmpty(objectKind);
        public float HealAmount => healAmount;
        public bool IsEdible => healAmount > 0f;
        public float FuelSeconds => fuelSeconds;
        public ItemData SmeltsInto => smeltsInto;
        public float SmeltSeconds => smeltSeconds;
        public bool IsFuel => fuelSeconds > 0f;
        public bool IsSmeltable => smeltsInto != null;

#if UNITY_EDITOR
        // virtual so subclasses (WeaponItemData, etc.) can extend validation.
        protected virtual void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(displayName)) displayName = name;
            if (maxStackSize < 1) maxStackSize = 1;
        }
#endif
    }
}

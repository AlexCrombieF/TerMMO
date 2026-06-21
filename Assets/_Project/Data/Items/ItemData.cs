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
